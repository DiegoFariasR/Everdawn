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
    }
}
