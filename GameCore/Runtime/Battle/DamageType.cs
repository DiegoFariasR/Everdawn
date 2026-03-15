namespace GameCore.Battle
{
    public enum EffectKind
    {
        Damage,
        Heal,
        Shield,
        RestoreBar,      // adds/drains a named bar (MP, Focus, Fury…); positive = restore
        ApplyEffect,     // applies a buff or debuff once per target (not per hit)
        GrantFocusedBuff, // grants Focused to actor for use with a refunded follow-up action
        Dispel,          // removes one buff or debuff; bar-linked statuses also eligible
    }

    // Determines dispel eligibility.
    public enum EffectAlignment
    {
        Buff,   // beneficial (e.g. Attack Up, Defense Up)
        Debuff, // harmful (e.g. Attack Down, Slow, Frozen)
    }

    // Elemental category; determines attacker stat and defender resistance.
    // Blunt and Slash are Physical sub-types (use STR, inherit Physical resistance/pen).
    // All other types use WIS.
    public enum EffectType
    {
        Physical,
        Blunt,     // Physical sub-type (STR). Inherits Physical resistance. Builds disruption bar.
        Slash,     // Physical sub-type (STR). Inherits Physical resistance. Builds bleed bar.
        Fire,
        Cold,
        Lightning,
        Holy,
        Void,
    }

    public enum SkillRange
    {
        Melee,
        Ranged,
        Self,
    }

    public enum SkillCategory
    {
        Attack,
        Spell,
        Passive,
        Reaction, // fires on trigger, never chosen; at most one per unit
    }

    public enum ReactionTrigger
    {
        OnHitBy, // fires after taking a damaging hit (not heals/shields)
    }

    // All non-null fields AND together; multiple conditions in a list also AND.
    public class TriggerCondition
    {
        public SkillRange? Range { get; }      // incoming skill must match this range
        public EffectType? DamageType { get; } // incoming skill must include a component of this type

        public TriggerCondition(SkillRange? Range = null, EffectType? DamageType = null)
        {
            this.Range = Range;
            this.DamageType = DamageType;
        }
    }
}
