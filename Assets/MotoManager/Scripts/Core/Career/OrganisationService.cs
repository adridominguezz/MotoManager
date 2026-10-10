using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>State of an organisation's cash.</summary>
    public enum CashAlert
    {
        None,
        /// <summary>Little cash left for what the season still costs.</summary>
        Low,
        /// <summary>Negative cash: something must be done before the end of the season.</summary>
        Insolvent
    }

    /// <summary>A second (third...) team for the player's organisation.</summary>
    public class ExpansionPlan
    {
        public string CategoryId;
        public string ManufacturerId;
        public string TeamName;
        public string ShortName;
        public string PrimaryColor = "#CC0000";
        public string SecondaryColor = "#FFFFFF";
        public double Loan;
    }

    /// <summary>What a team is worth to a buyer, piece by piece.</summary>
    public class TeamValuation
    {
        public double GridPlace;
        public double Facilities;
        public double Equipment;
        public double Inventory;
        public double Brand;
        public double Total { get { return GridPlace + Facilities + Equipment + Inventory + Brand; } }
    }

    /// <summary>What it costs to close a team: contracts to pay off and what the grid place still brings.</summary>
    public class ClosureQuote
    {
        public double RiderContracts;
        public double StaffContracts;
        public double GridPlaceRefund;
        public double Net { get { return GridPlaceRefund - RiderContracts - StaffContracts; } }
    }

    /// <summary>What moving a team to another category costs at the end of the season.</summary>
    public class CategoryChangeQuote
    {
        public string CategoryId;
        public double EntryCost;
        public double OldPlaceRefund;
        public double Bikes;
        public double Net { get { return EntryCost + Bikes - OldPlaceRefund; } }
        public ViabilityReport Viability;
        public bool PlaceAvailable = true;
    }

    /// <summary>
    /// The player's organisation as a business: opening more teams, moving a team to another category, selling or
    /// closing a team, and the ways out of a cash crisis (emergency loan, the owner's own money, selling). A
    /// player organisation still in the red at the end of a season is forced to sell; if even that does not cover
    /// its debts it goes bankrupt (the manager's reputation suffers, the game goes on).
    /// AI organisations are covered by their owners (see SeasonEndProcessor.OwnerFunding).
    /// </summary>
    public static class OrganisationService
    {
        /// <summary>A forced sale fetches this share of the team's value.</summary>
        public const double DistressShare = 0.6;
        /// <summary>The championship buys back a place in a closed grid at this share of its price.</summary>
        public const double PlaceBuyBackShare = 0.5;
        public const double EmergencyRate = 0.12;
        public const double BankruptcyReputation = 15;

        public static Organisation PlayerOrganisation(WorldState world)
        {
            return ManagerCareer.OwnedOrganisation(world);
        }

        public static List<Team> Teams(WorldState world, Organisation org)
        {
            var list = new List<Team>();
            if (org == null) return list;
            foreach (var id in org.TeamIds)
            {
                var t = world.GetTeam(id);
                if (t != null) list.Add(t);
            }
            return list;
        }

        /// <summary>Structural changes (new team, sale, closure) only happen between seasons.</summary>
        public static bool BetweenSeasons(GameController game)
        {
            return game.World.EventsCompleted == 0 || game.IsSeasonOver;
        }

        // ------------------------------------------------------------------ expansion

        /// <summary>Categories where the organisation could open another team, with their viability (its other teams' costs included).</summary>
        public static List<ViabilityReport> ExpansionOptions(WorldState world, GameDatabase db, Organisation org, double loan)
        {
            var list = new List<ViabilityReport>();
            if (org == null) return list;
            var have = new HashSet<string>();
            foreach (var t in Teams(world, org)) have.Add(t.CategoryId);
            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated || cat.IsCup || have.Contains(cat.Id)) continue;
                list.Add(ViabilityService.Evaluate(world, db, PlanFor(world, org, cat.Id, loan)));
            }
            return list;
        }

        private static TeamPlan PlanFor(WorldState world, Organisation org, string categoryId, double loan)
        {
            var m = world.PlayerManager;
            var terms = BankTerms(world, org);
            return new TeamPlan
            {
                CategoryId = categoryId, Capital = org.Cash + loan, Organisation = org,
                Reputation = m != null ? CareerService.NewTeamReputation(m) : 25,
                LoanAmount = loan, LoanRate = terms.Rate, LoanSeasons = terms.Seasons
            };
        }

        /// <summary>The bank looks at the organisation's cash and the owner's standing.</summary>
        public static LoanTerms BankTerms(WorldState world, Organisation org)
        {
            var m = world.GetManager(org.OwnerManagerId) ?? world.PlayerManager;
            double debt = 0;
            foreach (var l in org.Loans) debt += l.Outstanding;
            var terms = CareerService.LoanTermsFor(m, Math.Max(0, org.Cash));
            terms.MaxAmount = Math.Max(0, terms.MaxAmount - debt * 0.5);
            return terms;
        }

        /// <summary>
        /// Opens another team (between seasons, in a category where the organisation has none, only if the money
        /// holds). The new team starts empty and is run by a manager the owner can change in "My organisation".
        /// </summary>
        public static CareerResult Expand(GameController game, ExpansionPlan plan)
        {
            var world = game.World;
            var db = game.Db;
            var org = PlayerOrganisation(world);
            if (org == null) return Fail(Txt.F("org.e.noOrg", "You do not own an organisation."));
            if (!BetweenSeasons(game)) return Fail(Txt.F("org.e.season", "This can only be done between seasons."));
            var cat = db.GetCategory(plan.CategoryId);
            if (cat == null || !cat.Simulated || cat.IsCup) return Fail(Txt.F("career.e.category", "Unknown category."));
            foreach (var t in Teams(world, org))
                if (t.CategoryId == cat.Id) return Fail(Txt.F("org.e.sameCategory", "The organisation already has a team in {0}.", cat.Name));
            var terms = BankTerms(world, org);
            if (plan.Loan > terms.MaxAmount + 0.01) return Fail(Txt.F("career.e.loan", "The bank will lend at most {0}.", MathX.FormatMoney(terms.MaxAmount)));
            var report = ViabilityService.Evaluate(world, db, PlanFor(world, org, cat.Id, plan.Loan));
            if (report.Verdict == Viability.NotAvailable || report.Verdict == Viability.Unviable)
                return new CareerResult { Viability = report, Message = Txt.F("org.e.viable", "The organisation cannot sustain a team in {0}: it would run out of money.", cat.Name) };
            var supplier = world.GetManufacturer(plan.ManufacturerId);
            if (supplier == null || !supplier.Programs.ContainsKey(cat.Id)) return Fail(Txt.F("career.e.supplier", "That manufacturer does not supply bikes in {0}.", cat.Name));
            if (db.GetRules(cat.Id, world.Season).Economy.EntryModel == EntryModel.SlotPurchase && CareerService.Seller(world, db, cat.Id) == null)
                return Fail(Txt.F("career.e.noSeller", "No team is willing to sell its place."));

            if (plan.Loan > 0) CareerService.TakeLoan(world, org, plan.Loan, terms.Rate, terms.Seasons);
            string name = string.IsNullOrEmpty(plan.TeamName) ? org.Name + " " + cat.ShortName : plan.TeamName.Trim();
            var m = world.PlayerManager;
            var team = CareerService.EnterTeam(world, db, org, cat, supplier, name, plan.ShortName, plan.PrimaryColor, plan.SecondaryColor,
                m != null ? CareerService.NewTeamReputation(m) : 25, report.OperatingCost, m != null ? m.Nationality : null);
            if (team == null) return Fail(Txt.F("career.e.noSeller", "No team is willing to sell its place."));
            // A manager runs it from day one; he builds a basic staff (the owner can change the manager).
            var rng = world.Rng.Fork("expansion-" + team.Id);
            ManagerMarket.FillVacancies(world, db, rng, false);
            StaffService.EnsureStaff(world, db, team, rng, StaffService.SalaryScale(world, db, team));
            TeamAI.SetObjectives(world, db);
            world.AddNews(Txt.F("news.expands", "{0} enters {1}", org.Name, cat.Name), Txt.F("news.expandsBody", "{0} is the organisation's new team.", team.Name), cat.Id, true);
            return new CareerResult { Ok = true, Team = team, Viability = report, Message = Txt.F("org.r.expanded", "{0} is born.", team.Name) };
        }

        // ------------------------------------------------------------------ value, sale and closure

        public static TeamValuation Value(WorldState world, GameDatabase db, Team team)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            var v = new TeamValuation();
            v.GridPlace = rules.Economy.EntryModel == EntryModel.SlotPurchase ? rules.Economy.SlotPrice : rules.Economy.EntryFee * 0.5;
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType)))
                for (int l = 2; l <= team.Facility(f); l++)
                    v.Facilities += FinanceService.FacilityUpgradeUnit * l * l * rules.Economy.FacilityCostScale * TeamCapabilities.CostWeight(f) * 0.5;
            v.Equipment = rules.Economy.StartupEquipmentCost * 0.3;
            foreach (var i in team.Shop.Items) v.Inventory += i.StockValue * 0.5;
            v.Brand = (v.GridPlace + rules.Economy.StartupEquipmentCost) * MathX.Clamp(team.Reputation, 0, 100) / 100.0 * 0.4;
            v.GridPlace = Math.Round(v.GridPlace);
            v.Facilities = Math.Round(v.Facilities);
            v.Equipment = Math.Round(v.Equipment);
            v.Inventory = Math.Round(v.Inventory);
            v.Brand = Math.Round(v.Brand);
            return v;
        }

        /// <summary>
        /// Sells a team to a new private owner: the team keeps racing with its riders, staff, sponsors and manager.
        /// Allowed between seasons, or at any time while the organisation is insolvent (at a distressed price).
        /// </summary>
        public static CareerResult SellTeam(GameController game, Team team)
        {
            var world = game.World;
            var org = PlayerOrganisation(world);
            if (org == null || team == null || !org.TeamIds.Contains(team.Id)) return Fail(Txt.F("org.e.notYours", "That team is not yours."));
            bool distress = org.Cash < 0;
            if (!distress && !BetweenSeasons(game)) return Fail(Txt.F("org.e.season", "This can only be done between seasons."));
            double price = Sell(world, game.Db, org, team, distress);
            var res = new CareerResult { Ok = true, Team = team, Message = Txt.F("org.r.sold", "{0} sold for {1}.", team.Name, MathX.FormatMoney(price)) };
            WindUpIfEmpty(world, game.Db, org);
            return res;
        }

        private static double Sell(WorldState world, GameDatabase db, Organisation org, Team team, bool distress)
        {
            double price = Math.Round(Value(world, db, team).Total * (distress ? DistressShare : 1) / 1000) * 1000;
            var buyer = new Organisation
            {
                Id = world.NewId("org"), Name = team.Name, Owner = OwnerKind.Private, OwnerBacking = 50, DataConfidence = "generated (new owners)"
            };
            world.Organisations[buyer.Id] = buyer;
            // The new owners bring the price and a working reserve.
            Accounts.Post(world, buyer, null, LedgerKind.OwnerInvestment, price + team.SeasonBudget * 0.3, "New owners", true, false);
            Accounts.Post(world, buyer, null, LedgerKind.TeamSale, -price, org.Name, true, false);
            Accounts.Post(world, org, null, LedgerKind.TeamSale, price, buyer.Name, true, false);

            var player = world.PlayerManager;
            bool ran = player != null && player.Job != null && player.Job.TeamId == team.Id;
            org.TeamIds.Remove(team.Id);
            buyer.TeamIds.Add(team.Id);
            team.OrganisationId = buyer.Id;
            team.IsPlayer = false;
            team.PendingCategoryId = null;
            team.PendingSupplierId = null;
            var mgr = world.GetManager(team.ManagerId);
            if (mgr != null && mgr.Job != null && !mgr.IsPlayer) mgr.Job.OrganisationId = buyer.Id;
            if (ran)
            {
                CareerService.EndJob(world, player, "Team sold");
                if (world.PlayerTeamId == team.Id) world.PlayerTeamId = null;
                ManagerMarket.FillVacancies(world, db, world.Rng.Fork("sale-" + team.Id), false);
                // The owner takes charge of another of his teams if he has one.
                foreach (var other in Teams(world, org))
                {
                    ManagerCareer.RunOwnTeam(world, db, other);
                    break;
                }
            }
            world.AddNews(Txt.F("news.teamSold", "{0} changes hands", team.Name),
                distress ? Txt.F("news.teamSoldDistress", "{0} had to sell it to cover its debts.", org.Name) : Txt.F("news.teamSoldBody", "{0} sells the team to new owners.", org.Name), team.CategoryId, true);
            return price;
        }

        public static ClosureQuote ClosureCost(WorldState world, GameDatabase db, Team team)
        {
            var q = new ClosureQuote();
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.TeamId == team.Id) q.RiderContracts += Contracts.TerminationCost(world, db, r);
            }
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) q.StaffContracts += StaffMarket.SeveranceCost(world, s);
            }
            var rules = db.GetRules(team.CategoryId, world.Season);
            if (rules.Economy.EntryModel == EntryModel.SlotPurchase) q.GridPlaceRefund = Math.Round(rules.Economy.SlotPrice * PlaceBuyBackShare);
            return q;
        }

        /// <summary>
        /// Closes a team between seasons: riders and staff are paid off and leave, a place in a closed grid is bought
        /// back by the championship. The organisation keeps its other teams.
        /// </summary>
        public static CareerResult CloseTeam(GameController game, Team team)
        {
            var world = game.World;
            var db = game.Db;
            var org = PlayerOrganisation(world);
            if (org == null || team == null || !org.TeamIds.Contains(team.Id)) return Fail(Txt.F("org.e.notYours", "That team is not yours."));
            if (!BetweenSeasons(game)) return Fail(Txt.F("org.e.season", "This can only be done between seasons."));
            var q = ClosureCost(world, db, team);
            if (q.RiderContracts > 0) Accounts.Post(world, team, LedgerKind.RiderSeverance, -q.RiderContracts, "Closure");
            if (q.StaffContracts > 0) Accounts.Post(world, team, LedgerKind.Severance, -q.StaffContracts, "Closure");
            if (q.GridPlaceRefund > 0) Accounts.Post(world, org, team.Id, LedgerKind.SlotSale, q.GridPlaceRefund, "Championship", true, false);
            var player = world.PlayerManager;
            bool ran = player != null && player.Job != null && player.Job.TeamId == team.Id;
            if (ran) CareerService.EndJob(world, player, "Team closed");
            if (world.PlayerTeamId == team.Id) world.PlayerTeamId = null;
            string name = team.Name;
            CareerService.Withdraw(world, db, team);
            world.AddNews(Txt.F("news.teamClosed", "{0} closes", name), Txt.F("news.teamClosedBody", "{0} shuts the team down.", org.Name), team.CategoryId, true);
            if (ran)
                foreach (var other in Teams(world, org))
                {
                    ManagerCareer.RunOwnTeam(world, db, other);
                    break;
                }
            WindUpIfEmpty(world, db, org);
            return new CareerResult { Ok = true, Message = Txt.F("org.r.closed", "{0} has closed.", name) };
        }

        /// <summary>
        /// An organisation left without teams is wound up: loans are repaid from its cash and what remains goes to the
        /// owner. If the cash does not cover the debts, it is a bankruptcy: the rest is written off and the owner's
        /// reputation suffers.
        /// </summary>
        public static void WindUpIfEmpty(WorldState world, GameDatabase db, Organisation org)
        {
            if (org == null || org.TeamIds.Count > 0) return;
            var owner = world.GetManager(org.OwnerManagerId);
            foreach (var loan in new List<Loan>(org.Loans))
            {
                double pay = Math.Min(loan.Outstanding, Math.Max(0, org.Cash));
                if (pay > 0) Accounts.Post(world, org, null, LedgerKind.LoanRepayment, -pay, "Loan repaid", true, false);
                loan.Outstanding = Math.Round(loan.Outstanding - pay, 2);
            }
            bool bankrupt = false;
            foreach (var loan in org.Loans) if (loan.Outstanding > 0.01) bankrupt = true;
            if (org.Cash < -0.01) bankrupt = true;
            org.Loans.Clear();
            if (org.Cash > 0)
            {
                double rest = org.Cash;
                Accounts.Post(world, org, null, LedgerKind.OwnerWithdrawal, -rest, owner != null ? owner.FullName : "", true, false);
                if (owner != null) owner.PersonalWealth += rest;
            }
            if (bankrupt && owner != null)
            {
                owner.Reputation = MathX.Clamp(owner.Reputation - BankruptcyReputation, 0, 100);
                world.AddNews(Txt.F("news.bankrupt", "{0} goes bankrupt", org.Name), Txt.F("news.bankruptBody", "{0} could not pay its debts.", owner.FullName), null, true);
            }
            if (owner != null && owner.Job != null && owner.Job.OrganisationId == org.Id) CareerService.EndJob(world, owner, "Team sold");
            org.Owner = OwnerKind.Private;
            org.OwnerManagerId = null;
            org.DetailedLedger = false;
        }

        // ------------------------------------------------------------------ category change

        /// <summary>What moving the team to a category would cost at the end of the season (and whether the money holds).</summary>
        public static CategoryChangeQuote QuoteCategoryChange(WorldState world, GameDatabase db, Team team, string categoryId)
        {
            var q = new CategoryChangeQuote { CategoryId = categoryId };
            var to = db.GetRules(categoryId, world.Season);
            var from = db.GetRules(team.CategoryId, world.Season);
            q.EntryCost = to.Economy.EntryModel == EntryModel.SlotPurchase ? to.Economy.SlotPrice : to.Economy.EntryFee;
            q.PlaceAvailable = to.Economy.EntryModel != EntryModel.SlotPurchase || CareerService.Seller(world, db, categoryId) != null;
            q.OldPlaceRefund = from.Economy.EntryModel == EntryModel.SlotPurchase ? Math.Round(from.Economy.SlotPrice * PlaceBuyBackShare) : 0;
            q.Bikes = to.Economy.BikePurchasePerRider * to.RidersPerTeam;
            var org = world.OrganisationOf(team);
            if (org != null)
                q.Viability = ViabilityService.Evaluate(world, db, new TeamPlan
                {
                    CategoryId = categoryId, Capital = org.Cash + q.OldPlaceRefund, Organisation = org, Reputation = team.Reputation
                });
            return q;
        }

        /// <summary>Asks to move a team to another category at the end of the season (null cancels).</summary>
        public static CareerResult RequestCategoryChange(WorldState world, GameDatabase db, Team team, string categoryId, string supplierId)
        {
            var org = PlayerOrganisation(world);
            if (org == null || team == null || !org.TeamIds.Contains(team.Id)) return Fail(Txt.F("org.e.notYours", "That team is not yours."));
            if (categoryId == null || categoryId == team.CategoryId)
            {
                team.PendingCategoryId = null;
                team.PendingSupplierId = null;
                return new CareerResult { Ok = true, Team = team, Message = Txt.F("org.r.changeCancelled", "{0} stays in its category.", team.Name) };
            }
            var cat = db.GetCategory(categoryId);
            if (cat == null || !cat.Simulated || cat.IsCup) return Fail(Txt.F("career.e.category", "Unknown category."));
            foreach (var t in Teams(world, org))
                if (t.CategoryId == categoryId) return Fail(Txt.F("org.e.sameCategory", "The organisation already has a team in {0}.", cat.Name));
            var supplier = world.GetManufacturer(supplierId);
            if (supplier == null || !supplier.Programs.ContainsKey(categoryId)) return Fail(Txt.F("career.e.supplier", "That manufacturer does not supply bikes in {0}.", cat.Name));
            var q = QuoteCategoryChange(world, db, team, categoryId);
            if (!q.PlaceAvailable) return Fail(Txt.F("career.e.noSeller", "No team is willing to sell its place."));
            if (q.Viability != null && (q.Viability.Verdict == Viability.Unviable || q.Viability.Verdict == Viability.NotAvailable))
                return Fail(Txt.F("org.e.viable", "The organisation cannot sustain a team in {0}: it would run out of money.", cat.Name));
            team.PendingCategoryId = categoryId;
            team.PendingSupplierId = supplierId;
            return new CareerResult { Ok = true, Team = team, Message = Txt.F("org.r.changeRequested", "{0} will move to {1} at the end of the season.", team.Name, cat.Name) };
        }

        /// <summary>
        /// End of season, before the market: requested category changes take effect. The old place is bought back,
        /// the new one paid, new bikes bought; riders under contract and the staff move with the team.
        /// </summary>
        public static void ApplyCategoryChanges(WorldState world, GameDatabase db)
        {
            var teams = new List<Team>(world.Teams.Values);
            teams.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var team in teams)
            {
                if (team.PendingCategoryId == null) continue;
                var cat = db.GetCategory(team.PendingCategoryId);
                var supplier = world.GetManufacturer(team.PendingSupplierId);
                var org = world.OrganisationOf(team);
                string from = team.CategoryId;
                team.PendingCategoryId = null;
                team.PendingSupplierId = null;
                if (cat == null || org == null || supplier == null || !supplier.Programs.ContainsKey(cat.Id)) continue;
                var q = QuoteCategoryChange(world, db, team, cat.Id);
                var to = db.GetRules(cat.Id, world.Season);
                if (to.Economy.EntryModel == EntryModel.SlotPurchase)
                {
                    var seller = CareerService.Seller(world, db, cat.Id);
                    if (seller == null)
                    {
                        world.AddNews(Txt.F("news.moveFailed", "{0} stays in {1}", team.Name, db.GetCategory(from).Name), Txt.F("news.moveFailedBody", "No place could be bought."), from, true);
                        continue;
                    }
                    var sellerOrg = world.OrganisationOf(seller);
                    Accounts.Post(world, org, team.Id, LedgerKind.SlotPurchase, -to.Economy.SlotPrice, seller.Name, true, false);
                    if (sellerOrg != null) Accounts.Post(world, sellerOrg, seller.Id, LedgerKind.SlotSale, to.Economy.SlotPrice, team.Name, true, false);
                    CareerService.Withdraw(world, db, seller);
                }
                else Accounts.Post(world, team, LedgerKind.EntryFee, -to.Economy.EntryFee, cat.Name);
                if (q.OldPlaceRefund > 0) Accounts.Post(world, org, team.Id, LedgerKind.SlotSale, q.OldPlaceRefund, "Championship", true, false);
                Accounts.Post(world, team, LedgerKind.BikePurchase, -q.Bikes, null, true, false);

                team.CategoryId = cat.Id;
                team.ManufacturerId = supplier.Id;
                team.Status = TeamStatus.Independent;
                team.RiderSlots = 0;
                var baseBike = world.GetBike(supplier.Programs[cat.Id]);
                world.Bikes.Remove(team.BikeId);
                var bike = baseBike.Clone("bike_" + team.Id + "_" + world.Season);
                world.Bikes[bike.Id] = bike;
                team.BikeId = bike.Id;
                double offset = TeamAI.SpecOffset(team);
                foreach (var comp in new List<string>(bike.Components.Keys)) bike.Components[comp] = MathX.Clamp(bike.Components[comp] - offset, 1, 99);
                // Riders under contract (now or from next season) move with the team.
                foreach (var r in world.Riders.Values)
                    if (!r.IsRetired && (team.RiderIds.Contains(r.Id) || (r.Contract != null && r.Contract.TeamId == team.Id))) r.CategoryId = cat.Id;
                team.Shop.Fans = Math.Round(team.Shop.Fans * 0.5 + MerchService.FansTarget(world, db, team) * 0.5);
                world.AddNews(Txt.F("news.moves", "{0} moves to {1}", team.Name, cat.Name), Txt.F("news.movesBody", "The team leaves {0} with {1} machinery.", db.GetCategory(from).Name, supplier.Name), cat.Id, true);
            }
        }

        // ------------------------------------------------------------------ cash crisis

        public static CashAlert Alert(WorldState world, GameDatabase db, Organisation org)
        {
            if (org == null) return CashAlert.None;
            if (org.Cash < 0) return CashAlert.Insolvent;
            double budgets = 0;
            foreach (var t in Teams(world, org)) budgets += t.SeasonBudget;
            return org.Cash < budgets * 0.1 ? CashAlert.Low : CashAlert.None;
        }

        /// <summary>What an emergency loan would lend now (0 if one is already running or none is needed).</summary>
        public static double EmergencyLoanAmount(WorldState world, Organisation org)
        {
            if (org == null) return 0;
            foreach (var l in org.Loans) if (l.Emergency) return 0;
            double budgets = 0;
            foreach (var t in Teams(world, org)) budgets += t.SeasonBudget;
            double need = Math.Max(-org.Cash * 1.25, budgets * 0.15);
            return Math.Round(Math.Min(need, budgets * 0.5) / 10000) * 10000;
        }

        public static bool TakeEmergencyLoan(WorldState world, Organisation org)
        {
            double amount = EmergencyLoanAmount(world, org);
            if (amount <= 0) return false;
            var loan = CareerService.TakeLoan(world, org, amount, EmergencyRate, 2);
            loan.Emergency = true;
            return true;
        }

        /// <summary>The owner puts his own money into the organisation.</summary>
        public static bool Inject(WorldState world, Organisation org, double amount)
        {
            var owner = world.GetManager(org != null ? org.OwnerManagerId : null);
            if (owner == null || amount <= 0 || amount > owner.PersonalWealth + 0.01) return false;
            owner.PersonalWealth -= amount;
            Accounts.Post(world, org, null, LedgerKind.OwnerInvestment, amount, owner.FullName, true, false);
            return true;
        }

        /// <summary>After each event: counts events in the red and warns the owner once a crisis starts.</summary>
        public static void AfterEvent(WorldState world, GameDatabase db)
        {
            var org = PlayerOrganisation(world);
            if (org == null) return;
            if (org.Cash < 0)
            {
                org.InsolventEvents++;
                if (org.InsolventEvents == 1)
                    world.AddNews(Txt.F("news.cashCrisis", "{0} is out of cash", org.Name),
                        Txt.F("news.cashCrisisBody", "Take an emergency loan, put in your own money or sell a team before the season ends, or a sale will be forced."), null, true);
            }
            else org.InsolventEvents = 0;
        }

        /// <summary>
        /// End of season: a player organisation still in the red sells teams (weakest value first) at a distressed
        /// price until its cash is positive; without teams left it is wound up (bankruptcy if the debts remain).
        /// </summary>
        public static void SeasonEnd(WorldState world, GameDatabase db)
        {
            var org = PlayerOrganisation(world);
            if (org == null || org.Cash >= 0) return;
            var owner = world.GetManager(org.OwnerManagerId);
            var teams = Teams(world, org);
            teams.Sort((a, b) => Value(world, db, a).Total.CompareTo(Value(world, db, b).Total));
            foreach (var t in teams)
            {
                if (org.Cash >= 0) break;
                Sell(world, db, org, t, true);
            }
            if (owner != null) owner.Reputation = MathX.Clamp(owner.Reputation - 5, 0, 100);
            WindUpIfEmpty(world, db, org);
            org.InsolventEvents = 0;
        }

        private static CareerResult Fail(string message)
        {
            return new CareerResult { Message = message };
        }
    }
}
