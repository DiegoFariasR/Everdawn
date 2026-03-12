namespace GameCore.Battle;

// Internal request types — hidden behind the BattleSession public API.
// The client communicates through BattleCommand types defined in BattleCommand.cs.
internal abstract record BattleRequest;
internal sealed record StartBattleRequest : BattleRequest;
internal sealed record AutoPlayerActionRequest : BattleRequest;
internal sealed record ResumeFromSnapshotRequest(
    IReadOnlyList<UnitState> State,
    string? LastActorId,
    int AtStep
) : BattleRequest;
internal sealed record PlayerActionRequest(string SkillId, string? TargetId) : BattleRequest;
internal sealed record AdvanceOneTurnRequest : BattleRequest;
