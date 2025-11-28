```markdown
# MToolKit Runtime Modules

Core modular runtime for production Unity applications built on modern .NET practices. Each module has its own README for implementation details and extension points.

## Overview

MToolKit is a DI-first, event-driven framework that treats Unity as a host for a .NET application:

- VContainer for composition root and module wiring
- MessagePipe for message bus and cross-module communication
- R3 for reactive state and UI binding
- Serilog for structured logging and diagnostics
- xNode for visual authoring, exported to pure DTO runtime

The goal is long-lived, testable systems that can ship real games and non-game products on top of Unity.

## Modules

### Core System

- **Core**  
  - Composition root, plugin architecture, environment config (dev / stage / prod)  
  - Abstractions for bootstrapping, lifecycle, and module registration
- **MessageBus**  
  - Strongly-typed pub/sub via MessagePipe  
  - Request/response patterns, domain routing, and scoped event channels
- **Bootstrapper**  
  - Scene bootstrapping with dependency preloading, health checks, and timeout handling  
  - Safe startup ordering for services and plugins
- **Installer**  
  - DI registration and service configuration  
  - Split into global / per-scene install phases

### UI & Interaction

- **Navigation**  
  - View stack, modals, subviews, canvas routing  
  - Decoupled navigation requests over the message bus
- **Components**  
  - Reusable UI elements (buttons, inputs, toasts, spinners)  
  - Reactive binding hooks for R3 observables
- **Localization**  
  - Unity Localization integration  
  - Runtime locale switching, key-driven text and asset binding
- **Accessibility** *(In Progress)*  
  - High-contrast / large text modes  
  - Hook points for platform accessibility where available

### Data & Persistence

- **Persistence**  
  - Multi-domain save system (player / world / settings / graphs)  
  - ES3 backend with profile support, autosave, quicksave, and slots  
  - DI-driven `ISaveDomainController` pattern for new domains
- **Settings** *(95%)*  
  - Reactive settings model (audio, graphics, control schemes, gameplay)  
  - Bindable to UI with automatic persistence  
- **Save Migrations** *(Planned)*  
  - Versioned save formats for post-launch schema changes  
  - Central registry of migration steps per save domain

### Media & Content

- **Audio**  
  - Centralized SFX playback with mixer routing  
  - Category-based volume control (UI, SFX, VO, etc.)
- **Music**  
  - Cross-scene music controller with crossfades, playlists, and stingers
- **AssetLoader**  
  - Addressables wrapper with parallel loading, caching, and dependency tracking  
  - Unified API for sync/async loads and preloads

### Services

- **Input**  
  - Abstraction over the Unity Input System  
  - Action-mapped input, rebinding, and device fallback
- **Analytics**  
  - GameAnalytics integration (or pluggable provider)  
  - Event schema for funnel, economy, and error tracking
- **ErrorSystem**  
  - Global error handling and graceful degradation  
  - Integration with logging and analytics for crash/SEV reporting

### Game / Workflow Systems

- **VisualGraphs**  
  - Event-driven workflow and rules engine with xNode authoring  
  - Used for quests, dialogue, triggers, and arbitrary state machines  
  - Runtime is pure POCO DTOs (no xNode dependency at runtime)  
  - O(1) event routing by domain and message type  
  - Integrated with save system for graph/quest state persistence

### Infrastructure

- **Slog**  
  - Serilog-based structured logging layer  
  - Environment-aware sinks (editor console, files, remote)
- **Utilities**  
  - Common helpers, collections, and extension methods used across modules

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

Lessons from past untestable stacks are baked into the architecture: core systems are designed to be observable, debuggable, and hard to regress.

## Dependencies

### Open Source

- **VContainer** – DI container
- **MessagePipe** – Message bus
- **R3** – Reactive streams and properties
- **Serilog** – Structured logging
- **xNode** – Visual authoring for graphs (editor-only)

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

## Current Status

**Status**: 16 / 18 core systems complete (~89%)

- ✅ Complete: 16 systems  
- 🚧 In Progress: Settings polish, Accessibility  
- 🔜 Planned: Save Migrations, telemetry abstraction, richer performance profiling

**VisualGraphs Subsystem: ✅ Runtime-Ready, Test-Hardened**

- ✅ POCO runtime, DI-aware, xNode-free at runtime  
- ✅ Stable GUID export pipeline for graphs and nodes  
- ✅ Event routing, execution queue, and state management implemented and covered  
- ✅ Save/load integration for graph and quest state (ES3 + multi-domain save system)  
- ✅ Quest and dialogue integration verified end-to-end  
- ✅ Unit + property-style tests for plugin lifecycle, loader integration, and error boundaries  
- 🔜 Additional integration scenarios (multi-graph orchestration, bulk workflows) as needed

## Foundation Systems Included

- ✅ **Build / Environment Configuration**  
  - Dev / stage / prod environments with config overrides  
- ✅ **Scene Management & Bootstrapping**  
  - Ordered initialization, dependency preloading, timeout handling  
- ✅ **Crash / Diagnostics Path**  
  - Global error handling, logging, and analytics hooks  
- 🔧 **Deferred (Non-Critical)**  
  - Custom time management (Unity timeScale sufficient for now)  
  - Deeper platform abstraction where Unity APIs already suffice  
- 🎯 **Future Phases**  
  - Formalized `IBuildEnvironment` (DI-injected, no static reads)  
  - Telemetry provider abstraction (swap GameAnalytics/other backends)  
  - Content versioning and richer profiling hooks
```
