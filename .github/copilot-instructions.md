# Everdawn — Project Guidelines

This file is the living reference for AI assistants and contributors working on this project. Update it whenever a decision is made or a system changes.

---

## Project Overview

Everdawn is a single-player, offline, turn-based tactical RPG built in Unity. The architecture is designed with the roots of an online game: the Unity side is a thin presentation/client layer, and the main game logic lives in a plain C# core that behaves like an in-process backend.

The repository is public for free GitHub Actions CI and GitHub Pages hosting. Public does not mean open for random edits — it is a controlled project.

---

## Architecture Rules

### GameCore (plain C#, no Unity dependency)
GameCore is the **source of truth** for all gameplay logic. It must never reference `UnityEngine`. This allows it to be:
- tested with standard .NET tests (no Unity required)
- reused by other frontends (web sandbox, future clients)

GameCore contains:
- World map / location logic
- Activities
- Event flow logic
- Battle setup and rules
- Quests
- Inventory
- Progression
- Saveable game state
- Flow / state control
- Content pipeline and database

### UnityClient (presentation layer)
- The Unity project root is `UnityClient/`, not the repo root.
- UnityClient references GameCore as a local package/module/assembly.
- Responsible for: presentation, UI, sound, VFX, animation hooks, scene wiring, platform concerns.
- **Must not** decide game flow transitions — GameCore decides, UnityClient renders.
- **Must not** contain gameplay truth.

### BattleSandbox.Web (minimal battle viewer)
- A tiny, phone-friendly web app hosted on GitHub Pages.
- Focused only on battle scenario playback and stop-motion inspection.
- Allows: selecting a scenario, replaying in stop-motion, inspecting events/state/results.
- It is a battle debugger, not a second full client. It must not try to reproduce the full game.
- Does **not** run the test framework directly — it uses shared scenarios.

### GameCore.Scenarios (shared deterministic scenarios)
- Defines deterministic scenarios used by both `GameCore.Tests` and `BattleSandbox.Web`.
- A scenario includes enough data to reproduce a battle consistently: setup, seed, actions/behavior.
- Supports regression scenarios from previous bugs/failures.

### GameCore.Tests (.NET tests)
- Pull requests run GameCore.Tests via GitHub Actions.
- Battle correctness lives here, not in Unity-based tests.
- CI must be able to run without opening Unity.
- UnityClient.Tests can exist later for Unity-specific behavior.

---

## C# Language Version

All projects in the solution use **C# 9.0**, set explicitly via `<LangVersion>9.0</LangVersion>` in every `.csproj`.

**Rationale:** Unity 6.3 uses Roslyn with C# 9.0. `GameCore` is shared with UnityClient, so it must stay within C# 9 syntax. The whole solution uses the same version for consistency — tests, scenarios, and the web sandbox do not use newer language features even though their target frameworks would allow it.

**Rules:**
- Never use C# 10+ syntax anywhere in the solution (file-scoped namespaces, global usings declared in code, record structs, `CallerArgumentExpression`, etc.).
- `GameCore` already enforces `LangVersion>9</LangVersion>` and `ImplicitUsings>disable</ImplicitUsings>`. Other projects set `LangVersion>9.0</LangVersion>` and may use `ImplicitUsings>enable</ImplicitUsings>`.
- **Exception:** `BattleSandbox.Web` uses `LangVersion=12.0`. The Blazor WebAssembly SDK generates `global using` directives (C# 10 feature) from its package dependencies, and `Home.razor` uses C# 12 collection expressions. Since the web sandbox shares no source files with Unity and is fully isolated from the Unity build chain, it may use a higher language version. Its `.cs` files use explicit usings (no `ImplicitUsings`) for consistency with the rest of the solution.
- If Unity bumps its supported C# version, update all projects together and update this section.

---

## Game Flow Model

- The game is **action-driven, not time-driven**. Nothing evolves by time alone.
- Game state only changes when the player performs an action.
- Saving is only allowed in stable, non-event states.

### Flow Concepts

| Concept | Description |
|---|---|
| **Location** | A place on the world map (cities are just Locations with multiple Activities) |
| **Activity** | A selectable option inside a Location |
| **EventFlow** | The ordered or branching sequence started by an Activity |
| **EventNode** | One node inside an EventFlow (Conversation, Shop, Loot, Battle, Choice) |

### Location and Activity Rules
- If a Location has exactly one available Activity, auto-enter it (no selection screen).
- If it has multiple, show the list.
- Activities start linear or branching EventFlows.
- EventFlows chain multiple EventNodes in sequence or branch on decisions.

### State Separation
- **Persistent state**: the saveable truth (inventory, progression, quest state, world state).
- **Flow state**: where the player currently is and what actions are valid (not saved mid-event).
- Top-level modes: **Exploration** and **Event**.
- Save is only allowed when no active event is running.

---

## Battle Architecture

- Battle logic must be fully testable without Unity.
- Battles must be **deterministic** given the same scenario and seed.
- GameCore produces battle events or snapshots in small steps to support stop-motion playback.
- The battle system supports step-by-step inspection:
  - next event
  - next action
  - next turn
  - replay from start

### Skill Requirements
Skills can declare requirements a unit must meet to use them. Two kinds:
- **`PermittedTraits`** (`IReadOnlyList<BattleTrait>?`) — unit must have at least one of the listed traits (e.g. `ManaUser` for spells).
- **`PermittedEquipmentTypes`** (`IReadOnlyList<EquipmentType>?`) — unit must carry one of the listed equipment types (e.g. `[Blunt]` for mace skills, or `[Blunt, Slash]` to accept multiple).

`BattleSkill.MeetsRequirements(actor)` tests both. `InteractiveBattleSession` applies it to `AvailableSkillIds` and AI skill selection. `ValidationErrorCode.RequirementNotMet` is returned when a player submits a disqualified skill.

`EquipmentType` values: `None`, `Blunt`, `Slash`, `Pierce`, `Bow`, `Staff`. Set per-unit in YAML via `equipmentType`; per-skill via `permittedEquipmentTypes` (list) and `permittedTraits` (list). If both are set, the unit must satisfy each independently (at least one match per group).

### Barrier System
`EffectKind.Shield` skills grant a barrier that absorbs incoming damage before HP. Key rules:
- Stored as `barrier` key in the `_bars` dict. Not in `MaxBars` — it has no fixed cap.
- Displayed as a light-blue overlay on the HP bar proportional to `MaxHp`.
- Multiple grants accumulate additively.
- Decays each round in `StartOfRound`: `max(5, 20 − WIS/10)%` of remaining barrier.
- Focus/Fury empowerment does not apply to Shield skills.

### Status Bars: Thermal & Disruption
Two independent buildup-bar families drive CC. All bars run 0–100 via the `MaxBars`/`InitialBars` dict, so they flow automatically into `UnitState`, `BattleView`, and sandbox rendering.

**Thermal (`ThermalSystem.cs` — static, authoritative):**
- `cold` and `burn` bars oppose each other: cold cancels burn first, leftover builds cold.
- Soft threshold 50 → `slow` (AGI hit count halved). Hard threshold 100 → `frozen` (1-turn skip, bar retains at 40).
- Burn soft threshold 50 → `burning` DOT each turn (`0.5 × burn bar`). No hard freeze for burn.
- Decay per turn: cold −15, burn −10.
- `ApplyThermalBuildup(target, hitResults)` called per-hit in `ExecuteAction` for `Fire`/`Cold` components using `r.RawDamage` as thermal power.

**Disruption (`DisruptionSystem.cs` — static, authoritative):**
- Single `disruption` bar; no opposition mechanic.
- Soft threshold 50 → `dizzy` (actor output ×0.8). Hard threshold 100 → `stunned` (1-turn skip, bar retains at 40).
- Decay per turn: −20. `DisruptionPower` is explicit per-`SkillEffect` opt-in (default 0) — no blanket rule by damage type.
- `DisruptionResistance` is a direct `int` field on `BattleUnit` (not in the `Resistances` dict).

Frozen/stunned units are tracked in `_frozenUnits`/`_stunnedUnits` sets. `AutoAdvance` skips their turns automatically. `BuildStatusEffects` unifies thermal + disruption effects.

### Active Effect System (Buffs/Debuffs)
Runtime overlay system for temporary battle buffs and debuffs. Key types:
- **`ActiveEffectDefinition`** — reusable template (id, name, duration kind, stacking policy, stat/skill modifiers).
- **`ActiveEffectInstance`** — live session-scoped instance tracked per unit.
- **`RuntimeStatModifier`** — stat overlay resolved Set→Add→Multiply.
- **`RuntimeSkillModifier`** — skill overlay resolved Set→Modify→Add.
- **`EffectDurationKind`**: `ForTargetTurns`, `ForSourceTurns`, `UntilNextAction`.
- **`EffectStackingPolicy`**: `RefreshDuration`, `ReplaceIfStronger`, `StackIntensity`, `IndependentInstances`.

Public entry point: `IBattleEngine.ApplyActiveEffect(targetUnitId, definition, sourceUnitId)`. Base compiled skill/unit data is never mutated — all resolution is ephemeral. `TickActiveEffects` runs at end of `ExecuteAction`.

Supported `RuntimeStatKey` values include: `DamageDealtMultiplier`, `DamageTakenMultiplier`, per-type resistances (`PhysicalResistance`, `FireResistance`, …), per-type penetrations, and per-type damage dealt multipliers (`FireDamageDealtMultiplier`, …).

### Resistance, Penetration & Per-type Damage Multipliers
`BattleUnit` carries two attacker/defender stat groups:
- **`Resistances`** (`IReadOnlyDictionary<EffectType, int>`) — defender-side per damage type.
- **`Penetrations`** (`IReadOnlyDictionary<EffectType, int>`) — attacker-side, reduces effective resistance.
- **`DisruptionResistance`** / **`DisruptionPenetration`** — separate `int` fields (disruption is a bar mechanic, not a damage type).

In `DamageCalc.Compute`: effective resistance = `target.GetResistance(type) − actor.GetPenetration(type)`, applied before the damage formula. Per-type damage dealt multipliers (`RuntimeStatKey.*DamageDealtMultiplier`) apply as Layer 3 after resistance reduction.

Authored in YAML via unit modifier dicts (`set: { physicalResistance: 20 }`) and passive skill dicts (`penetration: { physical: 20 }`).

### Passive Skills
Skills with `category: Passive` grant permanent battle stats for the duration of a battle:
- YAML fields: `penetration` and `resistance` dicts (plus `disruption` key for disruption resistance/penetration).
- `CompileSkill` parses the dicts into `BattleSkill.PassivePenetrations`, `PassiveResistances`, etc.
- `CompileUnit` merges passive stats additively into the compiled `BattleUnit` after unit-level modifiers.
- Passive skills are never treated as battle actions: filtered from AI skill selection and `BattlePendingInput`.

### Thermal Protection
`BattleUnit.ThermalProtection` (int, default 0) is a bonus percentage that amplifies fire and cold resistances exclusively for thermal buildup accumulation — it does **not** reduce fire/cold damage taken.

Formula: `effectiveResistanceForBuildup = (int)(elementResistance * (1.0 + thermalProtection / 100.0))`. The 90% resistance cap inside `ThermalSystem` still applies.

Example: 50% cold resistance + 10 ThermalProtection → acts like 55% resistance for freeze buildup.

Authored per unit in YAML via modifiers (`modify: { thermalProtection: 20 }`). Accessible at runtime via `RuntimeStatKey.ThermalProtection` for active effects. Compiled in `ContentPipeline.CompileUnit` via `ModifierVariable.ThermalProtection`.

### Reaction Skills
A **Reaction** is a 4th skill slot (alongside basic, secondary, ultimate) limited to **one per character**. Reaction skills fire automatically when their trigger condition is met during any action, never as a chosen action.

Key rules:
- Tagged `Category: Reaction` in YAML; declared as `reaction: <skill-id>` in the unit YAML.
- Stored on `BattleUnit.ReactionSkill` (separate from `ResolvedSkills`). Never appears in action choices or AI selection.
- Has a `Trigger` field (`ReactionTrigger` enum). Current values: `OnHitBy`.
- `TriggerConditions` (`IReadOnlyList<TriggerCondition>?`) filters which hits qualify. Each `TriggerCondition` has optional `SkillRange? Range` and `EffectType? DamageType`; all non-null fields are AND-ed. Empty/null list = any damaging hit triggers.
- In YAML: `trigger: OnHitBy` + `triggerConditions: [{range: Melee}]` or `[{damageType: Physical}]`.
- `MatchesOnHitBy(reaction, usedSkill)` — static helper in `InteractiveBattleSession`; checks each condition against the incoming skill. Heals/shields/bar restores never fire the trigger.
- Reactions fire **after the full triggering action resolves**, before `CheckEnd`.
- Dead, frozen, or stunned units do not react. Reactions cannot trigger further reactions (no chaining).
- Reaction goes on cooldown after firing (same CD system as normal skills, ticked on the reactor's own turn).
- Execution path: `ExecuteReactions(actor, hitTargets, usedSkill)` → `ExecuteReactionAction(reactor, target, reaction)`.
- No MP cost, no Focus/Fury empowerment, no CD ticking of the reactor's other skills.
- First implemented reaction: `counter-strike` (ID) — fires on `OnHitBy` with `range: Melee`, physical hit at STR × 0.5, CD 2.

Design doc: `Docs/Design/reactions.md`.

### Preparation Skills
`SkillCategory.Preparation` is a skill category for setup actions that refund the actor's action. Using a Preparation skill does not consume the turn — the actor then takes a follow-up action in the same turn.

Key rules:
- Tagged `category: Preparation` in YAML. `refundsAction: true` is **auto-set by the ContentPipeline**; no need to write it in YAML.
- Once per turn per unit: a unit cannot use a second Preparation skill in the same turn. Tracked via `_preparedUnits: HashSet<string>` (set when a Preparation skill fires; cleared in `AdvanceTurn()` when the unit's turn ends).
- The once-per-turn lock is enforced in `AvailableSkillIds` and all AI skill selection paths.
- Preparation skills do not go through focus/fury empowerment, do not tick active effects, and do not fire reactions — same as any refunded action.
- The `focus` skill uses `category: Preparation` + `effectRef: Focused`; granting the Focused buff is its effect, not a special-cased mechanism.
- In C# test code, `BattleSkill` is constructed directly and `RefundsAction` does **not** need to be set — the runtime checks `Category == Preparation` directly, so the refund works without it.

---

## Content Pipeline

### Data Format Preferences
- **YAML** for human-authored static content (locations, units, items, quests, scenarios).
- **JSON** for saves, snapshots, exports, and machine-friendly data.
- Avoid XML unless there is a strong specific need.

### Pipeline Principle: Reader is Dumb, Pipeline is Smart

**Reader** responsibilities:
- Discover files
- Open files
- Parse YAML into raw objects

**Pipeline** responsibilities:
- Validate structure
- Validate references
- Merge base content and mods
- Resolve IDs and links
- Compile final definitions
- Build a read-only content database

**Gameplay code must never read YAML files directly during play.** It reads from the compiled in-memory content database.

### Recommended Pipeline Areas (inside GameCore/Content)
- `Sources` — file discovery and loading
- `Reading` — YAML parsing into raw objects
- `Raw` — raw parsed data types
- `Validation` — structure and reference validation
- `Merging` — base + mod data merging
- `Compiling` — final definition compilation
- `Database` — read-only content database for gameplay

### Content Files (in GameData/)
- Use many small YAML files, grouped by content type.
- Prefer one file per main content object.
- Recommended content types: locations, activities, eventflows, units, items, quests, scenarios.

### Data Principles
- Give all important content **stable string IDs**.
- Prefer references by ID over deeply nested data blobs.
- Keep authored static data separate from runtime state and save data.
- Load order: base data → mods → validate → compile final database.
- Modding is data-driven, not code-driven.

---

## GameData Structure

```
GameData/
  Base/        # Core game content (YAML)
  Mods/        # Override and extension content (YAML)
```

---

## Repository Structure

```
GameCore/
  Content/       # Content pipeline (reading, validation, merging, compiling, database)
  Flow/          # Flow/state control (exploration, event mode, transitions)
  World/         # World map, locations, activities
  Events/        # EventFlow, EventNode definitions and execution
  Battle/        # Battle setup, rules, deterministic engine
  Quests/        # Quest system
  Progression/   # Character and game progression
  Inventory/     # Items and inventory management
  SaveData/      # Save/load, persistent state serialization
  Contracts/     # Shared interfaces, enums, IDs, DTOs
GameCore.Scenarios/   # Shared deterministic battle scenarios
GameCore.Tests/       # .NET unit/integration tests
UnityClient/          # Unity presentation layer
BattleSandbox.Web/    # Phone-friendly web battle sandbox
GameData/
  Base/               # Core YAML content
  Mods/               # Mod YAML content
.github/workflows/    # CI pipelines (run GameCore.Tests on PRs)
```

---

## Design Priorities

1. Keep scope small
2. Keep architecture clean
3. Keep battle logic deterministic and testable
4. Keep Unity thin
5. Keep the project easy to mod
6. Keep the web sandbox minimal and useful
7. Prefer simple, robust solutions over overengineering

---

## Rules for AI Assistants

### Before Starting a Task
1. **Check `Docs/Design/` for design decisions** before implementing a mechanic.
2. **Open questions in design docs are undecided** — ask before assuming an answer.
3. **Check the Battle Architecture section** in this file for summaries of implemented systems before reading source code — it is faster than exploring the codebase.

### During Implementation
4. **Never collapse gameplay truth into UnityClient.** GameCore decides, UnityClient renders.
5. **New game mechanics go in GameCore/ first**, with tests in GameCore.Tests/.
6. **Data definitions go in GameData/Base/** as YAML files.
7. **Unity-specific code stays in UnityClient/** and only calls into GameCore.
8. **Keep PRs focused**: one feature or system per PR.
9. **Do not overengineer.** Build what is needed now, not what might be needed later.
10. **Battle scenarios must be deterministic.** Same seed + same inputs = same result, always.

### Before Closing a PR
11. **Run `dotnet format Everdawn.slnx` before every commit.** Do not wait for CI to catch formatting errors — format locally first. A pre-commit hook is installed at `.git/hooks/pre-commit` that does this automatically.
12. **Update `Docs/Design/` design docs** when a feature is implemented:
    - Tick `- [x]` checkboxes for items that are now fully implemented.
    - Add implementation details (types, enums, rules) under the relevant section.
    - Do **not** add new undecided features or resolve open questions without explicit instruction from the user.
13. **Update this file** when a new major system is implemented:
    - Add a brief subsection under the relevant Architecture section (e.g., "Battle Architecture").
    - Focus on what an AI needs to know to work with the system — public types, key rules, entry points — not internal implementation details.
    - Do **not** duplicate content already covered in `Docs/Design/`.

---

## Gameplay Quality Warnings

> **Scope:** These are design and playtesting warnings — not a per-PR checklist. Apply them when reviewing a new mechanic's design, during playtesting sessions, or when evaluating content. They are **not** AI assistant task rules; skip this section unless explicitly asked to review design quality.

Treat the following as development warnings. Apply them during design, implementation, playtesting, and content production — not just at launch. These are relevant once the game is large enough to evaluate end-to-end.

**[CRITICAL] Do not let combat become repetitive early.**
- Warn when one skill, combo, or tactic solves most encounters.
- Warn when normal fights stop asking the player to adapt.
- Ensure each character has multiple valid uses in battle.
- Ensure status, defense, setup, and resource control matter often.
- Avoid building encounters around one obvious best answer.

**[CRITICAL] Do not make battles feel slow.**
- Warn when animations, transitions, and UI flow delay the player too much.
- Ensure repeated actions resolve quickly.
- Ensure normal encounters end fast once the player understands them.
- Add battle speed-up, fast text, and animation reduction early.
- Avoid designing spectacle that becomes friction after repetition.

**[CRITICAL] Do not rely on presentation to carry weak writing.**
- Warn when scenes look good but do not deepen character, stakes, or conflict.
- Ensure each main character has a distinct voice, motive, and tension.
- Ensure important scenes change something meaningful.
- Keep the cast small enough to write well.
- Avoid flat protagonists and dialogue that only explains plot.

**[HIGH] Do not let the party feel fake or disconnected.**
- Warn when characters feel like separate solo stories sharing the same combat system.
- Ensure the party has a strong shared goal.
- Ensure members react to events, to each other, and to decisions.
- Add party banter, group scenes, and relationship development.
- Avoid party members disappearing from the narrative outside their own moments.

**[HIGH] Do not add progression systems that create menu work without meaningful choices.**
- Warn when a system adds complexity but does not change playstyle or decisions.
- Ensure upgrades are understandable, comparable, and impactful.
- Prefer fewer systems with stronger identity.
- Allow cheap recovery from bad build decisions.
- Avoid overdesigned crafting, socket, crystal, or upgrade layers that feel like chores.

**[HIGH] Do not allow balance to collapse into exploits or frustration.**
- Warn when one build, stat, or mechanic is clearly dominant.
- Warn when difficulty spikes depend on hidden knowledge or one required counter.
- Test the game as a critical-path player and as a completionist player.
- Ensure multiple builds and strategies remain viable.
- Avoid boss design that punishes experimentation unfairly.

**[HIGH] Do not waste the player's time.**
- Warn when the game forces long replays, unclear objectives, or excessive backtracking.
- Ensure retries are quick and friction is low.
- Ensure current objectives and recent story context are easy to review.
- Add save flexibility, cutscene skip, fast travel, and recap tools.
- Avoid long stretches of repeated content between meaningful decisions.

**[MEDIUM] Do not make party management annoying.**
- Warn when swapping characters creates heavy equipment, leveling, or setup friction.
- Ensure reserve characters stay usable through catch-up systems or shared progression.
- Save loadouts and support quick swap.
- Ensure forced party changes do not punish the player.
- Avoid constant manual re-equipping and underprepared story-mandated characters.

### Ongoing Review Rule
- If a feature is impressive once but annoying after 20 repetitions, redesign it.
- If a system looks deep but produces no new decisions, simplify or remove it.
- If a scene does not improve character, stakes, or clarity, rewrite or cut it.
- If a mechanic only works because the player has not solved it yet, it is not strong enough.
- If the game respects the player's time, the player will forgive lower production value more easily.

---

## Shorthand Dictionary

These phrases have defined meanings when used in requests:

| Phrase | Meaning |
|---|---|
| **"ship it"** | `dotnet format` → commit → new branch → push → create PR → squash-merge → sync local main → restart dev server on `http://localhost:5001` |
| **"push fix"** | `dotnet format` → commit → push directly to `main` (for small hotfixes) |
| **"start server"** | Kill port 5001 + `dotnet run --no-launch-profile --urls "http://localhost:5001"` in `BattleSandbox.Web/` |
| **"debug prod"** | Check recent GitHub Actions runs for failures (CI + Deploy) + verify `https://diegofariasr.github.io/Everdawn/` is reachable and returns HTTP 200 |