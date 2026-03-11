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

1. **Never collapse gameplay truth into UnityClient.** GameCore decides, UnityClient renders.
2. **Check `Docs/Design/` for design decisions** before implementing a mechanic.
3. **Open questions in design docs are undecided** — ask before assuming an answer.
4. **New game mechanics go in GameCore/ first**, with tests in GameCore.Tests/.
5. **Data definitions go in GameData/Base/** as YAML files.
6. **Unity-specific code stays in UnityClient/** and only calls into GameCore.
7. **Keep PRs focused**: one feature or system per PR.
8. **Update this file and relevant design docs** when a decision is made or a system changes.
9. **Do not overengineer.** Build what is needed now, not what might be needed later.
10. **Battle scenarios must be deterministic.** Same seed + same inputs = same result, always.
11. **Run `dotnet format Everdawn.slnx` before every commit.** Do not wait for CI to catch formatting errors — format locally first. A pre-commit hook is installed at `.git/hooks/pre-commit` that does this automatically.

---

## Shorthand Dictionary

These phrases have defined meanings when used in requests:

| Phrase | Meaning |
|---|---|
| **"ship it"** | `dotnet format` → commit → new branch → push → create PR → squash-merge → sync local main → restart dev server on `http://localhost:5001` |
| **"push fix"** | `dotnet format` → commit → push directly to `main` (for small hotfixes) |
| **"start server"** | Kill port 5001 + `dotnet run --no-launch-profile --urls "http://localhost:5001"` in `BattleSandbox.Web/` |
| **"debug production"** | Check recent GitHub Actions runs for failures (CI + Deploy) + verify `https://diegofariasr.github.io/Everdawn/` is reachable and returns HTTP 200 |
