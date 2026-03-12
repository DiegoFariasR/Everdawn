namespace GameCore.Battle;

/// <summary>
/// Runs the damage pipeline for one hit.
/// <para>
/// Each step transforms the current value and records <c>ValueBefore</c> and
/// <c>ValueAfter</c>. The ordered <see cref="DamageResult.Steps"/> list is the
/// full audit trail — use it to debug or predict any hit precisely.
/// </para>
/// <para>
/// To add a new layer: append a step block below the existing ones in <see cref="Compute"/>.
/// Read the current <c>value</c>, compute the transformed result, record a new
/// <see cref="DamageStep"/>, then update <c>value</c>.
/// </para>
/// </summary>
public static class DamageCalc
{
    /// <summary>
    /// Runs every pipeline layer in order and returns the full <see cref="DamageResult"/>.
    /// </summary>
    public static DamageResult Compute(
        BattleUnit actor,
        BattleUnit target,
        DamageType damageType,
        double skillMultiplier,
        double extraMultiplier,
        Random rng)
    {
        var steps = new List<DamageStep>();

        // ── Layer 1: Base ────────────────────────────────────────────────
        // Roll damage from the attacker's stat, skill multiplier, and variance.
        // ValueBefore = flat base stat (for reference). ValueAfter = rolled value.
        int baseAttack = actor.GetBaseAttack(damageType);
        int variance = Math.Max(1, baseAttack / 5);
        int rolled = Math.Max(
            0,
            (int)(baseAttack * skillMultiplier * extraMultiplier) + rng.Next(-variance, variance + 1)
        );
        steps.Add(new DamageStep("Base", baseAttack, rolled));
        int value = rolled;

        // ── Layer 2: Resistance ──────────────────────────────────────────
        // Reduce damage by the defender's resistance percentage for this damage type.
        // 0 = no change. 50 = half damage. 100 = immune. Negative = weakness (extra damage).
        int resistance = target.GetResistance(damageType);
        int afterResistance = Math.Max(0, (int)(value * (1.0 - resistance / 100.0)));
        steps.Add(new DamageStep("Resistance", value, afterResistance));
        value = afterResistance;

        // ── Add future layers here ───────────────────────────────────────

        return new DamageResult(damageType, steps);
    }
}
