namespace GameCore.Battle;

/// <summary>
/// Base for all messages the client sends to InteractiveBattleSession.
/// </summary>
public abstract record BattleRequest;

/// <summary>Start a fresh battle from the beginning.</summary>
public sealed record StartBattleRequest : BattleRequest;

/// <summary>
/// GameCore picks the best available action (SoulBurn > Skill > Attack based on MP)
/// and a random valid target for the current player unit.
/// </summary>
public sealed record AutoPlayerActionRequest : BattleRequest;

/// <summary>
/// Resume from a mid-battle snapshot. HP/MP come from the snapshot state;
/// turn order picks up after <paramref name="LastActorId"/>.
/// </summary>
public sealed record ResumeFromSnapshotRequest(
    IReadOnlyList<UnitState> State,
    string? LastActorId,
    int AtStep
) : BattleRequest;

/// <summary>Submit a player action targeting a specific unit.</summary>
public sealed record PlayerActionRequest(
    PlayerActionType Action,
    string TargetId
) : BattleRequest;
