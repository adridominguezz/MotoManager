using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.Career;

namespace MotoManager.Tools
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string root = FindDataRoot();
            var db = DataLoader.Load(new FileDataSource(root));
            string mode = args.Length > 0 ? args[0] : "laps";
            if (mode == "laps") return Laps(db, args);
            if (mode == "race") return RaceTest(db, args);
            if (mode == "calibrate") return Calibrate(db, root, args);
            if (mode == "season") return Season(db, args);
            if (mode == "bikes") return Bikes(db, args);
            if (mode == "profile") return Profile(db);
            if (mode == "preview") return Preview(db, args);
            if (mode == "circuits") return CircuitSheet(db, args);
            if (mode == "dump-tuning")
            {
                var path = System.IO.Path.Combine(root, "simulation", "tuning.json");
                System.IO.File.WriteAllText(path, JsonSettings.Serialize(new SimulationTuning(), true) + "\n");
                Console.WriteLine("wrote " + path);
                return 0;
            }
            Console.WriteLine("usage: laps <rules> [circuit] | race <rules> <circuit> [seed]");
            return 1;
        }

        public static string FindDataRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                var p = System.IO.Path.Combine(dir, "Assets", "MotoManager", "Resources", "Data");
                if (System.IO.Directory.Exists(p)) return p;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            throw new Exception("data root not found");
        }

        static RaceEntry AverageEntry(int i, double skill = 75)
        {
            var r = new Rider { Id = "r" + i, FirstName = "Test", LastName = "Rider" + i };
            foreach (var n in RiderAttributes.Names) r.Attributes.Set(n, skill);
            r.Attributes.Aggression = 50;
            var bike = BikePerformance.Average();
            for (int k = 0; k < bike.Values.Length; k++) bike.Values[k] = 70;
            return new RaceEntry { Rider = r, Bike = bike, GridPosition = i + 1, SetupQuality = 0.6 };
        }

        static int Laps(GameDatabase db, string[] args)
        {
            string rulesId = args.Length > 1 ? args[1] : "motogp_2026";
            var rules = db.Rules[rulesId];
            var circuits = args.Length > 2 ? new List<CircuitDefinition> { db.Circuits[args[2]] } : db.Circuits.Values.OrderBy(c => c.Id).ToList();
            foreach (var c in circuits)
            {
                var cfg = new RaceConfig { Rules = rules, Circuit = c, Tuning = db.Tuning, Laps = 3, TimeTrial = true, Seed = 7, TimeStep = 0.05, SessionType = SessionType.Practice };
                cfg.Weather.AirTemp = c.AverageAirTempC;
                var e = AverageEntry(0, 75);
                for (int k = 0; k < e.Bike.Values.Length; k++) e.Bike.Values[k] = 65;
                e.FrontCompound = TireCompound.Medium; e.RearCompound = TireCompound.Medium;
                cfg.Entries.Add(e);
                var sim = new RaceSimulation(cfg);
                double vmax = 0;
                while (!sim.IsFinished) { sim.Step(); vmax = Math.Max(vmax, sim.Riders[0].Speed); }
                var t = TrackModel.Get(c);
                var g = new TrackGeometry(t);
                double turn = t.Segments.Sum(s => s.AngleDeg);
                double refLap;
                string cat = rules.CategoryId;
                string refText = c.ReferenceLaps.TryGetValue(cat, out refLap) ? $"ref {MathX.FormatLapTime(refLap)} ratio {sim.Riders[0].BestLapTime / refLap:0.000}" : "ref n/a";
                Console.WriteLine($"{c.Id,-16} best {MathX.FormatLapTime(sim.Riders[0].BestLapTime)} {refText}  vmax {vmax * 3.6:0} km/h  turn {turn:0}  closure {g.ClosureError:0}m resid {g.ResidualError:0}m geo {g.GeometricLength / t.Length * 100:0}%  corners {t.CornerCount}");
            }
            return 0;
        }

        static double ReferenceLap(GameDatabase db, CategoryRules rules, CircuitDefinition c)
        {
            var cfg = new RaceConfig { Rules = rules, Circuit = c, Tuning = db.Tuning, Laps = 3, TimeTrial = true, Seed = 7, TimeStep = 0.05, SessionType = SessionType.Practice };
            cfg.Weather.AirTemp = c.AverageAirTempC;
            var e = AverageEntry(0, 75);
            for (int k = 0; k < e.Bike.Values.Length; k++) e.Bike.Values[k] = 65;
            e.FrontCompound = TireCompound.Medium; e.RearCompound = TireCompound.Medium;
            cfg.Entries.Add(e);
            var sim = new RaceSimulation(cfg);
            sim.RunToEnd();
            return sim.Riders[0].BestLapTime;
        }

        static string ScaleRadii(string layout, double f)
        {
            var parts = layout.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p[0] != 'L' && p[0] != 'R') continue;
                int at = p.IndexOf('@');
                int colon = p.IndexOf(':');
                string radius = colon > 0 ? p.Substring(at + 1, colon - at - 1) : p.Substring(at + 1);
                double r = double.Parse(radius, System.Globalization.CultureInfo.InvariantCulture);
                int nr = Math.Max(10, (int)Math.Round(r * f));
                parts[i] = p.Substring(0, at + 1) + nr + (colon > 0 ? p.Substring(colon) : "");
            }
            return string.Join(" ", parts);
        }

        /// <summary>Scales corner radii of every circuit so the reference rider matches the real race-pace lap.</summary>
        static int Calibrate(GameDatabase db, string root, string[] args)
        {
            var rules = db.Rules[args.Length > 1 ? args[1] : "motogp_2026"];
            string cat = rules.CategoryId;
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "circuits"), "*.json");
            foreach (var c in db.Circuits.Values.OrderBy(x => x.Id))
            {
                double target;
                if (!c.ReferenceLaps.TryGetValue(cat, out target)) continue;
                if (args.Length > 2 && args[2] != c.Id) continue;
                if (string.IsNullOrEmpty(c.Layout) && c.Shape != null)
                {
                    // Real-shape circuits: calibrate the radius factor, never touch the measured geometry.
                    double slo = 0.5, shi = 2.0, sbest = 1;
                    for (int it = 0; it < 16; it++)
                    {
                        double mid = Math.Sqrt(slo * shi);
                        var test = Newtonsoft.Json.JsonConvert.DeserializeObject<CircuitDefinition>(Newtonsoft.Json.JsonConvert.SerializeObject(c));
                        test.RadiusScale = mid;
                        test.Id = c.Id + "_rs" + it;
                        double lap = ReferenceLap(db, rules, test);
                        sbest = mid;
                        if (lap > target) slo = mid; else shi = mid;
                    }
                    foreach (var f in files)
                    {
                        var token = Newtonsoft.Json.Linq.JToken.Parse(System.IO.File.ReadAllText(f));
                        var items = token is Newtonsoft.Json.Linq.JArray arr ? arr.Children<Newtonsoft.Json.Linq.JObject>().ToList() : new List<Newtonsoft.Json.Linq.JObject> { (Newtonsoft.Json.Linq.JObject)token };
                        var match = items.FirstOrDefault(o => (string)o["id"] == c.Id);
                        if (match == null) continue;
                        match["radiusScale"] = Math.Round(sbest, 3);
                        System.IO.File.WriteAllText(f, token.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
                    }
                    c.RadiusScale = sbest;
                    Console.WriteLine($"{c.Id,-16} radiusScale {sbest:0.000} -> {MathX.FormatLapTime(ReferenceLap(db, rules, c))} (ref {MathX.FormatLapTime(target)})");
                    continue;
                }
                string original = c.Layout;
                double lo = 0.4, hi = 3.0, best = 1;
                for (int it = 0; it < 18; it++)
                {
                    double mid = Math.Sqrt(lo * hi);
                    var test = Newtonsoft.Json.JsonConvert.DeserializeObject<CircuitDefinition>(Newtonsoft.Json.JsonConvert.SerializeObject(c));
                    test.Layout = ScaleRadii(original, mid);
                    test.Id = c.Id + "_cal" + it;
                    double lap;
                    try { lap = ReferenceLap(db, rules, test); }
                    catch { hi = mid; continue; }
                    best = mid;
                    if (lap > target) lo = mid; else hi = mid;
                }
                string layout = ScaleRadii(original, best);
                foreach (var f in files)
                {
                    var text = System.IO.File.ReadAllText(f);
                    if (text.Contains(original)) System.IO.File.WriteAllText(f, text.Replace(original, layout));
                }
                c.Layout = layout;
                Console.WriteLine($"{c.Id,-16} radius x{best:0.00} -> {MathX.FormatLapTime(ReferenceLap(db, rules, c))} (ref {MathX.FormatLapTime(target)})");
            }
            return 0;
        }

        /// <summary>season [seed] [years]: simulates whole seasons headless and prints standings.</summary>
        static int Season(GameDatabase db, string[] args)
        {
            ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 2026;
            int years = args.Length > 2 ? int.Parse(args[2]) : 1;
            var errors = db.Validate();
            foreach (var e in errors) Console.WriteLine("DATA: " + e);
            var game = GameController.NewGame(db, new NewGameOptions { Seed = seed });
            for (int y = 0; y < years; y++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int events = 0;
                game.SimulateRestOfSeason(ev => { events++; if (Environment.GetEnvironmentVariable("SIM_VERBOSE") == "1") Console.WriteLine($"  event {ev.Plan.Group} {ev.Plan.Round.Name} {sw.Elapsed.TotalSeconds:0.0}s"); });
                Console.WriteLine($"== Season {game.World.Season}: {events} events in {sw.Elapsed.TotalSeconds:0.0}s");
                foreach (var cat in db.CategoriesOrdered())
                {
                    ChampionshipState champ;
                    if (!game.World.Championships.TryGetValue(cat.Id, out champ)) continue;
                    var table = champ.SortedRiders();
                    var top = table.Take(3).Select(e => { var r = game.World.GetRider(e.Id); var t = game.World.GetTeam(r.TeamId); return $"{r.ShortName} ({(t != null ? t.ShortName ?? t.Name : "-")}) {e.Points:0}pts {e.Wins}W"; });
                    var makers = champ.SortedManufacturers().Take(3).Select(e => $"{e.Id} {e.Points:0}");
                    Console.WriteLine($"  {cat.ShortName,-10} rounds {champ.Rounds.Count,2}  {string.Join(" | ", top)}");
                    Console.WriteLine($"  {"",-10} constructors: {string.Join(", ", makers)}");
                }
                if (Environment.GetEnvironmentVariable("SIM_ECON") == "1") Economy(game, db, "motogp");
                if (Environment.GetEnvironmentVariable("SIM_ECON") == "2") CategoryEconomy(game, db);
                if (Environment.GetEnvironmentVariable("SIM_MAKERS") == "1") Makers(game, db, "motogp");
                var summary = game.EndSeason();
                Console.WriteLine($"  -> retired {summary.Retired.Count}, market moves {summary.Market.Moves.Count(m => !m.Renewal)}, generated {summary.Market.GeneratedRiders}, youth {summary.YouthIntake}");
            }
            if (years > 1)
            {
                Console.WriteLine("== Champions");
                foreach (var cat in db.CategoriesOrdered().Where(c => c.Simulated))
                {
                    var list = game.World.Champions.Where(c => c.CategoryId == cat.Id).OrderBy(c => c.Season).ToList();
                    Console.WriteLine($"  {cat.ShortName,-9} " + string.Join(", ", list.Select(c => c.Season % 100 + ":" + c.RiderName.Split(' ').Last() + "/" + c.ManufacturerId)));
                }
                var date = new DateTime(game.World.Season, 3, 1);
                Console.WriteLine("== Grid levels (overall / age / real riders still racing)");
                foreach (var cat in db.CategoriesOrdered())
                {
                    var rs = game.World.Riders.Values.Where(r => !r.IsRetired && r.CategoryId == cat.Id && (r.TeamId != null || !cat.Simulated)).ToList();
                    if (rs.Count == 0) continue;
                    Console.WriteLine($"  {cat.ShortName,-11} {rs.Count,3} riders  overall {rs.Average(r => r.Attributes.Overall):0.0}  age {rs.Average(r => r.AgeOn(date)):0.0}  real {rs.Count(r => !r.Generated)}");
                }
                Console.WriteLine($"  active riders {game.World.Riders.Values.Count(r => !r.IsRetired)}, retired {game.World.Riders.Values.Count(r => r.IsRetired)}");
            }
            var json = SaveSystem.Serialize(game.World);
            var back = SaveSystem.Deserialize(json);
            Console.WriteLine($"save {json.Length / 1024} KB, reload ok: {SaveSystem.Serialize(back) == json}");
            var parts = Newtonsoft.Json.Linq.JObject.Parse(json);
            Console.WriteLine("  save parts: " + string.Join(", ", parts.Properties().OrderByDescending(p => p.Value.ToString(Newtonsoft.Json.Formatting.None).Length).Take(6)
                .Select(p => p.Name + " " + p.Value.ToString(Newtonsoft.Json.Formatting.None).Length / 1024 + "KB")));
            foreach (var n in game.World.News.Where(n => n.Important).Take(12)) Console.WriteLine("  NEWS " + n.Date.ToString("yyyy-MM-dd") + " " + n.Headline);
            return 0;
        }

        /// <summary>SIM_ECON=1: money, budgets and technical level of a category's teams at the end of a season.</summary>
        static void Economy(GameController game, GameDatabase db, string catId)
        {
            var w = game.World;
            foreach (var t in w.TeamsInCategory(catId))
            {
                var org = w.OrganisationOf(t);
                var bike = w.GetBike(t.BikeId);
                var totals = MotoManager.Core.Economy.Accounts.TotalsFor(w, org.Id, w.Season);
                double funding = totals.ContainsKey(LedgerKind.OwnerFunding) ? totals[LedgerKind.OwnerFunding].Cash : 0;
                double net = totals.Values.Where(v => v.Kind != LedgerKind.OwnerFunding).Sum(v => v.Cash);
                Console.WriteLine($"    {t.ShortName,-12} budget {t.SeasonBudget / 1e6,6:0.0}M  spent {MotoManager.Core.Economy.Accounts.SpentThisSeason(w, t) / 1e6,6:0.0}M  org cash {org.Cash / 1e6,7:0.0}M  org net {net / 1e6,6:0.0}M  tech {t.Personality.TechnicalLevel,5:0.0}  bike {bike.Components.Values.Average(),5:0.0}");
            }
        }

        /// <summary>SIM_MAKERS=1: per manufacturer in a category, bike level (factory team), engineering, concession tier and best riders.</summary>
        static void Makers(GameController game, GameDatabase db, string catId)
        {
            var w = game.World;
            var champ = w.Championships[catId];
            foreach (var e in champ.SortedManufacturers())
            {
                var m = w.GetManufacturer(e.Id);
                var teams = w.TeamsInCategory(catId).Where(t => t.ManufacturerId == e.Id).ToList();
                double bike = teams.Max(t => w.GetBike(t.BikeId).Components.Values.Average());
                var riders = teams.SelectMany(t => t.RiderIds).Select(id => w.GetRider(id)).OrderByDescending(r => r.Attributes.Overall).Take(2);
                string tier;
                m.ConcessionTier.TryGetValue(catId, out tier);
                Console.WriteLine($"    {e.Id,-9} pts {e.Points,4:0}  bike {bike,5:0.0}  eng {m.EngineeringStrength,5:0.0}  budget {m.DevelopmentBudget / 1e6,5:0}M  tier {tier}  bikes {teams.Count}  best: {string.Join(", ", riders.Select(r => r.ShortName + " " + r.Attributes.Overall.ToString("0")))}");
            }
        }

        /// <summary>SIM_ECON=2: average income and costs per team, by category and concept (owner funding excluded).</summary>
        static void CategoryEconomy(GameController game, GameDatabase db)
        {
            var w = game.World;
            foreach (var cat in db.CategoriesOrdered().Where(c => c.Simulated))
            {
                var teams = w.TeamsInCategory(cat.Id);
                var byKind = new SortedDictionary<LedgerKind, double>();
                foreach (var t in teams)
                    foreach (var kv in Accounts.TotalsFor(w, t.OrganisationId, w.Season, t.Id))
                    {
                        double v;
                        byKind.TryGetValue(kv.Key, out v);
                        byKind[kv.Key] = v + kv.Value.Cash / teams.Count;
                    }
                double income = byKind.Where(k => k.Value > 0).Sum(k => k.Value), costs = -byKind.Where(k => k.Value < 0).Sum(k => k.Value);
                Console.WriteLine($"  {cat.ShortName,-9} teams {teams.Count,2}  income {income / 1e6,6:0.00}M  costs {costs / 1e6,6:0.00}M  net {(income - costs) / 1e6,6:0.00}M  budget {teams.Average(t => t.SeasonBudget) / 1e6,6:0.00}M");
                Console.WriteLine("            " + string.Join("  ", byKind.Select(k => k.Key + " " + (k.Value / 1e6).ToString("0.00"))));
            }
        }

        /// <summary>bikes &lt;category&gt; [races]: identical riders on every team bike -> machinery ranking.</summary>
        static int Bikes(GameDatabase db, string[] args)
        {
            string catId = args.Length > 1 ? args[1] : "motogp";
            int races = args.Length > 2 ? int.Parse(args[2]) : 6;
            int laps0 = args.Length > 3 ? int.Parse(args[3]) : 12;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 1 });
            var rules = db.GetRules(catId, 2026);
            var circuits = db.Calendars.Values.SelectMany(c => c.Rounds).Select(r => r.CircuitId).Distinct().OrderBy(x => x).ToList();
            var sum = new Dictionary<string, double>();
            var cnt = new Dictionary<string, int>();
            var laps = new Dictionary<string, double>();
            var lapN = new Dictionary<string, int>();
            for (int i = 0; i < races; i++)
            {
                var circuit = db.Circuits[circuits[(i * 7) % circuits.Count]];
                var cfg = new RaceConfig { Rules = rules, Circuit = circuit, Tuning = db.Tuning, Seed = (ulong)(100 + i), TimeStep = 0.2, Laps = laps0 };
                cfg.Weather.AirTemp = circuit.AverageAirTempC;
                int grid = 1;
                foreach (var team in world.TeamsInCategory(catId))
                {
                    var bike = world.GetBike(team.BikeId);
                    var perf = BikePerformance.Compute(bike, db.GetBikeClass(bike.BikeClassId));
                    foreach (var rid in team.RiderIds)
                    {
                        var e = AverageEntry(grid, 75);
                        e.Rider.Id = team.Id + "|" + rid;
                        e.Bike = perf; e.ManufacturerId = team.ManufacturerId; e.Team = team; e.GridPosition = grid++;
                        cfg.Entries.Add(e);
                    }
                }
                // Rotate the grid so nobody always starts in front.
                foreach (var e in cfg.Entries) e.GridPosition = (e.GridPosition + i * 5) % cfg.Entries.Count + 1;
                var sim = new RaceSimulation(cfg);
                sim.RunToEnd();
                foreach (var r in sim.GetClassification())
                {
                    string m = r.ManufacturerId;
                    double v = r.Status == ResultStatus.Finished ? r.Position : cfg.Entries.Count;
                    sum[m] = (sum.ContainsKey(m) ? sum[m] : 0) + v;
                    if (r.BestLap > 0) { laps[m] = (laps.ContainsKey(m) ? laps[m] : 0) + r.BestLap - sim.FastestLap; lapN[m] = (lapN.ContainsKey(m) ? lapN[m] : 0) + 1; }
                    cnt[m] = (cnt.ContainsKey(m) ? cnt[m] : 0) + 1;
                }
            }
            foreach (var kv in sum.OrderBy(k => k.Value / cnt[k.Key]))
            {
                var team = world.Teams.Values.First(t => t.CategoryId == catId && t.ManufacturerId == kv.Key);
                var bike = world.GetBike(team.BikeId);
                var perf = BikePerformance.Compute(bike, db.GetBikeClass(bike.BikeClassId));
                Console.WriteLine($"{kv.Key,-12} avg pos {kv.Value / cnt[kv.Key],5:0.0}  best lap +{(lapN.ContainsKey(kv.Key) ? laps[kv.Key] / lapN[kv.Key] : 0):0.000}s  power {perf[PerfAttr.Power]:0} top {perf[PerfAttr.TopSpeed]:0} mid {perf[PerfAttr.MidCorner]:0} brake {perf[PerfAttr.Braking]:0} trac {perf[PerfAttr.Traction]:0} wear {perf[PerfAttr.TireWear]:0}");
            }
            return 0;
        }

        static int Profile(GameDatabase db)
        {
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 5 });
            var times = new Dictionary<string, double>();
            var perEvent = new List<double>();
            for (int e = 0; e < 10; e++)
            {
                var evWatch = System.Diagnostics.Stopwatch.StartNew();
                var ev = game.BeginNextEvent();
                foreach (var kv in ev.Weekends)
                {
                    var w = kv.Value;
                    while (!w.IsComplete)
                    {
                        string key = kv.Key + ":" + w.CurrentSession.Type;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        w.RunCurrentHeadless();
                        times[key] = (times.ContainsKey(key) ? times[key] : 0) + sw.Elapsed.TotalMilliseconds;
                    }
                }
                game.CompleteEvent(ev);
                perEvent.Add(evWatch.Elapsed.TotalMilliseconds);
                Console.WriteLine($"event {e + 1} {ev.Plan.Round.Name,-28} {string.Join(",", ev.Weekends.Keys),-24} {evWatch.Elapsed.TotalMilliseconds,6:0} ms");
            }
            foreach (var kv in times.OrderByDescending(k => k.Value)) Console.WriteLine($"{kv.Key,-28} {kv.Value,8:0} ms");
            Console.WriteLine($"total {times.Values.Sum():0} ms for 10 events; average of events 3-10: {perEvent.Skip(2).Average():0} ms");
            return 0;
        }

        // ---------------------------------------------------------------- SVG previews (what the race view draws)

        static string Pt(TrackGeometry g, double d, double lat, double ox, double oy, double k)
        {
            double x, y, h;
            g.Evaluate(d, lat, out x, out y, out h);
            return ((x - ox) * k).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," + ((oy - y) * k).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Band(TrackGeometry g, double from, double to, double latA, double latB, double ox, double oy, double k, string fill)
        {
            var pts = new List<string>();
            double len = to - from;
            int n = Math.Max(2, (int)(len / 3));
            for (int i = 0; i <= n; i++) pts.Add(Pt(g, from + len * i / n, latA, ox, oy, k));
            for (int i = n; i >= 0; i--) pts.Add(Pt(g, from + len * i / n, latB, ox, oy, k));
            return $"<polygon points='{string.Join(" ", pts)}' fill='{fill}'/>";
        }

        static string TrackSvg(TrackModel t, TrackGeometry g, double ox, double oy, double k, bool details)
        {
            var sb = new System.Text.StringBuilder();
            const double hw = 6;
            if (details)
                foreach (var s in t.Segments)
                {
                    if (!s.IsCorner) continue;
                    int outside = s.AngleDeg > 0 ? -1 : 1;
                    double runoff = 8 + 10 * s.BrakingIntensity;
                    sb.Append(Band(g, s.Start - 25, s.End + 20, outside * (hw + 1.3), outside * (hw + 1.3 + runoff), ox, oy, k, "#c4b082"));
                }
            sb.Append(Band(g, 0, t.Length, hw, -hw, ox, oy, k, "#3a3c42"));
            if (details)
            {
                foreach (var s in t.Segments)
                {
                    if (!s.IsCorner) continue;
                    int outside = s.AngleDeg > 0 ? -1 : 1;
                    sb.Append(Band(g, s.Start - 10, s.End + 10, outside * hw, outside * (hw + 1.3), ox, oy, k, "#c81e1e"));
                }
                // pit lane
                int side = g.Orientation;
                double entry = t.PitEntry, span = t.PitExit + t.Length - entry;
                var pts = new List<string>();
                for (int i = 0; i <= 40; i++) { double tt = i / 40.0; pts.Add(Pt(g, entry + span * tt, side * PitLat(tt), ox, oy, k)); }
                sb.Append($"<polyline points='{string.Join(" ", pts)}' fill='none' stroke='#55585f' stroke-width='{6 * k:0.0}'/>");
                sb.Append($"<line x1='{Pt(g, t.Length - 1, hw, ox, oy, k).Split(',')[0]}' y1='{Pt(g, t.Length - 1, hw, ox, oy, k).Split(',')[1]}' x2='{Pt(g, t.Length - 1, -hw, ox, oy, k).Split(',')[0]}' y2='{Pt(g, t.Length - 1, -hw, ox, oy, k).Split(',')[1]}' stroke='white' stroke-width='3'/>");
            }
            return sb.ToString();
        }

        static double PitLat(double t)
        {
            double a = 6, b = 15, r = 0.12;
            if (t < r) return a + (b - a) * t / r;
            if (t > 1 - r) return b + (a - b) * (t - (1 - r)) / r;
            return b;
        }

        /// <summary>preview &lt;category&gt; &lt;circuit&gt; &lt;seconds&gt;: SVG snapshot of a race in progress.</summary>
        static int Preview(GameDatabase db, string[] args)
        {
            string catId = args.Length > 1 ? args[1] : "motogp";
            string circuitId = args.Length > 2 ? args[2] : "mugello";
            double seconds = args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 120;
            string outPath = args.Length > 4 ? args[4] : "preview.svg";
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 1 });
            var rules = db.GetRules(catId, 2026);
            var c = db.Circuits[circuitId];
            var cfg = new RaceConfig { Rules = rules, Circuit = c, Tuning = db.Tuning, Seed = 3, TimeStep = 0.05, Laps = 20, FuelLimitLitres = rules.Fuel.TankLimitLitres };
            cfg.Weather.AirTemp = c.AverageAirTempC;
            int grid = 1;
            foreach (var team in world.TeamsInCategory(catId))
            {
                var bike = world.GetBike(team.BikeId);
                var perf = BikePerformance.Compute(bike, db.GetBikeClass(bike.BikeClassId));
                foreach (var id in team.RiderIds)
                    cfg.Entries.Add(new RaceEntry { Rider = world.GetRider(id), Team = team, Bike = perf, ManufacturerId = team.ManufacturerId, GridPosition = grid++, SetupQuality = 0.7 });
            }
            var sim = new RaceSimulation(cfg);
            while (sim.Time < seconds && !sim.IsFinished) sim.Step();
            var t = sim.Track;
            var g = new TrackGeometry(t);
            double pad = 60, k = 1100.0 / Math.Max(g.MaxX - g.MinX, g.MaxY - g.MinY);
            double ox = g.MinX - pad, oy = g.MaxY + pad;
            double w = (g.MaxX - g.MinX + 2 * pad) * k, h = (g.MaxY - g.MinY + 2 * pad) * k;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{w + 330:0}' height='{Math.Max(h, 760):0}' font-family='sans-serif'>");
            sb.Append($"<rect width='100%' height='100%' fill='#34603a'/>");
            sb.Append(TrackSvg(t, g, ox, oy, k, true));
            var ordered = sim.Riders.OrderBy(r => r.Position).ToList();
            foreach (var r in sim.Riders)
            {
                double lat = r.Status == RaceStatus.InPitLane ? g.Orientation * PitLat(r.PitProgress / t.PitLaneLength) : r.LateralOffset * 6;
                var p = Pt(g, r.Distance, lat, ox, oy, k).Split(',');
                string col = r.Entry.Team.Colors.Primary;
                string label = r.Entry.Rider.Number.ToString();
                sb.Append($"<circle cx='{p[0]}' cy='{p[1]}' r='7' fill='{col}' stroke='white' stroke-width='1.5'/>");
                sb.Append($"<text x='{p[0]}' y='{p[1]}' dx='9' dy='-6' font-size='11' fill='white' stroke='black' stroke-width='0.3'>{label} {r.Entry.Rider.Code}</text>");
            }
            double tx = w + 10;
            sb.Append($"<rect x='{w:0}' y='0' width='330' height='100%' fill='#111418'/>");
            sb.Append($"<text x='{tx}' y='28' font-size='18' fill='white'>{c.ShortName} · lap {sim.LeaderLap + 1}/{sim.TotalLaps} · {sim.Time:0}s</text>");
            int row = 0;
            foreach (var r in ordered)
            {
                double y = 60 + row++ * 30;
                string gap = r.Position == 1 ? "Leader" : r.Status == RaceStatus.Racing ? "+" + r.GapToLeader.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) : r.Status.ToString();
                sb.Append($"<rect x='{tx}' y='{y - 16}' width='5' height='20' fill='{r.Entry.Team.Colors.Primary}'/>");
                sb.Append($"<text x='{tx + 12}' y='{y}' font-size='15' fill='white'>{r.Position}. {r.Entry.Rider.ShortName}</text>");
                sb.Append($"<text x='{tx + 310}' y='{y}' font-size='14' fill='#9aa4b2' text-anchor='end'>{gap}</text>");
            }
            sb.Append("</svg>");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine("wrote " + outPath);
            return 0;
        }

        /// <summary>circuits [out.svg]: all circuits as drawn by the race view (shape check).</summary>
        static int CircuitSheet(GameDatabase db, string[] args)
        {
            string outPath = args.Length > 1 ? args[1] : "circuits.svg";
            var list = db.Circuits.Values.OrderBy(x => x.Id).ToList();
            int cols = 6, cell = 300;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{cols * cell}' height='{(list.Count + cols - 1) / cols * cell}' font-family='sans-serif'><rect width='100%' height='100%' fill='#1b1f24'/>");
            for (int i = 0; i < list.Count; i++)
            {
                var t = TrackModel.Get(list[i]);
                var g = new TrackGeometry(t);
                double k = (cell - 50) / Math.Max(g.MaxX - g.MinX, g.MaxY - g.MinY);
                double ox = g.MinX - 25 / k, oy = g.MaxY + 35 / k;
                sb.Append($"<g transform='translate({i % cols * cell},{i / cols * cell})'><text x='8' y='18' fill='white' font-size='14'>{list[i].ShortName}</text>");
                sb.Append(TrackSvg(t, g, ox, oy, k, false).Replace("#3a3c42", "#d0d4da"));
                var s = Pt(g, 0, 0, ox, oy, k).Split(',');
                sb.Append($"<circle cx='{s[0]}' cy='{s[1]}' r='4' fill='#e10600'/></g>");
            }
            sb.Append("</svg>");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine("wrote " + outPath);
            return 0;
        }

        static int RaceTest(GameDatabase db, string[] args)
        {
            var rules = db.Rules[args.Length > 1 ? args[1] : "motogp_2026"];
            var c = db.Circuits[args.Length > 2 ? args[2] : "mugello"];
            ulong seed = args.Length > 3 ? ulong.Parse(args[3]) : 1;
            var cfg = new RaceConfig { Rules = rules, Circuit = c, Tuning = db.Tuning, Seed = seed, TimeStep = double.Parse(Environment.GetEnvironmentVariable("SIM_DT") ?? "0.05", System.Globalization.CultureInfo.InvariantCulture) };
            cfg.Laps = (int)Math.Round(rules.RaceDistanceKm * 1000 / c.LengthM);
            cfg.FuelLimitLitres = rules.Fuel.TankLimitLitres;
            cfg.Weather.AirTemp = c.AverageAirTempC;
            var rng = new DeterministicRandom(seed);
            for (int i = 0; i < 22; i++)
            {
                var e = AverageEntry(i, 70 + rng.Range(-8, 8));
                for (int k = 0; k < e.Bike.Values.Length; k++) e.Bike.Values[k] = 60 + rng.Range(-10, 10);
                cfg.Entries.Add(e);
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sim = new RaceSimulation(cfg);
            sim.RunToEnd();
            sw.Stop();
            Console.WriteLine($"{c.Id} {cfg.Laps} laps, ticks {sim.Tick}, {sw.ElapsedMilliseconds} ms, weather {sim.Weather.Describe()}");
            foreach (var r in sim.GetClassification())
            {
                var st = sim.Riders.First(x => x.Entry.Rider.Id == r.RiderId);
                Console.WriteLine($"{r.Position,2} {r.RiderId,-4} grid {r.GridPosition,2} {r.Status,-8} {MathX.FormatLapTime(r.TotalTime)} gap {r.GapToWinner,7:0.000} best {MathX.FormatLapTime(r.BestLap)} wearF {st.Front.Wear:0.00} wearR {st.Rear.Wear:0.00} tF {st.Front.Temperature:0} tR {st.Rear.Temperature:0} fuel {st.Fuel:0.0} ovt {st.Overtakes} {r.Note}");
            }
            var counts = sim.Events.GroupBy(e => e.Type).Select(gp => gp.Key + ":" + gp.Count());
            Console.WriteLine(string.Join(" ", counts));
            foreach (var ev in sim.Events.Where(e => e.Important).Take(60)) Console.WriteLine("  " + ev);
            return 0;
        }
    }
}
