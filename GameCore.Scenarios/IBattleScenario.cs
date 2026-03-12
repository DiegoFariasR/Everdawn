using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>
    /// A deterministic battle scenario: a fixed setup and seed that always produces the same battle.
    /// <para>
    /// <b>Architecture note:</b> Scenarios do not discover content paths on their own.
    /// Callers must supply an <see cref="IContentSource"/> pointing at the appropriate
    /// <c>GameData/Base</c> root. This keeps scenario code host-agnostic and testable
    /// without file-system assumptions baked in.
    /// </para>
    /// </summary>
    public interface IBattleScenario
    {
        string Id { get; }
        string DisplayName { get; }
        int Seed { get; }
        bool IsPlayable { get; }

        /// <summary>
        /// Creates the fully-resolved <see cref="BattleSetup"/> for this scenario.
        /// </summary>
        /// <param name="source">
        /// Content source pointing at the <c>GameData/Base</c> root.
        /// Use <see cref="FileSystemContentSource"/> for local/test environments.
        /// </param>
        BattleSetup CreateSetup(IContentSource source);
    }
}
