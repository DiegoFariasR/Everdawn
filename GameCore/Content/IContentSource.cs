namespace GameCore.Content;

/// <summary>
/// Abstracts access to raw content files (YAML, JSON, etc.) for the content pipeline.
/// <para>
/// <b>Architecture note:</b> GameCore never discovers content paths on its own.
/// Hosts (tests, web sandbox, Unity editor) explicitly create and inject a concrete
/// implementation. This keeps GameCore free of file-system assumptions and makes
/// content loading portable across any host environment.
/// </para>
/// <para>
/// Implementations provided by hosts:
/// <list type="bullet">
///   <item><see cref="FileSystemContentSource"/> — wraps a local directory (tests, Unity editor)</item>
///   <item>Future: <c>HttpContentSource</c> for Blazor WASM, or <c>ResourceContentSource</c> for embedded data.</item>
/// </list>
/// </para>
/// </summary>
public interface IContentSource
{
    /// <summary>
    /// Returns the text content of a file at <paramref name="relativePath"/>
    /// (relative to the content root provided by this source).
    /// </summary>
    string ReadAllText(string relativePath);

    /// <summary>
    /// Lists all file names (not full paths) in <paramref name="relativeDirectory"/>
    /// that match <paramref name="searchPattern"/> (e.g. <c>"*.yml"</c>).
    /// Returns the relative path of each file (relative to the content root),
    /// suitable for passing back to <see cref="ReadAllText"/>.
    /// </summary>
    IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern);

    /// <summary>Returns true if the directory at <paramref name="relativeDirectory"/> exists.</summary>
    bool DirectoryExists(string relativeDirectory);

    /// <summary>Returns true if the file at <paramref name="relativePath"/> exists.</summary>
    bool FileExists(string relativePath);
}
