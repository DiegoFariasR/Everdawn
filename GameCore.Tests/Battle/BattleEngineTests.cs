using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Scenarios;
using GameCore.Tests;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class BattleEngineTests
    {
        private static readonly IBattleScenario Sample = new SampleScenario();

        [Fact]
        public void Run_ProducesAtLeastThreeSnapshots()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            // Minimum: start + one action + end
            Assert.True(result.Snapshots.Count >= 3,
                $"Expected >= 3 snapshots, got {result.Snapshots.Count}");
        }

        [Fact]
        public void Run_FirstSnapshotIsStart()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            Assert.Equal("start", result.Snapshots[0].Event.Type);
        }

        [Fact]
        public void Run_LastSnapshotIsEnd()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            Assert.Equal("end", result.Snapshots[^1].Event.Type);
        }

        [Fact]
        public void Run_WinningTeamIsPlayerOrEnemy()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            Assert.True(result.WinningTeam == "player" || result.WinningTeam == "enemy",
                $"Unexpected WinningTeam: '{result.WinningTeam}'");
        }

        [Fact]
        public void Run_SeedIsPreservedInResult()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            Assert.Equal(Sample.Seed, result.Seed);
        }

        [Fact]
        public void Run_AllUnitsHaveStateInEverySnapshot()
        {
            var setup = Sample.CreateSetup(TestContentSource.Default);
            var result = BattleEngine.Run(setup, Sample.Seed);
            int expected = setup.PlayerUnits.Count + setup.EnemyUnits.Count;
            foreach (var snapshot in result.Snapshots)
                Assert.Equal(expected, snapshot.UnitStates.Count);
        }

        [Fact]
        public void Run_NoUnitHpExceedsMax()
        {
            var setup = Sample.CreateSetup(TestContentSource.Default);
            var maxHp = setup.PlayerUnits.Concat(setup.EnemyUnits)
                            .ToDictionary(u => u.Id, u => u.MaxHp);
            var result = BattleEngine.Run(setup, Sample.Seed);
            foreach (var snapshot in result.Snapshots)
                foreach (var state in snapshot.UnitStates)
                    Assert.True(state.CurrentHp <= maxHp[state.UnitId],
                        $"Unit {state.UnitId} HP {state.CurrentHp} > max {maxHp[state.UnitId]}");
        }

        [Fact]
        public void Run_DefeatedUnitsStayAtZeroHp()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            var deadUnits = new HashSet<string>();
            foreach (var snapshot in result.Snapshots)
            {
                foreach (var state in snapshot.UnitStates)
                {
                    if (deadUnits.Contains(state.UnitId))
                        Assert.Equal(0, state.CurrentHp);
                    if (state.CurrentHp == 0)
                        deadUnits.Add(state.UnitId);
                }
            }
        }

        [Fact]
        public void Run_SnapshotStepsAreStrictlyAscending()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            for (int i = 1; i < result.Snapshots.Count; i++)
                Assert.True(result.Snapshots[i].Step > result.Snapshots[i - 1].Step,
                    $"Step at [{i}] ({result.Snapshots[i].Step}) is not > [{i - 1}] ({result.Snapshots[i - 1].Step})");
        }

        [Fact]
        public void Run_IsDeterministic()
        {
            var setup = Sample.CreateSetup(TestContentSource.Default);
            var a = BattleEngine.Run(setup, Sample.Seed);
            var b = BattleEngine.Run(setup, Sample.Seed);

            Assert.Equal(a.WinningTeam, b.WinningTeam);
            Assert.Equal(a.Snapshots.Count, b.Snapshots.Count);
            for (int i = 0; i < a.Snapshots.Count; i++)
            {
                Assert.Equal(a.Snapshots[i].Event.Type, b.Snapshots[i].Event.Type);
                Assert.Equal(a.Snapshots[i].Event.ActorId, b.Snapshots[i].Event.ActorId);
                Assert.Equal(a.Snapshots[i].Event.Value, b.Snapshots[i].Event.Value);
            }
        }

        [Fact]
        public void Run_DifferentSeeds_ProduceDifferentResults()
        {
            var setup = Sample.CreateSetup(TestContentSource.Default);
            var a = BattleEngine.Run(setup, 42);
            var b = BattleEngine.Run(setup, 999);
            // Seeds are preserved separately
            Assert.Equal(42, a.Seed);
            Assert.Equal(999, b.Seed);
            // Overwhelmingly likely to differ in a complex battle
            Assert.False(a.Snapshots.Count == b.Snapshots.Count &&
                         a.WinningTeam == b.WinningTeam &&
                         a.Snapshots.Zip(b.Snapshots, (x, y) => x.Event.Value == y.Event.Value).All(v => v),
                "Two different seeds produced byte-identical results, which is astronomically unlikely");
        }

        [Fact]
        public void Run_AtLeastOneDeathEventOccurs()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            Assert.Contains(result.Snapshots, s => s.Event.Type == "death");
        }

        [Fact]
        public void Run_AtLeastOneAttackEventOccurs()
        {
            var result = BattleEngine.Run(Sample.CreateSetup(TestContentSource.Default), Sample.Seed);
            Assert.Contains(result.Snapshots, s => s.Event.Type == "attack");
        }
    }
}
