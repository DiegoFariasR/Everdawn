using GameCore.Battle;

namespace GameCore.Scenarios;

/// <summary>
/// A sample battle: three heroes vs four enemies, mirroring the reference battle image.
/// Seed 42 is fixed — same inputs always produce the same battle.
/// <para>Also implements <see cref="IRegressionScenario"/>: expected outcomes are declared here
/// and validated automatically by the test suite.</para>
/// </summary>
public class SampleScenario : IBattleScenario, IRegressionScenario
{
    public string Id => "sample-scenario";
    public string DisplayName => "Sample Battle";
    public int Seed => 42;
    public bool IsPlayable => true;

    // ── IRegressionScenario ───────────────────────────────────────────────
    // Values established from a known-good run. Update intentionally if the engine or
    // scenario changes in a way that is expected and reviewed.
    public string ExpectedWinner => "player";
    // 1 start + 56 attack/skill events + 6 death events + 1 end = 64
    public int ExpectedSnapshotCount => 64;

    public override string ToString() => $"{DisplayName} [{Id}]";

    public BattleSetup CreateSetup() => new()
    {
        PlayerUnits =
        [
            new("paladin", "Paladin", "player", Level: 12, Str: 85, Wis:  0, Agi:  55, Skills:
            [
                new("paladin-strike",  "Strike",       MpCost: 0, Multiplier: 1.0, Modifiers: [SkillModifier.Basic]),
                new("paladin-bash",    "Shield Bash",  MpCost: 0, Multiplier: 1.4, Cooldown: 3),
                new("paladin-heal",    "Lay on Hands", MpCost: 0, Multiplier: 1.2,
                    Target: BattleSkillTarget.Ally, IsHeal: true, Cooldown: 3),
            ]),
            new("mage", "Mage", "player", Level: 12, Str: 62, Wis: 110, Agi:  70, Skills:
            [
                new("mage-bolt",   "Magic Bolt",   MpCost: 0, Multiplier: 1.0, DamageType: DamageType.Magical, Modifiers: [SkillModifier.Basic]),
                new("mage-burst",  "Arcane Burst", MpCost: 0, Multiplier: 1.6, Cooldown: 3, DamageType: DamageType.Magical),
                new("mage-meteor", "Meteor",        MpCost: 0, Multiplier: 2.0, IsAoe: true, Cooldown: 3, DamageType: DamageType.Magical, Modifiers: [SkillModifier.Ultimate]),
            ], Traits: [BattleTrait.MagicUser]),
            new("rogue", "Rogue", "player", Level: 12, Str: 54, Wis:  0, Agi: 120, Skills:
            [
                new("rogue-strike",  "Quick Strike", MpCost: 0, Multiplier: 1.0, Modifiers: [SkillModifier.Basic]),
                new("rogue-poison",  "Poison Blade", MpCost: 0, Multiplier: 1.5, Cooldown: 3),
                new("rogue-mark",    "Death Mark",   MpCost: 0, Multiplier: 2.5, Cooldown: 3, Modifiers: [SkillModifier.Ultimate]),
            ], Traits: [BattleTrait.Focus]),
        ],
        EnemyUnits =
        [
            new("goblin-w", "Goblin Warrior", "enemy", Level: 14, Str: 70, Wis:  0, Agi:  50, Skills:
            [
                new("gw-slash",   "Slash",   MpCost: 0, Multiplier: 1.0, Modifiers: [SkillModifier.Basic]),
                new("gw-warcry",  "War Cry", MpCost: 0, Multiplier: 1.6, Cooldown: 3),
                new("gw-frenzy",  "Frenzy",  MpCost: 0, Multiplier: 2.4, Cooldown: 3, Modifiers: [SkillModifier.Ultimate]),
            ]),
            new("goblin-a", "Goblin Archer", "enemy", Level: 14, Str: 55, Wis:  0, Agi: 100, Skills:
            [
                new("ga-shot",    "Arrow Shot",   MpCost: 0, Multiplier: 1.0, Modifiers: [SkillModifier.Basic]),
                new("ga-precise", "Precise Shot", MpCost: 0, Multiplier: 1.8, Cooldown: 3),
                new("ga-volley",  "Volley",       MpCost: 0, Multiplier: 2.2, Cooldown: 3, Modifiers: [SkillModifier.Ultimate]),
            ], Traits: [BattleTrait.Focus]),
            new("necro", "Necromancer", "enemy", Level: 15, Str: 80, Wis: 105, Agi:  65, Skills:
            [
                new("necro-bolt",  "Dark Bolt",   MpCost: 0, Multiplier: 1.0, DamageType: DamageType.Magical, Modifiers: [SkillModifier.Basic]),
                new("necro-drain", "Life Drain",  MpCost: 0, Multiplier: 1.8, Cooldown: 3, DamageType: DamageType.Magical),
                new("necro-soul",  "Soul Siphon", MpCost: 0, Multiplier: 3.0, Cooldown: 3, DamageType: DamageType.Magical, Modifiers: [SkillModifier.Ultimate]),
            ], Traits: [BattleTrait.MagicUser]),
        ],
    };
}
