using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    public class OfferResult
    {
        public bool Accepted;
        public string Message;
        /// <summary>Minimum salary the rider would have accepted (revealed after a rejection).</summary>
        public double AskingSalary;
        /// <summary>The terms the rider would sign (null when he will not talk any more).</summary>
        public ContractProposal Counter;
    }

    /// <summary>What a proposal costs the team, shown before confirming.</summary>
    public class ProposalCost
    {
        public double GuaranteedPerSeason;
        public double GuaranteedTotal;
        public double SigningBonus;
        /// <summary>Expected bonuses per season given the rider's level (an estimate).</summary>
        public double ExpectedBonusesPerSeason;
        public double TransferFee;
        public int FirstSeason;
        public int LastSeason;
        public double CashAfter;
    }

    public class TransferAnswer
    {
        public TransferStatus Status;
        public string Message;
        /// <summary>Fee the owning team would accept (when it does not accept the offer).</summary>
        public double Asking;
    }

    /// <summary>
    /// Negotiations with riders and between teams. A rider weighs guaranteed money, bonuses he expects to earn
    /// (more if he is ambitious), years, clauses and the seat itself, and either signs or says what he would sign.
    /// The same valuation is used for the player and the AI. Riders under contract need a release clause, an
    /// exit clause or a fee agreed with their team. Moves can start now (no transfer window) or next season.
    /// </summary>
    public static class ContractService
    {
        public const int Patience = 3;

        public static double AskingSalary(WorldState world, GameDatabase db, Team team, Rider rider)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            double demand = TransferMarket.SalaryDemand(rider, rules);
            if (rider.TeamId == team.Id) demand *= 1.0 - (rider.Personality.Loyalty - 50) / 250.0;
            demand *= 1.0 + (rider.Personality.Ambition - 50) / 300.0;
            return Math.Round(Math.Max(0, demand) / 1000) * 1000;
        }

        public static int SeatsFreeNextSeason(WorldState world, GameDatabase db, Team team)
        {
            var cat = db.GetCategory(team.CategoryId);
            int capacity = TransferMarket.Capacity(cat, db.GetRules(cat.Id, world.Season), team);
            int committed = 0;
            foreach (var r in world.Riders.Values)
            {
                if (r.IsRetired) continue;
                if (r.FutureContract != null) { if (r.FutureContract.TeamId == team.Id) committed++; continue; }
                if (r.TeamId == team.Id && (TransferMarket.IsCommitted(r, world.Season) || OptionExercised(r, world.Season))) committed++;
            }
            return capacity - committed;
        }

        /// <summary>The team has exercised its option on a contract that ends this season: the rider stays next year.</summary>
        public static bool OptionExercised(Rider r, int season)
        {
            if (r.IsRetired || r.Contract == null || r.Contract.EndSeason != season) return false;
            var opt = r.Contract.Clause(ClauseType.TeamOption);
            return opt != null && opt.Exercised;
        }

        public static int SeatsFreeNow(WorldState world, GameDatabase db, Team team)
        {
            var cat = db.GetCategory(team.CategoryId);
            return TransferMarket.Capacity(cat, db.GetRules(cat.Id, world.Season), team) - team.RiderIds.Count;
        }

        /// <summary>The usual starting point: asking salary, two seasons, standard bonuses.</summary>
        public static ContractProposal DefaultProposal(WorldState world, GameDatabase db, Team team, Rider rider)
        {
            double ask = AskingSalary(world, db, team, rider);
            return new ContractProposal
            {
                Salary = ask, Seasons = 2, WinBonus = Math.Round(ask * 0.05), PodiumBonus = Math.Round(ask * 0.02),
                ReleaseClause = Math.Round(ask * 2 * 2.5 / 1000) * 1000
            };
        }

        // ------------------------------------------------------------------ valuation

        /// <summary>Expected wins and podiums per season for the rider in the team's category (rough, from his level).</summary>
        public static void ExpectedResults(WorldState world, GameDatabase db, Team team, Rider rider, out double wins, out double podiums)
        {
            var levels = TransferMarket.CategoryLevels(world, db);
            double level;
            levels.TryGetValue(team.CategoryId ?? "", out level);
            double edge = (rider.Attributes.Overall - level) / 8.0 + (TransferMarket.BikeAppeal(world, team.CategoryId, team) - 0.5);
            int rounds = Math.Max(1, Season.GameController.RoundsInSeason(world, db, team.CategoryId));
            double winRate = MathX.Clamp(0.04 + edge * 0.12, 0, 0.6);
            double podiumRate = MathX.Clamp(0.12 + edge * 0.25, 0, 0.85);
            wins = winRate * rounds;
            podiums = Math.Max(0, podiumRate - winRate) * rounds;
        }

        public static ProposalCost Cost(WorldState world, GameDatabase db, Team team, Rider rider, ContractProposal p)
        {
            double wins, podiums;
            ExpectedResults(world, db, team, rider, out wins, out podiums);
            int first = p.StartNow ? world.Season : world.Season + 1;
            double fee = FeeNeeded(world, db, team, rider, p.StartNow);
            var c = new ProposalCost
            {
                GuaranteedPerSeason = p.Salary,
                GuaranteedTotal = p.Salary * Math.Max(1, p.Seasons),
                SigningBonus = p.SigningBonus,
                ExpectedBonusesPerSeason = wins * p.WinBonus + podiums * p.PodiumBonus,
                TransferFee = Math.Max(0, fee),
                FirstSeason = first,
                LastSeason = first + Math.Max(1, p.Seasons) - 1
            };
            c.CashAfter = Accounts.Cash(world, team) - c.SigningBonus - c.TransferFee;
            return c;
        }

        /// <summary>
        /// How much the deal is worth to the rider per season: salary, signing bonus spread over the deal, the
        /// bonuses he expects (valued more by ambitious riders, less by cautious ones), and clauses: freedom
        /// (low release clause, exit clause) is worth something to him, a team option costs him.
        /// </summary>
        public static double RiderValue(WorldState world, GameDatabase db, Team team, Rider rider, ContractProposal p)
        {
            double wins, podiums;
            ExpectedResults(world, db, team, rider, out wins, out podiums);
            double bonusWeight = 0.5 + rider.Personality.Ambition / 250.0;
            double value = p.Salary + p.SigningBonus / Math.Max(1, p.Seasons) + (wins * p.WinBonus + podiums * p.PodiumBonus) * bonusWeight;
            double baseSalary = Math.Max(1000, AskingSalary(world, db, team, rider));
            if (p.TeamOption) value -= baseSalary * 0.06;
            if (p.ExitForHigherCategory) value += baseSalary * (0.02 + rider.Personality.Ambition / 1000.0);
            if (p.ReleaseClause > 0 && p.ReleaseClause < baseSalary * 2) value += baseSalary * 0.04;
            if (p.PerformanceExtensionTopN > 0) value += baseSalary * 0.01;
            // Years: young riders like security, established stars prefer to keep options open.
            double age = rider.AgeOn(new DateTime(world.Season + 1, 3, 1));
            int preferred = rider.Reputation > 80 ? 1 : age < 24 ? 3 : age > 31 ? 1 : 2;
            value -= baseSalary * 0.03 * Math.Abs(Math.Max(1, p.Seasons) - preferred);
            return value;
        }

        // ------------------------------------------------------------------ proposing

        /// <summary>
        /// Puts the proposal to the rider. Accepted: the contract is signed (now or for next season), any fee is paid,
        /// the signing bonus too. Not accepted: the rider says what he would sign, until he loses patience.
        /// </summary>
        public static OfferResult Propose(WorldState world, GameDatabase db, Team team, Rider rider, ContractProposal p)
        {
            var result = new OfferResult { AskingSalary = AskingSalary(world, db, team, rider) };
            var cat = db.GetCategory(team.CategoryId);
            int start = p.StartNow ? world.Season : world.Season + 1;
            double age = rider.AgeOn(new DateTime(start, 3, 1));
            bool renewal = rider.TeamId == team.Id;
            if (rider.IsRetired) return Reject(result, Txt.F("neg.r.retired", "{0} has retired.", rider.FullName));
            if (rider.FutureContract != null && !(p.StartNow && rider.FutureContract.TeamId != team.Id)) return Reject(result, Txt.F("neg.r.signed", "{0} has already signed for next season.", rider.FullName));
            if (age < cat.MinAge || age > cat.MaxAge) return Reject(result, Txt.F("neg.r.age", "Age limits of {0} do not allow it.", cat.ShortName));
            if (!renewal && rider.CategoryId != cat.Id && age > cat.MaxEntryAge) return Reject(result, Txt.F("neg.r.tooOld", "Too old to enter {0}.", cat.ShortName));
            if (renewal && p.StartNow) return Reject(result, Txt.F("neg.r.alreadyHere", "He already races for you: renew from next season."));
            if (p.StartNow && SeatsFreeNow(world, db, team) <= 0) return Reject(result, Txt.F("neg.r.noSeatNow", "No free seat now: release a rider first."));
            // A renewal needs a seat like any other signing, unless the rider already holds his own seat for next year
            // (contract running on, or the team option exercised): then the new deal simply replaces that one.
            bool holdsSeat = renewal && (TransferMarket.IsCommitted(rider, world.Season) || OptionExercised(rider, world.Season));
            if (!p.StartNow && !holdsSeat && SeatsFreeNextSeason(world, db, team) <= 0)
                return Reject(result, Txt.F("neg.r.noSeatNext", "You have no free seat for next season."));

            // Under contract: a release clause, an exit clause or an agreement with his team is needed.
            double fee = FeeNeeded(world, db, team, rider, p.StartNow);
            if (fee < 0) return Reject(result, Txt.F("neg.r.underContract", "{0} is under contract until {1}: agree a transfer with his team first.", rider.FullName, rider.Contract.EndSeason));
            if (fee > 0 && Accounts.Cash(world, team) < fee + p.SigningBonus) return Reject(result, Txt.F("neg.r.noCash", "Not enough cash for the fee ({0}).", MathX.FormatMoney(fee)));

            string key = world.Season + "|" + team.Id + "|" + rider.Id;
            int attempts;
            world.NegotiationAttempts.TryGetValue(key, out attempts);
            if (attempts >= Patience) return Reject(result, Txt.F("neg.r.stopped", "{0} has stopped negotiating with you this season.", rider.FullName));

            // The seat must be attractive enough compared with what he has.
            var currentTeam = world.GetTeam(rider.TeamId);
            var currentCat = db.GetCategory(rider.CategoryId);
            double offerUtility = TransferMarket.SeatUtility(world, cat, team, rider);
            double required = result.AskingSalary;
            if (!renewal && currentCat != null && currentCat.Simulated && currentTeam != null)
            {
                double now = TransferMarket.SeatUtility(world, currentCat, currentTeam, rider);
                // A worse seat must be compensated with money (about 3% of the asking salary per utility point).
                if (offerUtility < now) required *= 1 + (now - offerUtility) * 0.03;
                if (offerUtility < now - 25) return Reject(result, Txt.F("neg.r.notInterested", "{0} is not interested in this seat.", rider.FullName));
            }
            double value = RiderValue(world, db, team, rider, p);
            if (value < required * 0.97)
            {
                world.NegotiationAttempts[key] = attempts + 1;
                var counter = Clone(p);
                counter.Salary = Math.Round(Math.Max(p.Salary, p.Salary + (required - value)) / 1000) * 1000;
                result.Counter = counter;
                result.AskingSalary = counter.Salary;
                return Reject(result, Txt.F("neg.r.counter", "{0} would sign for {1} per season with these terms.", rider.FullName, MathX.FormatMoney(counter.Salary)));
            }

            Sign(world, db, team, rider, p, fee);
            result.Accepted = true;
            result.Message = p.StartNow ? Txt.F("neg.r.joinsNow", "{0} joins now.", rider.FullName) : (renewal ? Txt.F("neg.r.renewed", "New contract until {0}.", world.Season + p.Seasons) : Txt.F("neg.r.joinsNext", "{0} will join for {1}.", rider.FullName, world.Season + 1));
            return result;
        }

        /// <summary>Old entry point: standard terms from next season at a given salary and length.</summary>
        public static OfferResult Offer(WorldState world, GameDatabase db, Team team, Rider rider, double salary, int years)
        {
            var p = DefaultProposal(world, db, team, rider);
            p.Salary = salary;
            p.Seasons = Math.Max(1, Math.Min(4, years));
            p.WinBonus = Math.Round(salary * 0.05);
            p.PodiumBonus = Math.Round(salary * 0.02);
            return Propose(world, db, team, rider, p);
        }

        /// <summary>
        /// The fee needed to sign the rider for the requested start: 0 if he is free by then, the release clause, a
        /// fee already agreed with his team, 0 with an applicable exit clause; -1 if an agreement is still needed.
        /// </summary>
        public static double FeeNeeded(WorldState world, GameDatabase db, Team team, Rider rider, bool startNow)
        {
            if (rider.TeamId == team.Id || rider.Contract == null) return 0;
            int from = startNow ? world.Season : world.Season + 1;
            if (rider.Contract.EndSeason < from) return 0;
            foreach (var a in world.TransferAgreements)
                if (a.RiderId == rider.Id && a.BuyerTeamId == team.Id && a.StartNow == startNow) return a.Fee;
            var cur = world.GetTeam(rider.Contract.TeamId);
            var curCat = cur != null ? db.GetCategory(cur.CategoryId) : null;
            var cat = db.GetCategory(team.CategoryId);
            if (rider.Contract.Clause(ClauseType.ExitForHigherCategory) != null && curCat != null && cat.Prestige > curCat.Prestige) return 0;
            double clause = Contracts.ReleaseClause(rider);
            return clause > 0 ? clause : -1;
        }

        private static void Sign(WorldState world, GameDatabase db, Team team, Rider rider, ContractProposal p, double fee)
        {
            int start = p.StartNow ? world.Season : world.Season + 1;
            var c = new Contract
            {
                Id = world.NewId("ctr"),
                TeamId = team.Id, ManufacturerId = team.ManufacturerId, StartSeason = start, EndSeason = start + Math.Max(1, p.Seasons) - 1,
                SalaryPerSeason = p.Salary, SigningBonus = p.SigningBonus, FactoryBacked = team.Status == TeamStatus.Factory,
                RiderFunding = TransferMarket.FundingFor(world, db, team, rider)
            };
            Contracts.StandardTerms(c);
            c.RaceBonuses = new List<PositionBonus> { new PositionBonus { MaxPosition = 1, Amount = p.WinBonus }, new PositionBonus { MaxPosition = 3, Amount = p.PodiumBonus } };
            if (p.ReleaseClause > 0) c.Clauses.Add(new ContractClause { Type = ClauseType.ReleaseClause, Amount = p.ReleaseClause });
            if (p.TeamOption) c.Clauses.Add(new ContractClause { Type = ClauseType.TeamOption, ExtraSeasons = 1 });
            if (p.PerformanceExtensionTopN > 0) c.Clauses.Add(new ContractClause { Type = ClauseType.PerformanceExtension, ChampionshipTopN = p.PerformanceExtensionTopN, ExtraSeasons = 1 });
            if (p.ExitForHigherCategory) c.Clauses.Add(new ContractClause { Type = ClauseType.ExitForHigherCategory });

            bool contracted = rider.Contract != null && rider.TeamId != team.Id && rider.Contract.EndSeason >= start;
            var seller = world.GetTeam(rider.TeamId);
            if (p.SigningBonus > 0) Accounts.Post(world, team, LedgerKind.RiderSigningBonus, -p.SigningBonus, rider.FullName);
            world.TransferAgreements.RemoveAll(a => a.RiderId == rider.Id);

            if (p.StartNow)
            {
                if (contracted) Contracts.PayAndRelease(world, db, rider, team, fee, fee > 0 && fee == Contracts.ReleaseClause(rider));
                else Contracts.Close(world, rider, ContractEnd.Expired);
                if (seller != null) seller.RiderIds.Remove(rider.Id);
                rider.FutureContract = null;
                rider.TeamId = team.Id;
                rider.CategoryId = team.CategoryId;
                rider.Contract = c;
                if (!team.RiderIds.Contains(rider.Id)) team.RiderIds.Add(rider.Id);
                if (rider.Number <= 0) rider.Number = RiderGenerator.FreeNumber(world, team.CategoryId, world.Rng.Fork("number-" + rider.Id));
                if (seller != null && seller != team && !seller.IsPlayer) TransferMarket.FillVacancyNow(world, db, seller);
                world.AddNews(Txt.F("news.joins", "{0} joins {1}", rider.FullName, team.Name), Txt.F("news.contractUntil", "Contract until {0}.", c.EndSeason), team.CategoryId, true);
            }
            else
            {
                if (contracted && fee > 0 && seller != null)
                {
                    // Fee paid now; he finishes the season with his team and moves at the end of it.
                    Accounts.Post(world, team, LedgerKind.TransferFee, -fee, rider.FullName, true, false);
                    Accounts.Post(world, seller, LedgerKind.TransferFee, fee, rider.FullName, true, false);
                    rider.Contract.EndSeason = world.Season;
                    rider.Contract.EndsByTransfer = true;
                }
                rider.FutureContract = c;
                world.AddNews(rider.TeamId == team.Id ? Txt.F("news.extends", "{0} extends with {1}", rider.FullName, team.Name) : Txt.F("news.signsFor", "{0} signs for {1}", rider.FullName, team.Name),
                    rider.TeamId == team.Id ? Txt.F("news.newDeal", "New deal from {0} until {1}.", start, c.EndSeason) : Txt.F("news.arrives", "Arrives in {0} until {1}.", start, c.EndSeason), team.CategoryId, true);
            }
            world.SetKnowledge(team.Id, rider.Id, Math.Max(world.GetKnowledge(team.Id, rider.Id), 0.6));
        }

        // ------------------------------------------------------------------ between teams

        /// <summary>
        /// The player offers a fee to the team holding a rider. AI teams accept a fair price, name their price for a
        /// reasonable offer and turn down the rest. An accepted fee becomes an agreement: the rider still has to agree.
        /// </summary>
        public static TransferAnswer ProposeTransfer(WorldState world, GameDatabase db, Team buyer, Rider rider, double fee, bool startNow)
        {
            var owner = world.GetTeam(rider.TeamId);
            if (owner == null || rider.Contract == null) return new TransferAnswer { Status = TransferStatus.Accepted, Message = Txt.F("tr.free", "{0} is free: no fee needed.", rider.FullName) };
            int from = startNow ? world.Season : world.Season + 1;
            double value = Contracts.TransferValue(world, db, rider, from);
            if (startNow) value *= 1.25; // losing a rider mid-season hurts more
            value = Math.Round(value / 1000) * 1000;
            if (value <= 0) return new TransferAnswer { Status = TransferStatus.Accepted, Message = Txt.F("tr.freeFrom", "{0} is free for {1}: no fee needed.", rider.FullName, from) };
            if (fee >= value)
            {
                world.TransferAgreements.RemoveAll(a => a.RiderId == rider.Id && a.BuyerTeamId == buyer.Id);
                world.TransferAgreements.Add(new TransferAgreement { RiderId = rider.Id, BuyerTeamId = buyer.Id, Fee = fee, StartNow = startNow, ExpiresAfterEvent = world.EventsCompleted + 3 });
                return new TransferAnswer { Status = TransferStatus.Accepted, Message = Txt.F("tr.accepts", "{0} accepts {1}. Now agree terms with {2}.", owner.Name, MathX.FormatMoney(fee), rider.FullName) };
            }
            if (fee >= value * 0.7) return new TransferAnswer { Status = TransferStatus.Rejected, Asking = value, Message = Txt.F("tr.wants", "{0} wants {1}.", owner.Name, MathX.FormatMoney(value)) };
            return new TransferAnswer { Status = TransferStatus.Rejected, Asking = value, Message = Txt.F("tr.rejects", "{0} rejects the offer outright.", owner.Name) };
        }

        /// <summary>
        /// AI teams approach the player for his best riders (for next season), with a fee. Runs after events;
        /// the offers wait for the player's answer.
        /// </summary>
        public static void AiApproaches(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            world.TransferOffers.RemoveAll(o => o.ExpiresAfterEvent < world.EventsCompleted);
            world.TransferAgreements.RemoveAll(a => a.ExpiresAfterEvent < world.EventsCompleted);
            var player = world.PlayerTeam;
            if (player == null) return;
            var levels = TransferMarket.CategoryLevels(world, db);
            foreach (var id in new List<string>(player.RiderIds))
            {
                var r = world.GetRider(id);
                if (r == null || r.Contract == null || r.FutureContract != null || r.Contract.EndSeason <= world.Season) continue;
                bool pending = false;
                foreach (var o in world.TransferOffers) if (o.RiderId == r.Id) pending = true;
                double level;
                levels.TryGetValue(player.CategoryId ?? "", out level);
                if (pending || r.Attributes.Overall < level + 2 || !rng.Chance(0.04)) continue;
                // The most prestigious AI team that would rate him above one of its own riders.
                Team best = null;
                double bestPrestige = -1;
                foreach (var t in world.Teams.Values)
                {
                    var cat = db.GetCategory(t.CategoryId);
                    var own = db.GetCategory(player.CategoryId);
                    if (t.IsPlayer || cat == null || !cat.Simulated || cat.IsCup || own == null || cat.Prestige < own.Prestige) continue;
                    double score = TransferMarket.EvaluateForTeam(world, db, t, r);
                    bool upgrade = false;
                    foreach (var oid in t.RiderIds)
                    {
                        var other = world.GetRider(oid);
                        if (other != null && score > TransferMarket.EvaluateForTeam(world, db, t, other) + 3) upgrade = true;
                    }
                    double prestige = cat.Prestige + t.Reputation * 0.1;
                    double fee0 = Contracts.TransferValue(world, db, r, world.Season + 1);
                    if (upgrade && Contracts.CanPay(world, t, fee0) && prestige > bestPrestige)
                    {
                        best = t;
                        bestPrestige = prestige;
                    }
                }
                if (best == null) continue;
                double fee = Math.Round(Contracts.TransferValue(world, db, r, world.Season + 1) * rng.Range(0.9, 1.2) / 1000) * 1000;
                world.TransferOffers.Add(new TransferOffer
                {
                    Id = world.NewId("tro"), RiderId = r.Id, FromTeamId = player.Id, ToTeamId = best.Id, Fee = fee,
                    Status = TransferStatus.Pending, ExpiresAfterEvent = world.EventsCompleted + 3
                });
                world.AddNews(Txt.F("news.bid", "{0} bids for {1}", best.Name, r.FullName), Txt.F("news.bidBody", "Offer to {0}: {1} for next season.", player.Name, MathX.FormatMoney(fee)), player.CategoryId, true);
            }
        }

        /// <summary>The player accepts an AI bid: fee paid now, the rider moves at the end of the season on the buyer's terms.</summary>
        public static bool AcceptBid(WorldState world, GameDatabase db, string offerId)
        {
            TransferOffer o = null;
            foreach (var x in world.TransferOffers) if (x.Id == offerId) o = x;
            if (o == null) return false;
            world.TransferOffers.Remove(o);
            var r = world.GetRider(o.RiderId);
            var buyer = world.GetTeam(o.ToTeamId);
            var seller = world.GetTeam(o.FromTeamId);
            if (r == null || buyer == null || seller == null || r.TeamId != seller.Id || r.Contract == null) return false;
            Accounts.Post(world, buyer, LedgerKind.TransferFee, -o.Fee, r.FullName, true, false);
            Accounts.Post(world, seller, LedgerKind.TransferFee, o.Fee, r.FullName, true, false);
            r.Contract.EndSeason = world.Season;
            r.Contract.EndsByTransfer = true;
            var rules = db.GetRules(buyer.CategoryId, world.Season);
            var c = new Contract
            {
                Id = world.NewId("ctr"), TeamId = buyer.Id, ManufacturerId = buyer.ManufacturerId, StartSeason = world.Season + 1, EndSeason = world.Season + 2,
                SalaryPerSeason = TransferMarket.AgreedSalary(buyer, r, rules), FactoryBacked = buyer.Status == TeamStatus.Factory
            };
            Contracts.StandardTerms(c);
            r.FutureContract = c;
            world.AddNews(Txt.F("news.willJoin", "{0} will join {1}", r.FullName, buyer.Name), Txt.F("news.willJoinBody", "{0} accepts {1}; he moves at the end of the season.", seller.Name, MathX.FormatMoney(o.Fee)), seller.CategoryId, true);
            return true;
        }

        public static void RejectBid(WorldState world, string offerId)
        {
            world.TransferOffers.RemoveAll(o => o.Id == offerId);
        }

        // ------------------------------------------------------------------ the player's own riders

        /// <summary>Exercises the team option in the contract's last season (applied at season end).</summary>
        public static bool ExerciseOption(WorldState world, GameDatabase db, Team team, Rider rider)
        {
            var c = rider.Contract;
            var opt = c != null && c.TeamId == team.Id ? c.Clause(ClauseType.TeamOption) : null;
            if (opt == null || opt.Exercised || c.EndSeason != world.Season) return false;
            if (SeatsFreeNextSeason(world, db, team) <= 0) return false;
            opt.Exercised = true;
            return true;
        }

        private static ContractProposal Clone(ContractProposal p)
        {
            return new ContractProposal
            {
                Salary = p.Salary, Seasons = p.Seasons, StartNow = p.StartNow, SigningBonus = p.SigningBonus, WinBonus = p.WinBonus, PodiumBonus = p.PodiumBonus,
                ReleaseClause = p.ReleaseClause, TeamOption = p.TeamOption, PerformanceExtensionTopN = p.PerformanceExtensionTopN, ExitForHigherCategory = p.ExitForHigherCategory
            };
        }

        private static OfferResult Reject(OfferResult r, string message)
        {
            r.Accepted = false;
            r.Message = message;
            return r;
        }
    }
}
