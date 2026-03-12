namespace GameCore.Battle;

// Internal pending input — projected to the public PendingInputView by BattleSession.
internal record BattlePendingInput(
    BattleUnit Actor,
    /// <summary>All skills this unit has (always at least one — the free basic skill).</summary>
    IReadOnlyList<BattleSkill> Skills,
    /// <summary>IDs of skills the unit can currently afford (MP check). Index 0 is always included.</summary>
    IReadOnlyList<string> AvailableSkillIds,
    /// <summary>Living enemy units — valid targets for damage/enemy-targeted skills.</summary>
    IReadOnlyList<BattleUnit> EnemyTargets,
    /// <summary>Living ally units including self — valid targets for heal/ally-targeted skills.</summary>
    IReadOnlyList<BattleUnit> AllyTargets,
    /// <summary>Remaining cooldown turns per skill ID. Only contains entries where cooldown > 0.</summary>
    IReadOnlyDictionary<string, int> SkillCooldowns
);
