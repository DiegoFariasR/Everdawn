using GameCore.Battle;
using GameCore.Scenarios;

namespace GameCore.Tests.Battle;

/// <summary>
/// Tests for BattleSession / IBattleEngine — the public contract that UnityClient
/// depends on to drive battles step by step.
/// </summary>
public class BattleSessionTests
{
    private static (BattleSession engine, BattleView view) CreateStartedSession()
    {
        var scenario = new SampleScenario();
        var engine = new BattleSession(scenario.Seed);
        var result = engine.Start(scenario.CreateSetup());
        return (engine, result.View);
    }

    // ── Start ────────────────────────────────────────────────────────────

    [Fact]
    public void Start_ReturnsSuccess()
    {
        var scenario = new SampleScenario();
        var engine = new BattleSession(scenario.Seed);
        var result = engine.Start(scenario.CreateSetup());
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Start_ViewLogContainsStartEvent()
    {
        var (_, view) = CreateStartedSession();
        Assert.Contains(view.FullLog, e => e.Type == "start");
    }

    [Fact]
    public void Start_ViewContainsAllUnits()
    {
        var scenario = new SampleScenario();
        var setup = scenario.CreateSetup();
        var engine = new BattleSession(scenario.Seed);
        var result = engine.Start(setup);
        Assert.Equal(setup.PlayerUnits.Count + setup.EnemyUnits.Count, result.View.Units.Count);
    }

    [Fact]
    public void Start_AllUnitsBeginAlive()
    {
        var (_, view) = CreateStartedSession();
        Assert.All(view.Units, s => Assert.True(s.IsAlive, $"Unit {s.UnitId} should start alive"));
    }

    [Fact]
    public void Start_AllUnitsBeginAtFullHp()
    {
        var scenario = new SampleScenario();
        var setup = scenario.CreateSetup();
        var maxHp = setup.PlayerUnits.Concat(setup.EnemyUnits)
                        .ToDictionary(u => u.Id, u => u.MaxHp);
        var engine = new BattleSession(scenario.Seed);
        var result = engine.Start(setup);
        foreach (var state in result.View.Units)
            Assert.Equal(maxHp[state.UnitId], state.CurrentHp);
    }

    [Fact]
    public void Start_PlayerGoesFirstInSampleScenario()
    {
        // Rogue has AGI=120, the highest initiative in the scenario.
        // AutoAdvance stops immediately because it's a player turn.
        var (_, view) = CreateStartedSession();
        Assert.NotNull(view.PendingInput);
        Assert.Equal("rogue", view.PendingInput!.ActorId);
    }

    [Fact]
    public void Start_PendingInputListsRogueSkills()
    {
        var (_, view) = CreateStartedSession();
        var skills = view.PendingInput!.Skills;
        Assert.NotEmpty(skills);
        // Index 0 is always the free basic action
        Assert.Equal(0, skills[0].Cost);
    }

    [Fact]
    public void Start_PendingInputListsLivingEnemyTargets()
    {
        var (_, view) = CreateStartedSession();
        var enemyIds = view.PendingInput!.EnemyTargetIds;
        Assert.NotEmpty(enemyIds);
    }

    [Fact]
    public void Start_Twice_ReturnsFailed()
    {
        var scenario = new SampleScenario();
        var engine = new BattleSession(scenario.Seed);
        engine.Start(scenario.CreateSetup());
        var second = engine.Start(scenario.CreateSetup());
        Assert.False(second.Success);
        Assert.NotNull(second.Error);
        Assert.Equal(ValidationErrorCode.SessionAlreadyStarted, second.Error!.Code);
    }

    // ── Auto-advance ─────────────────────────────────────────────────────

    [Fact]
    public void AutoAdvance_EventuallyEndsBattle()
    {
        var (engine, _) = CreateStartedSession();

        BattleStepResult? last = null;
        for (int i = 0; i < 500 && (last is null || !last.View.IsOver); i++)
            last = engine.TryExecute(new AdvanceTurnCommand());

        Assert.NotNull(last);
        Assert.True(last!.View.IsOver, "Battle should end within 500 single-turn advances");
        Assert.NotNull(last.View.WinningTeam);
    }

    [Fact]
    public void AutoAdvance_WinnerMatchesBattleEngineResult()
    {
        // Both BattleEngine.Run and BattleSession with the same seed must agree on who wins.
        var scenario = new SampleScenario();

        var engineResult = BattleEngine.Run(scenario.CreateSetup(), scenario.Seed);

        var engine = new BattleSession(scenario.Seed);
        engine.Start(scenario.CreateSetup());
        BattleStepResult last = null!;
        for (int i = 0; i < 500; i++)
        {
            last = engine.TryExecute(new AdvanceTurnCommand());
            if (last.View.IsOver) break;
        }

        Assert.Equal(engineResult.WinningTeam, last.View.WinningTeam);
    }

    [Fact]
    public void AutoAdvance_IsDeterministic()
    {
        static BattleView RunToEnd(IBattleScenario scenario)
        {
            var engine = new BattleSession(scenario.Seed);
            engine.Start(scenario.CreateSetup());
            BattleStepResult last = null!;
            for (int i = 0; i < 500; i++)
            {
                last = engine.TryExecute(new AdvanceTurnCommand());
                if (last.View.IsOver) break;
            }
            return last.View;
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
        var (engine, view) = CreateStartedSession();
        int logAfterStart = view.FullLog.Count;

        var next = engine.TryExecute(new AdvanceTurnCommand());
        Assert.True(next.View.FullLog.Count > logAfterStart,
            "FullLog should grow after advancing a turn");
    }

    // ── Validation rejections ─────────────────────────────────────────────

    [Fact]
    public void TryExecute_BeforeStart_ReturnsNotStarted()
    {
        var engine = new BattleSession(42);
        var result = engine.TryExecute(new AdvanceTurnCommand());
        Assert.False(result.Accepted);
        Assert.Equal(ValidationErrorCode.BattleNotStarted, result.Error!.Code);
    }

    [Fact]
    public void PlayerAction_WithUnknownSkill_ReturnsUnknownSkill()
    {
        var (engine, _) = CreateStartedSession();
        var result = engine.TryExecute(new PlayerActionCommand("nonexistent-skill", null));
        Assert.False(result.Accepted);
        Assert.Equal(ValidationErrorCode.UnknownSkill, result.Error!.Code);
    }

    [Fact]
    public void PlayerAction_WithInvalidTarget_ReturnsInvalidTarget()
    {
        var (engine, view) = CreateStartedSession();
        var basicSkillId = view.PendingInput!.Skills[0].Id;
        var result = engine.TryExecute(new PlayerActionCommand(basicSkillId, "ghost-unit"));
        Assert.False(result.Accepted);
        Assert.Equal(ValidationErrorCode.InvalidTarget, result.Error!.Code);
    }

    [Fact]
    public void RejectedCommand_LeavesStateUnchanged()
    {
        var (engine, view) = CreateStartedSession();
        int logCountBefore = view.FullLog.Count;
        engine.TryExecute(new PlayerActionCommand("nonexistent-skill", null));
        var viewAfter = engine.GetView();
        Assert.Equal(logCountBefore, viewAfter.FullLog.Count);
    }

    // ── Invariants ───────────────────────────────────────────────────────

    [Fact]
    public void Response_NoUnitHpExceedsMax()
    {
        var scenario = new SampleScenario();
        var setup = scenario.CreateSetup();
        var maxHp = setup.PlayerUnits.Concat(setup.EnemyUnits)
                        .ToDictionary(u => u.Id, u => u.MaxHp);

        var engine = new BattleSession(scenario.Seed);
        engine.Start(setup);
        for (int i = 0; i < 50; i++)
        {
            var result = engine.TryExecute(new AdvanceTurnCommand());
            foreach (var state in result.View.Units)
                Assert.True(state.CurrentHp <= maxHp[state.UnitId],
                    $"Unit {state.UnitId} HP {state.CurrentHp} > max {maxHp[state.UnitId]}");
            if (result.View.IsOver) break;
        }
    }
}
