# Damage Pipeline

Authoritative flow for how a single attack action resolves from skill selection to
HP reduction. Every node maps to a specific piece of code — update both together.

---

## Flow diagram

```mermaid
flowchart TD
    A([Actor's turn]) --> PRE

    subgraph PRE["Pre-hit resolution  (once per action)"]
        CDT["Tick actor cooldowns"] --> RSK
        RSK["Resolve effective skill<br/>(apply runtime skill modifiers from active effects)"] --> CST
        CST["Consume skill cost from bar (MP or Focus)"] --> FCS
        FCS{"Focused buff active<br/>AND skill is Attack or Spell?"}
        FCS -- yes --> FCA["Consume Focused buff<br/>Emit 'sharpens' event"]
        FCS -- no  --> HCR
        FCA --> HCR["Resolve effective hit count<br/>· BaseHits/ScalingHits override  OR  AGI-derived<br/>· Slow: AGI hits halved · Focus: +ExtraHits from buff"]
        HCR --> EMP["Compute empowerMult<br/>= DamageDealtMultiplier from active effects<br/>× furyDamageMult  (FuryUser: 1.0 + FuryDamageScale × fury/100)"]
        EMP --> DIZ["Snapshot actorIsDizzy  (Disruption bar ≥ 50)"]
    end

    DIZ --> LOOP

    subgraph LOOP["Per-hit loop  (repeated effectiveHits times, per target)"]

        subgraph PIPE["DamageCalc.Compute  — full audit trail in DamageResult.Steps"]
            L1["Layer 1 — Base<br/>Σ (stat × scaling) × TotalDamageMultiplier × empowerMult × perHitMult<br/>± variance (±20 % of stat base)"]
            L1 --> L2["Layer 2 — Resistance<br/>effectiveResistance = target.Resistance − actor.Penetration<br/>capped at 90  →  always ≥ 10 % lands<br/>× (1 − resistance / 100)"]
            L2 --> L3["Layer 3 — OutgoingTypeMult<br/>actor's per-type dealt multiplier from active effects<br/>skipped when = 1.0"]
            L3 --> L4["Layer 4 — IncomingTypeMult<br/>target's per-type taken multiplier from active effects<br/>skipped when = 1.0"]
            L4 --> L5["Layer 5 — AttackerOutput<br/>Dizzy → × 0.8<br/>skipped when = 1.0"]
            L5 --> L6["Layer 6 — DamageTaken<br/>flat defender-side multiplier from active effects<br/>skipped when = 1.0"]
            L6 --> SUM["totalDamage = Σ FinalDamage across all DamageComponents"]
        end

        SUM --> BAR{"Target has Barrier?"}
        BAR -- yes --> BABSORB["Barrier absorbs min(barrier, totalDamage)<br/>Remainder applied to HP"]
        BAR -- no  --> HP["Full totalDamage applied to HP"]
        BABSORB --> PHIT
        HP --> PHIT["Fury gain for target  (FuryUser: flat + HP% bonus per hit)<br/>CC buildup: Thermal / Disruption / Bleed"]
        PHIT --> DEAD{"Target HP ≤ 0?"}
        DEAD -- yes --> KILL([Target defeated · break hit loop])
        DEAD -- no  --> MORE{"More hits?"}
        MORE -- yes --> L1
        MORE -- no  --> ACT
    end

    ACT([End of action]) --> W["Apply skill cooldown<br/>Actor Fury gain  (FuryUser + direct damage: +SkillUseGain once per action)<br/>Tick active effects<br/>Execute reactions<br/>CheckEnd"]

    style PRE  fill:#1a1a2e,stroke:#4e4e8f,color:#ccc
    style LOOP fill:#12232e,stroke:#4e8f6e,color:#ccc
    style PIPE fill:#0f1b25,stroke:#2e6f8f,color:#ccc
```

---

## Traceability table

Every node in the diagram maps to a named location in code. When you change the
pipeline, update the matching row here and the Mermaid above.

| Diagram node | `DamageStep` name in `DamageResult.Steps` | Code location |
|---|---|---|
| Tick cooldowns | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — cooldown tick loop at top |
| Resolve effective skill | *(not in pipeline)* | `InteractiveBattleSession.ResolveEffectiveSkill` |
| Consume skill cost | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `CostBarKey` deduction block |
| Focus ExtraHits | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `isFocusEmpowered` + `ExtraHits` block |
| Hit count (AGI / BaseHits) | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `effectiveHits` resolution block |
| empowerMult / Fury scaling | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `furyDamageMult` + `GetFlatDamageDealtMultiplier` block |
| actorIsDizzy snapshot | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `actorIsDizzy` line before target loop |
| **Layer 1 — Base** | `"Base"` | `DamageCalc.cs` — Layer 1 block |
| **Layer 2 — Resistance** | `"Resistance"` | `DamageCalc.cs` — Layer 2 block |
| **Layer 3 — OutgoingTypeMult** | `"OutgoingTypeMult"` | `DamageCalc.cs` — Layer 3 block |
| **Layer 4 — IncomingTypeMult** | `"IncomingTypeMult"` | `DamageCalc.cs` — Layer 4 block |
| **Layer 5 — AttackerOutput** | `"AttackerOutput"` | `DamageCalc.cs` — Layer 5 block |
| **Layer 6 — DamageTaken** | `"DamageTaken"` | `DamageCalc.cs` — Layer 6 block |
| Barrier absorption | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — barrier block after `DamageCalc.Compute` |
| Fury gain (target) | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `FurySystem.ComputeHitGain` block inside hit loop |
| CC buildup | *(not in pipeline)* | `InteractiveBattleSession.ApplyCCBuildup` + `ApplyDisruptionBuildup` |
| Actor Fury gain (skill use) | *(not in pipeline)* | `InteractiveBattleSession.ExecuteAction` — `SkillUseGain` block after hit loop |
| Tick active effects | *(not in pipeline)* | `InteractiveBattleSession.TickActiveEffects` |
| Execute reactions | *(not in pipeline)* | `InteractiveBattleSession.ExecuteReactions` |

> `DamageStep` names are the string keys you query in `DamageResult.Steps` and in the
> BattleSandbox hit log. They are the canonical identifiers — keep diagram, table, and
> code strings in sync.

---

## Rules for adding a new layer

1. Append a step block inside `DamageCalc.Compute` below the last layer comment.
2. Use a `// ── Layer N: Name ──` comment and a `new DamageStep("Name", ...)` call.
3. Add a row to the traceability table above with the exact `DamageStep` name.
4. Update the Mermaid diagram.

Only add to `DamageCalc.Compute` things that are **per-component multipliers**.
HP routing (barrier) and hit-count mechanics stay in `ExecuteAction`.
