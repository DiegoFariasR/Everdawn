using GameCore.World;

namespace GameCore.Flow;

/// <summary>
/// The game state when the player is at a Location and can choose an Activity.
/// Captures the exploration rules: auto-enter when there is exactly one option,
/// show a list when there are multiple.
/// </summary>
public sealed record ExplorationState(Location CurrentLocation, IReadOnlyList<Activity> AvailableActivities)
{
    /// <summary>
    /// True when the location has exactly one activity and the client should enter it
    /// automatically without showing a selection screen.
    /// </summary>
    public bool ShouldAutoEnter => AvailableActivities.Count == 1;

    /// <summary>
    /// The single Activity to auto-enter. Only call this when <see cref="ShouldAutoEnter"/>
    /// is true — throws otherwise.
    /// </summary>
    public Activity AutoEnterActivity =>
        ShouldAutoEnter
            ? AvailableActivities[0]
            : throw new InvalidOperationException(
                "Cannot auto-enter: location has multiple activities.");
}
