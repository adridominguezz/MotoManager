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
        }
    }
}
