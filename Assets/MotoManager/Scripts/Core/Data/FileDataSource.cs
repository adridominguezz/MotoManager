using System.Collections.Generic;
using System.IO;

namespace MotoManager.Core.Data
{
    /// <summary>Reads the data folders from disk (tests, tools, editor). On device Unity uses a Resources based source.</summary>
    public class FileDataSource : IDataSource
    {
        private readonly string _root;

        public FileDataSource(string root)
        {
            _root = root;
        }

        public IEnumerable<DataFile> LoadFolder(string folder)
        {
            string dir = Path.Combine(_root, folder);
            if (!Directory.Exists(dir)) yield break;
            var files = new List<string>(Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories));
            files.Sort(System.StringComparer.Ordinal);
            foreach (var f in files)
                yield return new DataFile(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f));
        }
    }
}
