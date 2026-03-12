using GameCore.Battle;
using GameCore.Scenarios;

namespace GameCore.Tests.Battle;

/// <summary>
/// Tests against the shared scenario definitions in GameCore.Scenarios.
/// Validates scenario structure, and locks down regression anchors
/// so accidental engine or scenario changes are caught immediately.
/// </summary>
public class BattleScenarioTests
{
    // ── Scenario structure ───────────────────────────────────────────────

    [Fact]
    public void SampleScenario_HasCorrectId()
    {
        Assert.Equal("sample-scenario", new SampleScenario().Id);
    }

    [Fact]
    public void SampleScenario_HasDeterministicSeed()
    {
        Assert.Equal(42, new SampleScenario().Seed);
    }

    [Fact]
    public void SampleScenario_IsPlayable()
    {
        Assert.True(new SampleScenario().IsPlayable);
    }

    [Fact]
    public void SampleScenario_HasPlayerAndEnemyUnits()
    {
        var setup = new SampleScenario().CreateSetup();
        Assert.NotEmpty(setup.PlayerUnits);
        Assert.NotEmpty(setup.EnemyUnits);
    }

    [Fact]
    public void SampleScenario_AllUnitsHavePositiveMaxHp()
    {
        var setup = new SampleScenario().CreateSetup();
        foreach (var unit in setup.PlayerUnits.Concat(setup.EnemyUnits))
            Assert.True(unit.MaxHp > 0, $"Unit '{unit.Id}' has MaxHp <= 0");
    }

    [Fact]
    public void SampleScenario_AllUnitsHaveAtLeastOneSkill()
    {
        var setup = new SampleScenario().CreateSetup();
        foreach (var unit in setup.PlayerUnits.Concat(setup.EnemyUnits))
            Assert.NotEmpty(unit.ResolvedSkills);
    }

    [Fact]
    public void SampleScenario_FirstSkillOfEveryUnitIsFree()
    {
        // Rule: index 0 must always be the free basic skill (MpCost == 0).
        var setup = new SampleScenario().CreateSetup();
        foreach (var unit in setup.PlayerUnits.Concat(setup.EnemyUnits))
            Assert.Equal(0, unit.ResolvedSkills[0].MpCost);
    }

    [Fact]
    public void SampleScenario_AllUnitIdsAreUnique()
    {
        var setup = new SampleScenario().CreateSetup();
        var ids = setup.PlayerUnits.Concat(setup.EnemyUnits).Select(u => u.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ── Watch scenario ───────────────────────────────────────────────────

    [Fact]
    public void SampleScenarioWatch_IsNotPlayable()
    {
        Assert.False(new SampleScenarioWatch().IsPlayable);
    }

    [Fact]
    public void SampleScenarioWatch_SharesSeedWithPlayable()
    {
        Assert.Equal(new SampleScenario().Seed, new SampleScenarioWatch().Seed);
    }

    [Fact]
    public void SampleScenarioWatch_ProducesSameResultAsPlayable()
    {
        var playable = new SampleScenario();
        var watch = new SampleScenarioWatch();
        var rPlayable = BattleEngine.Run(playable.CreateSetup(), playable.Seed);
        var rWatch = BattleEngine.Run(watch.CreateSetup(), watch.Seed);
        Assert.Equal(rPlayable.WinningTeam, rWatch.WinningTeam);
        Assert.Equal(rPlayable.Snapshots.Count, rWatch.Snapshots.Count);
    }

    // ── Regression anchors ───────────────────────────────────────────────

    [Fact]
    public void SampleScenario_RunCompletesWithoutException()
    {
        var scenario = new SampleScenario();
        var ex = Record.Exception(() => BattleEngine.Run(scenario.CreateSetup(), scenario.Seed));
        Assert.Null(ex);
    }

    [Fact]
    public void SampleScenario_Seed42_WinnerIsPlayer()
    {
        // Regression anchor: seed 42 always produces a player victory.
        // If this changes, the engine or scenario definition changed non-trivially.
        var scenario = new SampleScenario();
        var result = BattleEngine.Run(scenario.CreateSetup(), scenario.Seed);
        Assert.Equal("player", result.WinningTeam);
    }

    [Fact]
    public void SampleScenario_Seed42_SnapshotCountIsStable()
    {
        // Regression anchor: exact battle length for seed 42.
        // Updated intentionally when the scenario or engine changes.
        // Value established by running the engine and recording the output.
        var scenario = new SampleScenario();
        var result = BattleEngine.Run(scenario.CreateSetup(), scenario.Seed);
        Assert.Equal(BattleScenarioAnchors.SampleSeed42SnapshotCount, result.Snapshots.Count);
    }
}

/// <summary>
/// Holds hardcoded regression anchor values for known scenarios.
/// Update these intentionally when the scenario or engine changes.
/// </summary>
internal static class BattleScenarioAnchors
{
    // BattleEngine.Run(SampleScenario, seed=42) produces exactly 64 snapshots:
    // 1 start + 56 attack events + 6 death events + 1 end = 64
    public const int SampleSeed42SnapshotCount = 64;
}
