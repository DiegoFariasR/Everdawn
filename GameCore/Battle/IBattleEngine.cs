namespace GameCore.Battle;

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
    /// Initializes the battle from the given setup. Must be called exactly once before
    /// any <see cref="TryExecute"/> calls. Returns a failed result if already started.
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
}
