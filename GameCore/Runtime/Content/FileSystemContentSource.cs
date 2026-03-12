using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace GameCore.Content
{
    /// <summary>
    /// An <see cref="IContentSource"/> backed by the local file system.
    /// Wraps a <c>basePath</c> directory and resolves all paths relative to it.
    /// <para>
    /// Hosts create this with an explicit path — GameCore never discovers paths on its own.
    /// </para>
    /// </summary>
    public sealed class FileSystemContentSource : IContentSource
    {
        private readonly string _basePath;

        /// <param name="basePath">
        /// Absolute path to the root content directory (e.g. the <c>GameData/Base</c> folder).
        /// </param>
        public FileSystemContentSource(string basePath)
        {
            _basePath = basePath;
        }

        /// <inheritdoc/>
        public string ReadAllText(string relativePath) =>
            File.ReadAllText(Path.Combine(_basePath, relativePath));

        /// <inheritdoc/>
        public IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern)
        {
            var dir = Path.Combine(_basePath, relativeDirectory);
            if (!Directory.Exists(dir))
                return Enumerable.Empty<string>();
            return Directory.EnumerateFiles(dir, searchPattern)
                .Select(f => Path.Combine(relativeDirectory, Path.GetFileName(f)));
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string relativeDirectory) =>
            Directory.Exists(Path.Combine(_basePath, relativeDirectory));

        /// <inheritdoc/>
        public bool FileExists(string relativePath) =>
            File.Exists(Path.Combine(_basePath, relativePath));
    }
}
