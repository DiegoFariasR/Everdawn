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
        /// <summary>
        /// The equipment type this unit is carrying.
        /// Value is a <see cref="GameCore.Battle.EquipmentType"/> name (e.g. "Blunt", "Bow").
        /// Defaults to "None" when not specified.
        /// </summary>
        public string EquipmentType { get; set; } = "None";
        public List<RawUnitSkill> Skills { get; set; } = new List<RawUnitSkill>();  // resolved by ContentPipeline
        public Dictionary<string, int> Resistances { get; set; } = new Dictionary<string, int>();
        public double FuryDamageScale { get; set; }  // unit-level scaling; applied to all damaging skills when actor has FuryUser trait

        /// <summary>
        /// Unit-level modifier IDs applied to this unit's resistances.
        /// Only resistance-related <see cref="GameCore.Battle.ModifierVariable"/> keys are used;
        /// skill variables (Cost, DamageMultiplier, etc.) in these modifiers are ignored.
        /// </summary>
        public List<string> Modifiers { get; set; } = new List<string>();
        // Reaction skill is auto-detected from the unit's skill list:
        // any skill that is tagged with the "reaction" modifier is placed in
        // BattleUnit.ReactionSkill and excluded from BattleUnit.Skills.
    }
}
