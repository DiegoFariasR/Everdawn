using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
    public enum BattleSkillTarget { Enemy, Ally }

    /// <summary>One stat-to-damage scaling entry within a damage component.</summary>
    public record DamageScaling(string Stat, double Scale = 1.0);

    /// <summary>
    /// One damage type component within a hit.
    /// <see cref="DamageType"/> is null for heal components (no resistance applied).
    /// </summary>
    public record DamageComponent(EffectType? DamageType, IReadOnlyList<DamageScaling> Scaling);

    /// <summary>
    /// One effect produced by a skill: its kind (damage/heal), intended target side, and per-hit formula.
    /// </summary>
    public record SkillEffect(EffectKind Kind, BattleSkillTarget Target, IReadOnlyList<DamageComponent> DamagePerHit);

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
        SkillCategory Category = SkillCategory.Attack
    )
    {
        /// <summary>Returns true if this skill carries the given modifier (case-insensitive).</summary>
        public bool HasModifier(string id) =>
            Modifiers?.Any(m => string.Equals(m, id, StringComparison.OrdinalIgnoreCase)) ?? false;

        /// <summary>True if this skill heals rather than damages.</summary>
        public bool IsHeal => Effects.Count > 0 && Effects[0].Kind == EffectKind.Heal;

        /// <summary>True if this skill grants a barrier (shield) rather than dealing damage.</summary>
        public bool IsShield => Effects.Count > 0 && Effects[0].Kind == EffectKind.Shield;

        /// <summary>Target side of the first effect. Defaults to Enemy if Effects is empty.</summary>
        public BattleSkillTarget Target => Effects.Count > 0 ? Effects[0].Target : BattleSkillTarget.Enemy;

        /// <summary>Primary damage type — first damage component's type. Null for heals.</summary>
        public EffectType? PrimaryEffectType =>
            Effects.Count > 0 && Effects[0].DamagePerHit.Count > 0
                ? Effects[0].DamagePerHit[0].DamageType
                : null;

        /// <summary>The skill is the unit's basic action (never triggers Focus empowerment).</summary>
        public bool IsBasic => HasModifier("basic");

        /// <summary>The skill is the unit's ultimate action.</summary>
        public bool IsUltimate => HasModifier("ultimate");

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
