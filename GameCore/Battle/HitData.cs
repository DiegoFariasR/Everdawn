namespace GameCore.Battle;

/// <summary>
/// One layer of the damage pipeline. Records the value entering the step and
/// the value it produced, so results can be fully traced and debugged.
/// </summary>
public record DamageStep(string Name, int ValueBefore, int ValueAfter);

/// <summary>
/// The fully resolved result of one hit after all pipeline layers have run.
/// <para>
/// <see cref="Steps"/> is the ordered audit trail — inspect it to see exactly
/// how each layer transformed the value. Every step was applied in sequence.
/// </para>
/// <para>
/// <see cref="RawDamage"/> is the output of the first step ("Base"): what the
/// attacker rolled before any mitigation.
/// <see cref="FinalDamage"/> is the output of the last step: what the defender
/// actually takes.
/// </para>
/// </summary>
public record DamageResult(DamageType DamageType, IReadOnlyList<DamageStep> Steps)
{
    /// <summary>Output of the "Base" step — damage before any mitigation.</summary>
    public int RawDamage => Steps.Count > 0 ? Steps[0].ValueAfter : 0;
    /// <summary>Output of the last pipeline step — damage the defender actually takes.</summary>
    public int FinalDamage => Steps.Count > 0 ? Steps[Steps.Count - 1].ValueAfter : 0;
};
