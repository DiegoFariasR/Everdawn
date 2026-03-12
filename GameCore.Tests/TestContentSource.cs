using GameCore.Content;

namespace GameCore.Tests;

/// <summary>
/// Provides a pre-configured <see cref="IContentSource"/> for use in tests.
/// <para>
/// Path discovery is the responsibility of the test host, not GameCore or the scenarios.
/// This class centralises the one place where tests walk up the directory tree to find
/// <c>GameData/Base/</c>.
/// </para>
/// </summary>
public static class TestContentSource
{
    private static readonly Lazy<IContentSource> _default = new(CreateDefault);

    /// <summary>
    /// A shared default source that reads from <c>GameData/Base/</c> in the repository.
    /// Discovered once and cached for the lifetime of the test process.
    /// </summary>
    public static IContentSource Default => _default.Value;

    private static IContentSource CreateDefault()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "GameData", "Base");
            if (Directory.Exists(candidate))
                return new FileSystemContentSource(candidate);
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not find GameData/Base/ by walking up from '{AppContext.BaseDirectory}'.");
    }
}
