# MToolKit

Modular runtime framework for production Unity applications built on modern .NET practices.
Each module has its own README for implementation details and extension points.

## Overview

MToolKit is a DI-first, event-driven framework that treats Unity as a host for a .NET application:

**Built on**

- VContainer for composition root and module wiring
- MessagePipe for message bus and cross-module communication
- R3 for reactive state and UI binding
- Serilog for structured logging and diagnostics
- xNode for visual authoring, exported to pure DTO runtime

**Provides**

- Multi-domain save + profiles
- MVC Navigation/UI stack
- Dedicated settings system with INI loader

The goal is long-lived, testable systems that can ship real games and non-game products on top of Unity.

**Intended for**

- Long-lived, content-heavy titles (live service, RPG, systemic sandboxes)
- Teams with multiple engineers working in the same codebase
- Projects that care about testability, observability, and clean module boundaries

**Not aimed at**

- 48-hour game jams or throwaway prototypes
- Single-scene experiments and quick “get something on screen” demos
- Projects that prefer ad-hoc singletons and scene-driven scripting

## Modules

| Module | Purpose | Status |
|--------|---------|--------|
| [Core](Runtime/Core/README.md) | DI, environment config, plugin architecture, bootstrapping | Production |
| [MessageBus](Runtime/MessageBus/README.md) | Message bus, subscriptions, request/response patterns, domain routing | Production |
| [Bootstrapper](Runtime/Bootstrapper/README.md) | Scene bootstrapping, dependency preloading, health checks, startup ordering | Production |
| [Installer](Runtime/Installer/README.md) | DI registration, service configuration, global/per-scene install phases | Production |
| [Navigation](Runtime/Navigation/README.md) | View stack, modals, subviews, canvas routing, async transitions | Production |
| [Components](Runtime/Components/README.md) | Reusable UI elements, reactive binding hooks for R3 observables | Production |
| [Localization](Runtime/Localization/README.md) | Unity Localization integration, runtime locale switching, key-driven text/asset binding | Production |
| [Persistence](Runtime/Persistence/README.md) | Profiles, slots, multi-domain saves (player/world/settings/graphs), ES3 backend | Production |
| [Settings](Runtime/Settings/README.md) | Reactive settings model, bindable to UI with automatic persistence | Production |
| [Audio](Runtime/Audio/README.md) | Centralized SFX playback, mixer routing, category-based volume control | Production |
| [Music](Runtime/Music/README.md) | Cross-scene music controller, crossfades, playlists, stingers | Production |
| [AssetLoader](Runtime/AssetLoader/README.md) | Addressables wrapper, parallel loading, caching, dependency tracking | Production |
| [Input](Runtime/Input/README.md) | Unity Input System abstraction, action-mapped input, rebinding, device fallback | Production |
| [Analytics](Runtime/Analytics/README.md) | GameAnalytics integration, event schema for funnel/economy/error tracking | Production |
| [ErrorSystem](Runtime/ErrorSystem/README.md) | Global error handling, graceful degradation, crash/SEV reporting | Production |
| [VisualGraphs](Runtime/VisualGraphs/README.md) | xNode-based graph authoring → runtime DTO, event-driven workflows, quests/dialogue | Production (Quest/Dialogue), WIP (general workflows) |
| [Slog](Runtime/Slog/README.md) | Serilog-based structured logging, environment-aware sinks | Production |
| [Utilities](Runtime/Utilities/README.md) | Common helpers, collections, extension methods | Production |
| Accessibility | High-contrast/large text modes, platform accessibility hooks | In Progress |
| Save Migrations | Versioned save formats, migration registry per domain | Planned |

## Getting Started

1. **Add MToolKit to your project**  
   Add the package via Unity Package Manager (git URL) or as a local package.

2. **Create a bootstrap scene**  
   Create a new scene and add a `GlobalInstaller` component to a GameObject. Configure required fields in the inspector (Input Action Asset, Global Plugin Config).  
   `GlobalInstaller` is the MonoBehaviour bridge that creates the root LifetimeScope and loads your plugin configuration.

3. **Implement a game installer**  
   Create a `LifetimeScope` for your game-specific services:

```csharp
using VContainer;
using VContainer.Unity;

// Lives in your bootstrap scene as a LifetimeScope for game-level services
public class GameInstaller : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
        builder.Register<MainMenuView>(Lifetime.Singleton);
    }
}
```

4. **Use services via dependency injection**  
   Services are automatically injected into constructors. Views can be resolved from the container or injected into other components.  
   MonoBehaviours at scene boundaries should use thin adapter components that receive their dependencies via DI, rather than resolving the container directly.

## Architecture Principles

1. **Unity as Host, .NET as Core**  
   - Unity provides rendering, input, and scene graph  
   - Core logic, workflows, and services live in testable .NET modules

2. **Dependency Injection Everywhere**  
   - VContainer composition root (global + per-scene)  
   - No hidden singletons; all services resolved via DI

3. **Event-Driven, Message-First Design**  
   - MessagePipe as the primary integration boundary between modules  
   - Explicit message contracts for cross-system behavior

4. **Reactive State**  
   - R3 for observable streams and UI binding  
   - No polling Update loops for business logic

5. **Plugin Architecture**  
   - Modules implemented as plugins with explicit lifecycle (register, setup, runtime init, shutdown)  
   - Feature sets can be added/removed without touching core systems

6. **Testability Under Engine Constraints**  
   - IL2CPP-safe mocking and custom test doubles  
   - Clear injection points and state boundaries for unit and integration tests
   
For detailed architectural decisions, see [ARCHITECTURAL_DECISIONS.md](ARCHITECTURAL_DECISIONS.md).

## Testing & Reliability

MToolKit is built and hardened with tests as a first-class concern:

- **Coverage Roadmap**  
  - Explicit test priority tiers per module (core, high, low, lowest)  
  - VisualGraphs subsystem fully mapped with class-level test priorities
- **Unit Tests**  
  - ~2k tests across core modules and VisualGraphs  
  - Lifecycle, DI registration, error boundaries, save integration, and event routing
- **Property-Style Tests**  
  - Invariants and round-trip properties for key services (e.g. VisualGraphPlugin lifecycle, graph load/unload laws)  
  - Focus on idempotency, reversibility, and safe behavior under invalid inputs
- **Engine-Aware Test Design**  
  - No Reflection.Emit; IL2CPP-compatible test doubles  
  - Minimal reliance on Unity scene state; tests focus on pure runtime behavior
- **Diagnostic Tooling**  
  - Editor windows for live inspection (e.g. QuestManager diagnostics: active/completed/claimed quests, objectives, and progress)  
  - Structured logging hooks to make production issues diagnosable, not mysterious
- **Test Location**  
  - Tests currently live in the sandbox/example project so they can run under the Unity Test Runner; core runtime remains POCO-only and does not depend on Unity assemblies

Lessons from past hard-to-test stacks are baked into the architecture: core systems are designed to be observable, debuggable, and hard to regress.

## Dependencies

Tested with:

- Unity 2021.3 or higher
- API Compatibility Level: .NET Framework
- Scripting Backend: Mono and IL2CPP are supported

### Open Source

- **VContainer** – DI container for game services
- **MessagePipe** – strongly-typed message bus between modules
- **R3** – Reactive UI states and settings
- **Serilog** – Structured logging
- **xNode** – editor-only graph authoring, exported to DTOs
- **NUnit** - For testing
- **NSubstitute** - For testing
- **FsCheck** - For testing

### Unity Packages

- **Unity Input System** – Input handling
- **Addressables** – Asset loading
- **Localization** – Localization and localized assets

### Third-Party Services

- **GameAnalytics** – Analytics provider (free tier supported)

### Free Commercial Plugins

- **DOTween** – Tweening/animation (free subset only)

### Commercial Plugins

- **Sirenix Odin Inspector/Validator** – Editor support and validation (required)  
- **ES3** – Save system backend (wrapped behind MToolKit persistence abstractions)

## Example Project

A playable sandbox game built entirely on MToolKit exercises profiles, multi-domain saves, quests, and dialogue.  
See: https://github.com/michael-tiller/mtoolkit-sandbox-overview

The full Unity project and builds are private but can be shared with hiring teams for deeper review.

## Repository Layout

```text
./Runtime/...                      # Core runtime modules
./Editor/...                       # Editor-only tools
./README.md                        # Readme (you are here)
./ARCHITECTURAL_DECISIONS.md       # Architectural decision log
./LICENSE.md                       # MIT License
./package.json                     # Unity package manifest
```

## License

MToolKit is released under the MIT License and maintained by Michael Tiller (@michael-tiller).

You may use, modify, and distribute this framework in commercial and non-commercial projects. See the [LICENSE.md](LICENSE.md) file for the full license text.

Copyright (c) 2025 Michael Tiller.

## Current Status

**Status**: 16 / 18 core systems complete (~89%)

- **Complete**: 16 systems  
- **In Progress**: Settings polish, Accessibility module  
- **Planned**: Save Migrations

**VisualGraphs Subsystem: Runtime-Ready, Test-Hardened**

- POCO runtime, DI-aware, xNode-free at runtime  
- Stable GUID export pipeline for graphs and nodes  
- Event routing, execution queue, and state management implemented and covered  
- Save/load integration for graph and quest state (ES3 + multi-domain save system)  
- Quest and dialogue integration verified end-to-end  
- Unit + property-style tests for plugin lifecycle, loader integration, and error boundaries  
- **Planned**: Additional integration scenarios (multi-graph orchestration, bulk workflows) as needed

**Foundation Systems**

- **Build / Environment Configuration**: Dev / stage / prod environments with config overrides  
- **Scene Management & Bootstrapping**: Ordered initialization, dependency preloading, timeout handling  
- **Crash / Diagnostics Path**: Global error handling, logging, and analytics hooks  
- **Deferred (Non-Critical)**: Custom time management (Unity timeScale sufficient for now); deeper platform abstraction where Unity APIs already suffice  
- **Future Phases**: Formalized `IBuildEnvironment` (DI-injected, no static reads); telemetry provider abstraction (swap GameAnalytics/other backends); content versioning and richer profiling hooks
