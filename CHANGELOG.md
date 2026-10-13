# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Releases are cut as annotated git tags and follow strict SemVer 2.0.0. Version
numbers are orthogonal to product milestones — a major bump signals an API break,
not a roadmap milestone.

## [Unreleased]

### Fixed

- Preserve fault-on-await when Addressables is disabled while clearing bootstrap compiler warnings; verify private serialized graph values during export. (v1.6.1)

### Added

- Add ordered, isolated GameRules, scoped state access, owned event teardown and canonical dialogue routing; preserve legacy interface implementers with default members. (v1.6.0)

- Add value transforms, conditional math, typed Select, and entry-derived event graph subscriptions; apply scoped Vector3 transform operations. (v1.5.0)
- Add typed scoped math and logic graph operations, including Subtract, Divide, Compare, AllTrue and AnyTrue, event graph contexts, and declaration validation. (v1.4.0)
- Shared graph-variable literal syntax supports all seven types with invariant finite numbers, escaped strings, vectors and RGBA colors; document the canonical Dirigible variables contract. (v1.3.0)

### Added

- Durable ES3 save contexts, awaited flush receipts, completion callbacks, and save-before-clone workflow, combined with discarded-local-restore recovery. Both coordinator save entry points retain the session boundary through flush completion and receipt notification (v1.2.0).
- Typed provisional capture participants preserve live controller save bindings across temporary-profile captures; coordinator scopes hold the session boundary and unwind participant scopes in reverse order (v1.2.0).

### Fixed

- Cancelled or failed profile save batches discard cached controller writes before local-session recovery, preserve the original failure, and publish no completed receipt (v1.2.0).

- `InputService` now enables a `Shared` action map and resolves gameplay maps when they use a name other than `Player`; shared Pause takes priority while UI/Player fallbacks remain supported (v1.2.0).

### Changed

- `Bootstrapper` no longer calls `DontDestroyOnLoad`; the target scene loads as Single, so the bootstrapper is disposed with its own scene at the transition (v1.0.2)
- `NavigationSystem`: initial-canvas-shown and view-validation logging raised to Information; cancellation/failure logs now include `initialViewsShown` state and the exception object (v1.0.2)

### Added

- `SaveSystemCoordinator.BeginLocalSessionAfterDiscard()` enables recovery after a failed
  local restore once gameplay ownership is discarded. Complete coordinator save/load
  operations block recovery; severe truncation, global failure and unclassified save
  blocks remain application-sticky. (v1.1.0)
- `QuestObjectiveAddressableLocator` utility to validate Addressables key existence without triggering load errors in EditMode tests (v1.0.1)

### Changed

- Quest objective executors now pre-check asset existence before attempting Addressables load (v1.0.1)
- Objective executors fall back to state-based lookup if asset doesn't exist (for missing or test-synthetic GUIDs) (v1.0.1)
- Refactored duplicate "enqueue all outputs" pattern into helper methods across objective executors (v1.0.1)

### Fixed

- `Bootstrapper`: guard `OnBootstrappedAsync`/`ForceQuit` against running after the Single-mode scene load has already destroyed the bootstrapper object (v1.0.2)
- `GlobalInstaller`: log the destroyed plugin's type name when skipping it during initialization, and warn (not verbose) when `NavigationPlugin` is missing for canvas visibility handling (v1.0.2)
- `Bootstrapper` now uses `DestroyImmediate` in Editor when not playing, preventing stale-handle errors (v1.0.1)
- Quest objective executors no longer fail-load when Addressable keys reference non-existent or synthetic-GUID assets (v1.0.1)
- Null reference exceptions by using optional-chaining and null-coalesce in objective definition access (v1.0.1)
- Negative progress values now clamped to 0 in increment/set objective executors (v1.0.1)

### Added

- Message-bus interception — `GameMessageBroker`/`GlobalAsyncMessageBroker` gain `InterceptPre<T>` / `InterceptPost<T>` / `ResetInterceptors()`, plus `HandlerPriority` (First/Normal/Last bands) and the opt-in `ICancellableMessage`. A pre-op runs before the message reaches the bus and may veto it (`Cancel`); a post-op always runs afterward — including after a cancel and after a throwing subscriber — and can observe cancellation. Bands run ascending and registration order is execution order within a band. Registries are per broker scope, so game and global registrations never cross. A throwing interceptor is attributed to its `owner` and surfaced through `ErrorSystem` (i.e. `ErrorPanel`) rather than swallowed, with recursion closed by error-type suppression plus a reporting-depth guard. **Scope limit:** this intercepts the two static broker façades only — publishes made through an injected or resolved `IPublisher<T>` (e.g. `QuestManager`, `SettingsSystem`) are not intercepted. Cancellable messages must be reference types; this is enforced at registration and at dispatch, not merely documented. Purely additive — no existing call site changes.

- `ProfileMetaData.WorldSettingsJson` — an opaque per-profile string blob for consuming projects to persist their own world/game-creation settings model, threaded through the constructor and `SaveProfile`'s pending-merge exactly like the existing `WorldSeed` field. `IProfileManager.SetWorldSettingsJson(string)` mirrors `SetWorldSeed`'s pending-field + live-metadata-update pattern. MToolKit does not interpret the JSON — it's opaque storage, same contract as the rest of `ProfileMetaData`. Legacy profiles without the field deserialize to an empty string (best-effort persistence).

- `IGraphicsSettings.DisableCrt` / `IGraphicsSettings.DisableBloom` — two effect-agnostic `ReactiveSetting<bool>` graphics settings (default `false` = effect on), surfaced as bound toggles via `GraphicsSettingsInitializer` and persisted through the INI plumbing (`SettingsSystem` load/save + `IniService` populate-defaults). The settings are deliberately effect-agnostic: MToolKit only stores/persists the flags; the consuming project subscribes to the reactive value and performs the actual render-pipeline toggle. Additive — the only implementer is MToolKit's own `GraphicsSettingsModule`, updated here, so no external caller changes.

- `Swatch` — implicit conversion operators to `Color` and `Gradient`, so a swatch feeds a Color/Gradient API directly (e.g. `image.color = swatch`) without a preset or manual unwrap. A Color-typed swatch projects to its flat color (or a flat two-stop gradient); a Gradient-typed swatch samples at t=0 for Color and returns its gradient; a null swatch yields white. Purely additive — no caller changes required.

- `IIniService.Initialization` and `ISettingsSystem.Initialization` — `UniTask` members exposing when the INI / settings load has completed, so consumers can await one shared load. Additive: these are DI service contracts consumed rather than externally implemented (the only implementers are MToolKit's own `SettingsSystem` / `IniService` plus test doubles, all updated here), so this is a minor addition, not an API break.

- `AudioPanelComponent` / `AudioScrollbarComponent` — MonoBehaviours that play UI audio cues (panel open/close, scrollbar movement) through `IAudioService`.

- `ThemePreset.SetStart(asset)` — swaps the wired themed asset at runtime and re-applies, so the new asset becomes the resolve key for future theme changes (active-state feedback, e.g. button tint swaps).

- EditMode + PlayMode test suites: `AudioServiceInitializationTests`, `IniSettingsInitializationTests`, `AudioPanelComponentTests`, `AudioScrollbarComponentTests`, `GlobalMusicManagerTests`.

- `GraphEventRouter` feedback-loop guards. (1) A synchronous routing depth budget (`MaxRouteDepth`, 16): a graph that publishes an event it also subscribes to re-enters `RouteAsync` on the same call chain and previously recursed until the process died (stack overflow / OOM, no managed exception logged). Routing now drops the message with an Error log at the budget and unwinds via `finally`. (2) A per-runner dispatch-rate watchdog (`MaxDispatchesPerWindow` 100/s): a frame-deferred republish loop re-enters at depth 0 every hop and livelocks the main thread instead of overflowing — the watchdog suspends delivery to a runner exceeding the budget for `RateSuspendSeconds` (5s) with one Error log. `TimeProvider` is injectable for deterministic tests. Both shapes observed live from one wildcard content graph: first a hard editor crash (1,139 synchronous iterations), then a livelock (10 MB/min log storm) once the depth guard stopped the crash.

- Runtime theme system (`MToolKit.Theme`). `CurrentTheme` singleton broadcasts the active `Theme` over R3 (`OnThemeUpdated` value stream + `OnThemeChanged` old/new pair) plus a serialized `UnityEvent<Theme, Theme>` for Inspector listeners. A generic `ThemePreset<TAsset>` base binds a themed ScriptableObject to a target component and follows theme changes — resolving the same-Id asset from each new theme, falling back to the wired anchor, with an `overrideTheme` emergency hatch (Odin warning). Concrete presets: `SwatchPreset` (`Graphic` colour), `TypesetPreset` (TMP font / size / style / spacing + optional override material for outlines), `SpacingPreset` (`HorizontalOrVerticalLayoutGroup` padding / gap). `SwatchRegistry` / `TypesetStyleRegistry` / `SpacingRegistry` resolve assets by name-Id; `Theme` aggregates them. Enables light/dark and localization-font theming.

### Changed

- `GameMessageBroker` and `GlobalAsyncMessageBroker` are now façades over a shared `MessageBrokerCore<TScope>`; they were previously the same ~450-line implementation twice, differing only in log strings, one log level, and where the editor-only diagnostic DTOs were declared. Statics on a closed generic are distinct per scope, which reproduces the two-independent-brokers property exactly. **No API change** — every public signature, optional-parameter default, and `#if UNITY_EDITOR` boundary is preserved, as are the per-broker double-init log levels (Global `Verbose`, Game `Warning`) and Serilog source contexts. The one behavioural change is the unregistered-type remediation string, which named a specific consuming project's installer; it is now scope-generic.

- `IniService.LoadAsync` is now a poll-based single-flight (lock + completion flag): the first caller runs the physical load, concurrent callers await a `UniTask.WaitUntil` on a completion flag, and the load error (if any) is rethrown to every caller. Replaces the `LoadCoreAsync().Preserve()` + `AttachExternalCancellation` sharing, which threw `InvalidOperationException: "Already continuation registered, can not await twice"` under concurrent awaiters (`UniTaskCompletionSource`/`AsyncLazy`/`Preserve` are all single-continuation while pending).

- `AudioService` init hardened: `InitializeAsync` is a single-flight that awaits `settingsSystem.Initialization` before building the one-shot pool, wrapped in try/catch so a *faulted* settings init logs a warning and continues instead of leaving the pool unbuilt; `LoadAudioSourcePrefabAsync` / `InitializePool` / `SetMixerVolume` are now `protected virtual` (test seams).

- `SettingsSystem` implements `IDisposable` (disposal guard); `InitializeIniIntegrationAsync` is an awaited `UniTask` (was fire-and-forget `UniTaskVoid`), preserved into the new `Initialization` property.

- `GlobalMusicManager` awaits settings initialization before playing default music, and its crossfade cancellation uses a generation counter to avoid stale-fade races.

- `GlobalMusicManager` drives music loudness reactively from `MasterVolume × MusicVolume` (R3 subscription) at the `AudioSource` as the single authority — replaces a once-computed `playbackTargetVolume` snapshot and the mixer `MusicVolume` param (which the Music group was observed not to honor at runtime). The crossfade reads the live target so a mid-fade change is applied.

- `SubviewButton.targetSubview` is no longer `[Required]`, and `OnClickSetSubview` null-guards a missing target — it logs a warning (Serilog) and returns instead of throwing, so a tab whose subview isn't wired degrades gracefully rather than NRE-ing.

### Fixed

- `GameLoader` scene loading probed the key with `Addressables.LoadResourceLocationsAsync` before calling `LoadSceneAsync`. A manifest scene with no location made Addressables raise `InvalidKeyException` through its own delayed-action pump, which reaches the Unity console as an unhandled *error log* some frames later — during an EditMode run that log lands in whichever unrelated test is executing and fails it (observed poisoning `ES3SaveableTests` from `GameLoaderTests`). The probe returns an empty result instead of logging, so a missing scene now yields one deliberate warning and the same graceful skip.

- `IniService.LoadAsync` always routed non-first callers through `UniTask.WaitUntil`, even when the load had already finished, so `Initialization.Status` read `Pending` for a completed load until the next player-loop tick. Added the already-completed fast path; late callers now return synchronously.

- Boot-time "await twice" fault in the shared INI load faulted `SettingsSystem.Initialization` — a persistent bootstrap singleton, so the fault was cached for the whole session — and, because `AudioService` now awaits it before building its pool while `AudioPlugin` fire-and-forgets init, it silently killed ALL audio. Fixed by the poll-based single-flight above.

- `AudioService.PlayOneShotAsync` NRE (`audioSourcePool` null) when a one-shot fired before init completed or after dispose — now guarded to skip the sound.

- Music mute/volume race — setting Music to 0 only intermittently silenced playback because volume was captured at imperative snapshot moments and split between the `AudioSource` and the (runtime-unreliable) mixer param. The reactive source-volume authority makes `Music = 0` deterministically silent.

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
