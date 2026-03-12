using GameCore.Content;

namespace BattleSandbox.Web;

/// <summary>
/// Provides a content source for the Battle Sandbox web application.
/// Wraps a <see cref="FileSystemContentSource"/> pointing at an explicitly configured
/// content root supplied by the host at startup.
/// <para>
/// The content root is registered in <c>Program.cs</c> via the DI container;
/// components receive it through <c>@inject IContentSource</c>.
/// This class is the single place in the sandbox that constructs the source from
/// the configured path — it does not search or walk the file system.
/// </para>
/// </summary>
public static class SandboxContentSource
{
    /// <summary>
    /// Creates a <see cref="FileSystemContentSource"/> from an explicit root path.
    /// Called once by <c>Program.cs</c> during host startup.
    /// </summary>
    /// <param name="contentRoot">
    /// Absolute path to the <c>GameData/Base</c> directory, provided by the host.
    /// </param>
    public static IContentSource Create(string contentRoot) =>
        new FileSystemContentSource(contentRoot);
}
