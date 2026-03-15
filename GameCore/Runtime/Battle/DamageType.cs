namespace GameCore.Battle
{
    /// <summary>
    /// What an effect does — routes heal and shield logic separately from damage.
    /// </summary>
    public enum EffectKind
    {
        Damage,
        Heal,
        Shield,
        /// <summary>
        /// Adds or subtracts a fixed amount from a named bar on the target (e.g. MP, Focus, Fury).
        /// Positive <see cref="SkillEffect.BarAmount"/> restores; negative drains.
        /// </summary>
        RestoreBar,
        /// <summary>
        /// Applies an active buff or debuff to the target for a fixed duration.
        /// The effect definition is compiled inline from the skill's YAML data.
        /// Applied once per target (not per hit). No damage is dealt.
        /// </summary>
        ApplyEffect,
        /// <summary>
        /// Grants the Focused buff to the actor (Self-targeting).
        /// Used by the Focus skill to set up the Focused state before the refunded follow-up action.
        /// Applied once per target, not per hit. No damage is dealt.
        /// </summary>
        GrantFocusedBuff,
        /// <summary>
        /// Removes one random buff or debuff from the target, depending on
        /// <see cref="SkillEffect.DispelAlignment"/>.
        /// Applied once per target, not per hit. No damage is dealt.
        /// Bar-linked status effects (slow, frozen, burning, dizzy, stunned) are also eligible;
        /// their backing bars are reset when dispelled.
        /// </summary>
        Dispel,
    }

    /// <summary>
    /// Classifies an active effect as beneficial (buff) or harmful (debuff).
    /// Used to determine which effects are valid targets for dispel skills.
    /// </summary>
    public enum EffectAlignment
    {
        /// <summary>The effect is beneficial to its target (e.g. Attack Up, Defense Up).</summary>
        Buff,
        /// <summary>The effect is harmful to its target (e.g. Attack Down, Slow, Frozen).</summary>
        Debuff,
    }

    /// <summary>
    /// The elemental category of a skill's effect.
    /// Determines which attacker stat and defender resistance apply.
    /// <list type="bullet">
    ///   <item><see cref="Physical"/> — uses STR (PhysAttack)</item>
    ///   <item>All other types — use WIS (MagicAttack)</item>
    /// </list>
    /// </summary>
    public enum EffectType
    {
        Physical,
        Fire,
        Cold,
        Lightning,
        Holy,
        Void,
    }

    /// <summary>How a skill is delivered to its target.</summary>
    public enum SkillRange
    {
        Melee,
        Ranged,
        Self,
    }

    /// <summary>
    /// High-level ability category.
    /// Drives silencing, UI tagging, and future mechanic hooks.
    /// </summary>
    public enum SkillCategory
    {
        Attack,
        Spell,
        Passive,
        /// <summary>
        /// Reaction skills fire automatically in response to a trigger, never as a chosen action.
        /// Each unit may equip at most one reaction skill (stored in <see cref="BattleUnit.ReactionSkill"/>).
        /// </summary>
        Reaction,
    }

    /// <summary>
    /// The condition that causes a <see cref="SkillCategory.Reaction"/> skill to fire.
    /// </summary>
    public enum ReactionTrigger
    {
        /// <summary>
        /// Fires after the unit takes a hit from a damaging action (not heals or shields).
        /// Filter what counts as a trigger by adding <see cref="TriggerCondition"/> entries to the skill.
        /// When no conditions are specified, any damaging hit will fire the reaction.
        /// </summary>
        OnHitBy,
    }

    /// <summary>
    /// A filter condition for a <see cref="ReactionTrigger.OnHitBy"/> trigger.
    /// All non-null fields in a single instance must match simultaneously (AND logic).
    /// Multiple conditions in the list also AND together.
    /// </summary>
    public class TriggerCondition
    {
        /// <summary>If set, the incoming skill must use this range (e.g. <see cref="SkillRange.Melee"/>).</summary>
        public SkillRange? Range { get; }

        /// <summary>
        /// If set, the incoming skill must contain at least one damage component of this type
        /// (e.g. <see cref="EffectType.Physical"/> to only react to physical hits).
        /// </summary>
        public EffectType? DamageType { get; }

        public TriggerCondition(SkillRange? Range = null, EffectType? DamageType = null)
        {
            this.Range = Range;
            this.DamageType = DamageType;
        }
    }
}
