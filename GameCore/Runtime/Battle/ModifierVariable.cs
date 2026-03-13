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
    /// Resistance variables (<see cref="PhysicalResistance"/> through <see cref="DisruptionResistance"/>)
    /// are applied when a modifier appears in a unit's modifier list and adjust the unit's resistance
    /// percentages. 0 = no mitigation, 50 = half damage, 100 = immune, negative = weakness.
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

        // ── Unit resistance variables ────────────────────────────────────────

        /// <summary>Physical damage resistance percentage for the unit.</summary>
        PhysicalResistance,
        /// <summary>Fire damage resistance percentage for the unit.</summary>
        FireResistance,
        /// <summary>Cold damage resistance percentage for the unit.</summary>
        ColdResistance,
        /// <summary>Lightning damage resistance percentage for the unit.</summary>
        LightningResistance,
        /// <summary>Holy damage resistance percentage for the unit.</summary>
        HolyResistance,
        /// <summary>Void damage resistance percentage for the unit.</summary>
        VoidResistance,
        /// <summary>Disruption bar gain resistance percentage for the unit.</summary>
        DisruptionResistance,
    }
}
