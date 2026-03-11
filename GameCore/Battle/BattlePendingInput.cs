namespace GameCore.Battle;

/// <summary>
/// Describes what the client must display and accept as input for the current player turn.
/// When null in a BattleResponse, it is not the player's turn (either game over or
/// enemies are resolving — but enemies always auto-resolve before the response returns).
/// </summary>
public record BattlePendingInput(
    BattleUnit Actor,
    bool CanUseSkill,
    bool CanUseSoulBurn,
    IReadOnlyList<BattleUnit> ValidTargets
);
