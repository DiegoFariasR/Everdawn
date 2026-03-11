namespace GameCore.Battle;

/// <summary>
/// A single event that occurred during a battle (attack, death, start, end).
/// </summary>
public record BattleEvent(
    string ActorId,
    string Description,
    string Type,
    string? TargetId = null,
    int Value = 0
);
