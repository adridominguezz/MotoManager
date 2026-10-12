using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// The paddock changes over the years like the real one: private owners who keep pouring money into a team
    /// eventually sell it or pull out, and the places that free up are taken by new entrants, so every
    /// championship keeps its grid. Works teams (manufacturers) and the cup organiser never quit; teams owned by
    /// the player follow the player's decisions (see <see cref="OrganisationService"/>).
    /// </summary>
    public static class UniverseService
    {
        private static readonly string[] Colours =
        {
            "#E10600", "#F26B1D", "#F5C518", "#2DBE60", "#00A3A3", "#1E88E5", "#283593", "#7B3FBF", "#D81B60", "#111111", "#FFFFFF", "#9E9E9E"
        };

        private static readonly string[] Patterns = { "{0} Racing", "Team {0}", "{0} Motorsport", "{0} Racing Team", "{0} Moto Racing" };

        /// <summary>The grid each championship keeps (recorded the first time it is needed).</summary>
        public static int GridTarget(WorldState world, string categoryId)
        {
            int n;
            if (!world.GridTargets.TryGetValue(categoryId, out n))
            {
                n = world.TeamsInCategory(categoryId).Count;
                world.GridTargets[categoryId] = n;
            }
            return n;
        }

        public static void RecordGrids(WorldState world, GameDatabase db)
        {
            foreach (var cat in db.CategoriesOrdered())
                if (cat.Simulated) GridTarget(world, cat.Id);
        }

        /// <summary>
        /// End of season, after the owners have covered the year's losses and before the market: owners whose
        /// patience has run out sell or withdraw, then empty places are filled by new entrants.
        /// </summary>
        public static void SeasonEnd(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            RecordGrids(world, db);
            var orgs = new List<Organisation>(world.Organisations.Values);
            orgs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var org in orgs)
            {
                if (org.Owner != OwnerKind.Private || org.TeamIds.Count == 0) continue;
                int patience = 2 + (int)(org.OwnerBacking / 25);
                if (org.OwnerFundedSeasons < patience) continue;
                org.OwnerFundedSeasons = 0;
                foreach (var id in new List<string>(org.TeamIds))
                {
                    var team = world.GetTeam(id);
                    if (team == null || team.IsPlayer) continue;
                    var cat = db.GetCategory(team.CategoryId);
                    if (cat == null || !cat.Simulated) continue;
                    // A team with a name finds a buyer; an unknown one closes.
                    if (team.Reputation >= 30 && rng.Chance(0.4 + team.Reputation / 200.0)) SellToNewOwners(world, db, org, team, rng);
                    else
                    {
                        world.AddNews(Txt.F("news.aiWithdraws", "{0} pulls out", team.Name),
                            Txt.F("news.aiWithdrawsBody", "Its owners will not keep covering the losses; the place goes back to the championship."), team.CategoryId, cat.Tier == 1);
                        CareerService.Withdraw(world, db, team);
                    }
                }
            }

            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated || cat.IsCup) continue;
                int target = GridTarget(world, cat.Id);
                int guard = 0;
                while (world.TeamsInCategory(cat.Id).Count < target && guard++ < 10)
                    if (NewEntrant(world, db, cat, rng) == null) break;
            }
        }

        /// <summary>New owners buy the team: it keeps racing with its riders, staff, sponsors and manager.</summary>
        public static void SellToNewOwners(WorldState world, GameDatabase db, Organisation seller, Team team, DeterministicRandom rng)
        {
            double price = Math.Round(OrganisationService.Value(world, db, team).Total * rng.Range(0.7, 1.0) / 1000) * 1000;
            var buyer = new Organisation
            {
                Id = world.NewId("org"), Name = team.Name, Owner = OwnerKind.Private, OwnerBacking = Math.Round(rng.Range(40, 80)),
                DataConfidence = "generated (new owners)"
            };
            world.Organisations[buyer.Id] = buyer;
            Accounts.Post(world, buyer, null, LedgerKind.OwnerInvestment, price + team.SeasonBudget * 0.4, "New owners", true, false);
            Accounts.Post(world, buyer, null, LedgerKind.TeamSale, -price, seller.Name, true, false);
            Accounts.Post(world, seller, null, LedgerKind.TeamSale, price, buyer.Name, true, false);
            seller.TeamIds.Remove(team.Id);
            buyer.TeamIds.Add(team.Id);
            team.OrganisationId = buyer.Id;
            var mgr = world.GetManager(team.ManagerId);
            if (mgr != null && mgr.Job != null && !mgr.IsPlayer) mgr.Job.OrganisationId = buyer.Id;
            var cat = db.GetCategory(team.CategoryId);
            world.AddNews(Txt.F("news.teamSold", "{0} changes hands", team.Name),
                Txt.F("news.aiSoldBody", "{0} sells the team to new owners after several seasons of losses.", seller.Name), team.CategoryId, cat != null && cat.Tier == 1);
        }

        /// <summary>
        /// A new team takes a free place: new owners with fresh capital, a bike from the manufacturer with the fewest
        /// customers in the category, a basic staff and a manager; riders come from the market.
        /// </summary>
        public static Team NewEntrant(WorldState world, GameDatabase db, ChampionshipCategory cat, DeterministicRandom rng)
        {
            var suppliers = CareerService.Suppliers(world, cat.Id);
            if (suppliers.Count == 0) return null;
            Manufacturer supplier = null;
            int fewest = int.MaxValue;
            foreach (var m in suppliers)
            {
                int n = 0;
                foreach (var t in world.TeamsInCategory(cat.Id)) if (t.ManufacturerId == m.Id) n++;
                n = n * 10 + rng.NextInt(0, 3);
                if (n < fewest) { fewest = n; supplier = m; }
            }
            var rules = db.GetRules(cat.Id, world.Season);
            var pools = new List<NamePool>(db.NamePools.Values);
            pools.Sort((a, b) => string.CompareOrdinal(a.Nationality, b.Nationality));
            var pool = pools.Count > 0 ? rng.PickWeighted(pools, p => cat.Pyramid == Pyramid.GrandPrix ? p.WeightGrandPrix : p.WeightSuperbike) : null;
            string word = pool != null && pool.LastNames.Count > 0 ? rng.Pick(pool.LastNames) : "Nova";
            string name = string.Format(rng.Pick(Patterns), word);

            double reputation = Math.Round(rng.Range(18, 34));
            double operating = rules.Economy.TypicalRiderSalary * rules.RidersPerTeam * 3;
            var probe = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = cat.Id, Capital = 1e12, Reputation = reputation });
            if (probe.OperatingCost > 0) operating = probe.OperatingCost;
            var org = new Organisation
            {
                Id = world.NewId("org"), Name = name, Owner = OwnerKind.Private, OwnerBacking = Math.Round(rng.Range(45, 85)),
                DataConfidence = "generated (new entrant)"
            };
            world.Organisations[org.Id] = org;
            double startup = (rules.Economy.EntryModel == EntryModel.SlotPurchase ? rules.Economy.SlotPrice : rules.Economy.EntryFee) +
                             rules.Economy.StartupEquipmentCost + rules.Economy.BikePurchasePerRider * rules.RidersPerTeam;
            Accounts.Post(world, org, null, LedgerKind.OwnerInvestment, startup + operating * 0.6, "Founders", true, false);
            string primary = rng.Pick(Colours);
            string secondary = primary == "#FFFFFF" ? "#111111" : rng.Chance(0.6) ? "#FFFFFF" : rng.Pick(Colours);
            var team = CareerService.EnterTeam(world, db, org, cat, supplier, name, null, primary, secondary, reputation, operating,
                pool != null ? pool.Nationality : null, true);
            if (team == null) return null;
            team.DataConfidence = "generated (new entrant)";
            StaffService.EnsureStaff(world, db, team, rng.Fork("entrant-staff-" + team.Id), StaffService.SalaryScale(world, db, team));
            ManagerMarket.FillVacancies(world, db, rng.Fork("entrant-manager-" + team.Id), false, world.Season + 1);
            world.AddNews(Txt.F("news.newEntrant", "{0} joins {1}", name, cat.Name),
                Txt.F("news.newEntrantBody", "A new team takes the free place with {0} machinery.", supplier.Name), cat.Id, cat.Tier == 1);
            return team;
        }
    }
}
