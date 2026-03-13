using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// A compiled skill modifier, loaded from content data.
    /// Modifiers apply three ordered action groups to skill variables:
    ///   1. Set    — override/replace variable values (last modifier with a key wins).
    ///   2. Modify — additive numeric delta applied after Set (all deltas for a key are summed).
    ///   3. Add    — append typed damage components to the first effect's DamagePerHit.
    /// <para>
    /// Scalar variables (skill cost, cooldown, disruption, etc.) are stored in <see cref="Set"/>
    /// and <see cref="Modify"/> keyed by <see cref="ModifierVariable"/>.
    /// </para>
    /// <para>
    /// Elemental resistance and penetration are stored in typed dictionaries keyed by
    /// <see cref="EffectType"/> (<see cref="SetResistances"/>, <see cref="ModifyResistances"/>,
    /// <see cref="SetPenetrations"/>, <see cref="ModifyPenetrations"/>).
    /// </para>
    /// </summary>
    public record BattleModifier(
        string Id,
        string Name,
        string Description,
        /// <summary>Override scalar variable values. Applied first; last modifier with a given key wins.</summary>
        IReadOnlyDictionary<ModifierVariable, object>? Set = null,
        /// <summary>Additive scalar numeric deltas. Applied after Set. All deltas for a key are summed.</summary>
        IReadOnlyDictionary<ModifierVariable, double>? Modify = null,
        /// <summary>Elemental resistance overrides keyed by EffectType. Last modifier per type wins.</summary>
        IReadOnlyDictionary<EffectType, int>? SetResistances = null,
        /// <summary>Elemental resistance additive deltas keyed by EffectType. Summed across all modifiers.</summary>
        IReadOnlyDictionary<EffectType, double>? ModifyResistances = null,
        /// <summary>Elemental penetration overrides keyed by EffectType. Last modifier per type wins.</summary>
        IReadOnlyDictionary<EffectType, int>? SetPenetrations = null,
        /// <summary>Elemental penetration additive deltas keyed by EffectType. Summed across all modifiers.</summary>
        IReadOnlyDictionary<EffectType, double>? ModifyPenetrations = null,
        /// <summary>
        /// Damage components appended to the first effect's DamagePerHit after Set and Modify.
        /// Null when none. Only affects Attack skills, not Spells.
        /// </summary>
        IReadOnlyList<DamageComponent>? AddDamagePerHit = null
    );
}
