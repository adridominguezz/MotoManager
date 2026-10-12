using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;
using Newtonsoft.Json.Linq;

namespace MotoManager.Core.Season
{
    /// <summary>
    /// Brings old saves up to date. Each step takes a save of version N to N+1 working on the raw JSON (so fields
    /// can be renamed, split or recomputed before the typed load); steps run in a chain, so a save from any older
    /// version reaches the current one. After the typed load, <see cref="Repair"/> fills anything still missing.
    /// </summary>
    public static class SaveMigrations
    {
        private static readonly Dictionary<int, Action<JObject>> Steps = new Dictionary<int, Action<JObject>>
        {
            // v1 -> v2: world history, records and grid targets arrive (E7 / 8.6). Old saves start them empty;
            // grid targets are recorded from the grids as they are.
            { 1, json =>
                {
                    if (json["History"] == null) json["History"] = new JArray();
                    if (json["Records"] == null) json["Records"] = new JObject();
                    if (json["GridTargets"] == null) json["GridTargets"] = new JObject();
                }
            }
        };

        /// <summary>Runs every step from the save's version to the current one. Returns the migrated JSON.</summary>
        public static JObject Migrate(JObject json, int targetVersion)
        {
            int version = json["Version"] != null ? (int)json["Version"] : 1;
            while (version < targetVersion)
            {
                Action<JObject> step;
                if (!Steps.TryGetValue(version, out step)) throw new FormatException("No migration from save version " + version);
                step(json);
                version++;
                json["Version"] = version;
            }
            return json;
        }

        /// <summary>After loading: collections that may be missing in old or hand-edited saves, and derived data.</summary>
        public static void Repair(WorldState world)
        {
            if (world.History == null) world.History = new List<SeasonHistory>();
            if (world.Records == null) world.Records = new Dictionary<string, CategoryRecords>();
            if (world.GridTargets == null) world.GridTargets = new Dictionary<string, int>();
            foreach (var m in world.Managers.Values)
                if (m.OrgRelations == null) m.OrgRelations = new Dictionary<string, double>();
            foreach (var t in world.Teams.Values)
            {
                if (t.Shop == null) t.Shop = new TeamShop();
                if (t.Shop.Campaigns == null) t.Shop.Campaigns = new List<MerchCampaign>();
            }
            foreach (var o in world.Organisations.Values)
                if (o.MerchOrdered == null) o.MerchOrdered = new Dictionary<string, int>();
            // Grids as they are now become the targets championships keep.
            if (world.GridTargets.Count == 0)
                foreach (var team in world.Teams.Values)
                {
                    int n;
                    world.GridTargets.TryGetValue(team.CategoryId ?? "", out n);
                    world.GridTargets[team.CategoryId ?? ""] = n + 1;
                }
        }
    }
}
