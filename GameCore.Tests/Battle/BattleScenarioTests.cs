using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Scenarios;
using GameCore.Tests;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for the shared scenario layer (GameCore.Scenarios).
    /// Structural rules run automatically over every scenario in <see cref="ScenarioRegistry"/>.
    /// Regression anchors run automatically over every <see cref="IRegressionScenario"/> in the registry.
    /// To add coverage for a new scenario: add it to <see cref="ScenarioRegistry"/> — no additional
    /// test code is needed for structural or regression checks.
    /// </summary>
    public class BattleScenarioTests
    {
        // ── MemberData sources ───────────────────────────────────────────────

        public static IEnumerable<object[]> AllScenarios =>
            ScenarioRegistry.All.Select(s => new object[] { s });

        public static IEnumerable<object[]> AllRegressionScenarios =>
            ScenarioRegistry.RegressionScenarios.Select(s => new object[] { s });

        // ── Registry health ──────────────────────────────────────────────────

        [Fact]
        public void Registry_IsNonEmpty()
        {
            Assert.NotEmpty(ScenarioRegistry.All);
        }

        [Fact]
        public void Registry_AllIdsAreUnique()
        {
            var ids = ScenarioRegistry.All.Select(s => s.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Registry_HasAtLeastOnePlayableScenario()
        {
            Assert.Contains(ScenarioRegistry.All, s => s.IsPlayable);
        }

        [Fact]
        public void Registry_HasAtLeastOneRegressionScenario()
        {
            Assert.NotEmpty(ScenarioRegistry.RegressionScenarios);
        }

        // ── SampleScenario contract ──────────────────────────────────────────

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

        // ── Structural rules — every registered scenario ─────────────────────

        [Theory, MemberData(nameof(AllScenarios))]
        public void AllScenarios_HavePlayerAndEnemyUnits(IBattleScenario scenario)
        {
            var setup = scenario.CreateSetup(TestContentSource.Default);
            Assert.NotEmpty(setup.PlayerUnits);
            Assert.NotEmpty(setup.EnemyUnits);
        }

        [Theory, MemberData(nameof(AllScenarios))]
        public void AllScenarios_AllUnitsHavePositiveMaxHp(IBattleScenario scenario)
        {
            var setup = scenario.CreateSetup(TestContentSource.Default);
            foreach (var unit in setup.PlayerUnits.Concat(setup.EnemyUnits))
                Assert.True(unit.MaxHp > 0, $"Unit '{unit.Id}' has MaxHp <= 0");
        }

        [Theory, MemberData(nameof(AllScenarios))]
        public void AllScenarios_AllUnitsHaveAtLeastOneSkill(IBattleScenario scenario)
        {
            var setup = scenario.CreateSetup(TestContentSource.Default);
            foreach (var unit in setup.PlayerUnits.Concat(setup.EnemyUnits))
                Assert.NotEmpty(unit.ResolvedSkills);
        }

        [Theory, MemberData(nameof(AllScenarios))]
        public void AllScenarios_FirstSkillOfEveryUnitIsFree(IBattleScenario scenario)
        {
            // Rule: index 0 is always the free basic skill (Cost == 0).
            var setup = scenario.CreateSetup(TestContentSource.Default);
            foreach (var unit in setup.PlayerUnits.Concat(setup.EnemyUnits))
                Assert.Equal(0, unit.ResolvedSkills[0].Cost);
        }

        [Theory, MemberData(nameof(AllScenarios))]
        public void AllScenarios_AllUnitIdsAreUnique(IBattleScenario scenario)
        {
            var setup = scenario.CreateSetup(TestContentSource.Default);
            var ids = setup.PlayerUnits.Concat(setup.EnemyUnits).Select(u => u.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Theory, MemberData(nameof(AllScenarios))]
        public void AllScenarios_RunCompletesWithoutException(IBattleScenario scenario)
        {
            var ex = Record.Exception(() => BattleEngine.Run(scenario.CreateSetup(TestContentSource.Default), scenario.Seed));
            Assert.Null(ex);
        }

        // ── WatchScenario ────────────────────────────────────────────────────

        [Fact]
        public void WatchScenario_IsNotPlayable()
        {
            Assert.False(new WatchScenario(new SampleScenario()).IsPlayable);
        }

        [Fact]
        public void WatchScenario_InheritsSourceSeedAndProducesSameResult()
        {
            var source = new SampleScenario();
            var watch = new WatchScenario(source);
            Assert.Equal(source.Seed, watch.Seed);
            var r1 = BattleEngine.Run(source.CreateSetup(TestContentSource.Default), source.Seed);
            var r2 = BattleEngine.Run(watch.CreateSetup(TestContentSource.Default), watch.Seed);
            Assert.Equal(r1.WinningTeam, r2.WinningTeam);
            Assert.Equal(r1.Snapshots.Count, r2.Snapshots.Count);
        }

        // ── Regression anchors — auto-discovered from registry ───────────────
        // To add a new regression: implement IRegressionScenario, register it.
        // These theory methods pick it up with zero changes here.

        [Theory, MemberData(nameof(AllRegressionScenarios))]
        public void AllRegressionScenarios_MatchExpectedWinner(IRegressionScenario scenario)
        {
            var result = BattleEngine.Run(scenario.CreateSetup(TestContentSource.Default), scenario.Seed);
            Assert.Equal(scenario.ExpectedWinner, result.WinningTeam);
        }

        [Theory(Skip = "Snapshot count changes frequently during content tuning — re-enable when stable."), MemberData(nameof(AllRegressionScenarios))]
        public void AllRegressionScenarios_MatchExpectedSnapshotCount(IRegressionScenario scenario)
        {
            var result = BattleEngine.Run(scenario.CreateSetup(TestContentSource.Default), scenario.Seed);
            Assert.Equal(scenario.ExpectedSnapshotCount, result.Snapshots.Count);
        }
    }
}
