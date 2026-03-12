using GameCore.Content;

namespace GameCore.Tests;

/// <summary>
/// Provides a pre-configured <see cref="IContentSource"/> for use in tests.
/// <para>
/// The content root is configured explicitly from the known output directory structure,
/// not discovered at runtime by walking up the directory tree.
/// Tests compile to <c>GameCore.Tests/bin/{Configuration}/{TFM}/</c>; the repository
/// <c>GameData/Base/</c> is exactly four directories above that output path.
/// </para>
/// </summary>
public static class TestContentSource
{
    private static readonly Lazy<IContentSource> _default = new(CreateDefault);

    /// <summary>
    /// A shared default source that reads from <c>GameData/Base/</c> in the repository.
    /// Resolved once and cached for the lifetime of the test process.
    /// </summary>
    public static IContentSource Default => _default.Value;

    private static IContentSource CreateDefault()
    {
        // Tests output to GameCore.Tests/bin/{Configuration}/{TFM}/.
        // GameData/Base/ is at the repository root, four directories above.
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GameData", "Base"));

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(
                $"Content root not found at '{root}'. " +
                "Ensure tests are run from the repository root.");

        return new FileSystemContentSource(root);
    }
}
