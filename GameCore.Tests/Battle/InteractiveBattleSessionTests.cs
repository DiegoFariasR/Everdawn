using GameCore.Battle;
using GameCore.Scenarios;

namespace GameCore.Tests.Battle;

/// <summary>
/// Tests for InteractiveBattleSession — the stateful request/response interface
/// that UnityClient uses to drive battles step by step.
/// </summary>
public class InteractiveBattleSessionTests
{
    private static InteractiveBattleSession CreateSession()
    {
        var scenario = new SampleScenario();
        return new InteractiveBattleSession(scenario.CreateSetup(), scenario.Seed);
    }

    // ── Start ────────────────────────────────────────────────────────────

    [Fact]
    public void Start_ReturnsNonNullResponse()
    {
        var session = CreateSession();
        var response = session.HandleRequest(new StartBattleRequest());
        Assert.NotNull(response);
    }

    [Fact]
    public void Start_LogContainsStartEvent()
    {
        var session = CreateSession();
        var response = session.HandleRequest(new StartBattleRequest());
        Assert.Contains(response.FullLog, e => e.Type == "start");
    }

    [Fact]
    public void Start_StateContainsAllUnits()
    {
        var scenario = new SampleScenario();
        var setup = scenario.CreateSetup();
        var session = new InteractiveBattleSession(setup, scenario.Seed);
        var response = session.HandleRequest(new StartBattleRequest());
        Assert.Equal(setup.PlayerUnits.Count + setup.EnemyUnits.Count, response.State.Count);
    }

    [Fact]
    public void Start_AllUnitsBeginAlive()
    {
        var session = CreateSession();
        var response = session.HandleRequest(new StartBattleRequest());
        Assert.All(response.State, s => Assert.True(s.IsAlive, $"Unit {s.UnitId} should start alive"));
    }

    [Fact]
    public void Start_AllUnitsBeginAtFullHp()
    {
        var scenario = new SampleScenario();
        var setup = scenario.CreateSetup();
        var maxHp = setup.PlayerUnits.Concat(setup.EnemyUnits)
                        .ToDictionary(u => u.Id, u => u.MaxHp);
        var session = new InteractiveBattleSession(setup, scenario.Seed);
        var response = session.HandleRequest(new StartBattleRequest());
        foreach (var state in response.State)
            Assert.Equal(maxHp[state.UnitId], state.CurrentHp);
    }

    [Fact]
    public void Start_PlayerGoesFirstInSampleScenario()
    {
        // Rogue has AGI=120, the highest initiative in the scenario.
        // AutoAdvance stops immediately because it's a player turn.
        var session = CreateSession();
        var response = session.HandleRequest(new StartBattleRequest());
        Assert.NotNull(response.PendingInput);
        Assert.Equal("rogue", response.PendingInput!.Actor.Id);
    }

    [Fact]
    public void Start_PendingInputListsRogueSkills()
    {
        var session = CreateSession();
        var response = session.HandleRequest(new StartBattleRequest());
        var skills = response.PendingInput!.Skills;
        Assert.NotEmpty(skills);
        // First skill is always the free basic attack
        Assert.Equal(0, skills[0].MpCost);
    }

    [Fact]
    public void Start_PendingInputListsLivingEnemyTargets()
    {
        var session = CreateSession();
        var response = session.HandleRequest(new StartBattleRequest());
        var enemies = response.PendingInput!.EnemyTargets;
        Assert.NotEmpty(enemies);
        Assert.All(enemies, e => Assert.Equal("enemy", e.Team));
    }

    [Fact]
    public void Start_Twice_Throws()
    {
        var session = CreateSession();
        session.HandleRequest(new StartBattleRequest());
        Assert.Throws<InvalidOperationException>(
            () => session.HandleRequest(new StartBattleRequest()));
    }

    // ── Auto-advance ─────────────────────────────────────────────────────

    [Fact]
    public void AutoAdvance_EventuallyEndsBattle()
    {
        var session = CreateSession();
        session.HandleRequest(new StartBattleRequest());

        BattleResponse? last = null;
        for (int i = 0; i < 500 && (last is null || !last.IsOver); i++)
            last = session.HandleRequest(new AdvanceOneTurnRequest());

        Assert.NotNull(last);
        Assert.True(last!.IsOver, "Battle should end within 500 single-turn advances");
        Assert.NotNull(last.WinningTeam);
    }

    [Fact]
    public void AutoAdvance_WinnerMatchesBattleEngineResult()
    {
        // Both BattleEngine.Run and InteractiveBattleSession with the same seed
        // must agree on who wins.
        var scenario = new SampleScenario();

        var engineResult = BattleEngine.Run(scenario.CreateSetup(), scenario.Seed);

        var session = new InteractiveBattleSession(scenario.CreateSetup(), scenario.Seed);
        session.HandleRequest(new StartBattleRequest());
        BattleResponse last = null!;
        for (int i = 0; i < 500; i++)
        {
            last = session.HandleRequest(new AdvanceOneTurnRequest());
            if (last.IsOver) break;
        }

        Assert.Equal(engineResult.WinningTeam, last.WinningTeam);
    }

    [Fact]
    public void AutoAdvance_IsDeterministic()
    {
        static BattleResponse RunToEnd(IBattleScenario scenario)
        {
            var session = new InteractiveBattleSession(scenario.CreateSetup(), scenario.Seed);
            session.HandleRequest(new StartBattleRequest());
            BattleResponse last = null!;
            for (int i = 0; i < 500; i++)
            {
                last = session.HandleRequest(new AdvanceOneTurnRequest());
                if (last.IsOver) break;
            }
            return last;
        }

        var scenario = new SampleScenario();
        var r1 = RunToEnd(scenario);
        var r2 = RunToEnd(scenario);

        Assert.Equal(r1.WinningTeam, r2.WinningTeam);
        Assert.Equal(r1.FullLog.Count, r2.FullLog.Count);
    }

    [Fact]
    public void AutoAdvance_FullLogGrowsOverTime()
    {
        var session = CreateSession();
        var start = session.HandleRequest(new StartBattleRequest());
        int logAfterStart = start.FullLog.Count;

        var next = session.HandleRequest(new AdvanceOneTurnRequest());
        Assert.True(next.FullLog.Count > logAfterStart,
            "FullLog should grow after advancing a turn");
    }

    // ── Invariants ───────────────────────────────────────────────────────

    [Fact]
    public void Response_NoUnitHpExceedsMax()
    {
        var scenario = new SampleScenario();
        var setup = scenario.CreateSetup();
        var maxHp = setup.PlayerUnits.Concat(setup.EnemyUnits)
                        .ToDictionary(u => u.Id, u => u.MaxHp);

        var session = new InteractiveBattleSession(setup, scenario.Seed);
        session.HandleRequest(new StartBattleRequest());
        for (int i = 0; i < 50; i++)
        {
            var response = session.HandleRequest(new AdvanceOneTurnRequest());
            foreach (var state in response.State)
                Assert.True(state.CurrentHp <= maxHp[state.UnitId],
                    $"Unit {state.UnitId} HP {state.CurrentHp} > max {maxHp[state.UnitId]}");
            if (response.IsOver) break;
        }
    }
}
