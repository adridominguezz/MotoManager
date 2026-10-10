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

        public static string PathFor(string slot)
        {
            return Path.Combine(Application.persistentDataPath, "saves", slot + ".json");
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
                Debug.LogError("Could not load save " + slot + ": " + e.Message);
                return null;
            }
        }

        public static void Delete(string slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
