#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
    public enum BattleSkillTarget { Enemy, Ally }

    /// <summary>Defines what Focus empowerment does to a skill when the actor has the Focused buff.</summary>
    public enum FocusEffectKind
    {
        /// <summary>Adds <see cref="BattleSkill.FocusEffectValue"/> extra hits to the skill's effective hit count.</summary>
        ExtraHit,
        /// <summary>Adds extra projectiles to a spell (mechanically identical to ExtraHit, distinct flavor).</summary>
        ExtraProjectile,
        /// <summary>Grants a bonus crit chance for this skill use. Reserved — requires a future crit system.</summary>
        BonusCrit,
        /// <summary>Improves the chance that CC or status effects from this skill apply. Reserved — not yet implemented.</summary>
        BonusStatusChance,
        /// <summary>Ignores a portion of the target's evasion. Reserved — requires a future evasion system.</summary>
        IgnoreEvasion,
    }

    /// <summary>One stat-to-damage scaling entry within a damage component.</summary>
    public record DamageScaling(string Stat, double Scale = 1.0);

    /// <summary>
    /// One damage type component within a hit.
    /// <see cref="DamageType"/> is null for heal components (no resistance applied).
    /// </summary>
    public record DamageComponent(
        EffectType? DamageType,
        IReadOnlyList<DamageScaling> Scaling,
        /// <summary>
        /// Flat power applied to CC buildup bars (burn/cold) per hit, independent of damage output.
        /// 0 = no CC buildup (default). Set explicitly on skills that should build status bars.
        /// </summary>
        int BuildupPower = 0);

    /// <summary>
    /// One effect produced by a skill: its kind (damage/heal), intended target side, and per-hit formula.
    /// </summary>
    public record SkillEffect(
        EffectKind Kind,
        BattleSkillTarget Target,
        IReadOnlyList<DamageComponent> DamagePerHit,
        /// <summary>
        /// Disruption power applied per hit to the target's disruption bar.
        /// 0 = no disruption (default). Set explicitly on blunt/impact and lightning skills that should stagger.
        /// </summary>
        int DisruptionPower = 0,
        /// <summary>
        /// The bar key to modify when <see cref="Kind"/> is <see cref="EffectKind.RestoreBar"/>.
        /// Matches keys used in <see cref="BattleUnit.MaxBars"/> (e.g. "mp", "focus", "fury").
        /// Ignored for other effect kinds.
        /// </summary>
        string? BarKey = null,
        /// <summary>
        /// Fixed amount to add to <see cref="BarKey"/> when <see cref="Kind"/> is <see cref="EffectKind.RestoreBar"/>.
        /// Positive values restore; negative values drain.
        /// </summary>
        int BarAmount = 0,
        /// <summary>
        /// The active effect to apply when <see cref="Kind"/> is <see cref="EffectKind.ApplyEffect"/>.
        /// Applied once per target, not per hit. Null for all other effect kinds.
        /// </summary>
        ActiveEffectDefinition? EffectDefinition = null,
        /// <summary>
        /// Whether a <see cref="EffectKind.Dispel"/> effect removes buffs or debuffs.
        /// Ignored for all other effect kinds.
        /// </summary>
        EffectAlignment? DispelAlignment = null
    );

    /// <summary>
    /// A skill that a BattleUnit can use in combat.
    /// </summary>
    public record BattleSkill(
        string Id,
        string Name,
        int Cost,
        /// <summary>Modifier hook: multiplies the entire skill output. Default 1.0 = identity.</summary>
        double DamageMultiplier,
        IReadOnlyList<SkillEffect> Effects,
        bool IsAoe = false,
        /// <summary>Turns this unit must wait before using this skill again. 0 = no cooldown.</summary>
        int Cooldown = 0,
        /// <summary>Cooldown this skill starts with at the beginning of battle (explicit override).</summary>
        int InitialCooldown = 0,
        /// <summary>Optional modifiers that change how this skill behaves.</summary>
        IReadOnlyList<string>? Modifiers = null,
        /// <summary>
        /// All category tags collected from this skill's applied modifiers (e.g. "basic", "ultimate", "reaction").
        /// Populated during unit compilation when modifiers are applied. Null when no modifiers are applied.
        /// Used for tag-based skill properties (<see cref="IsBasic"/>, <see cref="IsUltimate"/>) and
        /// unit-level tag count validation.
        /// </summary>
        IReadOnlyList<string>? ModifierTags = null,
        /// <summary>
        /// Base hit count for this skill. Floor(n) hits, each dealing DamageMultiplier × (n / floor(n)) of base.
        /// Total damage = DamageMultiplier × BaseHits × base. When 1.0 (default), the unit's HitCount applies instead.
        /// </summary>
        double BaseHits = 1.0,
        /// <summary>
        /// Optional stat-based bonus to hit count. Effective hits = floor(BaseHits + Σ(stat × scale)), min 1.
        /// When empty and BaseHits == 1.0, the unit's HitCount (AGI-derived) is used instead.
        /// </summary>
        IReadOnlyList<DamageScaling>? ScalingHits = null,
        /// <summary>How the skill is delivered to its target.</summary>
        SkillRange Range = SkillRange.Melee,
        /// <summary>Ability category used for silencing and UI tagging.</summary>
        SkillCategory Category = SkillCategory.Attack,
        /// <summary>
        /// Elemental penetration percentages granted to the unit while this passive skill is equipped.
        /// Null when the skill grants no passive penetration bonuses.
        /// Only meaningful when Category is Passive.
        /// </summary>
        IReadOnlyDictionary<EffectType, int>? PassivePenetrations = null,
        /// <summary>
        /// Disruption penetration percentage granted to the unit while this passive skill is equipped.
        /// Only meaningful when Category is Passive.
        /// </summary>
        int PassiveDisruptionPenetration = 0,
        /// <summary>
        /// Elemental resistance percentages granted to the unit while this passive skill is equipped.
        /// Null when the skill grants no passive elemental resistance bonuses.
        /// Only meaningful when Category is Passive.
        /// </summary>
        IReadOnlyDictionary<EffectType, int>? PassiveResistances = null,
        /// <summary>
        /// Disruption resistance percentage granted to the unit while this passive skill is equipped.
        /// Only meaningful when Category is Passive.
        /// </summary>
        int PassiveDisruptionResistance = 0,
        /// <summary>
        /// If set, the unit must have at least one of these traits to use this skill.
        /// For example, Spell skills may require <see cref="BattleTrait.ManaUser"/>.
        /// </summary>
        IReadOnlyList<BattleTrait>? PermittedTraits = null,
        /// <summary>
        /// If set, the unit must be carrying one of these equipment types to use this skill.
        /// For example, a mace skill requires <see cref="EquipmentType.Blunt"/>.
        /// </summary>
        IReadOnlyList<EquipmentType>? PermittedEquipmentTypes = null,
        /// <summary>
        /// The trigger condition that causes this reaction skill to fire automatically.
        /// Null for non-reaction skills.
        /// Only meaningful when <see cref="Category"/> is <see cref="SkillCategory.Reaction"/>.
        /// </summary>
        ReactionTrigger? Trigger = null,
        /// <summary>
        /// Filter conditions that all must match for the trigger to fire.
        /// Only meaningful when <see cref="Trigger"/> is <see cref="ReactionTrigger.OnHitBy"/>.
        /// When null or empty, any damaging hit will fire the reaction.
        /// Multiple conditions are AND-ed: all must match simultaneously.
        /// </summary>
        IReadOnlyList<TriggerCondition>? TriggerConditions = null,
        /// <summary>
        /// When true, this skill is explicitly tagged as a STR/strength-based skill.
        /// STR-tagged skills receive Fury-based damage bonuses when the actor has the Fury trait,
        /// and using them grants Fury to the actor.
        /// Must be set explicitly in data — never inferred from scaling or damage type.
        /// </summary>
        bool IsStrSkill = false,
        /// <summary>
        /// Maximum outgoing damage bonus granted by Fury at full Fury (100).
        /// At current Fury <c>f</c>, the multiplier bonus = FuryDamageScale × (f / 100).
        /// 0.0 = no Fury damage bonus (default). 0.5 = up to +50% damage at max Fury.
        /// Only applied when the actor has the Fury trait and <see cref="IsStrSkill"/> is true.
        /// </summary>
        double FuryDamageScale = 0.0,
        /// <summary>
        /// When true, using this skill spends <see cref="FocusCost"/> Focus from the actor's Focus bar.
        /// Any skill with a non-zero FocusCost is unavailable when the actor's Focus bar is below that cost.
        /// </summary>
        int FocusCost = 0,
        /// <summary>
        /// When true, the turn order does not advance after this skill resolves — the actor acts again immediately.
        /// Used by the Focus skill to grant a free follow-up action.
        /// </summary>
        bool RefundsAction = false,
        /// <summary>
        /// When true, the Focused buff (if active on the actor) is consumed and <see cref="FocusEffect"/> is applied.
        /// If the actor does not have Focused, the skill executes normally with no change.
        /// </summary>
        bool IsFocusCompatible = false,
        /// <summary>The empowerment kind applied when <see cref="IsFocusCompatible"/> is true and Focused is active.</summary>
        FocusEffectKind? FocusEffect = null,
        /// <summary>
        /// Magnitude of <see cref="FocusEffect"/> (e.g. 1 = one extra hit/projectile, 25 = +25% crit chance).
        /// Ignored when <see cref="FocusEffect"/> is null.
        /// </summary>
        double FocusEffectValue = 0.0
    )
    {
        /// <summary>Returns true if this skill carries the given modifier (case-insensitive).</summary>
        public bool HasModifier(string id) =>
            Modifiers?.Any(m => string.Equals(m, id, StringComparison.OrdinalIgnoreCase)) ?? false;

        /// <summary>
        /// Returns true if the given unit meets all requirements to use this skill.
        /// A unit must have at least one of the permitted traits (if any) and one of the permitted equipment types (if any).
        /// Both groups are independent OR gates; if both are set, the unit must satisfy each independently.
        /// </summary>
        public bool MeetsRequirements(BattleUnit actor)
        {
            if (PermittedTraits != null && PermittedTraits.Count > 0 && !PermittedTraits.Any(t => actor.HasTrait(t)))
                return false;
            if (PermittedEquipmentTypes != null && !PermittedEquipmentTypes.Contains(actor.EquipmentType))
                return false;
            return true;
        }

        /// <summary>True if this skill heals rather than damages.</summary>
        public bool IsHeal => Effects.Count > 0 && Effects[0].Kind == EffectKind.Heal;

        /// <summary>True if this skill grants a barrier (shield) rather than dealing damage.</summary>
        public bool IsShield => Effects.Count > 0 && Effects[0].Kind == EffectKind.Shield;

        /// <summary>True if this skill restores or drains a secondary bar (MP, Focus, Fury, …).</summary>
        public bool IsRestoreBar => Effects.Count > 0 && Effects[0].Kind == EffectKind.RestoreBar;

        /// <summary>True if this skill applies an active buff or debuff to its target.</summary>
        public bool IsApplyEffect => Effects.Count > 0 && Effects[0].Kind == EffectKind.ApplyEffect;

        /// <summary>True if this skill grants the Focused buff (Self-targeting setup action, no damage).</summary>
        public bool IsGrantFocusedBuff => Effects.Count > 0 && Effects[0].Kind == EffectKind.GrantFocusedBuff;

        /// <summary>True if this skill dispels a buff or debuff from its target.</summary>
        public bool IsDispel => Effects.Count > 0 && Effects[0].Kind == EffectKind.Dispel;

        /// <summary>Target side of the first effect. Defaults to Enemy if Effects is empty.</summary>
        public BattleSkillTarget Target => Effects.Count > 0 ? Effects[0].Target : BattleSkillTarget.Enemy;

        /// <summary>Primary damage type — first damage component's type. Null for heals.</summary>
        public EffectType? PrimaryEffectType =>
            Effects.Count > 0 && Effects[0].DamagePerHit.Count > 0
                ? Effects[0].DamagePerHit[0].DamageType
                : null;

        /// <summary>True if any of this skill's modifier tags is "basic".</summary>
        public bool IsBasic => ModifierTags?.Any(t => string.Equals(t, "basic", StringComparison.OrdinalIgnoreCase)) ?? false;

        /// <summary>True if any of this skill's modifier tags is "ultimate".</summary>
        public bool IsUltimate => ModifierTags?.Any(t => string.Equals(t, "ultimate", StringComparison.OrdinalIgnoreCase)) ?? false;

        /// <summary>True if this skill is a reaction (fires automatically on a trigger, never chosen as an action).</summary>
        public bool IsReaction => Category == SkillCategory.Reaction;

        /// <summary>
        /// The cooldown this skill enters battle with.
        /// Ultimate skills automatically start with at least 1 round of cooldown.
        /// </summary>
        public int EffectiveInitialCooldown => IsUltimate ? Math.Max(1, InitialCooldown) : InitialCooldown;

        /// <summary>
        /// Estimates total base damage for this skill from the given actor's stats.
        /// Sums all scaling entries in the first effect, then applies DamageMultiplier and NumberOfHits.
        /// </summary>
        public int EstimateBaseDmg(BattleUnit actor)
        {
            if (Effects.Count == 0) return 0;
            double total = 0;
            foreach (var comp in Effects[0].DamagePerHit)
                foreach (var s in comp.Scaling)
                    total += actor.GetStat(s.Stat) * s.Scale;
            double hits = BaseHits;
            if (ScalingHits != null)
                foreach (var s in ScalingHits)
                    hits += actor.GetStat(s.Stat) * s.Scale;
            hits = Math.Max(0.5, hits);
            return (int)(total * DamageMultiplier * hits);
        }
    }
}
