# Architecture: GameCore Package Boundaries

This document explains the key architectural boundaries in the Everdawn codebase,
focusing on what is authoritative, what is host-specific, and how Unity consumes GameCore.

---

## What is Authoritative

**GameCore** is the single source of truth for all game logic. It is a pure C# library
with no Unity dependencies (`noEngineReferences: true` in the `.asmdef`).

- All battle resolution lives in `BattleSession` / `InteractiveBattleSession`.
- `BattleEngine.Run()` is a thin facade that delegates to `BattleSession.RunFull()`.
- There is exactly **one** execution path for battle logic. Tests, the web sandbox,
  and Unity all run through the same code.
- Game state only changes when a validated command is accepted by `IBattleEngine`.

---

## What is Host-Specific

The following responsibilities belong to the **host** (Unity client, web sandbox, tests),
not to GameCore:

### Content Discovery

GameCore never searches for `GameData/` by walking directories. Instead, hosts
explicitly create and inject an `IContentSource`:

```
// Tests
IContentSource source = TestContentSource.Default;  // walks up from test bin dir

// Web sandbox
IContentSource source = SandboxContentSource.Default;  // walks from sandbox app dir

// Unity (future)
IContentSource source = new FileSystemContentSource(Application.streamingAssetsPath + "/GameData/Base");
```

### Scenario Setup

`IBattleScenario.CreateSetup(IContentSource source)` requires the caller to supply
a content source. Scenarios define **which** units to use — the host decides **where**
to load them from.

### UI, Sound, VFX, Networking

All presentation concerns live in `UnityClient/`. Unity renders the `BattleView`
returned by `IBattleEngine`, but never holds its own HP or turn-state copies.

---

## How Content is Injected

The content loading chain:

```
Host creates IContentSource
    ↓
IBattleScenario.CreateSetup(source)
    ↓
ContentPipeline.Load(source)
    ↓
ContentDatabase (compiled, read-only)
    ↓
BattleSetup (fully resolved, authoritative)
    ↓
IBattleEngine.Start(setup)
```

`ContentPipeline.Load(string basePath)` remains available as a convenience overload
that wraps `basePath` in a `FileSystemContentSource` internally.

The `Base/Mods/` structure is preserved for future mod support:
- `GameData/Base/` — core content (always loaded)
- `GameData/Mods/` — override/extension content (loaded after Base, last modifier wins)

---

## How Unity Consumes GameCore

GameCore is a **local Unity package** referenced in `UnityClient/Packages/manifest.json`:

```json
"com.everdawn.gamecore": "file:../GameCore"
```

### Package Layout

```
GameCore/
├── package.json               ← Unity package manifest (com.everdawn.gamecore)
├── GameCore.csproj            ← .NET SDK project (netstandard2.1, LangVersion 10)
├── Runtime/                   ← All runtime source code
│   ├── GameCore.asmdef        ← Unity assembly definition (noEngineReferences: true)
│   ├── Battle/                ← Battle engine, commands, views
│   ├── Content/               ← Content pipeline, IContentSource
│   ├── Flow/                  ← Game flow state
│   ├── World/                 ← World map, locations, activities
│   ├── Contracts/             ← (future) shared interfaces and DTOs
│   ├── Events/                ← (future) EventFlow execution
│   ├── Inventory/             ← (future) item system
│   ├── Progression/           ← (future) character progression
│   ├── Quests/                ← (future) quest system
│   ├── SaveData/              ← (future) save/load
│   ├── GlobalUsings.cs        ← Global using aliases (System, Collections.Generic, Linq)
│   └── Polyfills.cs           ← C# 9/10 polyfills for netstandard2.1 + Unity
└── GameData/                  ← (see GameData/ at repo root — not inside package)
```

### Unity Compatibility

GameCore targets `netstandard2.1` and `LangVersion 10` (C# 10). This is compatible
with Unity 6 (6000.x) and above. The `Polyfills.cs` file provides:

- `IsExternalInit` — required for `init`-only properties and `record` types in
  `netstandard2.1` builds that pre-date .NET 5.

C# features intentionally **avoided** in GameCore to stay Unity-safe:
- `required` members (C# 11) — use `init` properties without `required` instead
- Collection expressions `[...]` (C# 12) — use `new T[] { ... }` instead
- Primary constructors for non-record classes (C# 12)

CI validates this constraint with a dedicated step:
```yaml
- name: Unity compat check (LangVersion 10)
  run: dotnet build GameCore/GameCore.csproj -p:LangVersion=10
```

---

## Deterministic Scenarios

Battle scenarios are defined in `GameCore.Scenarios` (not in GameCore itself, since
scenarios depend on GameData content).

- `IBattleScenario` — defines a scenario (seed, display name, setup factory)
- `IRegressionScenario` — extends with expected outcomes (winner, snapshot count)
- `ScenarioRegistry.All` — the single list shared by tests and the web sandbox

Tests and sandbox both call `BattleEngine.Run(scenario.CreateSetup(source), scenario.Seed)`,
which delegates to `BattleSession.RunFull()` — the single authoritative execution path.
Same seed + same setup = same battle, always.
