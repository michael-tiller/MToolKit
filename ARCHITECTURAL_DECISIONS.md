# MToolKit – Architectural Decisions

## 1. Purpose

MToolKit is a Unity framework for shipping production games.

It is not:
- A game engine
- A networking stack
- A tooling suite or editor extension pack

It is a project accelerator: a set of opinionated runtime modules that solve common game infra and glue problems.

---

## 2. Core Architectural Principles

1. **POCO-first runtime**
   - Core logic lives in plain C# classes, not MonoBehaviours.
   - Unity objects exist only at boundaries (bootstrapping, scenes, UI, assets).

2. **DI-driven composition**
   - VContainer is the single dependency injection container.
   - All services are registered via installers/plugins and resolved via DI, not via `new`, singletons, or `FindObjectOfType`.

3. **Plugin-oriented modularity**
   - Features are packaged as "plugins" with clear lifecycles.
   - Each domain (Persistence, Input, VisualGraphs, etc.) exposes a plugin that can be enabled/disabled at configuration time.
   - Cross-plugin coupling goes through interfaces and message bus, not direct concrete references.

4. **Event-driven, message-based communication**
   - MessagePipe provides the central pub/sub bus.
   - Systems communicate via strongly-typed messages rather than calling each other directly.
   - Minimizes scene coupling and avoids `Update()`-driven polling where possible.

5. **Configuration over code**
   - Build-time and environment-specific differences come from configuration, not scattered `#if` or magic constants.
   - Framework constants and (future) `IBuildEnvironment` abstract environment details.

6. **Deterministic persistence boundaries**
   - Save data and settings are versionable DTOs.
   - Save/persistence logic lives behind explicit interfaces; ES3 is the current backend, not a leaked detail.

7. **Unity-optimized async patterns**
   - UniTask (Cysharp.UniTask) is the standard for all async operations.
   - Decision: async/await throughout the framework uses UniTask instead of `Task` for Unity-optimized performance and integration with Unity's lifecycle.
   - Enables efficient async operations without thread pool overhead and integrates with Unity coroutines and cancellation tokens.

---

## 3. High-Level Runtime Architecture

1. **Bootstrap Layer**
   - Minimal scene entrypoint MonoBehaviours.
   - Creates DI containers (per-domain / per-scene as needed).
   - Installs core plugins (DI, Logging, Persistence, Input, UI, etc.).
   - Orchestrates scene bootstrapping: dependency preloading, timeouts, progress reporting.

2. **Core Services Layer**
   - Long-lived services registered as singletons:
     - Logging (Serilog)
     - Persistence (ES3-based)
     - Settings system
     - Localization
     - Input abstraction
     - Audio backbone and Music service
     - UI/Navigation stack
     - Analytics
     - Error Handling system
   - Talk to each other through interfaces and the message bus.

3. **Domain-Specific Subsystems**
   - Visual Graph Subsystem for quests/dialogue:
     - xNode is editor-only for designing Quest/Dialogue graphs.
     - Graph assets are exported to runtime DTOs in the editor (import/build step).
     - Player builds only contain DTOs; xNode types are not included in runtime assemblies.
     - Runtime GraphRunner operates on DTOs only (xNode not referenced at runtime).
   - Future domains (Save Migration, Platform Abstraction, Telemetry, etc.) plug into the same pattern.

4. **UI Integration Layer**
   - UI/Navigation manages canvases, stacks, modals, and flows using MVP (Model-View-Presenter) pattern.
   - Views are MonoBehaviours with presentation logic; NavigationService coordinates navigation flow.
   - Input, Accessibility, Audio, and Localization integrate via dedicated interfaces.
   - UI is a consumer of services; it does not own core business logic.

---

## 4. Key Technology Decisions

1. **Dependency Injection: VContainer**
   - Chosen as the single DI framework.
   - Decision: no homegrown service locators or static singletons.
   - All services are registered via installers or domain plugins.

2. **Messaging: MessagePipe**
   - Chosen for async, strongly-typed pub/sub.
   - Decision: cross-domain communication is event-driven, not via global managers.

3. **Logging: Serilog**
   - Structured logging with sinks and enrichment.
   - Decision: logs are structured by default; printf-style debug logging is discouraged.
   - Logging configuration is externalized and environment-aware.

4. **Reactive State: R3**
   - R3 (Reactive Extensions for Unity) provides reactive properties and observables.
   - Decision: state changes are observable streams; UI and systems subscribe to reactive properties instead of polling.
   - Used throughout for settings, persistence state, and UI binding.

5. **Persistence: ES3**
   - ES3 is the current persistence backend.
   - Decision: persistence APIs exposed via interfaces (`IES3Service`, SaveController abstractions).
   - Allows swapping the backend later without rewriting domain logic.

6. **Settings: INI-backed Reactive Settings**
   - Settings are reactive objects (R3 `ReactiveProperty`) mirrored into INI storage.
   - Decision: settings changes are event-driven; UI and systems subscribe instead of polling.
   - Settings layout is stable and versionable to support future migrations.

7. **Assets: Addressables**
   - Addressables is the standard for asset loading.
   - Decision: all async content loading should go through MToolKit's Addressables service.
   - Enables parallel loading, caching, dependency management, and progress reporting.

8. **Input: Unity Input System + Abstraction**
   - Unity Input System is the low-level provider.
   - Decision: all gameplay input goes through an abstraction (current `InputRebinderService`, migrating to `IInputService`).
   - Keeps rebinding, profiles, persistence, and device logic out of game code.

9. **Visual Graphs: xNode Authoring, POCO Runtime**
   - xNode is editor-only for designing Quest/Dialogue graphs.
   - Decision: graph assets are exported to runtime DTOs in the editor (import/build step) with GUIDs and explicit types.
   - Player builds only contain DTOs; xNode types are not included in runtime assemblies.
   - Runtime runner executes DTO graphs with no xNode dependency, suitable for tests and headless environments.

10. **UI Navigation: MVP Pattern**
   - Navigation system uses Model-View-Presenter (MVP), not MVVM.
   - Decision: Views are MonoBehaviours that contain presentation logic; NavigationService acts as presenter/coordinator.
   - Views directly manipulate UI elements; no ViewModel layer or data binding abstraction.
   - Rationale: Unity's component model and GameObject lifecycle align better with MVP than MVVM. Views remain testable via DI injection of services.

---

## 5. Cross-Cutting Concerns

1. **Error Handling**
   - Central `ErrorSystemPlugin` orchestrates user-facing errors and logging.
   - Decision: errors are routed through a single system with:
     - Graceful degradation
     - User-friendly presentation
     - Analytics integration

2. **Accessibility**
   - Accessibility is treated as a first-class concern and critical for "production-ready" status.
   - Decision: features such as keyboard navigation, visual accessibility, and screen reader support integrate with:
     - Settings system (R3 reactive flags)
     - UI/Navigation
     - Input abstraction

3. **Testing**
   - Unit and property-based tests mirror runtime structure.
   - Decision: subsystems are testable without Unity runtime where possible.
   - Visual Graph runtime, plugins, and core services must support headless testing via DI.

4. **Versioning and Migration (Planned)**
   - Save Migration System will handle forward-only semantic version upgrades of save data.
   - Decision: migrations are forward-only, idempotent, and run before deserialization, with rollback on failure.

5. **Platform Abstraction (Planned)**
   - Runtime queries platform capability via interfaces (`IPlatformInfo`, `IPlatformAchievements`, etc.).
   - Decision: platform differences are handled in one place, not scattered across gameplay and UI code.

---

## 6. Non-Goals

1. **Full Game Engine**
   - MToolKit will not replace Unity's core engine features.

2. **Networking Stack**
   - No first-party networking solution is shipped; any networking layer sits on top or beside MToolKit.

3. **Heavy Editor Tool Suite**
   - Editor tooling is minimal and focused on supporting runtime (e.g., xNode authoring for VisualGraphs).

4. **Arbitrary Plugin Ecosystem**
   - MToolKit is modular but intentionally opinionated; it is not a generic "plugin marketplace."

---

## 7. Summary of Architectural Guarantees

- Core logic lives in POCOs and is DI-resolvable.
- Cross-system communication is event-driven via MessagePipe.
- State and configuration are explicit, versionable, and not baked into scenes.
- Unity-specific concerns are pushed to edges (bootstrapping, UI, assets).
- Major infra dependencies (logging, persistence, input, graphs) are hidden behind stable interfaces, enabling future swaps and enterprise features without rewriting games.
