using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>
    /// A watch-only (non-interactive) wrapper around any <see cref="IBattleScenario"/>.
    /// The battle runs automatically — the player observes the replay but does not control it.
    /// Use this to expose an existing scenario in the sandbox's replay/observe mode.
    /// </summary>
    public sealed class WatchScenario : IBattleScenario
    {
        private readonly IBattleScenario _source;

        public WatchScenario(IBattleScenario source) => _source = source;

        public string Id => _source.Id + "-watch";
        public string DisplayName => _source.DisplayName + " (Watch)";
        public int Seed => _source.Seed;
        public bool IsPlayable => false;
        public BattleSetup CreateSetup(IContentSource source) => _source.CreateSetup(source);

        public override string ToString() => $"{DisplayName} [{Id}]";
    }
}
