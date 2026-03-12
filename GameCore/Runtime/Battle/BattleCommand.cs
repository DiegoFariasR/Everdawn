namespace GameCore.Battle;

/// <summary>
/// Base for all commands the client sends to <see cref="IBattleEngine"/>.
/// Design intent: commands are data-only DTOs, serialization-friendly, no logic.
/// </summary>
public abstract record BattleCommand;

/// <summary>
/// Submit a specific skill targeting a specific unit.
/// For AoE skills, <see cref="TargetId"/> is ignored — all valid targets are hit automatically.
/// For single-target skills, <see cref="TargetId"/> must be a living unit on the correct team.
/// </summary>
public sealed record PlayerActionCommand(string SkillId, string? TargetId) : BattleCommand;

/// <summary>
/// Ask BattleCore to pick the best available action for the current player unit.
/// BattleCore selects the highest-index affordable skill against an auto-chosen target.
/// </summary>
public sealed record AutoPlayerActionCommand : BattleCommand;

/// <summary>
/// Auto-resolve exactly one actor's turn regardless of team.
/// Used by the sandbox to pace every turn individually with a delay.
/// </summary>
public sealed record AdvanceTurnCommand : BattleCommand;

/// <summary>
/// Override the current battle state from a snapshot taken during a deterministic replay.
/// Must be the first command sent after <see cref="IBattleEngine.Start"/>.
/// Enables "take control from here" in the sandbox.
/// </summary>
public sealed record ResumeFromSnapshotCommand(
    IReadOnlyList<UnitState> State,
    string? LastActorId,
    int AtStep
) : BattleCommand;
