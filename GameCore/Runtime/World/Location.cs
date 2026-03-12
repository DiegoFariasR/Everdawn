namespace GameCore.World;

/// <summary>
/// A place on the world map. Cities are just Locations with multiple Activities.
/// If a Location has exactly one available Activity, the client should auto-enter it.
/// </summary>
public sealed record Location(string Id, string DisplayName, IReadOnlyList<string> ActivityIds)
{
    /// <summary>
    /// True when the location should be entered automatically by the client without
    /// showing a selection screen. Equivalent to <c>ActivityIds.Count == 1</c>.
    /// </summary>
    public bool ShouldAutoEnter => ActivityIds.Count == 1;
}
