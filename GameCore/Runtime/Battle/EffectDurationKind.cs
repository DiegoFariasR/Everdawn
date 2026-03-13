namespace GameCore.Battle
{
    /// <summary>
    /// Determines how an active effect's remaining duration decrements over time.
    /// Duration ticks down once per qualifying action taken by the relevant unit.
    /// </summary>
    public enum EffectDurationKind
    {
        /// <summary>
        /// Duration counts down each time the <b>target</b> unit takes an action.
        /// Use for most buffs and debuffs that last a fixed number of the target's turns.
        /// </summary>
        ForTargetTurns,

        /// <summary>
        /// Duration counts down each time the <b>source</b> unit takes an action.
        /// Use for effects that are "owned" by the caster's turn rhythm.
        /// </summary>
        ForSourceTurns,

        /// <summary>
        /// Expires immediately after the target unit's <b>next</b> action.
        /// Use for one-shot reactive effects (e.g. "until your next attack").
        /// </summary>
        UntilNextAction,
    }
}
