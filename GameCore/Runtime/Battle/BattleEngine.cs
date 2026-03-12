namespace GameCore.Battle
{
    /// <summary>
    /// Thin facade over <see cref="BattleSession.RunFull"/> for the deterministic replay use-case.
    /// All battle resolution logic lives in <see cref="BattleSession"/> /
    /// <see cref="InteractiveBattleSession"/>; this class is the single public entry-point
    /// for callers that want a completed <see cref="BattleResult"/> without driving the session
    /// step-by-step.
    /// </summary>
    public static class BattleEngine
    {
        /// <summary>
        /// Runs a complete battle deterministically and returns a <see cref="BattleResult"/>
        /// with one snapshot per event — suitable for stop-motion replay.
        /// Delegates to <see cref="BattleSession.RunFull"/> which is the single authoritative
        /// execution path.
        /// </summary>
        public static BattleResult Run(BattleSetup setup, int seed) =>
            BattleSession.RunFull(setup, seed);
    }
}
