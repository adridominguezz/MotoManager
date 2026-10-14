using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>
    /// What a team's people and facilities actually do, in one place. Every effect of staff and facilities on the
    /// simulation goes through here, so the screens can explain them and nothing is counted twice.
    /// </summary>
    public static class TeamCapabilities
    {
        /// <summary>Relative running and building cost of each facility (1 = average).</summary>
        public static double CostWeight(FacilityType f)
        {
            switch (f)
            {
                case FacilityType.Factory: return 1.6;
                case FacilityType.WindTunnel: return 1.4;
                case FacilityType.Dyno: return 1.0;
                case FacilityType.Simulator: return 1.0;
                case FacilityType.ElectronicsLab: return 0.9;
                case FacilityType.Hospitality: return 0.8;
                case FacilityType.DataCenter: return 0.7;
                case FacilityType.RiderTrainingCenter: return 0.6;
                case FacilityType.Academy: return 0.6;
                default: return 0.4; // scouting centre
            }
        }

        public static double Skill(WorldState world, Team team, StaffRole role, double fallback = 40)
        {
            return StaffService.BestSkill(world, team, role, fallback);
        }

        // ------------------------------------------------------------------ development

        /// <summary>Facility that drives a bike component's development.</summary>
        public static FacilityType FacilityFor(string component)
        {
            switch (component)
            {
                case "Engine":
                case "ProductionEngine":
                case "SpecEngine":
                    return FacilityType.Dyno;
                case "Aerodynamics":
                    return FacilityType.WindTunnel;
                case "Electronics":
                    return FacilityType.ElectronicsLab;
                default:
                    return FacilityType.Factory;
            }
        }

        /// <summary>Specialist whose skill shapes a component's projects (null = the chief engineer).</summary>
        public static StaffRole SpecialistFor(string component)
        {
            switch (component)
            {
                case "Aerodynamics": return StaffRole.Aerodynamicist;
                case "Electronics": return StaffRole.ElectronicsEngineer;
                case "Tyres": return StaffRole.TireEngineer;
                default: return StaffRole.ChiefEngineer;
            }
        }

        /// <summary>Days of project work done per calendar day.</summary>
        public static double DevelopmentSpeed(WorldState world, Team team, string component)
        {
            double td = Skill(world, team, StaffRole.TechnicalDirector, 40);
            double ce = Skill(world, team, StaffRole.ChiefEngineer, 40);
            return 0.55 + td * 0.0025 + ce * 0.0015 + team.Facility(FacilityType.Factory) * 0.05 + team.Facility(FacilityFor(component)) * 0.03;
        }

        /// <summary>How many projects the factory can run in parallel.</summary>
        public static int ProjectCapacity(Team team)
        {
            return ProjectCapacityAt(team.Facility(FacilityType.Factory));
        }

        /// <summary>Parallel projects with the factory at <paramref name="factoryLevel"/>.</summary>
        public static int ProjectCapacityAt(int factoryLevel)
        {
            return 1 + (factoryLevel + 1) / 2;
        }

        /// <summary>Engineering quality behind a project (drives failure risk and results).</summary>
        public static double ProjectQuality(WorldState world, Team team, string component, string engineerId)
        {
            StaffMember s;
            double lead = engineerId != null && world.Staff.TryGetValue(engineerId, out s) ? s.Skill : Skill(world, team, SpecialistFor(component), 40);
            double td = Skill(world, team, StaffRole.TechnicalDirector, 40);
            return MathX.Clamp(lead * 0.6 + td * 0.25 + team.Facility(FacilityFor(component)) * 3, 10, 100);
        }

        // ------------------------------------------------------------------ the bike itself

        /// <summary>Component points a bike gains per facility level above the first (see <see cref="BikeFacilityFor"/>).</summary>
        public const double FacilityComponentBonusPerLevel = 1.25;

        /// <summary>
        /// Facility that directly lifts a bike component (better test benches, tunnel time and workshop finish
        /// show on the bike itself, not only in the projects): Dyno → engine, wind tunnel → aerodynamics, electronics
        /// lab → electronics, factory → chassis. Null for components no facility lifts.
        /// </summary>
        public static FacilityType? BikeFacilityFor(string component)
        {
            switch (component)
            {
                case "Engine":
                case "ProductionEngine":
                    return FacilityType.Dyno;
                case "Aerodynamics":
                    return FacilityType.WindTunnel;
                case "Electronics":
                    return FacilityType.ElectronicsLab;
                case "Chassis":
                    return FacilityType.Factory;
                default:
                    return null;
            }
        }

        /// <summary>The components of a bike class a facility lifts directly (empty for facilities off the bike).</summary>
        public static List<string> BikeComponentsOf(FacilityType facility, BikeClassDefinition bikeClass)
        {
            var res = new List<string>();
            if (bikeClass == null) return res;
            foreach (var c in bikeClass.Components)
                if (BikeFacilityFor(c) == facility) res.Add(c);
            return res;
        }

        /// <summary>Component points given by a facility at <paramref name="level"/> (level 1 gives nothing).</summary>
        public static double FacilityComponentBonus(int level)
        {
            return Math.Max(0, level - 1) * FacilityComponentBonusPerLevel;
        }

        /// <summary>
        /// Points a team's facilities add to one of its bike's components. Only components the regulations let the
        /// team develop gain anything (a frozen engine or a spec part stays as it is).
        /// </summary>
        public static double ComponentBonus(WorldState world, GameDatabase db, Team team, string component)
        {
            if (team == null) return 0;
            var facility = BikeFacilityFor(component);
            if (!facility.HasValue) return 0;
            if (world == null || db == null || !DevelopmentService.ComponentAllowed(world, db, team, component)) return 0;
            return FacilityComponentBonus(team.Facility(facility.Value));
        }

        /// <summary>A component's level on track: the bike's own plus the facility bonus (0-100).</summary>
        public static double EffectiveComponent(WorldState world, GameDatabase db, Team team, Motorcycle bike, string component)
        {
            return MathX.Clamp(bike.Component(component) + ComponentBonus(world, db, team, component), 0, 100);
        }

        /// <summary>Performance of a team's bike, facilities included (what the races and the screens use).</summary>
        public static BikePerformance Performance(WorldState world, GameDatabase db, Team team, Motorcycle bike, BikeClassDefinition bikeClass)
        {
            if (bike == null) return BikePerformance.Average();
            return BikePerformance.Compute(bike, bikeClass, c => ComponentBonus(world, db, team, c));
        }

        // ------------------------------------------------------------------ race weekend

        /// <summary>The race engineer working with a rider (assigned, or the best available).</summary>
        public static double RaceEngineerSkill(WorldState world, Team team, string riderId)
        {
            double best = -1;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (!world.Staff.TryGetValue(id, out s) || s.Role != StaffRole.RaceEngineer) continue;
                if (s.AssignedRiderId == riderId) return s.Skill;
                best = Math.Max(best, s.Skill);
            }
            return best < 0 ? 40 : best;
        }

        /// <summary>Starting set-up quality contribution of the crew and facilities (0-0.35).</summary>
        public static double SetupSupport(WorldState world, Team team, string riderId)
        {
            double re = RaceEngineerSkill(world, team, riderId);
            double ee = Skill(world, team, StaffRole.ElectronicsEngineer, 35);
            double da = Skill(world, team, StaffRole.DataAnalyst, 35);
            double te = Skill(world, team, StaffRole.TireEngineer, 35);
            double crew = re * 0.55 + ee * 0.15 + da * 0.15 + te * 0.15;
            return crew / 500.0 + team.Facility(FacilityType.Simulator) * 0.025 + team.Facility(FacilityType.ElectronicsLab) * 0.008 + team.Facility(FacilityType.DataCenter) * 0.007;
        }

        /// <summary>
        /// The best set-up the crew can reach in a weekend: a strong crew chief and data analyst find the last
        /// tenths, a weak crew stays short of them (0.72 - 1.0).
        /// </summary>
        public static double SetupCeiling(WorldState world, Team team, string riderId)
        {
            double re = RaceEngineerSkill(world, team, riderId);
            double da = Skill(world, team, StaffRole.DataAnalyst, 35);
            double ee = Skill(world, team, StaffRole.ElectronicsEngineer, 35);
            double crew = re * 0.6 + da * 0.25 + ee * 0.15;
            return MathX.Clamp(0.55 + crew / 250.0 + team.Facility(FacilityType.Simulator) * 0.015, 0.72, 1.0);
        }

        /// <summary>Tyre wear multiplier from the tyre engineer (0.88 with a top one, 1.08 without).</summary>
        public static double TireCare(WorldState world, Team team)
        {
            double te = Skill(world, team, StaffRole.TireEngineer, 30);
            return MathX.Clamp(1.14 - te / 400.0, 0.88, 1.08);
        }

        /// <summary>Extra reliability points of the bikes from the mechanics and the factory.</summary>
        public static double ReliabilityBonus(WorldState world, Team team, string riderId)
        {
            double mech = -1;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (!world.Staff.TryGetValue(id, out s) || s.Role != StaffRole.Mechanic) continue;
                if (s.AssignedRiderId == riderId) { mech = s.Skill; break; }
                mech = Math.Max(mech, s.Skill);
            }
            if (mech < 0) mech = 40;
            return (mech - 50) / 5.0 + (team.Facility(FacilityType.Factory) - 1) * 0.5;
        }

        /// <summary>Skill used for weather and race forecasts.</summary>
        public static double ForecastSkill(WorldState world, Team team)
        {
            return MathX.Clamp(Skill(world, team, StaffRole.DataAnalyst, 45) + (team.Facility(FacilityType.DataCenter) - 1) * 4, 0, 100);
        }

        // ------------------------------------------------------------------ riders

        /// <summary>Extra growth of the team's riders per season (coach, training centre, simulator; academy for the young).</summary>
        public static double RiderDevelopmentBonus(WorldState world, Team team, double age)
        {
            double coach = Skill(world, team, StaffRole.RiderCoach, 30);
            double b = (coach - 40) / 120.0 + team.Facility(FacilityType.RiderTrainingCenter) * 0.06 + team.Facility(FacilityType.Simulator) * 0.04;
            if (age < 22) b += team.Facility(FacilityType.Academy) * 0.06;
            return b;
        }

        /// <summary>Chance that an injury heals one event sooner.</summary>
        public static double QuickerRecoveryChance(WorldState world, Team team)
        {
            return MathX.Clamp(Skill(world, team, StaffRole.Physio, 20) / 150.0 + team.Facility(FacilityType.RiderTrainingCenter) * 0.04, 0, 0.8);
        }

        /// <summary>Knowledge gained per event about a scouted rider.</summary>
        public static double ScoutingRate(WorldState world, Team team)
        {
            return ScoutingRate(world, team, team.Facility(FacilityType.ScoutingCenter));
        }

        /// <summary>Scouting rate with the scouting centre at <paramref name="centreLevel"/> (what an upgrade would give).</summary>
        public static double ScoutingRate(WorldState world, Team team, int centreLevel)
        {
            return 0.06 + Skill(world, team, StaffRole.Scout, 30) / 900.0 + centreLevel * 0.012;
        }

        // ------------------------------------------------------------------ commercial

        /// <summary>Multiplier on what sponsors pay (hospitality for partners).</summary>
        public static double SponsorValueFactor(Team team)
        {
            return 1 + (team.Facility(FacilityType.Hospitality) - 1) * 0.02;
        }

        /// <summary>Multiplier on how keen sponsors are to approach the team.</summary>
        public static double SponsorInterestFactor(Team team)
        {
            return 1 + (team.Facility(FacilityType.Hospitality) - 1) * 0.05;
        }
    }
}
