namespace GameCore.Battle
{
    /// <summary>
    /// Represents a client's intent to begin a battle. This is the client-side request,
    /// not the authoritative battle configuration.
    /// <para>
    /// <b>Architecture note — client intent vs. backend authority:</b>
    /// <list type="bullet">
    ///   <item>This type is what the <em>client</em> sends when it wants to start a battle.</item>
    ///   <item><see cref="BattleSetup"/> is what <see cref="IBattleEngine"/> actually runs on —
    ///   it is always produced by the backend from authoritative data sources, never by the client.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Backend-authoritative flow (production):</b>
    /// <list type="number">
    ///   <item>Client sends <see cref="StartBattleRequest"/> to the backend.</item>
    ///   <item>Backend validates account/world/save state.</item>
    ///   <item>Backend resolves ally roster, stats, enemy composition, encounter rules, and seed
    ///   into an authoritative <see cref="BattleSetup"/>.</item>
    ///   <item>Backend calls <see cref="IBattleEngine.Start"/> with that setup.</item>
    ///   <item>Client receives and renders the initial <see cref="BattleView"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Offline/local mode (current):</b> the game flow layer acts as the backend and
    /// resolves this request into a <see cref="BattleSetup"/> locally. This is a temporary
    /// local-mode adapter — the authority model is unchanged.
    /// </para>
    /// </summary>
    public sealed record StartBattleRequest(
        /// <summary>
        /// Identifies the encounter or scenario the client wants to start.
        /// The backend resolves enemy composition, map rules, and encounter modifiers from this.
        /// </summary>
        string EncounterId,

        /// <summary>
        /// Identifies the party entering the battle.
        /// Null means use the current active party from the player's save state.
        /// </summary>
        string? PartyId = null
    );
}
