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
        /// Fires after the unit is hit by a melee-range, non-heal, non-shield attack.
        /// The reactor counter-attacks the original attacker.
        /// </summary>
        OnHitByMelee,
    }
}
