# Reactions

A **Reaction** is a 4th skill slot (alongside basic, secondary, ultimate), limited to **one per character**. Unlike regular skills, it is never chosen by the player on their turn — it fires automatically when its trigger condition is met during someone else's action.

## Slot Rules

- [ ] Each character can equip **at most 1** reaction skill
- [ ] Reactions use the `category: Reaction` field in YAML
- [ ] Reactions are never listed in the player's action choices and are never selected by AI as an action
- [ ] On the unit YAML, the reaction slot is declared as a separate `reaction:` field pointing to a skill id
- [ ] Modifiers use the `exclusiveWith: [reaction]` tag to enforce 1-per-unit (same pattern as `basic`/`ultimate`)

## Trigger Model

Each reaction has a `trigger` field declaring when it fires.

Implemented triggers:

| Trigger | Condition | Context |
|---|---|---|
| `OnHitBy` | Unit took ≥ 1 hit from a damaging action. Filter what qualifies via `triggerConditions`. | `sourceUnitId` = the attacker |

### `OnHitBy` Conditions

`triggerConditions` is an optional list of filter objects under a `OnHitBy` trigger.
All conditions in the list are AND-ed (every entry must match). When the list is absent or empty, any damaging hit fires the reaction.

Supported condition keys:

| Key | Values | Description |
|---|---|---|
| `range` | `Melee`, `Ranged`, `Self` | Incoming skill must use this range |
| `damageType` | `Physical`, `Fire`, `Cold`, `Lightning`, `Holy`, `Void` | Incoming skill must include at least one component of this type |

Example — only melee physical hits:
```yaml
trigger: OnHitBy
triggerConditions:
  - range: Melee
    damageType: Physical
```
Example — any fire hit (ranged or melee):
```yaml
trigger: OnHitBy
triggerConditions:
  - damageType: Fire
```

Planned triggers (not yet implemented):

| Trigger | Condition | Context |
|---|---|---|
| `OnAllyKilled` | Any ally is killed | `allyId` = who died |
| `OnHpDropBelow(X%)` | HP crosses a threshold downward for the first time in the battle | — |
| `OnTurnStart` | Start of the reacting unit's own turn | — |

## Execution Rules

- [ ] Reactions fire **after the full triggering action resolves** (not mid-hit), before the next action begins
- [ ] Dead units do not react (if the triggering hit kills them, no reaction fires)
- [ ] Frozen or stunned units do not react (CC fully suppresses reactions)
- [ ] When multiple units qualify simultaneously (e.g. AoE hit), all react in AGI order
- [ ] Reactions cannot trigger other reactions (no chaining — prevents infinite loops)
- [ ] Reactions consume no turn for the reacting unit

## Cooldown

- [ ] Reactions use the same cooldown system as regular skills
- [ ] CD decrements at round start (same as all other skills)
- [ ] When the reaction skill is on CD, the trigger is ignored and it does not fire
- [ ] Typical default CD for a reaction: 2 rounds

## Trigger Context

The trigger provides context about how the condition was met (e.g. `sourceUnitId` for the `OnHitByMelee` trigger). Reaction effects can target the source implicitly — no player or AI targeting decision needed. This is what makes "counter-attack the attacker" work without a target choice.

## YAML Authoring

```yaml
- id: counter-strike
  name: Counter Strike
  category: Reaction
  trigger: OnHitBy
  triggerConditions:
    - range: Melee
  cooldown: 2
  effects:
    - kind: Damage
      target: Enemy
      damagePerHit:
        - damageType: Physical
          scaling:
            - stat: str
              scale: 0.5
```

On the unit:

```yaml
reaction: counter-strike
```

Fields:
- `category: Reaction` — marks this as a reaction skill (never an action choice)
- `trigger` — the trigger type (currently only `OnHitBy`)
- `triggerConditions` — optional list of `{range, damageType}` filter objects (AND-ed); absent = any damaging hit
- `cooldown` — rounds the reaction is unavailable after firing
- `effects` — same effect system as regular skills; `sourceUnitId` from context is the default target

Reactions can also declare `permittedEquipmentTypes` to restrict them to specific weapon users (e.g. a parry reaction only for shield users).

## Implemented Reactions

### Counter Strike

- [x] Trigger: `OnHitBy` with `triggerConditions: [{range: Melee}]`
- [x] Effect: deal physical damage back at the attacker, scaling with STR × 0.5
- [x] Cooldown: 2 rounds
- [x] Flavor: pure melee deterrent — the attacker risks eating a free hit every 2 rounds
- [x] Natural fit: warriors, bruisers, any STR-heavy character

## Planned Reactions

These are design-horizon candidates, not committed features. Add to this list as ideas solidify.

| Name | Trigger | Effect |
|---|---|---|
| Spell Reflect | `OnHitBySpell` | Return portion of magic damage as void/holy to attacker |
| Vengeance | `OnAllyKilled` | Grant full Focus or Fury charge immediately |
| Dodge Step | `OnHitByMelee` | Apply a brief evasion buff instead of counterattacking |
| Mark for Death | `OnHitByAny` | Mark attacker so they take bonus damage from all sources for 1 round |
| Retribution Aura | `OnHitByMelee` | AoE holy damage to all enemies (paladin-style) |
| Death's Defiance | `OnHpDropBelow(20%)` | Grant a barrier or heal equal to X% max HP (once per battle) |

## Implementation Guide

When implementing, follow this order:

1. **`SkillCategory.Reaction`** — new enum value on `BattleSkill`. Reaction skills are filtered from action lists the same way passive skills are.
2. **`ReactionTrigger` enum** — `OnHitByMelee`, etc. Nullable `ReactionTrigger?` field on `BattleSkill`.
3. **`ReactionContext` struct** — carries `trigger, sourceUnitId, targetUnitId` for the effect to consume.
4. **`BattleUnit.ReactionSkill`** — nullable `BattleSkill?` compiled from the unit's `reaction:` YAML field. Kept separate from the `Skills` list.
5. **`IBattleEngine.ExecuteReactions(ReactionContext ctx)`** — called at end of `ExecuteAction`. Iterates all living, non-CC'd units in AGI order; for each, checks if their reaction skill's trigger matches and is not on CD; fires matching ones.
6. Reaction execution is a simplified `ExecuteAction` — same damage and effect pipeline, but emits a distinct event type (e.g. `"reaction"`) so the sandbox and log can display it differently. Does not consume the reacting unit's turn.
7. **Content pipeline** — parse `reaction:` field from unit YAML and `trigger:` field from skill YAML. Validate that `category: Reaction` skills have a `trigger` and that non-reaction skills don't.
8. **Tests** — verify counter-strike fires and deals damage; verify it doesn't fire on a spell hit; verify CD suppresses it; verify dead/frozen units don't react; verify no chaining.

## Open Questions

| Question | Status |
|---|---|
| Do reactions consume Focus / Fury / Mana? Default: No — gated by CD only. Allow opt-in in future. | Open |
| Can a reaction have its own `requiredEquipmentType`? (e.g. parry only for shield users) | Lean Yes |
| Does the AI weight enemy reactions when choosing attack targets? | Start No, revisit during balancing |
| Should reactions contribute to Focus / Fury buildup on the reacting unit? | Open |
| Can a boss have multiple reaction-like behaviors (bypassing the 1-per-unit limit)? | Open — could be a special boss flag |
