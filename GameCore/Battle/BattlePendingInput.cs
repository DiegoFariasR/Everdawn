namespace GameCore.Battle;

/// <summary>
/// Describes what the client must display and accept as input for the current player turn.
/// When null in a BattleResponse, it is not the player's turn (either game over or
/// enemies are resolving — but enemies always auto-resolve before the response returns).
/// </summary>
public record BattlePendingInput(
    BattleUnit Actor,
    /// <summary>All skills this unit has (always at least one — the free basic skill).</summary>
    IReadOnlyList<BattleSkill> Skills,
    /// <summary>IDs of skills the unit can currently afford (MP check). Index 0 is always included.</summary>
    IReadOnlyList<string> AvailableSkillIds,
    /// <summary>Living enemy units — valid targets for damage/enemy-targeted skills.</summary>
    IReadOnlyList<BattleUnit> EnemyTargets,
    /// <summary>Living ally units including self — valid targets for heal/ally-targeted skills.</summary>
    IReadOnlyList<BattleUnit> AllyTargets
);
