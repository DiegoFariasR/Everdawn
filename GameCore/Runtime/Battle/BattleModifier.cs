#nullable enable
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
        IReadOnlyList<DamageComponent>? AddDamagePerHit = null,
        /// <summary>
        /// Tag names of modifier categories that cannot share the same skill slot with this modifier.
        /// The content pipeline throws <see cref="System.InvalidOperationException"/> if a skill slot
        /// contains this modifier and any other modifier carrying one of these tags.
        /// References tag names (see <see cref="Tags"/>), not modifier IDs.
        /// </summary>
        IReadOnlyList<string>? ExclusiveWith = null,
        /// <summary>
        /// Category tags for this modifier (e.g. "basic", "ultimate", "reaction").
        /// Used by exclusiveWith checks and unit-level tag count validation.
        /// </summary>
        IReadOnlyList<string>? Tags = null,
        /// <summary>
        /// If set, overrides the compiled skill's <see cref="SkillCategory"/> to this value.
        /// Use <see cref="SkillCategory.Reaction"/> to mark the base skill as a reaction slot.
        /// </summary>
        SkillCategory? SetCategory = null,
        /// <summary>
        /// Trigger that causes the base skill to fire as a reaction when this modifier is applied.
        /// Only meaningful when <see cref="Tags"/> contains "reaction".
        /// </summary>
        ReactionTrigger? Trigger = null,
        /// <summary>
        /// Filter conditions for the reaction trigger. All conditions are AND-ed.
        /// Null or empty = any damaging hit fires the reaction.
        /// </summary>
        IReadOnlyList<TriggerCondition>? TriggerConditions = null
    );
}
