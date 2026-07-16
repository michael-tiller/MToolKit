# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Releases are cut as annotated git tags and follow strict SemVer 2.0.0. Version
numbers are orthogonal to product milestones — a major bump signals an API break,
not a roadmap milestone.

## [Unreleased]

### Added

- `GraphEventRouter` feedback-loop guards. (1) A synchronous routing depth budget (`MaxRouteDepth`, 16): a graph that publishes an event it also subscribes to re-enters `RouteAsync` on the same call chain and previously recursed until the process died (stack overflow / OOM, no managed exception logged). Routing now drops the message with an Error log at the budget and unwinds via `finally`. (2) A per-runner dispatch-rate watchdog (`MaxDispatchesPerWindow` 100/s): a frame-deferred republish loop re-enters at depth 0 every hop and livelocks the main thread instead of overflowing — the watchdog suspends delivery to a runner exceeding the budget for `RateSuspendSeconds` (5s) with one Error log. `TimeProvider` is injectable for deterministic tests. Both shapes observed live from one wildcard content graph: first a hard editor crash (1,139 synchronous iterations), then a livelock (10 MB/min log storm) once the depth guard stopped the crash.

- Runtime theme system (`MToolKit.Theme`). `CurrentTheme` singleton broadcasts the active `Theme` over R3 (`OnThemeUpdated` value stream + `OnThemeChanged` old/new pair) plus a serialized `UnityEvent<Theme, Theme>` for Inspector listeners. A generic `ThemePreset<TAsset>` base binds a themed ScriptableObject to a target component and follows theme changes — resolving the same-Id asset from each new theme, falling back to the wired anchor, with an `overrideTheme` emergency hatch (Odin warning). Concrete presets: `SwatchPreset` (`Graphic` colour), `TypesetPreset` (TMP font / size / style / spacing + optional override material for outlines), `SpacingPreset` (`HorizontalOrVerticalLayoutGroup` padding / gap). `SwatchRegistry` / `TypesetStyleRegistry` / `SpacingRegistry` resolve assets by name-Id; `Theme` aggregates them. Enables light/dark and localization-font theming.

### Changed

- `SubviewButton.targetSubview` is no longer `[Required]`, and `OnClickSetSubview` null-guards a missing target — it logs a warning (Serilog) and returns instead of throwing, so a tab whose subview isn't wired degrades gracefully rather than NRE-ing.

### Fixed

- `GraphRunner.HandleMessageAsync` now gates entry nodes on the triggering message: an entry node whose `Parameters` declare a `MessageType` only starts for messages of that exact type, and one declaring a `DomainFilter` only starts for that exact domain. Previously EVERY entry node in a multi-trigger graph fired on ANY subscribed message — trigger A's action chain ran for trigger B's event, which is both wrong and the ignition path for event-graph feedback loops. Entry nodes declaring neither (dialogue starts, legacy quest entries) keep the fire-on-dispatch behavior.

- `NavigationService.PushAsync` now activates the target canvas GameObject before pushing a view. Canvases that sit dormant during gameplay (e.g. the Overlay canvas used for modals) previously stayed inactive, so a modal pushed onto them never rendered. Activation only — never deactivates here, since other canvas types share this path and may be expected to stay active when their stack is empty.

## [1.0.0] - 2026-05-28

First release cut as an annotated git tag and the switchover to strict SemVer 2.0.0.
Earlier versions (0.3.5–0.7.0) shipped through `package.json` without tags under a
pre-1.0 "breaking changes bump the minor" convention; that convention is retired.
Version numbers are now orthogonal to product milestones — a major bump marks an
API break, not a roadmap point.

### Added

- `NavigationSystem.Instance` — static cross-scope accessor to the live `NavigationSystem`, set in `Awake` (with a duplicate-instance guard that keeps the prior instance and warns) and cleared on teardown. Lets game-scope installers forward `IModalService` across sibling-isolated VContainer scopes that share no DI parent chain.
- `TruncationEntry.ReasonBelowFloor` (`"below-floor"`) — canonical reason string for a best-effort load of a stamped save whose version is below the compatibility floor (no version-specific `Migrate` body runs); classified at least `Warning`.

### Changed

- **BREAKING:** `MigrationOutcome.TruncatedFromNewerBuild` renamed to `MigrationOutcome.TruncatedBestEffort`. *Migration:* update references to the new name; it now covers newer-build, below-floor, and shipping-build hash-drift loads.
- **BREAKING:** Removed `ForwardMigrator<T>.AllowsBestEffortNewerBuildLoad`. Per ADR-0016 a stamped save is never refused, so the opt-out no longer exists. *Migration:* delete any override — load-bearing schemas can no longer force `RefusedFatal` on a newer-build or hash-drift load.
- Below-floor loads (`loadedVersion < MinimumSupportedVersion`) now load best-effort (`EnsureContainers` + `Normalize` + re-stamp) with a `below-floor` truncation report instead of returning `RefusedFatal` (ADR-0016). `MinimumSupportedVersion` becomes the validated-transform boundary, not a refusal boundary; the only `RefusedFatal` path left is an unstamped save.
- `TruncationReport.ComputeSeverity` escalates `below-floor` entries to at least `Warning` (alongside `hash-drift-shipping`), since an unmigrated below-floor load is qualitatively stronger than plain version drift even at zero dropped items.
- `Runtime/Persistence/README.md` rewritten around ADR-0016: the compatibility floor documented as a validated-transform boundary, the 7-row load dispatch reflects never-refuse-stamped-saves, and `PolymorphicResolve` notes ES3 missing-type behavior is not yet empirically pinned.

## [0.7.0] - 2026-05-25

### Added

- `Runtime/Persistence/Prewarm/` — pre-load compatibility-check subsystem (ADR-0012 "save prewarm" tier). `ISavePrewarmChecker` / `SavePrewarmChecker` regex-scan the save file's metadata header (no DTO graph hydration, no Unity instantiation) and emit a `SavePrewarmReport` carrying build-version delta, per-domain schema-hash mismatches, and mod-manifest diff. `IContinueGuard` is the main-menu gate that consumes the report and surfaces the user modal; `IModManifestProvider` is the optional adapter for mod-aware games (omit for headless / template scenes). `SchemaHashRegistry.Build(...)` snapshots the current build's hashes once at startup, keyed by DTO type full name (so the checker matches against the save file's per-section `__type` field — robust against DomainKey vs save-section-key naming differences).
- `Runtime/Persistence/Interfaces/IPostLoadHydrator.cs` — `IPostLoadHydrator.HydrateAsync(CancellationToken)` formalizes post-load cross-reference hydration. Hydrators run inside the load scope (after every `ISaveDomainController.LoadAsync` completes, before the truncation reporter is drained) so cross-reference rot reported by handlers merges into the single drained report. Companion `INonMutatingHydrator` marker opts read-only/diagnostic hydrators out of the no-op-hydrator audit reflection guard.
- `Runtime/Persistence/Migration/IForwardMigratorBase.cs` — non-generic surface for `ForwardMigrator<T>` (exposes `Domain`, `CurrentSchemaVersion`, `CurrentSchemaHash`, `SaveDataType`) so infrastructure code (prewarm checker, diagnostics) can enumerate migrators without knowing each `TSaveData`. Generic `IForwardMigrator<TSaveData>` remains the typed surface used by save controllers.
- `TruncationEntry.ReasonRegistryReferenceDropped` / `ReasonLiveReferenceDropped` / `ReasonLiveReferenceDemoted` — canonical reason-prefix constants for the post-load hydrator pipeline. `ReasonLiveReferenceDemoted` carries a severity exemption (data is preserved; only the back-reference is lost), so a demote-only report stays at `Info`.
- `EnumClamp.ClampIntField<TEnum>(int, TEnum)` / `ClampByteField<TEnum>(byte, TEnum)` — storage-typed wrappers for `int`/`byte` fields backed by integer-enums; validates the fallback too so an out-of-range fallback (programmer error) can't silently stamp bad data.
- `SaveSystemCoordinator.IsSaveBlocked` — sticky `ReactiveProperty<bool>` that ratchets to `true` when `LoadAsync` drains a `BlockOverwrite`-severity truncation report. Guards `SaveAsync`, the auto-save loop, manual auto-save, and scene-change save uniformly; one-way (recovery requires app exit) so the save-safety invariant holds even when no presenter is alive.
- `AssetReferenceBase<T>(string guid, string subObjectName)` constructor — sub-asset addressing without requiring a custom subclass per sub-object name.

### Changed

- **BREAKING:** `IModalService.CreateModalView<T>` adds an optional `Action<T> postInit = null` parameter that runs after `Initialize` for subclass-specific setup (e.g., populating a scroll-list body). *Migration:* call sites are source-compatible (default value). External implementers of `IModalService` must add the new parameter to their method signature — a `null`-tolerant body is acceptable when the implementer has no per-subclass setup hook.
- `ForwardMigrator<T>` now implements `IForwardMigratorBase` via explicit interface implementation. Existing subclasses with `protected override string DomainKey => ...` are unaffected — the explicit-interface accessor delegates to `DomainKey`.
- `SaveSystemCoordinator.LoadAsync` now invokes `PostLoadHydrate` INSIDE the load scope so hydrator drops reported via `ITruncationReporter` merge into the single drained report (previously fired after the drain, so hydrator entries went unreported). Sets `IsSaveBlocked = true` BEFORE publishing `SaveTruncatedOnLoadMessage` when severity is `BlockOverwrite`, so any subscriber that reads `IsSaveBlocked` sees the consistent post-block state.
- `TruncationReport.ComputeSeverity` exempts entries whose reason starts with `ReasonLiveReferenceDemoted` from the drop accumulator and the "any non-zero drop" floor — demote preserves data, so a demote-only report stays at `Info`. `IsLoadBearing` still escalates if set on a demote entry (defensive — no current caller sets it).
- `GraphStateSaveController` demotes per-graph "save data contains state for graph X but no runner found" entries from Warning to Debug to avoid drowning the console with one warning per dungeon/dialogue/event graph in the save. The aggregate post-loop summary now escalates to Warning when `missingCount > 0`, preserving the operational signal.
- `ES3GameSaveSystem.LoadAsync` and `SaveSystemCoordinator.LoadAsync` error logs now include `ExType` and the full `ex.ToString()` (inner stack, nested exceptions) instead of just `ex.Message` — load failures inside ES3's reflection-driven deserializer often surface as bare `MissingMethodException`/`SerializationException` whose `Message` alone identifies neither the offending type nor the field.

### Fixed

- `NavigationInstaller.Install(IContainerBuilder builder)` was calling `base.Configure(builder)`, which resolved to `LifetimeScope`'s empty stub instead of this class's override — `INavigationService` and `IModalService` were silently dropped from the parent scope's registrations. Root cause of the `SaveTruncationDialogPresenter` not wiring in dependent game scopes. Now calls `Configure(builder)` directly.

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
