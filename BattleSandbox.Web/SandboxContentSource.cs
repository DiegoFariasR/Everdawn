using GameCore.Content;

namespace BattleSandbox.Web;

/// <summary>
/// Provides a content source for the Battle Sandbox web application.
/// Wraps a <see cref="FileSystemContentSource"/> pointing at the <c>GameData/Base</c>
/// directory discovered at runtime.
/// <para>
/// Path discovery is the host's responsibility — not GameCore's or the scenarios'.
/// This class is the single place in the sandbox that knows how to find content.
/// </para>
/// </summary>
public static class SandboxContentSource
{
    private static readonly Lazy<IContentSource> _default = new(CreateDefault);

    /// <summary>Shared content source for the current sandbox session.</summary>
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
