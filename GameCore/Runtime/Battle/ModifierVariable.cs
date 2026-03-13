namespace GameCore.Battle
{
    /// <summary>
    /// The skill and unit variables that a modifier can override (Set) or adjust (Modify).
    /// <para>
    /// Skill variables (<see cref="Cost"/>, <see cref="DamageMultiplier"/>, <see cref="IsAoe"/>,
    /// <see cref="Cooldown"/>, <see cref="InitialCooldown"/>) are applied when a modifier appears
    /// in a skill slot's modifier list.
    /// </para>
    /// <para>
    /// Penetration variables (<see cref="PhysicalPenetration"/> through <see cref="VoidPenetration"/>)
    /// reduce the target's effective resistance of that type when this unit attacks.
    /// Penetration can be set directly in a unit's YAML (<c>penetrations</c> dict) or adjusted
    /// via a modifier in a unit's modifier list.
    /// 0 = no penetration, 50 = ignore half the target's resistance, 100 = ignore all resistance.
    /// </para>
    /// </summary>
    public enum ModifierVariable
    {
        // ── Skill variables ──────────────────────────────────────────────────

        /// <summary>MP cost of the skill.</summary>
        Cost,
        /// <summary>Multiplier applied to the entire skill's damage output.</summary>
        DamageMultiplier,
        /// <summary>Whether the skill hits all enemies (AoE). Only valid for Set, not Modify.</summary>
        IsAoe,
        /// <summary>Turns the unit must wait before using the skill again.</summary>
        Cooldown,
        /// <summary>Cooldown the skill starts with at the beginning of battle.</summary>
        InitialCooldown,

        // ── Penetration variables ────────────────────────────────────────────

        /// <summary>Physical resistance penetration percentage for the attacker. Reduces the target's effective physical resistance.</summary>
        PhysicalPenetration,
        /// <summary>Fire resistance penetration percentage for the attacker.</summary>
        FirePenetration,
        /// <summary>Cold resistance penetration percentage for the attacker.</summary>
        ColdPenetration,
        /// <summary>Lightning resistance penetration percentage for the attacker.</summary>
        LightningPenetration,
        /// <summary>Holy resistance penetration percentage for the attacker.</summary>
        HolyPenetration,
        /// <summary>Void resistance penetration percentage for the attacker.</summary>
        VoidPenetration,
    }
}
