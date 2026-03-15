#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
    public enum BattleSkillTarget { Enemy, Ally }

    // What Focus empowerment does to a skill when the actor has the Focused buff.
    public enum FocusEffectKind
    {
        ExtraHit,
        ExtraProjectile,
        BonusCrit,          // reserved — requires a future crit system
        BonusStatusChance,  // reserved — not yet implemented
        IgnoreEvasion,      // reserved — requires a future evasion system
    }

    public record DamageScaling(string Stat, double Scale = 1.0);

    public record DamageComponent(
        EffectType? DamageType,
        IReadOnlyList<DamageScaling> Scaling,
        int BuildupPower = 0); // flat CC bar power per hit, independent of damage

    public record SkillEffect(
        EffectKind Kind,
        BattleSkillTarget Target,
        IReadOnlyList<DamageComponent> DamagePerHit,
        int DisruptionPower = 0,               // per-hit disruption bar power; 0 = no disruption
        string? BarKey = null,                 // bar to modify when Kind=RestoreBar (e.g. "mp", "focus")
        int BarAmount = 0,                     // amount to add (positive = restore, negative = drain)
        ActiveEffectDefinition? EffectDefinition = null,  // applied once per target when Kind=ApplyEffect
        EffectAlignment? DispelAlignment = null            // buff or debuff target when Kind=Dispel
    );

    public record BattleSkill(
        string Id,
        string Name,
        int Cost,
        double DamageMultiplier,               // multiplies entire skill output; 1.0 = identity
        IReadOnlyList<SkillEffect> Effects,
        bool IsAoe = false,
        int Cooldown = 0,                      // turns to wait before reuse
        int InitialCooldown = 0,               // cooldown the skill starts battle with
        IReadOnlyList<string>? Modifiers = null,
        IReadOnlyList<string>? ModifierTags = null,  // tags from applied modifiers (e.g. "basic", "ultimate")
        double BaseHits = 1.0,                 // floor(n) hits at DamageMultiplier×(n/floor(n)); at 1.0 uses AGI HitCount
        IReadOnlyList<DamageScaling>? ScalingHits = null,  // stat-based bonus added to BaseHits
        SkillRange Range = SkillRange.Melee,
        SkillCategory Category = SkillCategory.Attack,
        IReadOnlyDictionary<EffectType, int>? PassivePenetrations = null,    // passive-only
        int PassiveDisruptionPenetration = 0,                                 // passive-only
        IReadOnlyDictionary<EffectType, int>? PassiveResistances = null,     // passive-only
        int PassiveDisruptionResistance = 0,                                  // passive-only
        IReadOnlyList<BattleTrait>? PermittedTraits = null,           // unit needs at least one
        IReadOnlyList<EquipmentType>? PermittedEquipmentTypes = null, // unit needs one of these
        ReactionTrigger? Trigger = null,
        IReadOnlyList<TriggerCondition>? TriggerConditions = null,
        bool IsStrSkill = false,       // must be set explicitly; never inferred from scaling
        double FuryDamageScale = 0.0,  // max damage bonus at full Fury; bonus = scale × (fury/100)
        bool IsFocusCompatible = false,
        FocusEffectKind? FocusEffect = null,
        double FocusEffectValue = 0.0
    )
    {
        public bool HasModifier(string id) =>
            Modifiers?.Any(m => string.Equals(m, id, StringComparison.OrdinalIgnoreCase)) ?? false;

        // each gate is an independent OR: unit needs ≥1 matching trait AND ≥1 matching equipment type
        public bool MeetsRequirements(BattleUnit actor)
        {
            if (PermittedTraits != null && PermittedTraits.Count > 0 && !PermittedTraits.Any(t => actor.HasTrait(t)))
                return false;
            if (PermittedEquipmentTypes != null && !PermittedEquipmentTypes.Contains(actor.EquipmentType))
                return false;
            return true;
        }

        public bool IsHeal => Effects.Count > 0 && Effects[0].Kind == EffectKind.Heal;
        public bool IsShield => Effects.Count > 0 && Effects[0].Kind == EffectKind.Shield;
        public bool IsRestoreBar => Effects.Count > 0 && Effects[0].Kind == EffectKind.RestoreBar;
        public bool IsApplyEffect => Effects.Count > 0 && Effects[0].Kind == EffectKind.ApplyEffect;
        public bool IsGrantFocusedBuff => Effects.Count > 0 && Effects[0].Kind == EffectKind.GrantFocusedBuff;
        public bool IsDispel => Effects.Count > 0 && Effects[0].Kind == EffectKind.Dispel;
        public BattleSkillTarget Target => Effects.Count > 0 ? Effects[0].Target : BattleSkillTarget.Enemy;
        public EffectType? PrimaryEffectType =>
            Effects.Count > 0 && Effects[0].DamagePerHit.Count > 0
                ? Effects[0].DamagePerHit[0].DamageType
                : null;
        public bool IsBasic => ModifierTags?.Any(t => string.Equals(t, "basic", StringComparison.OrdinalIgnoreCase)) ?? false;
        public bool IsUltimate => ModifierTags?.Any(t => string.Equals(t, "ultimate", StringComparison.OrdinalIgnoreCase)) ?? false;
        public bool IsReaction => Category == SkillCategory.Reaction;
        // Bar that Cost is deducted from: FocusUser-gated skills use the focus bar, all others use mp.
        public string CostBarKey =>
            PermittedTraits != null && PermittedTraits.Contains(BattleTrait.FocusUser) ? "focus" : "mp";
        // ultimates automatically start with at least 1 round of cooldown
        public int EffectiveInitialCooldown => IsUltimate ? Math.Max(1, InitialCooldown) : InitialCooldown;

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
