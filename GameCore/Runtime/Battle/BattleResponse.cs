namespace GameCore.Battle;

// Internal response type — projected to the public BattleView by BattleSession.
internal record BattleResponse(
    /// <summary>Events produced by the most recent request (for animation / highlight).</summary>
    IReadOnlyList<BattleEvent> NewEvents,
    /// <summary>Full event log from the start of the session.</summary>
    IReadOnlyList<BattleEvent> FullLog,
    /// <summary>Current HP/MP/alive state for every unit.</summary>
    IReadOnlyList<UnitState> State,
    /// <summary>Non-null when it is a player unit's turn and input is required.</summary>
    BattlePendingInput? PendingInput,
    bool IsOver,
    string? WinningTeam,
    /// <summary>Current round number (1-based).</summary>
    int Round
);
