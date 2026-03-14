using System;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;
namespace GameCore.Scenarios
{
    /// <summary>
    /// Concrete implementation of <see cref="IBattleSandboxEngine"/>.
    /// <para>
    /// Constructed with an <see cref="IContentSource"/> (supplied by the host, e.g. HTTP for Blazor WASM
    /// or the file system for tests). All GameCore concerns — content loading, scenario resolution,
    /// and session lifecycle — are handled here so the presentation layer never touches them directly.
    /// </para>
    /// </summary>
    public sealed class BattleSandboxEngine : IBattleSandboxEngine
    {
        private readonly IContentSource _source;
        private IBattleEngine? _session;

        public BattleSandboxEngine(IContentSource source)
        {
            _source = source;
        }

        public BattleStartResult StartBattle(string scenarioId)
        {
            var scenario = GetScenario(scenarioId);
            var setup = scenario.CreateSetup(_source);
            _session = new BattleSession(scenario.Seed);
            return _session.Start(setup);
        }

        public BattleStepResult ExecuteCommand(BattleCommand command)
        {
            if (_session == null)
                throw new InvalidOperationException("No active battle. Call StartBattle first.");
            return _session.TryExecute(command);
        }

        public BattleResult RunFull(string scenarioId)
        {
            var scenario = GetScenario(scenarioId);
            var setup = scenario.CreateSetup(_source);
            return BattleSession.RunFull(setup, scenario.Seed);
        }

        private static IBattleScenario GetScenario(string scenarioId)
        {
            var scenario = ScenarioRegistry.All.FirstOrDefault(s => s.Id == scenarioId);
            if (scenario == null)
                throw new ArgumentException($"Unknown scenario: '{scenarioId}'.", nameof(scenarioId));
            return scenario;
        }
    }
}
