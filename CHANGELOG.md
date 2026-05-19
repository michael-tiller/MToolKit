# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the project is pre-1.0, breaking changes bump the minor component.

## [0.6.0] - 2026-05-19

### Added

- `Runtime/Persistence/Migration/` — forward-only save migration framework. `ForwardMigrator<T>` stamps schema version + 12-hex SHA-256 hash on save and dispatches version/hash resolution on load via `SchemaHashWalker` (schema graph hash), `ES3MemberEnumerator` (ES3-serialized member walk), `PolymorphicResolve` (abstract ref resolution), and `EnumClamp` (enum drift normalization). All save DTOs flowing through migration must implement `ISchemaStampedSaveData`.
- `ITruncationReporter` / `TruncationReporter` / `TruncationReport` — collect best-effort load truncation entries per load (newer-build loads, hash drift in shipping builds, dropped polymorphic refs). Drained by `SaveSystemCoordinator` after a successful load.
- `SaveTruncatedOnLoadMessage` — broadcast on `GameMessageBroker` when a load completes with truncation entries. Carries `Severity` (`Info` / `Warning` / `BlockOverwrite`) so UI can offer "back out, don't save over this" flows.
- `SaveSystemCoordinator.LastLoadTruncationReport` (pull-mechanism for callers that can't subscribe to the broker message) and `SaveSystemCoordinator.PostLoadHydrate` event (cross-controller post-load hydration; handlers awaited sequentially with isolated per-handler failures).
- `SaveSystemCoordinator.UnregisterLocalController(...)` / `SaveDomainControllerRegistry.UnregisterController(...)` — scene plugins call this on Shutdown so controllers holding scene-scoped Unity refs don't outlive their owning scene.
- `ProfileMetaData.WorldSeed` (serialized `int`) and **BREAKING:** `IProfileManager.SetWorldSeed(int)` — new required interface member. *Migration:* external implementers must add the member; a no-op body is acceptable if your project doesn't use a world seed. Existing save files load with `WorldSeed = 0` (no save migration required).
- `StartupFlowState.PendingScenarioId` (string) — set by the scenario picker before the gameplay scene loads; consumed and cleared by the orchestrator.
- `IntBoundDropdown` optional prev/next button bindings (`prevButton`, `nextButton`, `showNextPrevButtons`).
- `QuestEnums.QuestArchetype.Emergent` (value 5) — for runtime-spawned, storyteller-driven quests.
- `Runtime/VisualGraphs/TROUBLESHOOTING.md` — symptom-indexed guide for quest/graph runtime issues (reentrancy, registration leaks, stale progress, missing subscriptions).

### Changed

- **BREAKING:** `SaveSystemCoordinator(...)` constructor now requires `ITruncationReporter`. *Migration:* DI consumers via `GlobalInstaller` are unaffected (the installer now registers `TruncationReporter` and wires it into the factory). Direct constructor callers must pass `new TruncationReporter(...)` or `NullTruncationReporter.Instance`.
- `SaveSystemCoordinator.LoadAsync` is serialized via a private `SemaphoreSlim(1, 1)` so overlapping callers don't corrupt the truncation reporter's load scope. On success the coordinator drains the reporter, publishes `SaveTruncatedOnLoadMessage` when entries exist, and invokes `PostLoadHydrate` handlers.
- `ES3GameSaveSystem.SaveAsync` instruments per-controller timing (Warning above 100ms, Debug below) and the ES3 cache flush separately at Information, so total wall-clock cost is attributable.
- `QuestObjectiveIncrementNodeExecutor` / `QuestObjectiveSetNodeExecutor` use `objectiveDef.RequiredProgress` when initializing new progress, replacing the placeholder `Required = 1` that caused single-event objectives to read as complete on the first matching event.
- `VisualGraphPlugin.OnShutdown` unregisters its `GraphStateSaveController` from `SaveSystemCoordinator`, preventing stale controllers from leaking across scene transitions.
- `GuidScriptableObject` — `Guid`, `Timestamp`, and `DisplayName` setters relaxed from `protected` to public so tooling can set them without subclassing.
- Logging verbosity demoted (`Debug` → `Verbose`) across `Bootstrapper`, `ErrorSystemPlugin`, `InputRebinderPlugin`, `SettingsPlugin`, and `ES3GameSavePlugin`, continuing the verbosity-reduction theme from 0.5.0.
- `Runtime/VisualGraphs/VISUAL_GRAPHS_ROADMAP.md` adds a "Related Plans (External Consumers)" section pointing at dirigible2D's parity plan.
- `.gitignore` excludes Windows-reserved filenames `CON` / `CON.meta`.

### Fixed

- `GraphRunner.HandleMessageAsync` — per-dispatch reentrancy guard (`HashSet<string> executedThisDispatch`) prevents nodes wired into authoring-time feedback edges (e.g. `Check → Increment → Check` for re-evaluate-after-increment) from executing N times on a single message. Quest objective progress now increments by 1 per matching event as intended. `MaxExecutionSteps` (256) remains as the runaway safety net.
