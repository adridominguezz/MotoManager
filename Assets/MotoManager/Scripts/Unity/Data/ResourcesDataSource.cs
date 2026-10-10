using System.Collections.Generic;
using MotoManager.Core.Data;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// Reads the data folders packed as TextAssets under Assets/MotoManager/Resources/Data.
    /// Resources are used (instead of StreamingAssets) because they can be read synchronously on Android.
    /// Must be called on the main thread; the parsed text can then be handed to a worker thread.
    /// </summary>
    public class ResourcesDataSource : IDataSource
    {
        private readonly string _root;
        private readonly Dictionary<string, List<DataFile>> _cache = new Dictionary<string, List<DataFile>>();

        public ResourcesDataSource(string root = "Data")
        {
            _root = root;
        }

        /// <summary>Loads every folder now (main thread) so the source can later be used from any thread.</summary>
        public ResourcesDataSource Preload()
        {
            foreach (var folder in DataLoader.AllFolders) Read(folder);
            return this;
        }

        public IEnumerable<DataFile> LoadFolder(string folder)
        {
            return Read(folder);
        }

        private List<DataFile> Read(string folder)
        {
            List<DataFile> files;
            if (_cache.TryGetValue(folder, out files)) return files;
            files = new List<DataFile>();
            var assets = Resources.LoadAll<TextAsset>(_root + "/" + folder);
            var sorted = new List<TextAsset>(assets);
            sorted.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var asset in sorted)
            {
                files.Add(new DataFile(asset.name, asset.text));
                Resources.UnloadAsset(asset);
            }
            _cache[folder] = files;
            return files;
        }
    }
}
