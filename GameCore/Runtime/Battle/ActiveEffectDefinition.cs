#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// A reusable template for a runtime active effect (buff or debuff).
    /// <para>
    /// Definitions are the static, content-time description of an effect.
    /// When an effect is applied to a unit, an <see cref="ActiveEffectInstance"/> is created
    /// from this definition and tracked at runtime.
    /// </para>
    /// <para>
    /// Content wiring (applying effects via skill YAML data) is deferred.
    /// Effects can be applied programmatically via <see cref="IBattleEngine.ApplyActiveEffect"/>.
    /// </para>
    /// </summary>
    public record ActiveEffectDefinition(
        /// <summary>Stable string identifier for this effect type (e.g. "attackUp", "guard").</summary>
        string Id,

        /// <summary>Human-readable name shown in the UI and battle log.</summary>
        string Name,

        /// <summary>How this effect's remaining duration counts down.</summary>
        EffectDurationKind DurationKind,

        /// <summary>Initial duration (in qualifying turns) when first applied.</summary>
        int Duration,

        /// <summary>Controls behaviour when the same definition is applied to a unit that already has it active.</summary>
        EffectStackingPolicy StackingPolicy = EffectStackingPolicy.RefreshDuration,

        /// <summary>
        /// Optional skill modifier applied when this effect's target unit acts.
        /// Temporarily adjusts outgoing skill behaviour without permanently modifying any skill.
        /// </summary>
        RuntimeSkillModifier? SkillModifier = null,

        /// <summary>
        /// Stat modifiers applied while this effect is active on the target unit.
        /// Affects outgoing damage, incoming damage, and disruption resistance.
        /// </summary>
        IReadOnlyList<RuntimeStatModifier>? StatModifiers = null,

        /// <summary>
        /// Per-damage-type outgoing damage multipliers. Each entry multiplies the actor's damage
        /// for that type (e.g. Physical → 1.2 for +20% physical damage dealt).
        /// </summary>
        IReadOnlyDictionary<EffectType, double>? DamageDealtMultiplierByType = null,

        /// <summary>
        /// Per-damage-type incoming damage multipliers applied to the target
        /// (e.g. allTypes → 0.8 for taking 20% less damage of any type).
        /// </summary>
        IReadOnlyDictionary<EffectType, double>? DamageTakenMultiplierByType = null,

        /// <summary>
        /// Per-damage-type flat resistance modifier (additive %). Positive = more resistance.
        /// </summary>
        IReadOnlyDictionary<EffectType, int>? ResistanceModifierByType = null,

        /// <summary>
        /// Per-damage-type flat penetration modifier (additive %). Positive = pierces more resistance.
        /// </summary>
        IReadOnlyDictionary<EffectType, int>? PenetrationModifierByType = null,

        /// <summary>
        /// Whether this effect is a buff (beneficial) or a debuff (harmful).
        /// Determines eligibility for dispel skills targeting buffs or debuffs.
        /// </summary>
        EffectAlignment Alignment = EffectAlignment.Buff
    );
}
