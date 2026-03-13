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
    /// Disruption variables (<see cref="DisruptionResistance"/>, <see cref="DisruptionPenetration"/>)
    /// are applied when a modifier appears in a unit's modifier list and adjust disruption-bar mechanics.
    /// Elemental resistance and penetration (Physical, Fire, Cold, Lightning, Holy, Void) are expressed
    /// as typed dictionaries keyed by <see cref="EffectType"/> — see <see cref="BattleModifier"/>.
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

        // ── Disruption variables ─────────────────────────────────────────────

        /// <summary>
        /// Disruption bar gain resistance percentage for the unit.
        /// 0 = no mitigation, 50 = half buildup, 100 = immune, negative = weakness.
        /// Kept separate from elemental resistance because disruption is a bar mechanic, not a damage type.
        /// </summary>
        DisruptionResistance,

        /// <summary>
        /// Disruption resistance penetration percentage. Reduces the target's effective disruption
        /// resistance when this unit applies disruption.
        /// Kept separate from elemental penetration because disruption is a bar mechanic, not a damage type.
        /// </summary>
        DisruptionPenetration,
    }
}
