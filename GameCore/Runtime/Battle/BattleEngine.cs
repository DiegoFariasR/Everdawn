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
        /// <param name="maxRounds">
        /// If positive, the battle is stopped after this many rounds and counted as an enemy win.
        /// When 0 (default), the battle runs until a team is eliminated.
        /// </param>
        public static BattleResult Run(BattleSetup setup, int seed, int maxRounds = 0) =>
            BattleSession.RunFull(setup, seed, maxRounds);
    }
}
