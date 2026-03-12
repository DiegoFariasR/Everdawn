namespace GameCore.Battle;

/// <summary>
/// Identifies the category of a validation failure returned by <see cref="IBattleEngine"/>.
/// </summary>
public enum ValidationErrorCode
{
    /// <summary><see cref="IBattleEngine.Start"/> must be called before executing commands.</summary>
    BattleNotStarted,
    /// <summary><see cref="IBattleEngine.Start"/> was already called on this session.</summary>
    SessionAlreadyStarted,
    /// <summary>The battle has ended — no further commands are accepted.</summary>
    BattleAlreadyOver,
    /// <summary>A <see cref="PlayerActionCommand"/> was sent when it is not the player's turn.</summary>
    NotPlayerTurn,
    /// <summary>The specified skill ID does not exist on the current actor.</summary>
    UnknownSkill,
    /// <summary>The skill is on cooldown and cannot be used this turn.</summary>
    SkillOnCooldown,
    /// <summary>The actor does not have enough MP for the skill.</summary>
    InsufficientMp,
    /// <summary>The target ID is not a valid living target for the selected skill.</summary>
    InvalidTarget,
}

/// <summary>
/// Describes why a command was rejected by <see cref="IBattleEngine.TryExecute"/>.
/// Included in <see cref="BattleStepResult"/> when <see cref="BattleStepResult.Accepted"/> is false.
/// </summary>
public sealed record ValidationError(ValidationErrorCode Code, string Message);
