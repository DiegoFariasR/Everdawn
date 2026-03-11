using GameCore.Battle;

namespace GameCore.Scenarios;

/// <summary>
/// Watch-mode (auto-replay) version of the sample scenario.
/// Uses the same setup and seed — just observed, not controlled.
/// </summary>
public class SampleScenarioWatch : IBattleScenario
{
    private readonly SampleScenario _inner = new();

    public string Id => "sample-scenario-watch";
    public string DisplayName => "Sample Battle (Watch)";
    public int Seed => _inner.Seed;
    public bool IsPlayable => false;
    public BattleSetup CreateSetup() => _inner.CreateSetup();
}
