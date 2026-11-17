# Hierarchical action menus

Reference `MToolKit.ContextMenus` from the assembly that supplies your UI or adapters.
Gameplay assemblies do not need to reference a presenter. The runtime module references
MTK Runtime and Theme; it does not reference Dirigible or an optional graphics package.

This guide covers the runtime API, authoring and extension points. See
[architecture](ContextMenusArchitecture.md), [migration](ContextMenusMigration.md),
and the [shared style settings and rebuild wizard](ContextMenuAuthoring.md).

## Open a runtime hierarchy

Resolve `IContextMenuHostService` from a configured `ContextMenuPlugin`, or construct
`ContextMenuHostService` with a `MenuHostConfig` and the optional existing asset,
input, prompt, theme and rebinding services. A `ContextMenuHost` component can hold
the same service. Configure a default profile or pass an explicit/named profile.

```csharp
var targetContext = new MenuContext(target, () => target != null && target.CanInspect);
var inspect = new MenuAction("object.inspect", new MenuLabel("Inspect", "inspect"),
    new MenuCommandBehavior(context => Inspect(context.Target)),
    description: new MenuLabel("Inspect this object", "inspect_description"));
await menus.OpenAsync(new MenuOpenRequest(new[] { inspect }, targetContext,
    rootCanvas, screenPosition, profileName: "DefaultWorldContext",
    gameplayActions: gameplayActions));
```

Keep the actual gameplay command and its final domain validation in the application.
Do not erase concrete MessagePipe message types to publish through an interface generic.
Pass the runtime gameplay actions that must be suspended. Opening a menu cannot infer
which actions move or fire in your game. Their individual enabled states are restored
on close; a held button remains suspended through release to prevent replay.

`MenuContext.Invalidate()`, context cancellation, host close/replacement and host
disposal retire the session. A destroyed Unity target is rejected during validity checks;
pass its destruction cancellation token when immediate lifetime-driven retirement is
required. The service observes destruction of the request's root canvas and retains the
request cancellation registration after `OpenAsync` returns. Destroying that canvas or
canceling the request retires a DI-owned session too. A retired request cannot cancel a
replacement on another canvas. Close the host on an application-specific active-tool
change. `ContextMenuHost` also closes on component disable and disposes a service it
created on destruction; a directly constructed or DI-owned service still needs its
owning UI/plugin to dispose it. A pending prefab/provider completion cannot reopen a
retired session.

Public UI operations are main-thread entry points. A session opened on Unity's owner
thread returns provider results and command outcomes to that owner before framework
state evaluation, navigation changes and cleanup. Descriptor changes, invalidation and
cancellation originating on workers are posted with generation guards. Host/cache asset
completion and failure cleanup use the same owner. This does not move a custom provider's
or command's internal code to Unity's thread: switch explicitly before touching Unity
objects inside your own asynchronous implementation. Managed-only session consumers do
not acquire a Unity player-loop dependency.

## Define behavior and state

`MenuAction` is a runtime descriptor with a stable `MenuActionId`. Labels carry a
literal fallback plus an optional localization key/table. Icons, images, binding hints,
descriptions, metadata and presentation overrides are optional.

An action has two independent optional Sprite fields: `Icon` for a compact symbol
and `Image` for artwork or a thumbnail. Assign either, both, or neither. Runtime
callers pass named `icon:` and `image:` arguments to `MenuAction`; authored
`MenuActionDefinitionSO` assets expose both fields. Child, dynamic-provider and
secondary placements preserve both independently.

`MenuWidgetState.Icon` and `.Image` expose the same data to every presentation.
The supplied action and center widgets bind `Icon` and `ActionImage` targets.
Icons use the semantic icon theme role; artwork retains its own authored color.
An empty dedicated artwork leaf collapses, and a reused widget clears the prior sprite.
An artwork container with descendants stays active while its empty graphic is hidden,
so independently wired overlay icons and labels remain visible.
Custom prefabs can omit either target or arrange both freely. A custom `IMenuWidget`
can consume the same state without adopting the default component or layout.

Supply an `IMenuActionBehavior`, use the command/toggle/choice/header behaviors, or
implement an application behavior. Presenters do not switch over a gameplay action enum.
Use `MenuActionOutcome` for the finite post-execution policy: keep, refresh, close,
go back or enter. Toggle/choice commands normally refresh and expose their checked state
through the state delegate.

A parent with children navigates on normal activation. Its optional `primary` behavior
executes only through `InvokePrimaryAsync`. Secondary descriptors have independent
visibility/enabled/checked state and outcomes; an unavailable primary can still expose
an available secondary command. Re-evaluation immediately before invocation prevents
stale availability from authorizing a command.

Notify a changed descriptor with `NotifyChanged()`, or call `Session.RefreshAsync()`
after updating application state. The native framework does not poll gameplay state.
An adapter for older opaque Func delegates can perform a bounded compatibility refresh.

## Author reusable assets

Derive a project ScriptableObject from `MenuActionDefinitionSO` and override
`CreateAction(MenuContext)`. Supply behavior, evaluated state and custom metadata
there. The base `Create` method applies the authored identity, localized text, icon,
binding hint and secondary definitions. Keep user state in `context.Target` or a
context-owned service; do not store the current target, selected value or inventory
on a reusable definition asset.

Use `MenuSubmenuDefinitionSO` for authored children, an optional lazy provider,
an explicit primary definition and an optional child profile. Use
`MenuHeaderDefinitionSO` for a heading/separator and `MenuActionSetSO` for roots.
The same child definition can appear under different authored parents: its placements
receive stable parent-qualified IDs. Duplicate sibling IDs, null required definitions,
missing IDs and authored cycles produce diagnostics.

A `MenuChildrenProviderSO` or runtime `IMenuChildrenProvider` resolves fresh children
when the branch opens and on explicit refresh. Return stable IDs for the same logical
choices; replacement objects with the same IDs retain selection where still valid.
Removed/hidden choices discard stale selection. Honor the supplied cancellation token.
The authored adapter checks context validity before and after resolution and scopes
the generated placements. Providers must not mutate gameplay while resolving labels.

For example, an authored toggle can supply behavior and state while the base class
applies localization, icons and auxiliary definitions:

```csharp
public sealed class OptionState { public bool Checked; }

[CreateAssetMenu(menuName = "My Game/Option Action")]
public sealed class OptionActionSO : MenuActionDefinitionSO
{
    protected override MenuAction CreateAction(MenuContext context)
        => new MenuAction(Id, LabelDescriptor,
            new MenuToggleBehavior(current =>
            {
                var option = (OptionState)current.Target;
                option.Checked = !option.Checked;
            }),
            state: current => new MenuActionState(
                enabled: current.Target is OptionState,
                isChecked: current.Target is OptionState option && option.Checked));
}
```

Import `MToolKit.ContextMenus`, `MToolKit.ContextMenus.Authoring` and `UnityEngine`
for that example. Open the resulting definition with an `OptionState` target. Mutable
state remains outside the shared asset. Authored placement IDs are qualified: a child
`inspect` under `objects` becomes `objects/inspect`; deeper parents retain the entire
path. Use the materialized descriptor's ID for selection rather than assuming its raw
definition ID remains unchanged at every depth.

## Choose a presentation

The profile's root prefab supplies `IContextMenuPresenter`; it chooses the implementation
without changing any action definition.

- `ListMenuPresenter`: inline foldouts by default, replacement/flyout policies,
  configurable pagination or native scrolling, checked/disabled rows and local Back.
- `RadialMenuPresenter`: single ring, arbitrary-depth replacement, or a concentric
  two-ring sliding window with a partial child arc.
- A custom presenter: implement the lifecycle and semantic operations on a root
  RectTransform, bind the shared session and own your transforms/animation.

Lists preserve pagination by default. Set `MenuProfile.ListOverflow` to
`MenuListOverflowPolicy.Scrolling` for a native vertical `ScrollRect` with clipped
content. `MaximumVisibleItems` limits the viewport's readable rows; the content
contains the full expanded hierarchy. Keyboard/controller selection uses MTK's
existing reveal helper, and clipped rows cannot be confirmed outside the viewport.
The footer stays outside the clip. No item size or font reduction is used to fit
additional rows. The same policy is exposed by `ListMenuThemeSet.Layout.Overflow`
and the List recipe's inline `Overflow` setting; rebuild the profile after editing
its authoring source.

Scrolling rows use native pointer release and drag cancellation, so starting a drag
does not invoke the row command. Wheel/drag input preserves the viewport offset;
Page bindings move selection by a viewport and reveal it. Keep the existing Point
binding assigned for mixed mouse/keyboard/controller use: layout-generated hover
events then leave keyboard/controller selection intact until deliberate pointer
input takes ownership. Profiles without a resolved Point binding retain their
legacy native-hover fallback. Custom widgets retain their configured callbacks.

`MenuSession` exposes `RootActions`, current `Actions`, visible paged `Rows` with depth
and expansion state, `Selected`, `Path`, `PathActions`, `ParentActions` and
`ParentSelected`. Use the parent/current collections for a sliding two-ring window;
the session path has no two-level maximum. `Changed` updates presentation and `Closed`
retires its lifecycle. `Select`, `ActivateAsync`, `InvokePrimaryAsync`,
`EnterSubmenuAsync`, `GoBack`, `ExpandAsync`, `Collapse`, paging,
`InvokeSecondaryAsync` and `RefreshAsync` supply the shared operations.

A submenu's `PresentationOverride` can carry another `MenuProfile`. The host tracks
the logical path and restores the applicable ancestor profile on Back. Replacing an
entire prefab does not create another gameplay execution system or MTK navigation view.

Profiles contain geometry, input, composition, minimum targets, accessible fallback,
center/backdrop and skin references. Direct root/item/center/secondary prefabs avoid
asset-loading overhead. `PresenterReference` is a typed Addressables reference;
configure an Addressables-backed `IRuntimeAssetService` for GUID loading. MTK applications
configured with a Resources backend should use direct prefabs or deliberately supply
the appropriate loader. Do not silently change an application's global backend.

A host prewarms and caches frequently used presenter prefabs. Each host owns its cache;
independent hosts may share an asset service. Canceling an individual open does not
transfer an asset handle to its caller.

## Replace widgets and styling

A widget root implements `IMenuWidget` and receives a `MenuWidgetState`. The supplied
action and center widgets accept optional label, icon, description, checked marker,
selection indicator, cost/progress/variant and secondary controls. An icon-only or
label-only prefab is valid. Optional fields can be absent; a configured widget root
that lacks the required interface is an authoring error.

`IMenuActionDetails` is optional metadata for cost/variant/progress. Reuse the selected
action state for the center instead of repeating gameplay or tooltip calculations.
A custom center can consume other typed metadata without adding a global action type
system. Set `ShowCenter=false` for no center, leave the prefab empty for the default,
or provide a custom `IMenuWidget` root.

The framework attempts cleanup of every widget component and its remaining owned
resources even when a custom `Unbind` throws. Diagnostics retain the first failure;
teardown still releases prompt/theme bindings, generated roots and native navigation
ownership. Custom widgets should make their own unbind idempotent and release each
subscription they create.

A `MenuSkin` uses semantic MTK Swatch, Typeset and Spacing assets. An active theme
resolves their semantic IDs while the menu is open. A per-role explicit color wins,
then the active semantic token, then its authored token/default. `OverrideTheme`
keeps authored typography/spacing/swatches local. Supplied widget components can opt
out of theme binding; a custom prefab is free to own all of its visuals.

Configuration handles routine variation. Replacing item/center/secondary prefabs
handles visual variation. Implementing widget/renderer/presenter contracts handles
behavioral variation. Implementing `IContextMenuPresenter` directly keeps the custom
presenter in control of animation and child transforms. Deriving `MenuPresenterBase`
deliberately opts into its widget/focus/containment/transition infrastructure; the gallery's
card presenter demonstrates that convenience path.

`MenuProfile.Transition` accepts a `MenuTransitionAsset`. Its `Create(RectTransform)`
returns an `IMenuTransition` controller implementing `PlayAsync`,
`CompleteImmediately` and `Dispose`. Default presenters request Open after first layout
and Navigation for structural changes; selection, theme and binding refreshes do not
restart transitions. `ReducedMotion` completes synchronously. `CanvasGroupFadeTransition`
provides an unscaled-time opacity example. Cancellation and binding generations prevent
an old transition from changing a rebound menu. These are optional visuals, not action
execution or navigation owners.

## Input and focus

Reference Input System actions in `MenuInputBindings`. The host resolves existing
runtime-clone identities through `IInputActionSource`; foreign authored menu-only
assets get a host-owned clone. Existing rebinding and prompt services keep their
original ownership. Labels and secondary hints resolve the actual runtime binding.

`InputRebinderService` implements the optional `IScopedInputRebinderService`.
`RegisterScopedAction(action)` returns an idempotent registration lease. Registrations
distinguish actual `InputAction` instances even when names and GUIDs match; a permanent
`RegisterAction` owner survives disposal of menu scopes. The router releases scopes before
restoring input ownership, and the last scoped release cancels an owned interactive rebind.
A custom legacy rebinder can keep the existing permanent API for application-owned
actions. Hosts with foreign cloned assets require scoped support to register those clones;
the host omits unsupported legacy registration instead of retaining destroyed actions.

Default hint widgets own `IInputPromptService.Bind` subscriptions for the resolved runtime
action, including foreign profile clones. Rebinding and device/locale changes update the
bound hint; unbind disposes that subscription. Default selected-action centers,
auxiliary controls and navigation request `InputPromptStyle.Glyph` explicitly, leaving the application's
shared prompt preference unchanged. Keep operation labels such as Back and Inspect;
the binding beside them is native TMP sprite markup. An optional `BindingHint`
text target is sufficient on an item, center or auxiliary widget. Missing targets
are supported, and custom widgets retain control of their own composition.
Default menu items show no confirmation hint. The primary hint belongs to the
selected action's center and is unbound when selection clears. Auxiliary hints
use their own explicit/configured bindings; an unbound auxiliary never inherits
Submit. A custom presenter can supply an explicit binding override when composing
its own item state.
Framework-owned footer controls use native TMP preferred widths in their uGUI
layout, so wide keycaps remain visible and rebinding can release unused space.
The measured glyph width is reserved as a minimum at narrow footer widths; labels
receive the remaining space without compressing the glyph out of view.

InputAction GUID alone does not establish ownership. MTK `InputService` resolves an
action only when its asset is the authored asset it initialized; unrelated assets can
legitimately carry the same GUID. A consuming game that executes its canonical asset
directly should supply an `IInputActionSource` preserving that exact instance and only
delegate other known authored/runtime pairs. Pass the gameplay execution actions to
`MenuOpenRequest.GameplayActions`; suspending a different clone does not suspend gameplay.

Choose one navigation owner per profile: native uGUI navigation or semantic routing.
Semantic profiles route direction, confirm, Back, cancel, page, ring focus and secondary
commands through the presenter/session; they scope EventSystem navigation ownership
to prevent the same submit/cancel edge from being processed twice. Native profiles
use the EventSystem's configured UI actions. Pointer-origin confirmation remains
distinct from keyboard submit so a custom center button can own an overlapping click.

Analog selection applies a dead zone and angular hysteresis. Concentric controller
selection uses explicit ring focus/confirmation; stick magnitude is not a mouse radius.
Pointer-out clears pointer selection; the selected center and its auxiliary controls retain
their action context while being operated. `MenuProfile.AnalogSelectionResetDelay` controls
the analog-neutral grace period in seconds: `.35` by default, `0` for immediate clearing;
values must be finite and nonnegative. Shared Common Settings exposes the same value
for explicit profile rebuilds. New interaction or menu cleanup cancels a pending reset.
Bind a hold action and open with `activationHeld:true` for hold/release confirmation.
The router uses event callbacks; it does not poll hardcoded gamepad controls.

Back is local session navigation and root cancellation. It does not publish MTK
`BackRequestMessage` or pop unrelated navigation views. Focus and owned input state
are restored when the host retires the menu.

Use `MenuOpenRequest.anchorOverride` for a per-open anchor without changing a shared
profile asset. `MenuPresentationContext.ResolveAnchor(profile.Anchor)` gives custom
presenters the effective choice. The optional live `backInputAllowed` predicate lets
a game's interaction/cancellation owner consume a Back edge before the menu handles it.
The host gates semantic Back and default presenters gate native Back through
`MenuPresentationContext.CanHandleBackInput`. Programmatic `Session.GoBack()` remains
an explicit caller operation; custom native presenters should check the context predicate
before handling an input-origin Back. This predicate does not change explicit Cancel,
command outcomes or direct service Close.

## Geometry and containment

Angles are degrees from +X, counterclockwise positive; clockwise rings use a negative
sweep. Rendering, pointer hit testing and analog selection consume the same cached
ring/segment definitions. Inner radial boundaries are inclusive, outer boundaries
exclusive, and angular gaps are not interactive. RectTransform coordinate conversion
handles canvas scaling; rectangular widget bounds do not select visible native wedges.
With `DrawSegments=false`, button-only radial compositions use their actual topmost
rectangular widget hit rather than requiring a visible button corner to fall inside an
invisible annulus. Analog selection still uses ring direction. Center/auxiliary controls
own their pointer hits before the presenter considers an overlapping wedge.

The default presenters retain usable geometry and target sizes. They reposition within
the root canvas; a concentric arc can fall back to the current ring when space is
insufficient. If the required layout still cannot fit, configure an accessible list
`FallbackProfile`. Missing fallbacks produce a diagnostic rather than silently
shrinking text and hit targets. Global UI-scale changes trigger containment updates.
Radial containment includes the actual active center and auxiliary descendants, including
asymmetric or oversized custom content. A bounded post-layout recheck reports late layout
growth to the host; it does not shrink or rearrange a custom widget's private children.

## Samples and optional graphics

Import **Hierarchical Action Menus** from the Package Manager samples, import TMP
Essentials if the project needs them, open `ActionMenuGallery.unity` in the imported
folder and enter Play Mode. The sample ships ten editable presenter/widget prefabs,
five menu profiles (four root styles and a custom child presentation), a host config, input,
theme/action assets and the saved scene. No generation step is needed to open it.
Its [README](../Samples~/HierarchicalActionMenus/README.md) describes bindings,
fresh-destination generation and the recorded checks. For another gallery, call
`MenuGalleryAssetBuilder.Build("Assets/MyMenuGallery")` with an unused destination.
The builder creates persisted editable assets
in an unused destination. It accepts imported Runtime/Editor/Tests source folders,
refuses existing generated data, and requires saved open scenes. Unity `SaveScene` also
writes unrelated dirty persistent assets, so generation refuses such edits before creating
output and identifies their paths. Save or discard those edits yourself; the builder does
not serialize, restore or silently save them. Temporary prefab objects live in a private
saved authoring scene, and only generated assets are explicitly saved.
The guard protects serializable native assets and importer settings. Dirty internal
caches on imported shader/font/texture objects are excluded because Unity cannot save
those foreign objects back to their source files.
The sample uses its own input/service setup; integrate its profiles with your application's
existing services rather than running a second standalone input owner in a game HUD.
The gallery's `MenuGalleryTheme` is a scene-owned `IThemeService` and does not create or
replace `CurrentTheme`. That local theme isolation does not make its standalone
`InputService` a second production gameplay input owner.

Native rendering has no UI Extensions or Procedural UI Tool dependency. UI Extensions
sector/raycast code informed the assessment. The installed Procedural UI Tool supports
decorative basic SDF shapes and border progress, which does not supply arbitrary annular
sector geometry. Applications can compose optional decoration in their own prefabs;
MTK does not redistribute Asset Store content. See the architecture record for sources
and migration boundaries.

## Validate an integration

Enable this package's tests in the consuming project's `Packages/manifest.json` using
the `testables` entry `com.michael-tiller.mtoolkit`. In Unity's Test Runner, run the
`MToolKit.ContextMenus.Tests.Editor` assembly for actions, navigation, geometry, input,
profiles and widget ownership. Run `MToolKit.ContextMenus.Tests.Runtime` in PlayMode
for request/canvas lifetime and active-prefab replacement behavior.

After importing the sample, run `MToolKit.ContextMenus.Samples.Tests.Editor` in EditMode
and `MToolKit.ContextMenus.Samples.Tests.PlayMode` in PlayMode. Open the saved gallery
for visual inspection of the four root styles, nested custom replacement, long labels,
theme/scale changes and constrained viewport fallback. Exercise mouse, keyboard and an
actual controller, including hold/release, secondary actions, ring focus and gameplay
input restoration. Repeat the relevant interaction checks in a development player.

For a closed Editor, a focused command-line run can select the same assembly:

```text
<Unity> -batchmode -projectPath <Project> -runTests -testPlatform EditMode -assemblyNames MToolKit.ContextMenus.Tests.Editor -testResults <Project>/Temp/context-menus-editmode.xml -logFile <Project>/Temp/context-menus-editmode.log
```

Use `PlayMode` and the corresponding runtime/sample assembly for runtime checks. Do not
add `-quit` to `-runTests`; the test runner exits after its scheduled work completes.
On Windows, launch through `cmd.exe /c start "" /wait "<Unity.exe>" ...` so the caller
waits for the Editor worker. If the project is already open, use that Editor's Test Runner.
Inspect the new result XML and log rather than treating successful compilation as a
rendering or input result.
