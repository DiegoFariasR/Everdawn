using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>A deterministic battle scenario: a fixed setup and seed that always produces the same battle.</summary>
    public interface IBattleScenario
    {
        string Id { get; }
        string DisplayName { get; }
        int Seed { get; }
        bool IsPlayable { get; }

        /// <summary>Creates the fully-resolved <see cref="BattleSetup"/> for this scenario.</summary>
        /// <param name="source">Content source pointing at the <c>GameData/Base</c> root.</param>
        BattleSetup CreateSetup(IContentSource source);
    }
}
