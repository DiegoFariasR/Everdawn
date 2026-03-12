using GameCore.Battle;

namespace GameCore.Tests.Battle;

public class FocusTraitTests
{
    // ── BattleUnit derived props ──────────────────────────────────────────

    [Fact]
    public void Focus_MaxFocus_IsOneHundred()
    {
        var unit = MakeUnit(traits: [BattleTrait.Focus]);
        Assert.Equal(100, unit.MaxBars.TryGetValue("focus", out int v) ? v : 0);
    }

    [Fact]
    public void Focus_InitialFocus_IsFifty()
    {
        var unit = MakeUnit(traits: [BattleTrait.Focus]);
        Assert.Equal(50, unit.InitialBars.TryGetValue("focus", out int v) ? v : 0);
    }

    [Fact]
    public void NoTrait_MaxFocus_IsZero()
    {
        var unit = MakeUnit(traits: null);
        Assert.False(unit.MaxBars.ContainsKey("focus"));
    }

    [Fact]
    public void NoTrait_InitialFocus_IsZero()
    {
        var unit = MakeUnit(traits: null);
        Assert.False(unit.InitialBars.ContainsKey("focus"));
    }

    // ── Focus starts at 50 ────────────────────────────────────────────────

    [Fact]
    public void FocusUnit_StartsWithFiftyFocus()
    {
        // Player goes first (Agi 50 > enemy Agi 1) — enemy never acts before the initial view.
        var session = StartSimpleFocusBattle(playerAgi: 50);
        var focusUnit = session.GetView().Units.First(u => u.UnitId == "focus-unit");
        Assert.Equal(50, focusUnit.GetBar("focus"));
    }

    // ── Focus gain per hit ────────────────────────────────────────────────

    [Fact]
    public void FocusUnit_GainsTenFocusPerOffensiveHit()
    {
        // Player Str 200 (attack 1600) one-shots a weak enemy (Str 1 = 100 HP).
        // Enemy cannot retaliate after dying, so the +10 focus from the hit is preserved.
        var session = StartSimpleFocusBattle(playerAgi: 50);
        session.TryExecute(new PlayerActionCommand("basic", "target"));
        var focusUnit = session.GetView().Units.First(u => u.UnitId == "focus-unit");
        // Started at 50, gained 10 from 1 hit, no retaliation (enemy dead) → 60
        Assert.Equal(60, focusUnit.GetBar("focus"));
    }

    [Fact]
    public void FocusUnit_MultiHit_GainsTenFocusPerHit()
    {
        // Player Agi 150 → HitCount = 2. Str 200 → attack 1600, variance 320.
        // Enemy Str 20 → HP 2000. First hit (max 1920) always survives; second (min 1280) always kills.
        // Both hits land before retaliation → +20 focus total.
        var session = StartSimpleFocusBattle(playerAgi: 150, enemyStr: 20);
        session.TryExecute(new PlayerActionCommand("basic", "target"));
        var focusUnit = session.GetView().Units.First(u => u.UnitId == "focus-unit");
        // Started at 50, gained 20 from 2 hits, no retaliation (enemy dead) → 70
        Assert.Equal(70, focusUnit.GetBar("focus"));
    }

    // ── Focus empowerment ─────────────────────────────────────────────────

    [Fact]
    public void FocusUnit_AtFullFocus_NonBasicSkill_FiresEmpowermentEvent()
    {
        // Use ResumeFromSnapshotCommand to jump straight to focus = 100.
        var session = StartFocusBattleAt100();
        var result = session.TryExecute(new PlayerActionCommand("special", "target"));
        Assert.Contains(result.Events, e => e.Description.Contains("empowers"));
    }

    [Fact]
    public void FocusUnit_AfterEmpowerment_FocusResets()
    {
        // Empowerment resets focus to 50; the killing blow adds +10 → final 60.
        var session = StartFocusBattleAt100();
        session.TryExecute(new PlayerActionCommand("special", "target"));
        var focusUnit = session.GetView().Units.First(u => u.UnitId == "focus-unit");
        Assert.Equal(60, focusUnit.GetBar("focus"));
    }

    [Fact]
    public void FocusUnit_AtFullFocus_BasicAttack_DoesNotConsumeEmpowerment()
    {
        // Basic attack (skillIdx 0) must NOT trigger empowerment even when focus is 100.
        var session = StartFocusBattleAt100();
        var result = session.TryExecute(new PlayerActionCommand("basic", "target"));
        Assert.DoesNotContain(result.Events, e => e.Description.Contains("empowers"));
        // Focus stays at 100 (was 100, +10 from hit but capped at 100).
        var focusUnit = session.GetView().Units.First(u => u.UnitId == "focus-unit");
        Assert.Equal(100, focusUnit.GetBar("focus"));
    }

    // ── Focus loss on incoming damage ─────────────────────────────────────

    [Fact]
    public void FocusUnit_LosesTenFocusPerIncomingHit()
    {
        // Enemy Agi 200 > player Agi 1 → enemy is first in turn order and acts during
        // the initial AutoAdvance inside Start(), before player ever moves.
        var setup = new BattleSetup
        {
            PlayerUnits =
            [
                new("focus-unit", "Fighter", "player", Level: 1, Str: 200, Wis: 0, Agi: 1,
                    Skills: [new("basic", "Strike", Cost: 0, DamageMultiplier: 1.0)],
                    Traits: [BattleTrait.Focus]),
            ],
            EnemyUnits =
            [
                // Agi 2 > player Agi 1 (enemy goes first) but HitCount = 1 + 2/100 = 1 (single hit).
                new("attacker", "Enemy", "enemy", Level: 1, Str: 10, Wis: 0, Agi: 2,
                    Skills: [new("e-basic", "Hit", Cost: 0, DamageMultiplier: 1.0)]),
            ],
        };
        var session = new BattleSession(seed: 0);
        session.Start(setup);
        // Enemy acted first → player focus: 50 → 40.
        var focusUnit = session.GetView().Units.First(u => u.UnitId == "focus-unit");
        Assert.Equal(40, focusUnit.GetBar("focus"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Strong focus attacker (Str 200 = 1600 attack) vs a weak enemy.
    /// Default enemyStr 1 → 100 HP: player one-shots in a single basic hit.
    /// enemyStr 20 → 2000 HP: player needs exactly 2 hits (HitCount 2) to kill.
    /// </summary>
    private static BattleSession StartSimpleFocusBattle(int playerAgi = 50, int enemyStr = 1)
    {
        var setup = new BattleSetup
        {
            PlayerUnits =
            [
                new("focus-unit", "Fighter", "player", Level: 1, Str: 200, Wis: 0, Agi: playerAgi,
                    Skills:
                    [
                        new("basic",   "Strike",     Cost: 0, DamageMultiplier: 1.0, Modifiers: ["basic"]),
                        new("special", "Power Blow", Cost: 0, DamageMultiplier: 1.5, Cooldown: 2),
                    ],
                    Traits: [BattleTrait.Focus]),
            ],
            EnemyUnits =
            [
                new("target", "Dummy", "enemy", Level: 1, Str: enemyStr, Wis: 0, Agi: 1,
                    Skills: [new("def-basic", "Slash", Cost: 0, DamageMultiplier: 1.0)]),
            ],
        };
        var session = new BattleSession(seed: 0);
        session.Start(setup);
        return session;
    }

    /// <summary>
    /// Returns a session where it is the player's turn and their focus is already at 100.
    /// Uses ResumeFromSnapshotCommand to inject the pre-condition state directly.
    /// </summary>
    private static BattleSession StartFocusBattleAt100()
    {
        const int playerStr = 200;
        const int enemyStr = 1;
        var setup = new BattleSetup
        {
            PlayerUnits =
            [
                new("focus-unit", "Fighter", "player", Level: 1, Str: playerStr, Wis: 0, Agi: 50,
                    Skills:
                    [
                        new("basic",   "Strike",     Cost: 0, DamageMultiplier: 1.0, Modifiers: ["basic"]),
                        new("special", "Power Blow", Cost: 0, DamageMultiplier: 1.5, Cooldown: 2),
                    ],
                    Traits: [BattleTrait.Focus]),
            ],
            EnemyUnits =
            [
                new("target", "Dummy", "enemy", Level: 1, Str: enemyStr, Wis: 0, Agi: 1,
                    Skills: [new("def-basic", "Slash", Cost: 0, DamageMultiplier: 1.0)]),
            ],
        };
        var session = new BattleSession(seed: 0);
        session.Start(setup);
        // Inject full-focus state: player at max HP with focus 100, enemy at max HP.
        UnitState[] state =
        [
            new("focus-unit", playerStr * 100, true, new Dictionary<string, int> { ["focus"] = 100 }),
            new("target",     enemyStr  * 100, true, null),
        ];
        // Resume as if the enemy (last in turn order by Agi) just acted → player goes next.
        session.TryExecute(new ResumeFromSnapshotCommand(state, LastActorId: "target", AtStep: 0));
        return session;
    }

    private static BattleUnit MakeUnit(IReadOnlyList<BattleTrait>? traits) =>
        new("unit", "Unit", "player", Level: 1, Str: 50, Wis: 50, Agi: 50, Traits: traits);
}
