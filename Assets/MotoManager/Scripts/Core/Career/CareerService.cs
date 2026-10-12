using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    public class FoundingPlan
    {
        public string CategoryId;
        public string ManufacturerId;
        public string TeamName;
        public string ShortName;
        /// <summary>Part of the manager's personal wealth put into the new organisation.</summary>
        public double Investment;
        /// <summary>Loan taken by the new organisation (0 = none).</summary>
        public double Loan;
        public string PrimaryColor = "#E10600";
        public string SecondaryColor = "#FFFFFF";
    }

    public class CareerResult
    {
        public bool Ok;
        public string Message;
        public Team Team;
        public ViabilityReport Viability;
    }

    public class LoanTerms
    {
        public double MaxAmount;
        public double Rate;
        public int Seasons;
    }

    /// <summary>
    /// The manager's career: the player's profile, starting as an employee of an existing team or founding an
    /// organisation, salaries, loans and the AI managers that run every other team. The manager is never the team:
    /// employment, ownership and money stay separate.
    /// </summary>
    public static class CareerService
    {
        // ------------------------------------------------------------------ managers

        public static ManagerProfile CreatePlayer(WorldState world, GameDatabase db, string firstName, string lastName, string nationality, string backgroundId)
        {
            ManagerBackground bg;
            if (!db.ManagerBackgrounds.TryGetValue(backgroundId ?? "", out bg)) bg = new ManagerBackground { Id = "custom", Reputation = 30, Negotiation = 50, SportingManagement = 50, Finance = 50, Relations = 50, Commercial = 50 };
            var m = new ManagerProfile
            {
                Id = world.NewId("mgr"),
                FirstName = string.IsNullOrEmpty(firstName) ? "Alex" : firstName,
                LastName = string.IsNullOrEmpty(lastName) ? "Manager" : lastName,
                Nationality = nationality ?? "ES",
                BackgroundId = bg.Id,
                IsPlayer = true,
                BirthYear = world.Season - 38,
                Reputation = bg.Reputation,
                Negotiation = bg.Negotiation,
                SportingManagement = bg.SportingManagement,
                Finance = bg.Finance,
                Relations = bg.Relations,
                Commercial = bg.Commercial,
                PersonalWealth = bg.PersonalWealth,
                DataConfidence = "player"
            };
            world.Managers[m.Id] = m;
            world.PlayerManagerId = m.Id;
            return m;
        }

        /// <summary>Every team gets an AI manager (invented people: real team managers are not modelled).</summary>
        public static void EnsureAiManagers(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            // Real team managers from the data run their teams.
            var real = new List<StaffTemplate>(db.Staff.Values);
            real.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var s in real)
            {
                var team = world.GetTeam(s.TeamId);
                if (s.Role != StaffRole.TeamManager || team == null || world.GetManager(team.ManagerId) != null) continue;
                double k = s.Skill;
                var m = new ManagerProfile
                {
                    Id = "mgr_" + s.Id, FirstName = s.FirstName, LastName = s.LastName, Nationality = s.Nationality, BirthYear = s.BirthYear,
                    Reputation = k, Negotiation = k, SportingManagement = k, Finance = k - 5, Relations = k, Commercial = k - 5,
                    DataConfidence = "real person, approximate values"
                };
                world.Managers[m.Id] = m;
                Employ(world, db, m, team, team.Reputation > 70 ? Autonomy.Limited : Autonomy.Standard, 2);
            }
            // The rest: managers out of work first, then new names.
            ManagerMarket.FillVacancies(world, db, rng, false);
        }

        public static ManagerProfile GenerateAiManager(WorldState world, GameDatabase db, DeterministicRandom rng, double level)
        {
            var pools = new List<NamePool>(db.NamePools.Values);
            pools.Sort((a, b) => string.CompareOrdinal(a.Nationality, b.Nationality));
            var pool = pools.Count > 0 ? rng.PickWeighted(pools, p => p.WeightGrandPrix + p.WeightSuperbike) : null;
            Func<double> skill = () => Math.Round(MathX.Clamp(level * 0.6 + 20 + rng.Gaussian(0, 10), 15, 95));
            var m = new ManagerProfile
            {
                Id = world.NewId("mgr"),
                FirstName = pool != null && pool.FirstNames.Count > 0 ? rng.Pick(pool.FirstNames) : "Sam",
                LastName = pool != null && pool.LastNames.Count > 0 ? rng.Pick(pool.LastNames) : "Manager",
                Nationality = pool != null ? pool.Nationality : "IT",
                BirthYear = world.Season - rng.NextInt(35, 66),
                Reputation = Math.Round(MathX.Clamp(level * 0.8 + rng.Gaussian(0, 8), 5, 95)),
                Negotiation = skill(),
                SportingManagement = skill(),
                Finance = skill(),
                Relations = skill(),
                Commercial = skill(),
                DataConfidence = "generated (fictional person)"
            };
            world.Managers[m.Id] = m;
            return m;
        }

        /// <summary>Places a manager at a team under a new contract (the previous manager, if any, leaves).</summary>
        public static void Employ(WorldState world, GameDatabase db, ManagerProfile m, Team team, Autonomy autonomy, int seasons, double salary = -1, int startSeason = 0)
        {
            int from = startSeason > 0 ? startSeason : world.Season;
            var previous = world.GetManager(team.ManagerId);
            if (previous != null && previous != m) EndJob(world, previous, "Replaced");
            if (m.Job != null) EndJob(world, m, "Moved");
            var rules = db.GetRules(team.CategoryId, world.Season);
            m.Job = new ManagerContract
            {
                OrganisationId = team.OrganisationId,
                TeamId = team.Id,
                Role = ManagerRole.TeamManager,
                StartSeason = from,
                EndSeason = from + Math.Max(1, seasons) - 1,
                SalaryPerSeason = salary >= 0 ? salary : SalaryFor(rules, m, team),
                Autonomy = autonomy,
                AssignedBudget = team.SeasonBudget
            };
            team.ManagerId = m.Id;
            m.History.Add(new ManagerStint { OrganisationId = team.OrganisationId, TeamId = team.Id, TeamName = team.Name, CategoryId = team.CategoryId, Role = ManagerRole.TeamManager, FromSeason = from, ToSeason = from });
        }

        public static void EndJob(WorldState world, ManagerProfile m, string reason)
        {
            if (m.Job == null) return;
            var team = world.GetTeam(m.Job.TeamId);
            if (team != null && team.ManagerId == m.Id) team.ManagerId = null;
            if (m.History.Count > 0)
            {
                var last = m.History[m.History.Count - 1];
                last.ToSeason = world.Season;
                last.EndReason = reason;
            }
            m.Job = null;
        }

        /// <summary>Market salary of a manager for a team: the category's level, the manager's standing and the team's.</summary>
        public static double SalaryFor(CategoryRules rules, ManagerProfile m, Team team)
        {
            double s = rules.Economy.ManagerSalary * (0.6 + m.Reputation / 100.0) * (0.8 + team.Reputation / 250.0);
            return Math.Round(s / 1000) * 1000;
        }

        /// <summary>
        /// Paid with each of the team's events: the organisation's cost, the manager's own income (personal wealth,
        /// outside the organisation's accounts).
        /// </summary>
        public static void PaySalary(WorldState world, Team team, int roundsInSeason)
        {
            var m = world.GetManager(team.ManagerId);
            if (m == null || m.Job == null || m.Job.TeamId != team.Id || m.Job.SalaryPerSeason <= 0) return;
            double share = Math.Round(m.Job.SalaryPerSeason / Math.Max(1, roundsInSeason), 2);
            Accounts.Post(world, team, LedgerKind.ManagerSalary, -share, m.FullName);
            m.PersonalWealth += share;
        }

        // ------------------------------------------------------------------ starting as an employee

        /// <summary>
        /// Teams that would hire the player now. Boards weigh reputation, sporting management and contacts against
        /// the size of the job: a newcomer finds work in the smaller categories, not at a MotoGP works team.
        /// </summary>
        public static List<JobOffer> JobOffers(WorldState world, GameDatabase db, ManagerProfile m)
        {
            var offers = new List<JobOffer>();
            double standing = m.Reputation + m.SportingManagement * 0.25 + m.Relations * 0.15;
            var teams = new List<Team>(world.Teams.Values);
            teams.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var team in teams)
            {
                var cat = db.GetCategory(team.CategoryId);
                if (cat == null || !cat.Simulated || team.IsPlayer) continue;
                var org = world.OrganisationOf(team);
                if (org == null || org.Owner == OwnerKind.Player) continue;
                // Only some boards are looking for a manager right now, and not those the manager fell out with.
                if (!Vacant(world, team)) continue;
                double relation = m.RelationWith(org.Id);
                if (relation <= -20) continue;
                double required = cat.Prestige * 0.35 + team.Reputation * 0.45 + (team.Status == TeamStatus.Factory ? 10 : 0);
                if (standing + relation * 0.3 < required) continue;
                var rules = db.GetRules(cat.Id, world.Season);
                offers.Add(new JobOffer
                {
                    TeamId = team.Id,
                    OrganisationId = team.OrganisationId,
                    CategoryId = cat.Id,
                    SalaryPerSeason = SalaryFor(rules, m, team),
                    Seasons = team.Reputation > 60 ? 1 : 2,
                    Autonomy = team.Reputation > 70 || org.Owner == OwnerKind.Manufacturer ? Autonomy.Limited : team.Reputation > 40 ? Autonomy.Standard : Autonomy.Full,
                    AssignedBudget = team.SeasonBudget,
                    Objective = TeamAI.Of(team).Objective
                });
            }
            offers.Sort((a, b) =>
            {
                int c = world.GetTeam(b.TeamId).Reputation.CompareTo(world.GetTeam(a.TeamId).Reputation);
                return c != 0 ? c : string.CompareOrdinal(a.TeamId, b.TeamId);
            });
            if (offers.Count > MaxOffers) offers.RemoveRange(MaxOffers, offers.Count - MaxOffers);
            return offers;
        }

        public const int MaxOffers = 6;

        /// <summary>Whether a team's board is looking for a manager (stable for a season; about a third of teams).</summary>
        public static bool Vacant(WorldState world, Team team)
        {
            var current = world.GetManager(team.ManagerId);
            if (current == null || current.Job == null) return true;
            if (current.IsPlayer) return false;
            // A board whose manager's contract is running out, or who has lost faith in him, listens to offers.
            if (current.Job.EndSeason <= world.Season || current.Job.BoardConfidence < ManagerCareer.RenewFrom) return true;
            return DeterministicRandom.Combine(world.MasterSeed, DeterministicRandom.Hash(world.Season + "|vacancy|" + team.Id)) % 100 < 25;
        }

        /// <summary>The player takes the job: he runs the team; the organisation keeps owning it and its money.</summary>
        public static CareerResult AcceptJob(WorldState world, GameDatabase db, ManagerProfile m, JobOffer offer)
        {
            var team = world.GetTeam(offer.TeamId);
            if (team == null) return new CareerResult { Message = Txt.F("career.e.noTeam", "The team no longer exists.") };
            Employ(world, db, m, team, offer.Autonomy, offer.Seasons, offer.SalaryPerSeason);
            Season.WorldFactory.SetPlayerTeam(world, team.Id);
            return new CareerResult { Ok = true, Team = team, Message = Txt.F("career.r.hired", "{0} is the new manager of {1}.", m.FullName, team.Name) };
        }

        // ------------------------------------------------------------------ loans

        /// <summary>
        /// What a bank would lend a new project: a multiple of the owner's own stake, better with financial skill
        /// and reputation; the rate falls with both.
        /// </summary>
        public static LoanTerms LoanTermsFor(ManagerProfile m, double investment)
        {
            return new LoanTerms
            {
                MaxAmount = Math.Round(Math.Max(0, investment) * (0.2 + m.Finance / 200.0 + m.Reputation / 400.0) / 10000) * 10000,
                Rate = Math.Round(0.11 - m.Finance * 0.0004 - m.Reputation * 0.0002, 4),
                Seasons = 4
            };
        }

        public static Loan TakeLoan(WorldState world, Organisation org, double amount, double rate, int seasons)
        {
            if (amount <= 0) return null;
            var loan = new Loan { Id = world.NewId("loan"), Principal = amount, Outstanding = amount, Rate = rate, Seasons = Math.Max(1, seasons), StartSeason = world.Season };
            org.Loans.Add(loan);
            Accounts.Post(world, org, null, LedgerKind.LoanDrawdown, amount, "Loan", true, false);
            return loan;
        }

        /// <summary>Instalments and interest, once per event of the organisation's main team.</summary>
        public static void ServiceLoans(WorldState world, Organisation org, int roundsInSeason)
        {
            for (int i = org.Loans.Count - 1; i >= 0; i--)
            {
                var loan = org.Loans[i];
                double interest = Math.Round(loan.Outstanding * loan.Rate / Math.Max(1, roundsInSeason), 2);
                double principal = Math.Min(loan.Outstanding, Math.Round(loan.Principal / (loan.Seasons * Math.Max(1, roundsInSeason)), 2));
                Accounts.Post(world, org, null, LedgerKind.Interest, -interest, "Loan interest");
                Accounts.Post(world, org, null, LedgerKind.LoanRepayment, -principal, "Loan repayment", true, false);
                loan.Outstanding = Math.Round(loan.Outstanding - principal, 2);
                if (loan.Outstanding <= 0.01) org.Loans.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ founding

        /// <summary>
        /// Sponsors interested in backing a new project of this manager. Each offer is tied to one category and only
        /// pays if the team is founded there. Commercial skill and reputation bring more and better approaches.
        /// </summary>
        public static List<SponsorOffer> FoundingSponsorOffers(WorldState world, GameDatabase db, ManagerProfile m, DeterministicRandom rng)
        {
            world.SponsorOffers.RemoveAll(o => o.TeamId == FoundingProbeId);
            var list = new List<SponsorOffer>();
            int count = 1 + (int)(m.Commercial / 25) + (m.Reputation > 40 ? 1 : 0);
            var cats = new List<ChampionshipCategory>();
            foreach (var c in db.CategoriesOrdered()) if (c.Simulated && !c.IsCup) cats.Add(c);
            var engaged = SponsorService.Commitments(world);
            for (int i = 0; i < count && cats.Count > 0; i++)
            {
                var cat = rng.Pick(cats);
                var probe = new Team { Id = FoundingProbeId, CategoryId = cat.Id, Reputation = Math.Max(10, m.Reputation * 0.6 + m.Commercial * 0.2) };
                var candidates = new List<SponsorTemplate>();
                foreach (var s in SponsorService.AllTemplates(world, db))
                    if (engaged.CanApproach(s, probe.Id) && SponsorService.Interest(world, db, s, probe, cat) > 0.3) candidates.Add(s);
                if (candidates.Count == 0) continue;
                var pick = rng.Pick(candidates);
                var offer = SponsorMarket.MakeOffer(world, db, probe, cat, pick, world.Season, rng, false);
                if (offer == null) continue;
                offer.RequiredCategoryId = cat.Id;
                offer.Objectives = new List<SponsorObjective>();
                engaged.Add(pick.Id, probe.Id);
                world.SponsorOffers.Add(offer);
                list.Add(offer);
            }
            return list;
        }

        public const string FoundingProbeId = "_founding";

        public static double ConfirmedFoundingSponsorship(WorldState world, string categoryId)
        {
            double total = 0;
            foreach (var o in world.SponsorOffers)
                if (o.TeamId == FoundingProbeId && o.RequiredCategoryId == categoryId) total += o.PaymentPerSeason;
            return total;
        }

        /// <summary>The viability of every category for this manager's project (founding sponsors count where they apply).</summary>
        public static List<ViabilityReport> FoundingOptions(WorldState world, GameDatabase db, ManagerProfile m, double investment, double loan)
        {
            var list = new List<ViabilityReport>();
            foreach (var cat in db.CategoriesOrdered())
                if (cat.Simulated) list.Add(ViabilityService.Evaluate(world, db, PlanFor(world, m, cat.Id, investment, loan)));
            return list;
        }

        private static TeamPlan PlanFor(WorldState world, ManagerProfile m, string categoryId, double investment, double loan)
        {
            var terms = LoanTermsFor(m, investment);
            return new TeamPlan
            {
                CategoryId = categoryId, Capital = investment + loan, Reputation = NewTeamReputation(m),
                ConfirmedSponsorIncome = ConfirmedFoundingSponsorship(world, categoryId),
                LoanAmount = loan, LoanRate = terms.Rate, LoanSeasons = terms.Seasons
            };
        }

        public static double NewTeamReputation(ManagerProfile m)
        {
            return Math.Round(MathX.Clamp(m.Reputation * 0.5 + m.Commercial * 0.1 + 5, 5, 45));
        }

        /// <summary>Manufacturers that supply bikes to private teams in a category.</summary>
        public static List<Manufacturer> Suppliers(WorldState world, string categoryId)
        {
            var list = new List<Manufacturer>();
            foreach (var m in world.Manufacturers.Values) if (m.Programs.ContainsKey(categoryId)) list.Add(m);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>
        /// Founds the player's organisation and team. Only categories the money can sustain are accepted (risky ones
        /// are allowed, unviable ones are not). In a closed grid the place is bought from the weakest team, which
        /// withdraws; its riders and staff become free. Every euro is posted: investment, loan, entry, equipment, bikes.
        /// </summary>
        public static CareerResult Found(WorldState world, GameDatabase db, ManagerProfile m, FoundingPlan plan)
        {
            var cat = db.GetCategory(plan.CategoryId);
            if (cat == null || !cat.Simulated) return Fail(Txt.F("career.e.category", "Unknown category."));
            if (plan.Investment <= 0 || plan.Investment > m.PersonalWealth + 0.01) return Fail(Txt.F("career.e.wealth", "You cannot invest more than your personal wealth."));
            var terms = LoanTermsFor(m, plan.Investment);
            if (plan.Loan > terms.MaxAmount + 0.01) return Fail(Txt.F("career.e.loan", "The bank will lend at most {0}.", MathX.FormatMoney(terms.MaxAmount)));
            var report = ViabilityService.Evaluate(world, db, PlanFor(world, m, cat.Id, plan.Investment, plan.Loan));
            if (report.Verdict == Viability.NotAvailable || report.Verdict == Viability.Unviable)
                return new CareerResult { Viability = report, Message = Txt.F("career.e.viable", "This project is not financially viable in {0}.", cat.Name) };
            var supplier = world.GetManufacturer(plan.ManufacturerId);
            string baseBikeId;
            if (supplier == null || !supplier.Programs.TryGetValue(cat.Id, out baseBikeId)) return Fail(Txt.F("career.e.supplier", "That manufacturer does not supply bikes in {0}.", cat.Name));
            var rules = db.GetRules(cat.Id, world.Season);
            var rng = world.Rng.Fork("founding-" + m.Id);

            // The organisation and the owner's money.
            var org = new Organisation
            {
                Id = world.NewId("org"),
                Name = plan.TeamName,
                Owner = OwnerKind.Player,
                OwnerManagerId = m.Id,
                DetailedLedger = true,
                OwnerBacking = 0,
                DataConfidence = "player"
            };
            world.Organisations[org.Id] = org;
            m.PersonalWealth -= plan.Investment;
            Accounts.Post(world, org, null, LedgerKind.OwnerInvestment, plan.Investment, m.FullName, true, false);
            if (plan.Loan > 0) TakeLoan(world, org, plan.Loan, terms.Rate, terms.Seasons);

            var team = EnterTeam(world, db, org, cat, supplier, plan.TeamName, plan.ShortName, plan.PrimaryColor, plan.SecondaryColor, NewTeamReputation(m), report.OperatingCost, m.Nationality);
            if (team == null) return Fail(Txt.F("career.e.noSeller", "No team is willing to sell its place."));

            // People: the owner runs it; riders and staff are the owner's to hire (the team starts empty).
            if (m.Job != null)
            {
                var previous = world.GetTeam(m.Job.TeamId);
                EndJob(world, m, "Founded own team");
                if (previous != null)
                {
                    previous.IsPlayer = false;
                    EnsureAiManagers(world, db, world.Rng.Fork("manager-" + previous.Id + "-" + world.Season));
                }
            }
            m.Job = new ManagerContract { OrganisationId = org.Id, TeamId = team.Id, Role = ManagerRole.OwnerManager, StartSeason = world.Season, EndSeason = world.Season + 99, Autonomy = Autonomy.Full, AssignedBudget = team.SeasonBudget, BoardConfidence = 100 };
            m.History.Add(new ManagerStint { OrganisationId = org.Id, TeamId = team.Id, TeamName = team.Name, CategoryId = cat.Id, Role = ManagerRole.OwnerManager, FromSeason = world.Season, ToSeason = world.Season });
            team.ManagerId = m.Id;

            // Founding sponsors for this category become deals; the others lapse.
            foreach (var o in new List<SponsorOffer>(world.SponsorOffers))
            {
                if (o.TeamId != FoundingProbeId) continue;
                world.SponsorOffers.Remove(o);
                if (o.RequiredCategoryId != cat.Id) continue;
                o.TeamId = team.Id;
                SponsorService.Sign(world, team, o);
            }
            team.Shop.Fans = MerchService.FansTarget(world, db, team);
            Season.WorldFactory.SetPlayerTeam(world, team.Id);
            TeamAI.SetObjectives(world, db);
            SponsorMarket.NewTeamOffers(world, db, team, world.Rng.Fork("new-team-sponsors-" + team.Id));
            world.AddNews(Txt.F("news.founds", "{0} founds {1}", m.FullName, team.Name), Txt.F("news.foundsBody", "A new team joins {0} with {1} machinery.", cat.Name, supplier.Name), cat.Id, true);
            return new CareerResult { Ok = true, Team = team, Viability = report, Message = Txt.F("career.r.born", "{0} is born.", team.Name) };
        }

        /// <summary>
        /// A new team of an organisation enters a category: the grid place (bought from the weakest private team in a
        /// closed grid, an entry fee in an open one), equipment and bikes, all posted to the organisation. The team
        /// starts without riders or staff. Null if no place can be bought.
        /// </summary>
        public static Team EnterTeam(WorldState world, GameDatabase db, Organisation org, ChampionshipCategory cat, Manufacturer supplier, string name, string shortName,
            string primary, string secondary, double reputation, double operatingCost, string country = null, bool vacantPlace = false)
        {
            var rules = db.GetRules(cat.Id, world.Season);
            string baseBikeId = supplier.Programs[cat.Id];
            // A place on the grid.
            if (rules.Economy.EntryModel == EntryModel.SlotPurchase && vacantPlace)
            {
                // A place left free by a team that withdrew: bought from the championship.
                Accounts.Post(world, org, null, LedgerKind.SlotPurchase, -rules.Economy.SlotPrice, cat.Name, true, false);
            }
            else if (rules.Economy.EntryModel == EntryModel.SlotPurchase)
            {
                var seller = Seller(world, db, cat.Id);
                if (seller == null) return null;
                Accounts.Post(world, org, null, LedgerKind.SlotPurchase, -rules.Economy.SlotPrice, seller.Name, true, false);
                var sellerOrg = world.OrganisationOf(seller);
                if (sellerOrg != null) Accounts.Post(world, sellerOrg, seller.Id, LedgerKind.SlotSale, rules.Economy.SlotPrice, name, true, false);
                Withdraw(world, db, seller);
            }
            else
            {
                Accounts.Post(world, org, null, LedgerKind.EntryFee, -rules.Economy.EntryFee, cat.Name);
            }

            // The team.
            var baseBike = world.GetBike(baseBikeId);
            var team = new Team
            {
                Id = world.NewId("team"),
                Name = name,
                ShortName = string.IsNullOrEmpty(shortName) ? name : shortName,
                ManufacturerId = supplier.Id,
                CategoryId = cat.Id,
                Status = TeamStatus.Independent,
                Country = country,
                Reputation = reputation,
                SeasonBudget = Math.Round(operatingCost / 1000) * 1000,
                OrganisationId = org.Id,
                DataConfidence = vacantPlace ? "generated (new entrant)" : "player"
            };
            team.Colors.Primary = primary;
            team.Colors.Secondary = string.IsNullOrEmpty(secondary) ? "#FFFFFF" : secondary;
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType))) team.Facilities[f] = 1;
            var bike = baseBike.Clone("bike_" + team.Id);
            world.Bikes[bike.Id] = bike;
            team.BikeId = bike.Id;
            world.Teams[team.Id] = team;
            org.TeamIds.Add(team.Id);
            team.Personality = TeamAI.Generate(world, db, team);
            double offset = TeamAI.SpecOffset(team);
            foreach (var comp in new List<string>(bike.Components.Keys)) bike.Components[comp] = MathX.Clamp(bike.Components[comp] - offset, 1, 99);
            Accounts.Post(world, team, LedgerKind.Equipment, -rules.Economy.StartupEquipmentCost, null, true, false);
            Accounts.Post(world, team, LedgerKind.BikePurchase, -rules.Economy.BikePurchasePerRider * rules.RidersPerTeam, null, true, false);

            team.Shop.Fans = MerchService.FansTarget(world, db, team);
            return team;
        }

        /// <summary>The team that sells its grid place: the weakest private team that is not part of a larger structure if possible.</summary>
        public static Team Seller(WorldState world, GameDatabase db, string categoryId)
        {
            Team best = null;
            double bestScore = double.MaxValue;
            foreach (var t in world.TeamsInCategory(categoryId))
            {
                var org = world.OrganisationOf(t);
                if (t.IsPlayer || org == null || org.Owner == OwnerKind.Player || org.Owner == OwnerKind.Manufacturer || t.Status == TeamStatus.Factory) continue;
                double score = t.Reputation + (org.TeamIds.Count > 1 ? 20 : 0) + org.Cash / 1e6;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }

        /// <summary>A team leaves the championship: riders and staff become free, sponsors and the manager leave.</summary>
        public static void Withdraw(WorldState world, GameDatabase db, Team team)
        {
            foreach (var id in new List<string>(team.RiderIds))
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                Contracts.Close(world, r, ContractEnd.TeamWithdrew);
                r.TeamId = null;
                r.Contract = null;
            }
            team.RiderIds.Clear();
            foreach (var id in new List<string>(team.StaffIds))
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) s.TeamId = null;
            }
            team.StaffIds.Clear();
            team.Sponsors.Clear();
            world.SponsorOffers.RemoveAll(o => o.TeamId == team.Id);
            var mgr = world.GetManager(team.ManagerId);
            if (mgr != null) EndJob(world, mgr, "Team withdrew");
            var org = world.OrganisationOf(team);
            if (org != null) org.TeamIds.Remove(team.Id);
            world.Teams.Remove(team.Id);
            world.Bikes.Remove(team.BikeId);
            world.AddNews(Txt.F("news.withdraws", "{0} withdraws", team.Name), Txt.F("news.withdrawsBody", "{0} sells its place on the grid.", team.Name), team.CategoryId, true);
        }

        private static CareerResult Fail(string message)
        {
            return new CareerResult { Message = message };
        }
    }
}
