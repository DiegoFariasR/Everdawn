#nullable enable
using System.Collections.Generic;
namespace GameCore.Content.Raw
{
    /// <summary>Raw stat-to-damage scaling entry as parsed from YAML.</summary>
    public class RawDamageScaling
    {
        public string Stat { get; set; } = "str";
        /// <summary>Damage coefficient for this stat. Defaults to 1.0; can be omitted from YAML.</summary>
        public double Scale { get; set; } = 1.0;
    }

    /// <summary>
    /// Raw damage component within a hit — one damage type with one or more stat contributions.
    /// <see cref="DamageType"/> is null for heal components (no resistance applied).
    /// </summary>
    public class RawDamageComponent
    {
        public string? DamageType { get; set; }
        public List<RawDamageScaling> Scaling { get; set; } = new List<RawDamageScaling>();
    }

    /// <summary>Raw skill effect as parsed from YAML.</summary>
    public class RawEffect
    {
        public string Kind { get; set; } = "Damage";
        public string Target { get; set; } = "Enemy";
        public List<RawDamageComponent> DamagePerHit { get; set; } = new List<RawDamageComponent>();
    }

    /// <summary>Raw skill data as parsed directly from YAML. No validation or compilation yet.</summary>
    public class RawSkill
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Cost { get; set; }
        /// <summary>Modifier hook: multiplies overall skill output. Default 1.0 = identity.</summary>
        public double DamageMultiplier { get; set; } = 1.0;
        /// <summary>
        /// Base hit count for this skill. Floor(n) hits, each dealing DamageMultiplier × (n / floor(n)) of base damage.
        /// Total damage = DamageMultiplier × baseHits × base. Defaults to 1.0 (single hit, no split).
        /// </summary>
        public double BaseHits { get; set; } = 1.0;
        /// <summary>
        /// Optional stat-based bonus added to BaseHits at runtime.
        /// Effective hits = floor(BaseHits + Σ(actor.GetStat(stat) × scale)), min 1.
        /// </summary>
        public List<RawDamageScaling> ScalingHits { get; set; } = new List<RawDamageScaling>();
        /// <summary>How the skill is delivered: Melee, Ranged, or Self. Defaults to Melee.</summary>
        public string Range { get; set; } = "Melee";
        /// <summary>Ability category: Attack, Spell (can be silenced), or Passive. Defaults to Attack.</summary>
        public string Category { get; set; } = "Attack";
        public bool IsAoe { get; set; }
        public int Cooldown { get; set; }
        public int InitialCooldown { get; set; }
        public List<RawEffect> Effects { get; set; } = new List<RawEffect>();
        /// <summary>
        /// Passive penetration bonuses granted to the unit while this passive skill is equipped.
        /// Keys are damage type names (e.g. "physical", "fire") or "disruption".
        /// Only applied when Category is Passive.
        /// </summary>
        public Dictionary<string, int> Penetration { get; set; } = new Dictionary<string, int>();
        /// <summary>
        /// Passive resistance bonuses granted to the unit while this passive skill is equipped.
        /// Keys are damage type names (e.g. "cold") or "disruption".
        /// Only applied when Category is Passive.
        /// </summary>
        public Dictionary<string, int> Resistance { get; set; } = new Dictionary<string, int>();
        /// <summary>
        /// If set, the unit must have this trait to use this skill.
        /// Value is a <see cref="GameCore.Battle.BattleTrait"/> name (e.g. "MagicUser").
        /// </summary>
        public string? RequiredTrait { get; set; }
        /// <summary>
        /// If set, the unit must be equipped with this weapon type to use this skill.
        /// Value is a <see cref="GameCore.Battle.WeaponType"/> name (e.g. "Blunt", "Slash").
        /// </summary>
        public string? RequiredWeaponType { get; set; }
    }
}
