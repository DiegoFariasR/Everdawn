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
        /// <summary>
        /// Flat power applied to CC buildup bars (burn/cold) per hit, independent of damage output.
        /// 0 = no CC buildup (default). Set explicitly on skills that should build status bars.
        /// </summary>
        public int BuildupPower { get; set; } = 0;
        public List<RawDamageScaling> Scaling { get; set; } = new List<RawDamageScaling>();
    }

    /// <summary>Raw skill effect as parsed from YAML.</summary>
    public class RawEffect
    {
        public string Kind { get; set; } = "Damage";
        public string Target { get; set; } = "Enemy";
        public List<RawDamageComponent> DamagePerHit { get; set; } = new List<RawDamageComponent>();
        /// <summary>
        /// Bar key to modify when Kind is "restoreBar" (e.g. "mp", "focus", "fury").
        /// Ignored for other effect kinds.
        /// </summary>
        public string? BarKey { get; set; }
        /// <summary>
        /// Fixed amount to add to the bar when Kind is "restoreBar".
        /// Positive restores; negative drains.
        /// </summary>
        public int BarAmount { get; set; }
        // ── ApplyEffect fields ────────────────────────────────────────────────
        /// <summary>
        /// References a pre-compiled buff definition by ID (from buff-definitions.yml).
        /// Mutually exclusive with inline <see cref="EffectId"/> / <see cref="Stats"/>.
        /// When set, the duration, stacking policy, and stats all come from the buff definition.
        /// </summary>
        public string? EffectRef { get; set; }
        /// <summary>Stable ID for the active effect definition. Required when Kind is "applyEffect" with inline stats.</summary>
        public string? EffectId { get; set; }
        /// <summary>Display name shown in the UI and battle log. Defaults to EffectId when omitted.</summary>
        public string? EffectName { get; set; }
        /// <summary>Duration in qualifying turns. Used when Kind is "applyEffect".</summary>
        public int Duration { get; set; }
        /// <summary>How duration counts down: ForTargetTurns, ForSourceTurns, or UntilNextAction.</summary>
        public string DurationKind { get; set; } = "ForTargetTurns";
        /// <summary>
        /// Stat changes to apply while the effect is active.
        /// </summary>
        public RawEffectStats Stats { get; set; } = new RawEffectStats();
        /// <summary>
        /// For Kind=dispel: whether to remove a buff or a debuff from the target.
        /// Values: "Buff" or "Debuff". Required when Kind is "dispel".
        /// </summary>
        public string? DispelAlignment { get; set; }
    }

    /// <summary>
    /// Structured stat changes for an ApplyEffect active buff/debuff.
    /// Per-type entries use lists of single-key dicts, e.g. [{physical: 1.2}].
    /// Flat scalars are applied to all types (e.g. damageTakenMultiplier: 0.8).
    /// </summary>
    public class RawEffectStats
    {
        // ── Per-type dealt multiplier ─────────────────────────────────────────
        /// <summary>
        /// Per damage-type outgoing damage multipliers.
        /// Each entry is a single-key dict: [{physical: 1.2}, {elemental: 1.5}].
        /// Keywords: allTypes, elemental (fire/cold/lightning), divine (holy/void), or any EffectType name.
        /// Value is a multiplier (1.2 = +20%).
        /// </summary>
        public List<Dictionary<string, double>>? DamageDealtMultiplier { get; set; }
        // ── Per-type taken multiplier ─────────────────────────────────────────
        /// <summary>
        /// Per damage-type incoming damage multipliers.
        /// Each entry is a single-key dict: [{allTypes: 0.8}, {physical: 0.5}].
        /// Keywords: allTypes, elemental, divine, or any EffectType name.
        /// Value is a multiplier (0.8 = take 20% less).
        /// </summary>
        public List<Dictionary<string, double>>? DamageTakenMultiplier { get; set; }
        // ── Flat (all-type) healing / barrier ─────────────────────────────────
        /// <summary>Multiplier on all healing received.</summary>
        public double? ReceivingHealingMultiplier { get; set; }
        /// <summary>Multiplier on all barrier received.</summary>
        public double? ReceivingBarrierMultiplier { get; set; }
        // ── Per-type resistance / penetration ─────────────────────────────────
        /// <summary>
        /// Per damage-type resistance adjustments.
        /// Each entry is a single-key dict: [{fire: 20}, {cold: 10}].
        /// Value is a flat percentage added to the unit's resistance (20 = +20%).
        /// </summary>
        public List<Dictionary<string, int>>? Resistance { get; set; }
        /// <summary>
        /// Per damage-type penetration adjustments.
        /// Each entry is a single-key dict: [{physical: 15}].
        /// Value is a flat percentage added to the unit's penetration.
        /// </summary>
        public List<Dictionary<string, int>>? Penetration { get; set; }
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
        /// If set, the unit must be carrying one of these equipment types to use this skill.
        /// Values are <see cref="GameCore.Battle.EquipmentType"/> names (e.g. "Blunt", "Slash").
        /// </summary>
        public List<string>? RequiredEquipmentTypes { get; set; }
        /// <summary>
        /// The trigger condition for a reaction skill. Only meaningful when Category is "Reaction".
        /// Value is a <see cref="GameCore.Battle.ReactionTrigger"/> name (e.g. "OnHitBy").
        /// </summary>
        public string? Trigger { get; set; }
        /// <summary>
        /// Filter conditions for the trigger. Each entry is AND-ed with the others.
        /// Supports keys: <c>range</c> (e.g. "Melee") and <c>damageType</c> (e.g. "Physical").
        /// When empty or absent, any damaging hit fires the reaction.
        /// </summary>
        public List<RawTriggerCondition>? TriggerConditions { get; set; }
        /// <summary>
        /// <summary>Focus bar cost. Any skill with a non-zero value requires that much Focus to use.</summary>
        public int FocusCost { get; set; }
        /// <summary>When true, the turn does not advance after this skill resolves — the actor acts again.</summary>
        public bool RefundsAction { get; set; }
        /// <summary>When true, this skill can be empowered by the Focused buff.</summary>
        public bool IsFocusCompatible { get; set; }
        /// <summary>
        /// The Focus empowerment effect applied when the actor has the Focused buff.
        /// Valid values: ExtraHit, ExtraProjectile, BonusCrit, BonusStatusChance, IgnoreEvasion.
        /// </summary>
        public string? FocusEffect { get; set; }
        /// <summary>Magnitude of the Focus effect (e.g. 1 = one extra hit, 25 = +25 crit chance).</summary>
        public double FocusEffectValue { get; set; }
        /// <summary>
        /// Optional authoring description used to generate an SVG icon for this skill.
        /// Not used at runtime. Format: one short sentence describing the intended visual.
        /// </summary>
        public string? IconDescription { get; set; }
        // ── Fury system ────────────────────────────────────────────────────────
        /// <summary>
        /// When true, this skill is explicitly tagged as a STR/strength-based skill.
        /// STR-tagged skills receive Fury-based damage bonuses and grant Fury on use.
        /// </summary>
        public bool IsStrSkill { get; set; }
        /// <summary>
        /// Maximum outgoing damage bonus multiplier granted by Fury at full Fury (100).
        /// 0.0 = no bonus (default). 0.5 = up to +50% damage at max Fury.
        /// Only applied when the actor has the Fury trait and <see cref="IsStrSkill"/> is true.
        /// </summary>
        public double FuryDamageScale { get; set; }
    }

    /// <summary>
    /// One filter condition entry under a reaction skill's <c>triggerConditions</c> list.
    /// All non-null fields must match simultaneously (AND logic).
    /// </summary>
    public class RawTriggerCondition
    {
        /// <summary>If set, the incoming skill must have this range (e.g. "Melee", "Ranged").</summary>
        public string? Range { get; set; }
        /// <summary>If set, the incoming skill must include a damage component of this type (e.g. "Physical", "Fire").</summary>
        public string? DamageType { get; set; }
    }
}
