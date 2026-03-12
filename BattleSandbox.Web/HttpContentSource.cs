using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GameCore.Content;

namespace BattleSandbox.Web
{
    /// <summary>
    /// An <see cref="IContentSource"/> backed by HTTP. Designed for Blazor WebAssembly,
    /// where the local file system is not accessible.
    /// <para>
    /// All content files are pre-fetched asynchronously at startup via <see cref="LoadAsync"/>
    /// and cached in memory. Once loaded, reads are served synchronously from the cache,
    /// satisfying the synchronous <see cref="IContentSource"/> contract.
    /// </para>
    /// <para>
    /// The list of available files is read from a <c>content-index.json</c> manifest
    /// published alongside the content in the app's <c>wwwroot</c>. The manifest is
    /// committed to the repository and updated whenever content files are added or removed.
    /// </para>
    /// </summary>
    public sealed class HttpContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _cache;

        private HttpContentSource(Dictionary<string, string> cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Reads the <c>content-index.json</c> manifest from <paramref name="baseUrl"/>,
        /// fetches every listed file, and returns a fully-loaded <see cref="HttpContentSource"/>.
        /// Call this once at host startup (in <c>Program.cs</c>) and register the result as a
        /// singleton <see cref="IContentSource"/> in the DI container.
        /// </summary>
        /// <param name="http">The <see cref="HttpClient"/> configured with the app base address.</param>
        /// <param name="baseUrl">
        /// URL path to the content root as served from <c>wwwroot</c>, e.g. <c>"GameData/Base"</c>.
        /// </param>
        public static async Task<HttpContentSource> LoadAsync(HttpClient http, string baseUrl)
        {
            baseUrl = baseUrl.TrimEnd('/');

            // Fetch the manifest that lists all available content files.
            var indexJson = await http.GetStringAsync($"{baseUrl}/content-index.json");
            var filePaths = JsonSerializer.Deserialize<string[]>(indexJson)
                ?? Array.Empty<string>();

            // Pre-fetch every file and cache the text content.
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relativePath in filePaths)
            {
                var normalised = relativePath.Replace('\\', '/');
                var url = $"{baseUrl}/{normalised}";
                cache[normalised] = await http.GetStringAsync(url);
            }

            return new HttpContentSource(cache);
        }

        /// <inheritdoc/>
        public string ReadAllText(string relativePath)
        {
            var key = relativePath.Replace('\\', '/');
            if (_cache.TryGetValue(key, out var text))
                return text;
            throw new FileNotFoundException($"Content file not found in cache: '{relativePath}'.");
        }

        /// <inheritdoc/>
        public IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern)
        {
            var dir = relativeDirectory.Replace('\\', '/').TrimEnd('/') + '/';

            // Translate glob pattern (e.g. "*.yml") to a file extension filter.
            var extension = searchPattern.TrimStart('*');

            return _cache.Keys
                .Where(k =>
                    k.StartsWith(dir, StringComparison.OrdinalIgnoreCase) &&
                    k.EndsWith(extension, StringComparison.OrdinalIgnoreCase) &&
                    !k.Substring(dir.Length).Contains('/'))
                .ToList();
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string relativeDirectory)
        {
            var dir = relativeDirectory.Replace('\\', '/').TrimEnd('/') + '/';
            return _cache.Keys.Any(k => k.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public bool FileExists(string relativePath)
        {
            return _cache.ContainsKey(relativePath.Replace('\\', '/'));
        }
    }
}
