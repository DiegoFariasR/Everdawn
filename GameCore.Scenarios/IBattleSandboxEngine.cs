using System.Collections.Generic;
using GameCore.Battle;
namespace GameCore.Scenarios
{
    /// <summary>
    /// The presentation layer's single entry point for driving a battle in the sandbox.
    /// <para>
    /// Encapsulates all GameCore concerns: content loading, scenario setup, and session management.
    /// The caller only sees view types (<see cref="BattleStartResult"/>, <see cref="BattleStepResult"/>,
    /// <see cref="BattleResult"/>) — never raw <see cref="BattleSetup"/> or <see cref="BattleUnit"/>.
    /// </para>
    /// </summary>
    public interface IBattleSandboxEngine
    {
        /// <summary>
        /// Starts (or restarts) a battle for the given scenario.
        /// Resets any in-progress session. Returns unit display info alongside the initial view.
        /// </summary>
        BattleStartResult StartBattle(string scenarioId);

        /// <summary>
        /// Executes a command against the current session.
        /// Call <see cref="StartBattle"/> before this.
        /// </summary>
        BattleStepResult ExecuteCommand(BattleCommand command);

        /// <summary>
        /// Runs a full AI battle for the given scenario and returns all snapshots.
        /// Does not disturb any active play-mode session.
        /// </summary>
        BattleResult RunFull(string scenarioId);
    }
}
