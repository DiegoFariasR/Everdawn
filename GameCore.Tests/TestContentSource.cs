using System;
using System.IO;
using GameCore.Content;

namespace GameCore.Tests
{
    /// <summary>
    /// Provides a pre-configured <see cref="IContentSource"/> for use in tests.
    /// <para>
    /// The content root is a host-local path inside the test output directory.
    /// <c>GameData/Base/</c> is copied into the output during build
    /// (see <c>GameCore.Tests.csproj</c> Content items), so no repository-relative
    /// path math is needed at runtime.
    /// </para>
    /// </summary>
    public static class TestContentSource
    {
        private static readonly Lazy<IContentSource> _default = new(CreateDefault);

        /// <summary>
        /// A shared default source that reads from <c>GameData/Base/</c> in the test output.
        /// Resolved once and cached for the lifetime of the test process.
        /// </summary>
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
