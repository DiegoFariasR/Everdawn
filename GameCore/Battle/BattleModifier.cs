namespace GameCore.Battle;

/// <summary>
/// A skill modifier definition, loaded from content data.
/// Modifiers describe how a skill is used (e.g. as a basic action or ultimate).
/// Any non-null stat field overrides the corresponding field on the base skill
/// when the modifier is applied during content compilation.
/// </summary>
public record BattleModifier(
    string Id,
    string Name,
    string Description,
    int? Cost = null,
    double? Multiplier = null,
    bool? IsAoe = null,
    BattleSkillTarget? Target = null,
    EffectKind? Kind = null,
    int? Cooldown = null,
    int? InitialCooldown = null,
    EffectType? EffectType = null
);
