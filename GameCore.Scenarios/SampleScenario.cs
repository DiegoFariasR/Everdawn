using GameCore.Battle;

namespace GameCore.Scenarios;

/// <summary>
/// A sample battle: three heroes vs four enemies, mirroring the reference battle image.
/// Seed 42 is fixed — same inputs always produce the same battle.
/// </summary>
public class SampleScenario : IBattleScenario
{
    public string Id => "sample-scenario-play";
    public string DisplayName => "Sample Battle";
    public int Seed => 42;
    public bool IsPlayable => true;

    public BattleSetup CreateSetup() => new()
    {
        PlayerUnits =
        [
            new("knight",  "Knight",         "player", Level: 12, MaxHp: 8500, MaxMp: 45,  Attack: 650, Initiative: 55),
            new("mage",    "Mage",           "player", Level: 12, MaxHp: 6200, MaxMp: 120, Attack: 950, Initiative: 70),
            new("rogue",   "Rogue",          "player", Level: 12, MaxHp: 5400, MaxMp: 65,  Attack: 780, Initiative: 90),
        ],
        EnemyUnits =
        [
            new("goblin-w",  "Goblin Warrior",  "enemy", Level: 11, MaxHp: 4500, MaxMp: 0,   Attack: 580, Initiative: 50),
            new("goblin-a",  "Goblin Archer",   "enemy", Level: 11, MaxHp: 3500, MaxMp: 30,  Attack: 520, Initiative: 80),
            new("necro",     "Necromancer",     "enemy", Level: 12, MaxHp: 5500, MaxMp: 100, Attack: 750, Initiative: 65),
        ],
    };
}
