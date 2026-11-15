# Hierarchical action menu architecture and migration boundary

This record explains the module boundaries, extension contracts and ownership decisions.
See [the usage guide](ContextMenus.md) for authoring and validation instructions and
[the migration guide](ContextMenusMigration.md) for the Dirigible compatibility boundary.

## Repository and source boundary

MToolKit owns the reusable action/session, presentation, geometry and input infrastructure.
Dirigible owns its gameplay adapters, domain command validation and compatibility facade.
Neither the framework action model nor its supplied profiles reference Dirigible gameplay
assemblies.

The design reuses MTK Navigation, Theme, Input, Localization, AssetLoader, Core,
installation and UI scaling. Dirigible's context-menu, priority, menu-bar and canonical
Actions contracts remain integration concerns, including the polymorphic action-tree
direction in ADR-0023. No Asset Store source is part of the public module.

`ARCHITECTURAL_DECISIONS.md` remains authoritative: POCO-first services, VContainer composition, plugin lifecycle, MessagePipe contracts, R3 reactive state, UniTask async operations, structured Serilog diagnostics, and MVP UI. The menu feature consumes these systems; it does not create another DI container, global action registry, navigation framework, localization engine, rebinding engine, or gameplay dispatcher.

## Existing capability map

| Responsibility | Existing MTK implementation | Menu use |
| --- | --- | --- |
| Plugin/configuration | `Core/Abstractions/ConfigPlugin<TService,TInterface,TConfig>`, `DomainPlugin<TService,TInterface>`, `AbstractRuntimePlugin`, `IDependencyDeclaration` | Register configured menu service/profile defaults and explicit dependencies. Await prewarm in runtime initialization and dispose host state on shutdown. |
| Composition | `Installer/GlobalInstaller.cs`, VContainer `IContainerBuilder`/`IObjectResolver` | `ContextMenuPlugin` constructs the service through normal registration. `ContextMenuHost` can receive the injected service. Presenter prefabs are instantiated by the host and receive explicit `MenuPresentationContext` services; automatic injection into arbitrary prefab fields is not promised. |
| Navigation | `Navigation/Interfaces/INavigationService.cs`, `NavigationService`, `NavigationSystem`, `BackRequestMessage` | Coordinate host placement/focus with existing navigation, while handling menu path/Back locally. |
| Runtime input | `Input/Interfaces/IInputService.cs`, `InputService`, `Input/Prompts/IInputActionSource` | Use the existing action asset and resolve authored action references to its runtime clone. Route events to menu semantics. |
| Binding/glyphs | `IInputRebinderService`, `IInputPromptService`, `TMPInputPrompt`, `InputPromptService.Changed` | Keep hints tied to effective bindings and active device; preserve existing rebinding and locale notifications. |
| Localization | `ILocalizationService.Language`, Unity `LocalizedString`, `LocalizationHelper` | Resolve label/description identities with fallback; refresh bound visible text on locale changes. |
| Assets | `AssetReferenceBase<T>`, `IRuntimeAssetService`, `IAssetLoader` | `ContextMenuPrefabReference : AssetReferenceBase<GameObject>` supplies the optional presenter reference. `MenuPrefabCache` owns prewarmed assets. Item, center and secondary widget references are direct `GameObject` references. |
| Theme | `IThemeService`, `ThemePreset<TAsset>`, `SwatchPreset`, `TypesetPreset`, `SpacingPreset` | Semantic visual tokens, live updates and authored local overrides. |
| Responsive UI | `UserUiScaleCanvas`, `UiScaleCapacity`, `TypesetPreset` accessibility calculation | React to existing scale/viewport events, preserve minimum text/hit sizes, reposition or use configured overflow. |
| List overflow | `MenuSession` page state and `ListMenuPresenter` | The supplied list bounds its page capacity and maps the scroll wheel to paging. It does not claim to contain a smooth `ScrollRect`; custom presenters may reuse `UI/ScrollIntoViewOnSelect.cs` when they supply one. |

Important API details:

- `IInputService` itself provides initialization, enable/disable, polling and pointer edges. `InputService` additionally implements `IInputActionSource`, exposing `RuntimeActions` and `ResolveRuntimeAction(InputAction)`. `Initialize` clones the authored asset. Menu callbacks must use the resolved runtime action by stable InputAction GUID, not mutate/bind only the source asset.
- `IInputPromptService` already exposes `Resolve`, `ResolveReference`, `Localize`, `Compose`, `Bind(TMP_Text, Func<string>)`, `Context`, and `Changed`. The service observes effective binding, device, locale and prompt-settings changes. Menu widgets need only own their binding lifetime.
- `ILocalizationService` describes locale lifecycle, not a translated-label lookup API. Use an explicit table/key/fallback descriptor and Unity `LocalizedString` or existing `LocalizationHelper` calls; do not assume a nonexistent `ILocalizationService.GetText`.
- `IRuntimeAssetService.LoadAsync<T>(string, CancellationToken)` returns a loaded asset and pairs with `Release(Object)`. `AssetReferenceBase<T>` is the typed authoring reference convention. An instantiated prefab's lifetime is distinct from the loaded prefab asset's retained handle.
- `UserUiScaleCanvas` exposes `ScaleChanged`, `ViewportChanged`, `ConstraintChanged`, `Capacity`, and `EffectiveUserScale`. CanvasScaler already applies the resolution/user scaling. Do not multiply local menu radii by user scale a second time.

## Assembly dependency direction

The module assembly and namespace are `MToolKit.ContextMenus`, under `Runtime/ContextMenus/`. The existing runtime assembly remains `MToolKit.Runtime` and theme remains `MToolKit.Theme`.

```text
Dirigible gameplay/context adapters and compatibility facade
                       |
                       v
               MToolKit.ContextMenus
                |                |
                v                v
         MToolKit.Runtime <--- MToolKit.Theme
                |
       existing framework dependencies
```

`MToolKit.Theme` already references `MToolKit.Runtime`. Placing menu code that consumes theme types directly into the Runtime assembly would create a cycle. The new menu assembly references both existing assemblies; neither existing assembly references the new module. A menu plugin subclasses the existing plugin bases from the new assembly. Test assemblies explicitly reference the new module rather than relying on an auto-reference accident.

All adapters referencing ActorId, tool services, construction, zones, world snapshots, or Dirigible ActionRequest stay in Dirigible. The public package must compile without any Dirigible assembly or optional graphics package.

## Small extension surface

These public contracts separate action semantics, session state, presentation and ownership.
See [ContextMenus.md](ContextMenus.md) for usage against the current API.

| Contract | Responsibility |
| --- | --- |
| `MenuActionId` | Stable typed string identity local to an action hierarchy/placement; no global gameplay enum. |
| `MenuAction` | Runtime action descriptor: identity, localized display facts, live state, optional icon/hint/metadata, behavior, children, secondary actions, and optional submenu profile override. Authored assets produce the same descriptor. |
| `IMenuActionBehavior` | Polymorphic activation for command, submenu, toggle, choice and noninteractive/header behavior. Presenters never switch on a gameplay action kind. |
| `IMenuChildrenProvider` | Resolve context-sensitive children on entry/refresh, with cancellation and stable child identities. Static collections use the same resolution boundary. |
| `MenuContext` | Opaque caller context plus validity predicate, cancellation and invalidation. Knows no actor, tile, inventory or tool implementation. |
| `MenuSession` | Hierarchy/path, selection, expansion, pages, interaction mode, validity, execution coordination and close. POCO independent of scene/prefab. |
| `IContextMenuPresenter` | Bind session/profile, render state changes and own all presentation/layout/animation behavior; deterministic unbind/disposal. |
| `IContextMenuHostService`, `ContextMenuHostService` | One active menu per service instance, named/explicit profile selection, pending/prewarm generation, presenter replacement, ownership and cleanup. `ContextMenuHost` is the optional component; `ContextMenuPlugin` is the normal configured plugin. |
| `MenuOpenRequest`, `MenuHostConfig`, `NamedMenuProfile` | Caller context/canvas/position, explicit or named profile, optional anchor override, held activation, gameplay actions to suspend and live input-origin Back ownership predicate; configured project defaults remain separate from action definitions. |
| `MenuProfile` | ScriptableObject composition: direct prefabs or a typed presenter reference, navigation, anchoring, geometry, widgets, skin, input bindings, minimum targets, optional `Transition` and `FallbackProfile`. `DrawSegments` permits button-only radial compositions; `ShowCenter` controls the center. |
| `MenuInputBindings`, `MenuInputRouter`, `MenuInputOwnership` | Resolve configured actions, emit semantic operations and retain exact enabled-state ownership. The ownership coordinator also prevents held-control replay during release. These are concrete public types; there is no `IMenuInputLease` API. |
| `IMenuPointerConfirmationTarget` | Optional additive pointer-origin operations, `ConfirmPointer` and `EndPointerHold`, preserved through the host proxy. Existing custom targets can retain `IMenuInputTarget`. |
| `IMenuWidget`, `MenuWidgetState`, `IMenuActionDetails` | One shared snapshot for replaceable action/center/secondary widgets; optional game-provided cost, variant and progress metadata. |
| `IMenuViewportAwarePresenter`, `IMenuContainmentEvents` | Optional containment result and change notification used by the host to select the configured accessible fallback. Custom presenters opt into these contracts explicitly. |
| `RadialSegmentGeometry` | Cached annular angular/radial definition shared by mesh generation, content anchors and shape hit testing. |

Keep widget binding composable and optional rather than introducing one marker interface for every label field. A custom presenter can own its complete widget hierarchy. Native widgets bind one selected-action snapshot and expose useful replaceable responsibilities such as action display, center information and selection feedback. Missing optional text/icon/center/secondary components are valid; missing a required presenter is a useful configuration error.

## Action resolution and invocation

Actions contain behavior and presentation facts without knowing whether the caller chose list, radial, replacement, concentric or a custom presenter. Runtime descriptors are valid without ScriptableObjects. For authored actions, derive `MenuActionDefinitionSO` and implement `CreateAction(MenuContext)`; its public `Create` applies authored identity, localization, icon, binding and secondary definitions. `MenuActionSetSO`, `MenuSubmenuDefinitionSO`, `MenuHeaderDefinitionSO` and `MenuChildrenProviderSO` cover common authored compositions. Definitions do not retain mutable session state.

`MenuDefinitionPlacement` qualifies reused child and secondary identities by their authored parent placement and forwards source change notifications. Duplicate sibling IDs, missing required definitions and authored cycles produce diagnostics. A submenu definition can supply a primary definition and `ProfileOverride`; a runtime descriptor carries the same choice through `PresentationOverride`.

Visibility, enabled state, checked state and disabled reason are evaluated against the current MenuContext. Sessions subscribe to caller invalidation and explicit refresh signals. Re-evaluate children on entering the submenu, on requested refresh and on relevant invalidation. Preserve selection/expansion by stable identity only when that identity remains available in the newly resolved level. Remove stale selections and descendant paths when their parent vanishes or becomes inaccessible. Duplicate sibling identities fail with an actionable diagnostic rather than selecting an arbitrary command.

Every command and secondary action is revalidated immediately before execution, even if the UI previously showed it enabled. `MenuContext` rejects a destroyed Unity target using Unity's lifetime check, while the game adapter's predicate/invocation service validates its domain snapshots; ordinary CLR null checks alone do not establish Unity object validity. Async provider completion carries both session/open generation and provider request generation, so an old result cannot restore stale selection or reopen a retired menu.

Entering a submenu is a navigation operation and never executes its optional primary command. A parent with both children and a command exposes a deliberate primary invocation distinct from Enter. The default list parent activation expands/collapses its foldout; radial confirmation enters according to the selected navigation policy. Secondary operations are descriptors with independent availability and configured keep/refresh/navigate/close outcomes. Binding identities belong to input configuration, not gameplay action definitions.

Successful leaf command execution uses one shared path and its configured post-execution result. A repeated activation while execution is pending does not start a duplicate command. Cancellation is coordinated by the session. Command exceptions propagate through its UniTask; game-specific adapters retain their own structured domain diagnostics. Host cleanup guards independent presenter unbind, destruction and router disposal so a custom lifecycle exception does not skip later owned resources.

## Session and presentation behavior

The session owns a path of stable action identities and per-level selection/page state. Inline expanded branches are session state separate from the current replacement path. Back first returns to the previous menu level (or collapses the applicable expanded branch); at root it cancels the host session. Selection changes update the same action snapshot used by item widgets and center information.

Default presentations consume that state:

- **List:** `ListMenuPresenter` uses `MenuSession.Rows`, inline expansion by default, configured replacement/flyout navigation, disabled/checked feedback and bounded pagination. `MenuAnchorPolicy` supports cursor/top-left, centered, top-right, bottom-left, bottom-right, top-center and bottom-center placement; a request may override the profile's anchor without mutating it. Scroll-wheel input pages rather than claiming smooth scrolling. Keyboard/controller state remains in the session.
- **Single ring:** Render visible actions as a configurable annular ring with angular gaps and icon/label anchors. The center is optional; empty center/dead zone is not an action.
- **Concentric:** Retain selected parent and show associated children in an independently sized outer arc. The two rings are a sliding window on the arbitrary-depth session path. Selecting a different parent refreshes associated children; entering deeper advances the window rather than imposing a depth-two limit.
- **Replacement:** Entering a parent resolves its children and optional profile override, which can replace segment/root/center prefabs or the entire presenter. Execution and Back stay in the same session.
- **Custom:** A prefab-hosted `IContextMenuPresenter` owns its transforms, rendering, hit testing and animation. It receives the shared session and presentation context. The host does not automatically inject arbitrary fields or apply native-presenter transforms after replacement.

The host resolves and binds the candidate, publishes the new presenter/router, and then retires the old presenter/router. It retains the same `MenuSession` and logical path; `PathActions` lets it resolve the applicable profile and restore an ancestor profile on Back. Overlapping input leases prevent a replacement from enabling gameplay between levels. A custom root owns its private children; the host does not continue default layout or animation after replacement.

## Native geometry, rendering and responsive placement

`NativeRadialGraphic` uses `MaskableGraphic.OnPopulateMesh` and shape-aware raycast filtering. `RadialRingGeometry` trims the configured gap from both visible edges. `RadialSegmentGeometry` then exposes inner/outer radius, visible start, signed sweep, center angle, content anchor and ring/segment indices. Rendering and selection consume that same definition. Angles start on +X, with a negative sweep for clockwise order, and normalize across 0/360.

Hit testing transforms the screen point into the geometry owner's local RectTransform using the actual event/canvas camera. Separate ring instances determine the owning band and segment; empty center, inter-ring gap, angular gap and outside all return no segment. `RadialAnalogSelector` applies the controller dead zone independently. With native wedge visuals enabled, rectangular icon/button descendants cannot expand the authoritative wedge hit region.

Geometry/mesh caches invalidate on structural/layout/scale changes and relevant visual state changes, not every frame. Use bounded segment tessellation and cached widget reuse; avoid per-frame LINQ, forced canvas rebuilds and global pointer polling. Borders and separators use the same radial/angular boundaries, so visible and interactive regions agree.

Profiles own logical dimensions; the existing UI scale system owns scaling. `MenuScreenPlacement` transforms bounds into canvas coordinates and repositions them without reducing text or target size. The list bounds its page capacity; the radial presenter bounds angular capacity and can reduce a concentric window to its current ring. If the result still cannot fit, the host uses `FallbackProfile` or reports the containment failure. `IMenuContainmentEvents` makes resize/scale fallback event driven. A caller can override profile anchoring through the presentation context without changing the shared profile asset.

## Input ownership and navigation coordination

Existing INavigationService is a View stack, not a menu path stack. NavigationSystem handles BackRequestMessage by popping the addressed canvas. Menu Back is therefore consumed locally; do not publish BackRequestMessage for nested menu navigation or use a full-screen View per level.

`MenuInputOwnership` captures individual gameplay/menu action-enabled states; it does not enable whole maps on close. `MenuInputRouter` subscribes to configured action callbacks. When the rebinder implements optional `IScopedInputRebinderService`, the router owns a `RegisterScopedAction` lease for each actual runtime action. `InputRebinderService` distinguishes actual references, retains permanent application registrations, reference-counts scopes and cancels an owned interactive operation before final scoped removal. Scope release precedes input-ownership restoration. Legacy rebinders keep permanent application registration; a host with owned foreign clones omits unsupported legacy registration.

`MenuProfileActionSource` resolves existing service-owned actions and creates disabled host-owned clones for explicitly prepared foreign profile assets. It does not silently bind unrelated gameplay actions to a foreign clone. `MenuPresentationContext.ResolveInputAction` gives glyph rendering the same execution identity. Supplied hint widgets own real `IInputPromptService.Bind` subscriptions against that resolved action, so foreign-clone rebinding/device changes update the bound text without changing action definitions.

InputAction GUIDs identify actions within a known asset relationship; they do not authorize mapping an unrelated same-GUID asset. MTK's `InputService` resolves only its initialized authored asset into its runtime clone. Dirigible's scoped source gives its directly executed canonical instance priority, then delegates other explicitly known MTK ownership. Gameplay suspension uses those execution instances, including the game's Colony/ARPG actions, rather than disabling an unrelated authored copy.

The live `MenuOpenRequest.BackInputAllowed` predicate coordinates input-origin Back with a game's higher-priority interaction owner. Semantic routing checks it before forwarding Back; default native presenters consult `MenuPresentationContext.CanHandleBackInput`. Programmatic `MenuSession.GoBack()` is still explicit navigation. This is input-edge ownership, not a second MTK navigation stack.

Default presenters capture and restore EventSystem selection. Their scoped navigation lease preserves the previous `sendNavigationEvents` value, stacks independent host owners and restores correctly when hosts close out of order. Semantic profiles disable only the EventSystem navigation pass; native pointer dispatch remains active. Native profiles leave navigation to the configured uGUI module. A complete custom presenter owns its own input/focus implementation through the shared semantic contract.

Use one activation owner. `InputSystemUIInputModule` records pointer state in its Input System callbacks and sends uGUI clicks later on release; a semantic router callback can run first. The optional pointer-origin contract therefore remains distinct from keyboard Submit. The default presenter raycasts native center/secondary/footer controls before wedge selection, so overlapping controls cannot activate a primary action behind them. A focused native center control receives one semantic Submit/Move while the duplicate EventSystem navigation pass is disabled. Default widget Cancel forwarding reaches the owning presenter; native Cancel is consumed only by a profile configured for native navigation. Secondary bindings remain independent of primary availability and never encode a hardcoded face button in a gameplay definition.

`RadialAnalogSelector` applies the configured dead zone and angular hysteresis to the explicitly focused `RadialRingGeometry`. Controller concentric navigation enters child focus through confirmation or a configured ring-focus action; D-pad navigation follows that focus too. Stick magnitude does not choose a ring. Hold begins neutral, and a release with no valid directional target cancels. Pointer hold release retains its origin so a native center control does not submit a hidden primary or receive an unintended keyboard Submit.

There is currently no MTK-wide focus/input lease service. Keep this feature's ownership seam narrow and explicit. In addition, IInputService's pointer/cancel edge properties currently read Mouse/Keyboard directly, independently of action-map enablement. Dirigible's existing ActionInteractionService must still guard world/tool consumers and cancel/pause claims; disabling a gameplay map alone does not prove absence of world-input leakage.

## Theme precedence and optional widgets

Use semantic Swatch, Typeset and Spacing assets. ThemePreset resolves the authored token Id against the active theme and SetStart changes the active-state token. SwatchPreset already supports any Graphic, including the new MaskableGraphic sectors; no global graphics binder is necessary. TypesetPreset supplies font/material/spacing and accessible typography. SpacingPreset applies standard LayoutGroups; radial radii/angular spacing remain profile geometry, with semantic spacing used where meaningful.

`MenuSkin.GetColor` applies explicit local color first, then the active theme's matching semantic token, then the authored token/readable default. `OverrideTheme` keeps authored swatches/typesets/spacing local. Default widgets resolve the provided theme service and reuse `TypesetPreset` and `SpacingPreset` with the supported `ThemePreset.SetOverrideTheme` API; they do not reflect into private preset state. Framework-created buttons disable competing color transitions. `MenuActionWidget.UseSkin=false` and a custom widget/presenter permit deliberate visual ownership. Theme/prompt/locale changes update visible state; main-thread dispatch and binding generations reject stale callbacks.

### Transition ownership

`MenuProfile.Transition` optionally references a `MenuTransitionAsset`. Its `Create(RectTransform)` returns a per-presenter `IMenuTransition` controller exposing `PlayAsync`, `CompleteImmediately` and `Dispose`. Default presenters request Open after their first successful render, then Navigation when the path, page or expanded row structure changes. Selection, glyph and theme-only refreshes do not restart animation. `ReducedMotion` uses synchronous completion. A new transition cancels its predecessor; unbind cancels/disposes the controller, and binding/play generations fence late completion.

`CanvasGroupFadeTransition` is the native example. It validates a finite nonnegative duration, uses unscaled time and active UniTask yields, and changes only opacity. It restores authored alpha on disposal, preserves colors/transforms/input flags, and leaves an auto-added inert CanvasGroup in place until root destruction to avoid deferred-destruction races during rapid rebind. Transition assets hold configuration; controllers hold runtime state. A complete custom presenter retains its animation ownership and receives no forced framework transition.

Center information consumes the selected-action state; title/description/icon/availability/metadata and secondary action hints are optional. None, default and custom center prefabs are supported composition choices. Cost/progress/cooldown details stay opaque presentation metadata supplied by the action provider or optional game adapter. MTK does not reference Dirigible tooltips or duplicate their business logic.

## Asset ownership and asynchronous lifecycle

`MenuProfile.PresenterPrefab` is direct-first. Its alternative `PresenterReference` is `ContextMenuPrefabReference : AssetReferenceBase<GameObject>`, loaded by GUID through an explicitly Addressables-backed `IRuntimeAssetService`. Profiles themselves are already supplied assets; item/center/secondary prefabs are direct `GameObject` references, not additional typed asynchronous reference APIs. Applications using the existing Resources backend should keep direct references or deliberately provide the appropriate loader; the menu never switches the application's backend silently.

`MenuPrefabCache` coalesces same-key requests and retains one successful asset acquisition per host/key. Individual caller cancellation stops that wait and fences visibility, while a reusable prewarm can continue under cache ownership. Disposing the host releases retained assets; a late completion releases its acquisition instead of reopening the menu. Presenter/widget instance ownership is distinct from the loaded prefab's ownership.

Close/replacement/scene unload/object destruction/plugin shutdown invalidate callbacks, cancel session/provider work, unbind presenter and widget subscriptions, release instances/assets owned by the host, and restore focus/input exactly once. New Open safely replaces the old session without resurrecting old callbacks. Preserve useful diagnostics for invalid/missing required presenter components and asset/config failures.

Lifetime integration is explicit. The service links the request's root-canvas destruction token and retains a cancellation observer for the entire request, after its opening await completes. Canvas destruction or request/context cancellation closes a DI-owned session without needing a `ContextMenuHost` component. Request generation and registration identity prevent a retired canvas/callback from closing a replacement. `ContextMenuHost` additionally closes on component disable and disposes a service it owns on destruction. The owning UI/plugin still disposes a DI-owned service; canvas disable alone is not canvas destruction. Pass a context destruction token for immediate target retirement. A Unity target's destroyed-object check also prevents invocation on the next semantic operation or refresh; it is not a per-frame destruction watcher.

Public UI operations enter on Unity's main thread. A session captures that owner when opened in Unity and returns provider/command continuations to it before framework state evaluation or navigation mutation. Authored provider wrappers, host prefab awaits and cache completion/error/late-release paths also return before touching Unity state. Worker descriptor changes, cancellation and invalidation post generation-fenced callbacks. Managed-only sessions retain their non-player-loop behavior. Custom providers and commands remain responsible for Unity access inside their own asynchronous bodies.

`AddressablesAssetLoader` records successful acquisitions rather than replacing one handle for a repeated result object. It releases failed/canceled untransferred handles and counts independent successful acquisition/group shares. Backend dispatch retains the owning loader for each service acquisition, including alias keys. `Release` returns an owned share; a Unity-destroyed object still has a CLR identity that must not silently lose its ownership record. The menu cache consumes this ownership contract for shared prewarming.

## Dirigible compatibility and convergence

Dirigible.Actions supplies ActionDefinitionSO, ActionDescriptor, ActionRequest, ActionCatalog, IActionInvocationService, ActionTargetSnapshot, IActionContextInvalidation and ActionInteractionService. MenuActionSO derives ActionDefinitionSO; MenuBarCategory derives SubmenuActionEntrySO; MenuBarLeaf derives FixedActionEntrySO. Preserve this existing canonical catalog and polymorphic execution.

ADR-0023 requires reusable action assets with behavior dispatch through polymorphism, not a LeafKind switch or duplicated execution sites. The menu feature follows that direction. Shared MTK contracts unify presentation, hierarchy and session mechanics; they do not replace Dirigible's domain validation, catalog placement rules or concrete handlers.

| Existing game boundary | Retained/adapted behavior |
| --- | --- |
| `IContextMenuService` and `IContextMenuLifecycle.Closed` | Compatibility facade preserves Open(items, screen position, anchor, Canvas), Close, readiness/failure API, latest pending Open and Closed semantics while delegating reusable behavior to MTK. |
| `ContextMenuItem` | Project Label/IsLiteralLabel, Icon, Children, IsChecked and live CurrentAvailability into menu descriptors. Retain the original item as metadata for game-specific display such as ColorSwatch, its ActionRequest when present and its legacy callback. |
| `IActionInvocationService` | Evaluate and Invoke remain authoritative for catalog/placement/control-generation/target validation. MTK behavior delegates into this service rather than caching UI eligibility as execution authority. |
| `ContextMenuItem.ForMessage<T>` | Retain the typed closure calling GameMessageBroker.Publish<T>. Never erase concrete T into a base interface generic publisher. |
| `IContextActionProvider`, `RightClickDispatcher` | Retain world hit capture, provider order, actor/object/tile targeting and dynamic game action assembly. Produce compatible hierarchy descriptors through the facade. |
| Active-tool/context changes | Keep IActiveToolService subscriptions and IActionContextInvalidation in the game adapter. They close/invalidate the generic session without adding game dependencies to MTK. |
| `ContextPriorityFan`, `ContextPriorityButton` | Preserve 1-9 priority callbacks and mutable-target validation; represent checked choice state structurally instead of adding text markers. |
| SelectionCommandDescriptor/RowBinder | Preserve command/option dispatch and existing row ownership; adapt its picker hierarchy without rewriting unrelated command rows. |
| BuildMenuOpener | Preserve recipe callback/message behavior, cursor/canvas anchor, and construction/tool boundaries. |
| Zone/Harvest context providers | Preserve target snapshots, designation mutations and concrete messages; only reusable presentation mechanics move. |
| Tooltip | Optional presentation adapter may project existing selected-action/tooltip data, but game tooltip implementations stay in Dirigible. |

Keep current context-menu prefab/config GUIDs as compatibility fixtures and the supported legacy fallback. Before migration the list used replacement-only children, availability polling and checked-text markers. The profiled facade projects its items into MTK sessions and structured checked state, while retaining the original callbacks and final domain validation. Older opaque availability delegates still require a bounded game-side compatibility refresh; that polling is not part of the MTK module. See [the migration guide](ContextMenusMigration.md) for the executable boundary and input ownership details.

## Sample authoring and native editor constraints

The importable sample lives in `Samples~/HierarchicalActionMenus` and ships its saved `ActionMenuGallery.unity`, ten presenter/widget prefabs, six menu profiles, host config, themes, actions and input assets. Import the sample, import TMP Essentials if needed, open the saved scene and enter Play Mode. Five named root profiles share one action set; the sixth profile is the custom replacement. An authored Interactions branch switches to that card presenter, and Build/Doors/Metal doors demonstrates hierarchy beyond two visible rings. `MenuGalleryTheme` is a local `IThemeService`; it does not register a `CurrentTheme` singleton.

The builder is for a fresh destination, such as `MenuGalleryAssetBuilder.Build("Assets/MyMenuGallery")`; generating into the shipped occupied sample folder is not the normal opening flow. Generation refuses existing generated folders/scenes before mutation, while allowing imported source folders. Temporary prefab objects are created in a private saved authoring scene so the caller's active scene is not dirtied. Unity's native `EditorSceneManager.SaveScene` also flushes unrelated dirty persistent assets. The safe contract therefore refuses native asset/importer edits before writing output, reporting their paths, and checks again before the public scene save. Imported shader/font/texture objects' dirty foreign caches are excluded because scene saving cannot write those foreign objects back to their source files. It does not serialize/restore user files or silently save edits. Open untitled scenes must be saved by their owner first. These are editor-authoring constraints, not runtime menu input policies.

## Optional graphics assessment boundary

Native geometry, selection and action execution are unconditional. Optional graphics may decorate compatible visuals, but cannot become selection/gameplay owners or required public-package dependencies.

- [Excalidraw issue 4986](https://github.com/excalidraw/excalidraw/issues/4986) is an open proposal, not evidence of a shipped Excalidraw menu implementation. It is a visual reference for the partial child arc and sliding hierarchy.
- [Unity UI Extensions](https://github.com/Unity-UI-Extensions/com.unity.uiextensions) was evaluated from the installed 3.0.0 source and [official package manifest](https://raw.githubusercontent.com/Unity-UI-Extensions/com.unity.uiextensions/release/package.json): Unity 6000.0, uGUI 2.0.0, BSD-3-Clause. `RadialLayout` arranges children; `UICircleSegmented` draws ring segments; the component under `UIGraphicSector` is actually `UnityEngine.UI.Extensions.Sector`, a `MaskableGraphic` with shape-aware raycasting. It was an architectural reference only. Its sibling-driven layout/rebuild behavior and wide-sector raycast branch require independent validation; MTK does not require or wrap it.
- [Procedural UI Tool](https://assetstore.unity.com/packages/tools/gui/procedural-ui-tool-335514) was evaluated from the installed `ProceduralUITool.Runtime` API and shader. `ProceduralUIComponent` modifies `Image`/`RawImage` material using `ProceduralUIProfile`, with `SetProfile`, `ForceUpdate` and `SetProgress`. Its basic shape SDF and angular border-progress mask do not provide arbitrary annular wedges, inner holes, radial-edge borders or shape-aware raycasting. The public custom-vertices field does not establish unsupported shader geometry. It is suitable for consumer-owned compatible decoration; no adapter or vendor content is included in MTK.

Both integrations are evaluated-only. Procedural UI Tool remains an optional consumer-owned Asset Store package, with no vendor content redistributed in MToolKit. Native rendering and generic samples have no dependency on either optional package.

No adapter is selected merely from a package name or screenshot. Record actual API findings and the integrate/evaluated-only decision with evidence before claiming integration. Compile optional adapters behind appropriate assembly/package constraints and validate absence. Do not copy vendor code, shaders, fonts, prefabs or textures into MTK samples.

## Validation responsibilities

The [usage guide](ContextMenus.md#validate-an-integration) lists the runnable EditMode
and PlayMode assemblies. Core tests exercise action execution, arbitrary-depth navigation,
geometry, input identity/ownership and widget/profile behavior. Runtime tests exercise
canvas/request lifetime and presenter replacement. Imported sample tests exercise its saved
assets and configured controls.

Applications retain responsibility for their domain commands, gameplay input claims and
compatibility consumers. Inspect supplied or replaced prefabs in the rendered gallery,
exercise physical controllers and confirm the relevant paths in a development player.
Keep native input/rendering checks distinct from pure navigation/geometry tests and from
assessment of optional graphics packages.
