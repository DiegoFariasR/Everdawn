using System.Collections.Generic;
namespace GameCore.Content.Raw
{
    /// <summary>Raw unit data as parsed directly from YAML. No validation or compilation yet.</summary>
    public class RawUnit
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public int Str { get; set; }
        public int Wis { get; set; }
        public int Agi { get; set; }
        public List<string> Traits { get; set; } = new List<string>();
        public List<RawUnitSkill> Skills { get; set; } = new List<RawUnitSkill>();  // resolved by ContentPipeline
        public Dictionary<string, int> Resistances { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Base penetration values keyed by effect type name (case-insensitive).
        /// Penetration reduces the target's effective resistance when this unit attacks.
        /// </summary>
        public Dictionary<string, int> Penetrations { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Unit-level modifier IDs applied to this unit's stats (resistances, penetration, etc.).
        /// Only unit-level <see cref="GameCore.Battle.ModifierVariable"/> keys are used;
        /// skill variables (Cost, DamageMultiplier, etc.) in these modifiers are ignored.
        /// </summary>
        public List<string> Modifiers { get; set; } = new List<string>();
    }
}
