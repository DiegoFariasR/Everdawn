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
    }
}
