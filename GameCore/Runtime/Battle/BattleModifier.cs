#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    // Three ordered action groups applied to skill variables:
    //   1. Set    — override values (last modifier per key wins).
    //   2. Modify — additive numeric delta (all deltas for a key are summed).
    //   3. Add    — append damage components to the first effect's DamagePerHit.
    public record BattleModifier(
        string Id,
        string Name,
        string Description,
        IReadOnlyDictionary<ModifierVariable, object>? Set = null,                 // scalar overrides; applied first
        IReadOnlyDictionary<ModifierVariable, double>? Modify = null,              // scalar additive deltas; applied after Set
        IReadOnlyDictionary<EffectType, int>? SetResistances = null,              // elemental resistance overrides
        IReadOnlyDictionary<EffectType, double>? ModifyResistances = null,        // elemental resistance additive deltas
        IReadOnlyDictionary<EffectType, int>? SetPenetrations = null,             // elemental penetration overrides
        IReadOnlyDictionary<EffectType, double>? ModifyPenetrations = null,       // elemental penetration additive deltas
        IReadOnlyList<DamageComponent>? AddDamagePerHit = null,                   // appended to first effect's DamagePerHit; Attack only
        IReadOnlyList<string>? ExclusiveWith = null,  // tag names that cannot share a skill slot with this modifier
        IReadOnlyList<string>? Tags = null,           // category tags (e.g. "basic", "ultimate", "reaction")
        SkillCategory? SetCategory = null,            // overrides SkillCategory when set
        ReactionTrigger? Trigger = null,
        IReadOnlyList<TriggerCondition>? TriggerConditions = null  // null/empty = any damaging hit fires the reaction
    );
}
