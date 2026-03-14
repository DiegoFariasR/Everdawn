using System.Collections.Generic;
namespace GameCore.Content.Raw
{
    /// <summary>
    /// Container for typed Add actions supported by a modifier.
    /// Currently supports appending damage components to the first effect's DamagePerHit list.
    /// </summary>
    public class RawModifierAdd
    {
        /// <summary>
        /// Damage components appended to the first effect's DamagePerHit list.
        /// Applied after all Set and Modify operations. Only affects Attack skills, not Spells.
        /// </summary>
        public List<RawDamageComponent> DamagePerHit { get; set; } = new List<RawDamageComponent>();
    }

    /// <summary>
    /// Typed override values for a modifier's Set action group.
    /// Scalar skill variables are explicit nullable fields; elemental resistance and penetration
    /// are expressed as string-keyed dictionaries (keys match <see cref="GameCore.Battle.EffectType"/>
    /// names, case-insensitive). Crowd-control resistance/penetration types (e.g. "disruption")
    /// also go in those same dicts and are routed to their runtime variables by the pipeline.
    /// </summary>
    public class RawModifierSet
    {
        public int? Cost { get; set; }
        public double? DamageMultiplier { get; set; }
        public bool? IsAoe { get; set; }
        public int? Cooldown { get; set; }
        public int? InitialCooldown { get; set; }
        public double? ExtraHits { get; set; }
        /// <summary>
        /// If set, overrides the skill's <c>category</c> to this value (e.g. "Reaction").
        /// Value is a <see cref="GameCore.Battle.SkillCategory"/> name, case-insensitive.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Resistance overrides keyed by type name (e.g. "physical", "fire", "disruption").
        /// Elemental keys map to <see cref="GameCore.Battle.EffectType"/>; CC keys like
        /// "disruption" are routed to their dedicated runtime variable by the pipeline.
        /// Last modifier wins per type. 0 = none, 50 = half damage, 100 = immune, negative = weakness.
        /// </summary>
        public Dictionary<string, int> Resistance { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Penetration overrides keyed by type name (e.g. "physical", "fire", "disruption").
        /// Elemental keys map to <see cref="GameCore.Battle.EffectType"/>; CC keys like
        /// "disruption" are routed to their dedicated runtime variable by the pipeline.
        /// Last modifier wins per type. Subtracted from the target's effective resistance.
        /// </summary>
        public Dictionary<string, int> Penetration { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Typed additive deltas for a modifier's Modify action group.
    /// Scalar skill variables are explicit nullable fields; elemental resistance and penetration
    /// are expressed as string-keyed dictionaries so they can be authored as a single typed block.
    /// CC types like "disruption" also go in those dicts — routed by the pipeline.
    /// All deltas for the same key are summed across all modifiers.
    /// </summary>
    public class RawModifierModify
    {
        public double? Cost { get; set; }
        public double? DamageMultiplier { get; set; }
        public double? Cooldown { get; set; }
        public double? InitialCooldown { get; set; }
        public double? ExtraHits { get; set; }

        /// <summary>Resistance deltas keyed by type name (elemental or CC, e.g. "disruption").</summary>
        public Dictionary<string, double> Resistance { get; set; } = new Dictionary<string, double>();

        /// <summary>Penetration deltas keyed by type name (elemental or CC, e.g. "disruption").</summary>
        public Dictionary<string, double> Penetration { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Raw modifier data as parsed directly from YAML.
    /// A modifier applies three ordered action groups:
    ///   1. Set    — override/replace variable values (last modifier with a key wins).
    ///   2. Modify — additive numeric delta applied after Set (all deltas for a key are summed).
    ///   3. Add    — append typed damage components to the first effect's DamagePerHit.
    /// </summary>
    public class RawModifier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public RawModifierSet Set { get; set; } = new RawModifierSet();
        public RawModifierModify Modify { get; set; } = new RawModifierModify();
        public RawModifierAdd Add { get; set; } = new RawModifierAdd();

        /// <summary>
        /// Tags of modifier categories that cannot share the same skill slot with this modifier.
        /// If a skill slot includes this modifier and any other modifier carrying one of the listed
        /// tags, the content pipeline throws. References tag names, not modifier IDs.
        /// </summary>
        public List<string> ExclusiveWith { get; set; } = new List<string>();

        /// <summary>
        /// Category tags for this modifier (e.g. "basic", "ultimate", "reaction").
        /// Used by exclusiveWith checks and unit-level tag count validation.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// The trigger that causes the base skill to fire as a reaction when this modifier is applied.
        /// Value is a <see cref="GameCore.Battle.ReactionTrigger"/> name (e.g. "OnHitBy").
        /// Only meaningful when <see cref="Tags"/> contains "reaction".
        /// </summary>
        public string? Trigger { get; set; }

        /// <summary>
        /// Filter conditions for the reaction trigger. Each entry is AND-ed.
        /// Supports keys: <c>range</c> (e.g. "Melee") and <c>damageType</c> (e.g. "Physical").
        /// When absent or empty, any damaging hit fires the reaction.
        /// </summary>
        public List<RawTriggerCondition>? TriggerConditions { get; set; }
    }
}
