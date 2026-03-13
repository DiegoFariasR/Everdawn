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
    /// <para>
    /// Penetration variables (<see cref="PhysicalPenetration"/> through <see cref="DisruptionPenetration"/>)
    /// are applied when a modifier appears in a unit's modifier list and reduce the effective resistance
    /// of the target when this unit deals damage of that type. Penetration is subtracted from the
    /// target's resistance before the damage formula is applied.
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

        // ── Unit penetration variables ───────────────────────────────────────

        /// <summary>Physical resistance penetration percentage. Reduces the target's effective physical resistance when this unit deals physical damage.</summary>
        PhysicalPenetration,
        /// <summary>Fire resistance penetration percentage. Reduces the target's effective fire resistance when this unit deals fire damage.</summary>
        FirePenetration,
        /// <summary>Cold resistance penetration percentage. Reduces the target's effective cold resistance when this unit deals cold damage.</summary>
        ColdPenetration,
        /// <summary>Lightning resistance penetration percentage. Reduces the target's effective lightning resistance when this unit deals lightning damage.</summary>
        LightningPenetration,
        /// <summary>Holy resistance penetration percentage. Reduces the target's effective holy resistance when this unit deals holy damage.</summary>
        HolyPenetration,
        /// <summary>Void resistance penetration percentage. Reduces the target's effective void resistance when this unit deals void damage.</summary>
        VoidPenetration,
        /// <summary>Disruption resistance penetration percentage. Reduces the target's effective disruption resistance when this unit applies disruption.</summary>
        DisruptionPenetration,
    }
}
