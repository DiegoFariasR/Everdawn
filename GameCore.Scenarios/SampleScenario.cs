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
            new("paladin", "Paladin", "player", Level: 12, MaxHp: 8500, MaxMp: 0, Attack: 650, Initiative: 55, Skills:
            [
                new("paladin-strike",  "Strike",       MpCost: 0, Multiplier: 1.0),
                new("paladin-bash",    "Shield Bash",  MpCost: 0, Multiplier: 1.4, Cooldown: 3),
                new("paladin-heal",    "Lay on Hands", MpCost: 0, Multiplier: 1.2,
                    Target: BattleSkillTarget.Ally, IsHeal: true, Cooldown: 3),
            ]),
            new("mage", "Mage", "player", Level: 12, MaxHp: 6200, MaxMp: 0, Attack: 950, Initiative: 70, Skills:
            [
                new("mage-bolt",   "Magic Bolt",   MpCost: 0, Multiplier: 1.0),
                new("mage-burst",  "Arcane Burst", MpCost: 0, Multiplier: 1.6, Cooldown: 3),
                new("mage-meteor", "Meteor",        MpCost: 0, Multiplier: 2.0, IsAoe: true, Cooldown: 3),
            ]),
            new("rogue", "Rogue", "player", Level: 12, MaxHp: 5400, MaxMp: 0, Attack: 780, Initiative: 90, Skills:
            [
                new("rogue-strike",  "Quick Strike", MpCost: 0, Multiplier: 1.0),
                new("rogue-poison",  "Poison Blade", MpCost: 0, Multiplier: 1.5, Cooldown: 3),
                new("rogue-mark",    "Death Mark",   MpCost: 0, Multiplier: 2.5, Cooldown: 3),
            ]),
        ],
        EnemyUnits =
        [
            new("goblin-w", "Goblin Warrior", "enemy", Level: 14, MaxHp: 7000, MaxMp: 0, Attack: 820, Initiative: 50, Skills:
            [
                new("gw-slash",   "Slash",   MpCost: 0, Multiplier: 1.0),
                new("gw-warcry",  "War Cry", MpCost: 0, Multiplier: 1.6, Cooldown: 3),
                new("gw-frenzy",  "Frenzy",  MpCost: 0, Multiplier: 2.4, Cooldown: 3),
            ]),
            new("goblin-a", "Goblin Archer", "enemy", Level: 14, MaxHp: 5500, MaxMp: 0, Attack: 750, Initiative: 80, Skills:
            [
                new("ga-shot",    "Arrow Shot",   MpCost: 0, Multiplier: 1.0),
                new("ga-precise", "Precise Shot", MpCost: 0, Multiplier: 1.8, Cooldown: 3),
                new("ga-volley",  "Volley",       MpCost: 0, Multiplier: 2.2, Cooldown: 3),
            ]),
            new("necro", "Necromancer", "enemy", Level: 15, MaxHp: 9000, MaxMp: 0, Attack: 1050, Initiative: 65, Skills:
            [
                new("necro-bolt",  "Dark Bolt",   MpCost: 0, Multiplier: 1.0),
                new("necro-drain", "Life Drain",  MpCost: 0, Multiplier: 1.8, Cooldown: 3),
                new("necro-soul",  "Soul Siphon", MpCost: 0, Multiplier: 3.0, Cooldown: 3),
            ]),
        ],
    };
}
