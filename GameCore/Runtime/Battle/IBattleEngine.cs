namespace GameCore.Battle
{
    /// <summary>
    /// The public contract for a stateful battle session.
    /// <para>
    /// Usage: create a <see cref="BattleSession"/>, call <see cref="Start"/> once with the
    /// setup, then drive the battle by calling <see cref="TryExecute"/> with commands.
    /// The current authoritative state is always available via <see cref="GetView"/>.
    /// </para>
    /// <para>
    /// Architecture rules:
    /// <list type="bullet">
    ///   <item>The client suggests actions. BattleCore validates and resolves truth.</item>
    ///   <item>All state changes are validated before they happen.</item>
    ///   <item>Invalid commands return a rejected result — they never mutate state.</item>
    ///   <item>The client must never mutate HP, cooldowns, turn order, or victory state directly.</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IBattleEngine
    {
        /// <summary>
        /// Initializes the battle from a fully-resolved, authoritative <see cref="BattleSetup"/>.
        /// Must be called exactly once before any <see cref="TryExecute"/> calls.
        /// Returns a failed result if already started.
        /// <para>
        /// <b>Caller responsibility:</b> The <paramref name="setup"/> passed here must already be
        /// the backend-resolved truth. Do not pass client-assembled setup in production.
        /// In the backend-authoritative model, this is called after the backend validates
        /// a <see cref="StartBattleRequest"/> and builds the authoritative configuration.
        /// </para>
        /// </summary>
        BattleStartResult Start(BattleSetup setup);

        /// <summary>
        /// Validates and executes a command. All rules are enforced here — the client must
        /// never assume a command will be accepted without checking <see cref="BattleStepResult.Accepted"/>.
        /// An accepted result mutates state; a rejected result leaves state unchanged.
        /// </summary>
        BattleStepResult TryExecute(BattleCommand command);

        /// <summary>
        /// Returns the current authoritative view of the battle state.
        /// Throws if <see cref="Start"/> has not been called.
        /// </summary>
        BattleView GetView();

        /// <summary>
        /// Applies a runtime active effect to a target unit.
        /// <para>
        /// This is the programmatic hook for buff and debuff application. In a future content
        /// wiring pass, skills will call this internally during action resolution.
        /// For now, callers (tests, scripted events) apply effects directly.
        /// </para>
        /// <para>
        /// Stacking policy from the definition controls behaviour when the same effect is already active.
        /// </para>
        /// </summary>
        /// <param name="targetUnitId">ID of the unit to apply the effect to.</param>
        /// <param name="definition">The effect definition (template) to apply.</param>
        /// <param name="sourceUnitId">ID of the unit that caused the effect (self for self-buffs).</param>
        void ApplyActiveEffect(string targetUnitId, ActiveEffectDefinition definition, string sourceUnitId);
    }
}
