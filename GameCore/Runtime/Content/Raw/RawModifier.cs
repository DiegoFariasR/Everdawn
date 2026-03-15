using System.Collections.Generic;
// Modifier applies three ordered action groups:
//   1. Set    — override values (last modifier per key wins).
//   2. Modify — additive numeric delta (all deltas for a key are summed).
//   3. Add    — append damage components to the first effect's DamagePerHit.
namespace GameCore.Content.Raw
{
    public class RawModifierAdd
    {
        public List<RawDamageComponent> DamagePerHit { get; set; } = new List<RawDamageComponent>();
    }

    // Scalar skill variables are explicit nullable fields.
    // Resistance/penetration are string-keyed dicts ("physical", "disruption", etc.).
    public class RawModifierSet
    {
        public int? Cost { get; set; }
        public double? DamageMultiplier { get; set; }
        public bool? IsAoe { get; set; }
        public int? Cooldown { get; set; }
        public int? InitialCooldown { get; set; }
        public double? ExtraHits { get; set; }
        public string? Category { get; set; } // overrides SkillCategory (e.g. "Reaction")
        // 0=none, 50=half damage, 100=immune, negative=weakness; CC keys ("disruption") also valid
        public Dictionary<string, int> Resistance { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Penetration { get; set; } = new Dictionary<string, int>();
    }

    // All deltas for the same key are summed across all modifiers.
    public class RawModifierModify
    {
        public double? Cost { get; set; }
        public double? DamageMultiplier { get; set; }
        public double? Cooldown { get; set; }
        public double? InitialCooldown { get; set; }
        public double? ExtraHits { get; set; }
        public Dictionary<string, double> Resistance { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> Penetration { get; set; } = new Dictionary<string, double>();
    }

    public class RawModifier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public RawModifierSet Set { get; set; } = new RawModifierSet();
        public RawModifierModify Modify { get; set; } = new RawModifierModify();
        public RawModifierAdd Add { get; set; } = new RawModifierAdd();
        public List<string> ExclusiveWith { get; set; } = new List<string>(); // tag names that cannot share this slot
        public List<string> Tags { get; set; } = new List<string>();           // e.g. "basic", "ultimate", "reaction"
        public string? Trigger { get; set; }                                   // ReactionTrigger name (e.g. "OnHitBy")
        public List<RawTriggerCondition>? TriggerConditions { get; set; }      // null/empty = any hit fires
    }
}
