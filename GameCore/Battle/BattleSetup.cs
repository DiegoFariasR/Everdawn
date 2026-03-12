namespace GameCore.Battle;

/// <summary>
/// The authoritative, fully-resolved configuration for a battle. This is what
/// <see cref="IBattleEngine"/> runs on — it is never constructed by the client directly
/// in production.
/// <para>
/// <b>Architecture note:</b> In the backend-authoritative model, this is produced by the
/// backend after validating a <see cref="StartBattleRequest"/>. The backend resolves unit
/// rosters, derived stats, equipment, learned skills, encounter rules, the RNG seed,
/// and all other combat configuration before passing this to <see cref="IBattleEngine.Start"/>.
/// The client never touches setup assembly in production — it only sends intent
/// (see <see cref="StartBattleRequest"/>) and receives results.
/// </para>
/// <para>
/// In the current offline/local mode, the game flow layer temporarily acts as the
/// backend and resolves setup locally. This is a local-mode adapter, not a change
/// to the authority model.
/// </para>
/// </summary>
public class BattleSetup
{
    public required IReadOnlyList<BattleUnit> PlayerUnits { get; init; }
    public required IReadOnlyList<BattleUnit> EnemyUnits { get; init; }
}
