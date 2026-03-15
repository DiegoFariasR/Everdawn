namespace GameCore.Battle
{
    public enum EffectKind
    {
        Damage,          // deals damage to one or more targets
        Heal,            // restores HP to the target
        Shield,          // grants a barrier that absorbs damage before HP
        RestoreBar,      // adds/drains a named bar (MP, Focus, Fury…); positive = restore
        ApplyEffect,     // applies a buff or debuff once per target (not per hit)
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
        Physical,  // generic physical (STR); parent type for Blunt and Slash
        Blunt,     // Physical sub-type (STR). Inherits Physical resistance. Builds disruption bar.
        Slash,     // Physical sub-type (STR). Inherits Physical resistance. Builds bleed bar.
        Fire,      // elemental (WIS). Builds burn bar.
        Cold,      // elemental (WIS). Builds cold bar.
        Lightning, // elemental (WIS).
        Holy,      // elemental (WIS).
        Void,      // elemental (WIS).
    }

    public enum SkillRange
    {
        Melee,   // targets an adjacent unit
        Ranged,  // targets any enemy regardless of position
        Self,    // targets only the caster
    }

    public enum SkillCategory
    {
        Attack,      // standard physical attack skill
        Spell,       // magical skill (typically WIS-scaling)
        Passive,     // permanent stat bonuses; never an action choice
        Reaction,    // fires on trigger, never chosen; at most one per unit
        Preparation, // refunds action; at most one Preparation buff active per unit at a time
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
