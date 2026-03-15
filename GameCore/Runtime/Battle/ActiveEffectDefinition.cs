#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    // Static content-time template; at runtime becomes an ActiveEffectInstance per unit.
    public record ActiveEffectDefinition(
        string Id,
        string Name,
        EffectDurationKind DurationKind,
        int Duration,
        EffectStackingPolicy StackingPolicy = EffectStackingPolicy.RefreshDuration,
        RuntimeSkillModifier? SkillModifier = null,              // temporarily adjusts outgoing skill behavior
        IReadOnlyList<RuntimeStatModifier>? StatModifiers = null, // outgoing/incoming damage and disruption resistance
        IReadOnlyDictionary<EffectType, double>? DamageDealtMultiplierByType = null,  // per-type outgoing damage multipliers
        IReadOnlyDictionary<EffectType, double>? DamageTakenMultiplierByType = null,  // per-type incoming damage multipliers
        IReadOnlyDictionary<EffectType, int>? ResistanceModifierByType = null,        // additive flat resistance per type
        IReadOnlyDictionary<EffectType, int>? PenetrationModifierByType = null,       // additive flat penetration per type
        EffectAlignment Alignment = EffectAlignment.Buff         // determines dispel eligibility
    );
}
