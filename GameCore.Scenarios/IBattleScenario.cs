using GameCore.Battle;

namespace GameCore.Scenarios;

/// <summary>
/// A deterministic battle scenario: a fixed setup and seed that always produces the same battle.
/// </summary>
public interface IBattleScenario
{
    string Id { get; }
    string DisplayName { get; }
    int Seed { get; }
    bool IsPlayable { get; }
    BattleSetup CreateSetup();
}
