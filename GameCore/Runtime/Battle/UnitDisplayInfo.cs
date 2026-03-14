#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Static per-unit info for rendering purposes.
    /// Built once at battle start from <see cref="BattleUnit"/> and passed to the presentation layer.
    /// <para>
    /// Produced by <see cref="BattleSession"/> and included in <see cref="BattleStartResult"/> and
    /// <see cref="BattleResult"/> so that the client never needs to hold a raw <see cref="BattleUnit"/>.
    /// </para>
    /// </summary>
    public sealed record UnitDisplayInfo(
        string Id,
        string Name,
        string Team,
        int Level,
        int MaxHp,
        int Str,
        int Wis,
        int Agi,
        int PhysAttack,
        int MagicAttack,
        int Initiative,
        int HitCount,
        IReadOnlyDictionary<string, int> MaxBars,
        IReadOnlyList<BattleTrait> Traits,
        IReadOnlyDictionary<EffectType, int> Resistances,
        IReadOnlyList<SkillDisplayInfo> Skills
    );

    /// <summary>
    /// A pre-computed view of a single skill — enough for the presentation layer to render skill
    /// buttons, stat labels, and estimated damage without holding a <see cref="BattleSkill"/>.
    /// </summary>
    public sealed record SkillDisplayInfo(
        string Id,
        string Name,
        bool IsBasic,
        bool IsUltimate,
        EffectType? PrimaryEffectType,
        double DamageMultiplier,
        double BaseHits,
        int Cost,
        bool IsAoe,
        BattleSkillTarget Target,
        int Cooldown,
        int EffectiveInitialCooldown,
        int BaseDmg,
        SkillRange Range
    );
}
