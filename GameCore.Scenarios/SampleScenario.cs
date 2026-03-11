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
            new("knight", "Knight", "player", Level: 12, MaxHp: 8500, MaxMp: 80, Attack: 650, Initiative: 55, Skills:
            [
                new("knight-strike",   "Strike",       MpCost:  0, Multiplier: 1.0),
                new("knight-bash",     "Shield Bash",  MpCost: 30, Multiplier: 1.4),
                new("knight-heal",     "Lay on Hands", MpCost: 60, Multiplier: 1.2,
                    Target: BattleSkillTarget.Ally, IsHeal: true),
            ]),
            new("mage", "Mage", "player", Level: 12, MaxHp: 6200, MaxMp: 120, Attack: 950, Initiative: 70, Skills:
            [
                new("mage-bolt",   "Magic Bolt",   MpCost:  0, Multiplier: 1.0),
                new("mage-burst",  "Arcane Burst", MpCost: 30, Multiplier: 1.6),
                new("mage-meteor", "Meteor",        MpCost: 60, Multiplier: 2.0, IsAoe: true),
            ]),
            new("rogue", "Rogue", "player", Level: 12, MaxHp: 5400, MaxMp: 65, Attack: 780, Initiative: 90, Skills:
            [
                new("rogue-strike",     "Quick Strike", MpCost:  0, Multiplier: 1.0),
                new("rogue-poison",     "Poison Blade", MpCost: 30, Multiplier: 1.5),
                new("rogue-mark",       "Death Mark",   MpCost: 60, Multiplier: 2.5),
            ]),
        ],
        EnemyUnits =
        [
            new("goblin-w", "Goblin Warrior", "enemy", Level: 11, MaxHp: 4500, MaxMp: 40, Attack: 580, Initiative: 50, Skills:
            [
                new("gw-slash",   "Slash",   MpCost:  0, Multiplier: 1.0),
                new("gw-warcry",  "War Cry", MpCost: 25, Multiplier: 1.3),
                new("gw-frenzy",  "Frenzy",  MpCost: 40, Multiplier: 1.8),
            ]),
            new("goblin-a", "Goblin Archer", "enemy", Level: 11, MaxHp: 3500, MaxMp: 40, Attack: 520, Initiative: 80, Skills:
            [
                new("ga-shot",    "Arrow Shot",    MpCost:  0, Multiplier: 1.0),
                new("ga-precise", "Precise Shot",  MpCost: 25, Multiplier: 1.4),
                new("ga-volley",  "Volley",        MpCost: 40, Multiplier: 1.9),
            ]),
            new("necro", "Necromancer", "enemy", Level: 12, MaxHp: 5500, MaxMp: 100, Attack: 750, Initiative: 65, Skills:
            [
                new("necro-bolt",  "Dark Bolt",   MpCost:  0, Multiplier: 1.0),
                new("necro-drain", "Life Drain",  MpCost: 25, Multiplier: 1.5),
                new("necro-soul",  "Soul Siphon", MpCost: 50, Multiplier: 2.4),
            ]),
        ],
    };
}
