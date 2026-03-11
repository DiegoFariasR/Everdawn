namespace GameCore.Battle;

/// <summary>
/// Returned by InteractiveBattleSession.HandleRequest().
/// Contains everything the client needs to render the current battle state.
/// Clients must never query session internals directly — this is the full picture.
/// </summary>
public record BattleResponse(
    /// <summary>Events produced by the most recent request (for animation / highlight).</summary>
    IReadOnlyList<BattleEvent> NewEvents,
    /// <summary>Full event log from the start of the session.</summary>
    IReadOnlyList<BattleEvent> FullLog,
    /// <summary>Current HP/MP/alive state for every unit.</summary>
    IReadOnlyList<UnitState> State,
    /// <summary>Non-null when it is a player unit's turn and input is required.</summary>
    BattlePendingInput? PendingInput,
    bool IsOver,
    string? WinningTeam
);
