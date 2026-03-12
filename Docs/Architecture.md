# Architecture: GameCore Package Boundaries

This document explains the key architectural boundaries in the Everdawn codebase,
focusing on what is authoritative, what is host-specific, and how Unity consumes GameCore.

---

## What is Authoritative

**GameCore** is the single source of truth for all game logic. It is a pure C# library
with no Unity dependencies (`noEngineReferences: true` in the `.asmdef`).

- All battle resolution lives in `BattleSession` / `InteractiveBattleSession`.
- `BattleSession.RunFull()` is the single authoritative execution path for battles.
- `BattleEngine.Run()` is a thin facade that delegates directly to `BattleSession.RunFull()`.
- Tests, the web sandbox, and Unity all run through the same code path.
- Game state only changes when a validated command is accepted by `IBattleEngine`.

---

## What is Host-Specific

The following responsibilities belong to the **host** (Unity client, web sandbox, tests),
not to GameCore:

### Content Root Configuration

GameCore never searches for `GameData/` by walking directories. Each host explicitly
configures a content root and creates an `IContentSource` from it:

```
// Tests (GameCore.Tests/TestContentSource.cs)
// Resolved from the known output path — four directories above the test binary.
IContentSource source = TestContentSource.Default;

// Web sandbox (BattleSandbox.Web/Program.cs)
// Registered as a DI service at host startup; injected into components.
builder.Services.AddSingleton<IContentSource>(SandboxContentSource.Create(contentRoot));

// Unity (future)
IContentSource source = new FileSystemContentSource(
    Application.streamingAssetsPath + "/GameData/Base");
```

No host adapter walks parent directories at runtime. Each host calculates or receives
the content root once, explicitly, at startup.

### Scenario Setup

`IBattleScenario.CreateSetup(IContentSource source)` requires the caller to supply
a content source. Scenarios define **which** units to use — the host decides **where**
to load them from.

### UI, Sound, VFX, Networking

All presentation concerns live in `UnityClient/`. Unity renders the `BattleView`
returned by `IBattleEngine`, but never holds its own HP or turn-state copies.

---

## IContentSource: the Content Access Boundary

`IContentSource` is the explicit boundary between host file-system concerns and
GameCore's content pipeline. GameCore never resolves paths or opens files on its own.

The content loading chain:

```
Host configures content root
    ↓
Host creates IContentSource (e.g. FileSystemContentSource)
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
│   ├── GlobalUsings.cs        ← Explicit global using aliases (System, Collections.Generic, Linq)
│   └── Polyfills.cs           ← C# 9/10 polyfills for netstandard2.1 + Unity
└── GameData/                  ← (see GameData/ at repo root — not inside package)
```

### Unity Compatibility

GameCore targets `netstandard2.1` and `LangVersion 10` (C# 10). This is compatible
with Unity 6 (6000.x) and above. Compatibility constraints enforced in every file:

- `GlobalUsings.cs` declares all global usings **explicitly**. Unity compiles `.cs`
  files directly and never reads the SDK-generated file in `obj/`, so every namespace
  must appear here rather than relying on `ImplicitUsings`.
- `Polyfills.cs` provides `IsExternalInit` — required for `init`-only properties and
  `record` types in `netstandard2.1` builds that pre-date .NET 5.

C# features intentionally **avoided** in GameCore to stay Unity-safe:
- `required` members (C# 11) — use `init` properties without `required` instead
- Collection expressions `[...]` (C# 12) — use `new T[] { ... }` instead
- Primary constructors for non-record classes (C# 12)
- Raw string literals `"""` (C# 11)

CI validates both constraints with a dedicated step:

```yaml
- name: Unity compat check (LangVersion 10, no implicit usings)
  run: dotnet build GameCore/GameCore.csproj --no-restore --configuration Release
       -p:LangVersion=10 -p:ImplicitUsings=disable
```

This step catches both accidental use of newer C# syntax **and** any namespace that
relies on the SDK-generated implicit-usings file instead of `GlobalUsings.cs`.

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
