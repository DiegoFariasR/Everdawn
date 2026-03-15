using System;
using System.IO;
using GameCore.Content;

namespace GameCore.Tests
{
    public static class TestContentSource
    {
        private static readonly Lazy<IContentSource> _default = new(CreateDefault);

        // Shared default content source from GameData/Base; resolved once and cached per test process.
        public static IContentSource Default => _default.Value;

        private static IContentSource CreateDefault()
        {
            // GameData/Base is copied into the test output by the project's Content items.
            // The path is local to the test host — no repository root discovery required.
            var root = Path.Combine(AppContext.BaseDirectory, "GameData", "Base");

            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(
                    $"Content root not found at '{root}'. " +
                    "Ensure the test project's GameData/Base Content items are present in the build output.");

            return new FileSystemContentSource(root);
        }
    }
}
