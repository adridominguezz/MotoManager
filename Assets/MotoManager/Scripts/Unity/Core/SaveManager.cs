using System;
using System.IO;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>Save games in Application.persistentDataPath. Writes go to a temp file first so a crash never corrupts a save.</summary>
    public static class SaveManager
    {
        public const string AutoSlot = "autosave";
        /// <summary>Manual save slots (the autosave is separate and written after every event).</summary>
        public static readonly string[] Slots = { "slot1", "slot2", "slot3" };

        private static string _root;

        /// <summary>
        /// Reads the save folder on the main thread: Unity only allows Application.persistentDataPath there, and saving
        /// and loading run on the worker thread.
        /// </summary>
        public static void Init()
        {
            if (_root == null) _root = Path.Combine(Application.persistentDataPath, "saves");
        }

        public static string PathFor(string slot)
        {
            if (_root == null) Init();
            return Path.Combine(_root, slot + ".json");
        }

        public static bool Exists(string slot)
        {
            return File.Exists(PathFor(slot));
        }

        /// <summary>Serialises on the calling thread (main thread recommended between events).</summary>
        public static string Serialize(WorldState world)
        {
            return SaveSystem.Serialize(world);
        }

        /// <summary>File writing only: safe to call from a worker thread with a JSON produced earlier.</summary>
        public static void Write(string slot, string json)
        {
            string path = PathFor(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static WorldState Load(string slot)
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return null;
            try
            {
                return SaveSystem.Deserialize(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError("Could not load save " + slot + ": " + e);
                return null;
            }
        }

        public static void Delete(string slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(MetaPath(slot))) File.Delete(MetaPath(slot));
        }

        private static string MetaPath(string slot)
        {
            return PathFor(slot).Replace(".json", ".meta");
        }

        /// <summary>What a save holds, shown before loading it (written with the save, read without loading the world).</summary>
        public static SaveMeta MetaFor(WorldState world)
        {
            var m = world.PlayerManager;
            var team = world.PlayerTeam;
            return new SaveMeta
            {
                Manager = m != null ? m.FullName : "",
                Team = team != null ? team.Name : "",
                Season = world.Season,
                GameDate = world.CurrentDate.ToString("yyyy-MM-dd"),
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
        }

        public static void WriteMeta(string slot, SaveMeta meta)
        {
            string path = MetaPath(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Join("\n", new[] { meta.Manager, meta.Team, meta.Season.ToString(), meta.GameDate, meta.SavedAt }));
        }

        public static SaveMeta ReadMeta(string slot)
        {
            try
            {
                string path = MetaPath(slot);
                if (!File.Exists(path)) return Exists(slot) ? new SaveMeta() : null;
                var lines = File.ReadAllText(path).Split('\n');
                int season;
                return new SaveMeta
                {
                    Manager = lines.Length > 0 ? lines[0] : "",
                    Team = lines.Length > 1 ? lines[1] : "",
                    Season = lines.Length > 2 && int.TryParse(lines[2], out season) ? season : 0,
                    GameDate = lines.Length > 3 ? lines[3] : "",
                    SavedAt = lines.Length > 4 ? lines[4] : ""
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public class SaveMeta
    {
        public string Manager = "";
        public string Team = "";
        public int Season;
        public string GameDate = "";
        public string SavedAt = "";
    }
}
