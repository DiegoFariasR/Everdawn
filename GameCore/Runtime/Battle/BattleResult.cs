using System;
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// The full result of a completed battle: all snapshots and the winning team.
    /// </summary>
    public class BattleResult
    {
        public IReadOnlyList<BattleSnapshot> Snapshots { get; init; } = Array.Empty<BattleSnapshot>();
        public string WinningTeam { get; init; } = string.Empty;
        public int Seed { get; init; }
        public IReadOnlyList<UnitDisplayInfo> PlayerUnits { get; init; } = Array.Empty<UnitDisplayInfo>();
        public IReadOnlyList<UnitDisplayInfo> EnemyUnits { get; init; } = Array.Empty<UnitDisplayInfo>();
    }
}
