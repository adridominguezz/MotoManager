using System;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    public class OfferResult
    {
        public bool Accepted;
        public string Message;
        /// <summary>Minimum salary the rider would have accepted (revealed after a rejection).</summary>
        public double AskingSalary;
    }

    /// <summary>
    /// Contract negotiations initiated by the player. Riders decide like in the AI market: the seat must be at
    /// least as attractive as what they have, and the money must reach their demand (adjusted by ambition and
    /// loyalty). Deals for next season are stored as <see cref="Rider.FutureContract"/> and applied at season end.
    /// </summary>
    public static class ContractService
    {
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
                if (r.TeamId == team.Id && TransferMarket.IsCommitted(r, world.Season)) committed++;
            }
            return capacity - committed;
        }

        public static OfferResult Offer(WorldState world, GameDatabase db, Team team, Rider rider, double salary, int years)
        {
            var result = new OfferResult { AskingSalary = AskingSalary(world, db, team, rider) };
            var cat = db.GetCategory(team.CategoryId);
            int next = world.Season + 1;
            double age = rider.AgeOn(new DateTime(next, 3, 1));
            if (rider.IsRetired) return Reject(result, rider.FullName + " has retired.");
            if (rider.FutureContract != null) return Reject(result, rider.FullName + " has already signed for next season.");
            if (age < cat.MinAge || age > cat.MaxAge) return Reject(result, "Age limits of " + cat.ShortName + " do not allow it.");
            bool renewal = rider.TeamId == team.Id;
            if (!renewal && rider.CategoryId != cat.Id && age > cat.MaxEntryAge) return Reject(result, "Too old to enter " + cat.ShortName + ".");
            if (SeatsFreeNextSeason(world, db, team) <= 0 && !(renewal && !TransferMarket.IsCommitted(rider, world.Season)))
                return Reject(result, "You have no free seat for next season.");

            // Under contract elsewhere beyond this season: only a step up can tempt him.
            if (!renewal && TransferMarket.IsCommitted(rider, world.Season))
            {
                var current = db.GetCategory(rider.CategoryId);
                if (current != null && cat.Prestige < current.Prestige + 10)
                    return Reject(result, rider.FullName + ": under contract until " + rider.Contract.EndSeason + ".");
            }

            // Is the seat attractive enough?
            var currentTeam = world.GetTeam(rider.TeamId);
            var currentCat = db.GetCategory(rider.CategoryId);
            double offerUtility = TransferMarket.SeatUtility(world, cat, team, rider);
            if (!renewal && currentCat != null && currentCat.Simulated && currentTeam != null)
            {
                double now = TransferMarket.SeatUtility(world, currentCat, currentTeam, rider);
                if (offerUtility + (salary / Math.Max(1, result.AskingSalary) - 1) * 20 < now - 5)
                    return Reject(result, rider.FullName + " prefers the current situation.");
            }
            if (salary < result.AskingSalary * 0.95)
                return Reject(result, rider.FullName + " asks for at least " + MathX.FormatMoney(result.AskingSalary) + " per season.");

            years = Math.Max(1, Math.Min(4, years));
            var contract = new Contract
            {
                TeamId = team.Id, ManufacturerId = team.ManufacturerId, StartSeason = next, EndSeason = next + years - 1,
                SalaryPerSeason = salary, WinBonus = Math.Round(salary * 0.05), PodiumBonus = Math.Round(salary * 0.02),
                FactoryBacked = team.Status == TeamStatus.Factory, RiderFunding = rider.PersonalBacking
            };
            if (renewal)
            {
                // Extension of the current deal (keeps him under contract, no seat change).
                rider.Contract.EndSeason = contract.EndSeason;
                rider.Contract.SalaryPerSeason = salary;
                rider.Contract.RiderFunding = rider.PersonalBacking;
                rider.Morale = Math.Min(100, rider.Morale + 5);
                world.AddNews(rider.FullName + " extends with " + team.Name, "New deal until " + contract.EndSeason + ".", team.CategoryId, true);
            }
            else
            {
                rider.FutureContract = contract;
                world.AddNews(rider.FullName + " signs for " + team.Name, "Arrives in " + next + " on a " + years + "-year deal.", team.CategoryId, true);
            }
            world.SetKnowledge(team.Id, rider.Id, Math.Max(world.GetKnowledge(team.Id, rider.Id), 0.6));
            result.Accepted = true;
            result.Message = renewal ? "Contract extended until " + contract.EndSeason + "." : rider.FullName + " will join for " + next + ".";
            return result;
        }

        private static OfferResult Reject(OfferResult r, string message)
        {
            r.Accepted = false;
            r.Message = message;
            return r;
        }
    }
}
