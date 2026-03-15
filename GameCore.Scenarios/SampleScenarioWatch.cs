using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
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
