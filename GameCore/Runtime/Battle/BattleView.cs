#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    public sealed record SkillView(
        string Id,
        string Name,
        int Cost,
        double TotalDamageMultiplier,
        bool IsAoe,
        bool IsHeal,
        int Cooldown,
        BattleSkillTarget Target,
        EffectType? PrimaryEffectType,
        bool IsBasic,
        bool IsUltimate,
        int EffectiveInitialCooldown,
        int BaseDmg,
        double BaseHits,
        IReadOnlyList<DamageScaling> ScalingHits,
        SkillRange Range,
        SkillCategory Category
    );

    public sealed record PendingInputView(
        string ActorId,
        string ActorName,
        IReadOnlyList<SkillView> Skills,
        IReadOnlyList<string> AvailableSkillIds,
        IReadOnlyList<string> EnemyTargetIds,
        IReadOnlyList<string> AllyTargetIds,
        IReadOnlyDictionary<string, int> SkillCooldowns // only entries where cooldown > 0
    );

    // Authoritative battle state seen by the client. Never holds logic — only data to render.
    public sealed record BattleView(
        IReadOnlyList<UnitState> Units,
        PendingInputView? PendingInput,  // non-null when player input is required
        IReadOnlyList<BattleEvent> FullLog,
        bool IsOver,
        string? WinningTeam,
        int Round = 1  // 1-based; increments at end of each full turn-order cycle
    );
}
