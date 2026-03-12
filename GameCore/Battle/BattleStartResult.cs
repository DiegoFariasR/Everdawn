namespace GameCore.Battle;

/// <summary>
/// Returned by <see cref="IBattleEngine.Start"/>.
/// </summary>
public sealed record BattleStartResult(
    bool Success,
    ValidationError? Error,
    BattleView View,
    IReadOnlyList<BattleEvent> Events
);
