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
        /// Affects outgoing damage, incoming damage, elemental resistances, and disruption resistance.
        /// </summary>
        IReadOnlyList<RuntimeStatModifier>? StatModifiers = null
    );
}
