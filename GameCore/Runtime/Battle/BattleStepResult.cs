#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Returned by <see cref="IBattleEngine.TryExecute"/>.
    /// <para>
    /// When <see cref="Accepted"/> is false, the command was rejected due to a rule violation.
    /// No state was changed. <see cref="View"/> reflects the unchanged current state and
    /// <see cref="Error"/> describes why the command was rejected.
    /// </para>
    /// <para>
    /// When <see cref="Accepted"/> is true, the command was valid and state has advanced.
    /// <see cref="Events"/> contains the events produced by this specific step (for animations).
    /// <see cref="View"/> reflects the new authoritative state.
    /// </para>
    /// </summary>
    public sealed record BattleStepResult(
        bool Accepted,
        ValidationError? Error,
        BattleView View,
        IReadOnlyList<BattleEvent> Events
    );
}
