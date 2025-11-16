# MToolKit Runtime Modules

Core framework modules for production Unity games. Each module is documented with its own README.

## Overview

MToolKit provides essential systems required for shipped game titles. It's a modular framework built on dependency injection, reactive programming, and message-driven architecture.

## Modules

### Core System
- **Core** - Dependency injection, plugin architecture, build/env configuration (dev/stage/prod)
- **MessageBus** - Decoupled message publishing/subscription
- **Bootstrapper** - Scene bootstrapping with dependency preloading, timeout handling
- **Installer** - DI registration and service configuration

### UI & Interaction
- **Navigation** - View management, modals, subviews, canvas management
- **Components** - Reusable UI components (buttons, inputs, spinners, etc.)
- **Localization** - Multi-language support via Unity Localization
- **Accessibility** - (In Progress) Screen reader support, high-contrast mode, scalable text

### Data & Persistence
- **Persistence** - Save system with ES3 integration, multiple slots, autosave, cloud backup
- **Settings** - Reactive settings system with UI binding, volume control, graphics settings
- **Save Migrations** - (Planned) Save data versioning for post-launch updates

### Media & Content
- **Audio** - Audio playback service with mixer integration, volume persistence
- **Music** - Cross-scene music playback with crossfading, looping
- **AssetLoader** - Addressables integration with parallel loading, lifecycle management, caching, dependency tracking

### Services
- **Input** - Input abstraction with rebinding and device fallback
- **Analytics** - GameAnalytics integration with event tracking, revenue tracking
- **ErrorSystem** - Global error handling with graceful degradation, diagnostics, analytics integration

### Game Systems
- **VisualGraphs** - Event-driven visual graph system for quests/dialogue with xNode authoring

### Infrastructure
- **Slog** - Structured logging with Serilog
- **Utilities** - Helper classes, extensions, and data structures

## Architecture Principles

1. **Loose Coupling** - Modules are independent and loosely coupled
2. **Dependency Injection** - VContainer-based DI for all services
3. **Reactive Programming** - R3 for reactive state management
4. **Event-Driven** - MessagePipe for decoupled communication
5. **Plugin Architecture** - Extensible plugin-based design

## Dependencies

### Open Source Dependencies

- **VContainer** - Dependency injection
- **MessagePipe** - Async messaging
- **R3** - Reactive properties
- **Serilog** - Structured logging
- **xNode** - Visual graph authoring

### Unity Dependencies

- **Unity Input System** - Input handling
- **Unity Addressables** - Asset loading
- **Unity Localization** - Localization

### Third-Party Services

- **GameAnalytics** - Analytics (free until threshold)

### Free Commercial Plugins

- **DOTween** - Animation (free version; only free features are used)

### Commercial Plugins

The following paid plugins are currently integrated into the project as hard dependencies.

- **Sirenix Odin Inspector/Validator** - Editor enhancements (required)
- **ES3** - Save system to be abstracted out

## Current Status

**Status**: 16/18 core systems complete (89%)
- ✅ Complete: 16 systems
- 🚧 In Progress: 2 systems (Settings 95%, Accessibility)
- 🔜 Planned (Post-Launch): Save Migrations (for post-launch updates with save structure changes)
- ❌ Missing: 0 critical gaps for initial launch

**Visual Graph Subsystem: ✅ Complete**
- ✅ Runtime infrastructure complete (POCO, DI-aware, event-driven)
- ✅ xNode authoring with stable GUIDs
- ✅ O(1) event routing, state management
- ✅ Save system integration (ES3) with quest state persistence
- ✅ Dialogue UI integration (message-based architecture)
- ✅ Tested and working (branching dialogue → quest integration)
- 🔜 Phase 2: Formal test coverage suite (target: 100%)

## Foundation Systems Included

✅ **Build/Environment Configuration** - Dev/stage/prod environment support with config overrides (IBuildEnvironment formalization in Phase 2)  
✅ **Scene Management** - Bootstrapping with dependency preloading and timeout handling  
✅ **Crash/Diagnostics** - Error handling with graceful degradation and analytics integration  
🔧 **Deferred** - Time management (Unity's timeScale sufficient), Platform abstractions (Unity APIs sufficient)  
🎯 **Phase 2 Planned** - Build environment formalization (eliminate static reads, DI-injected IBuildEnvironment)  
🎯 **Phase 3 Planned** - Telemetry abstraction, Content versioning, Performance profiling

