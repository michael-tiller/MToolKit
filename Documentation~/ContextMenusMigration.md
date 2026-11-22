# Integrating hierarchical action menus

The reusable module is `MToolKit.ContextMenus`. It owns action descriptors, session
navigation, input routing, widgets, profiles, native geometry and presentation. An
application retains target selection, gameplay validation and execution. See
[usage and extension APIs](ContextMenus.md) and [the dependency decision](ContextMenusArchitecture.md).

## Establish the ownership boundary

Reference the new assembly from UI/integration assemblies. Do not make MTK Runtime
reference it: Theme already references Runtime, so the menu assembly consumes both.
Register `ContextMenuPlugin` with a `MenuHostConfig`, or construct a host using the
application's existing asset, input, prompt, theme and rebinding services. One service
instance represents one menu host. `ContextMenuHost` is an optional component adapter;
if a service is constructed directly, its owning UI/plugin must close and dispose it.
The service observes the request's root-canvas destruction and caller/context cancellation
for the full session, including after opening finishes. A DI-owned session therefore
retires when its canvas is destroyed; disabling a UI owner still needs its own close
hook or the optional component's `OnDisable`. Retired request cancellation cannot close
a replacement on another canvas.

Create a complete list profile first and make it the accessible fallback for radial
profiles. A presenter prefab needs an `IContextMenuPresenter` on its root. An item,
center or secondary prefab needs `IMenuWidget` on its root; optional text/icons and
checked/highlight components may be absent. Keep layout/input/profile choices outside
gameplay command definitions.

Pass a `MenuContext` containing the current target snapshot, a validity predicate and
the relevant destruction/cancellation token. Close or invalidate it on active-tool or
selection-context changes. The framework rechecks validity and availability before
invocation; the domain command must retain its final gameplay validation too.

## Keep the existing action dispatcher

Project existing commands into `MenuAction` behaviors. Preserve the original typed
callback, immutable request/snapshot and concrete message publication. An existing
`Publish<SpecificMessage>` must remain that concrete generic call; wrapping it in an
MTK descriptor does not justify publishing an interface instead.

Map labels to `MenuLabel(fallback, key, table)` and checked state to
`MenuActionState.IsChecked`. Remove presentation-only check markers from labels.
Supply a `primary` behavior when a parent also has a command. Normal activation of
that parent enters/expands its children; `InvokePrimaryAsync` is the deliberate command
path. Auxiliary commands belong in `secondaryActions` with their own availability and
post-execution outcome.

For dynamic children use `IMenuChildrenProvider` or `MenuChildrenProviderSO`, return
stable IDs and honor cancellation. Children resolve on entry/refresh. Notify mutable
state with `NotifyChanged` or explicit refresh. An older API exposing only opaque
availability delegates may keep a bounded adapter refresh until it can publish changes;
the reusable MTK module does not need gameplay polling.

Authored `MenuActionDefinitionSO` assets and runtime descriptors share the same session
contract. Reused authored children receive parent-qualified placement IDs. Treat these
materialized IDs as the selection path; keep the definition's gameplay identity separate
when a domain catalog already supplies one.

## Resolve the actual input instances

Supply `IInputActionSource` for the actions the application actually executes. MTK's
`InputService` maps only its own authored asset into its runtime clone. Another asset
with the same action GUID is not that relationship. A game executing an authored
canonical asset directly should preserve its exact instance and delegate only explicitly
owned clone mappings. Rebinding, hints, execution and gameplay suspension must agree
on that identity.

Use `IScopedInputRebinderService` for temporary runtime actions. MTK's rebinder implements
this optional contract alongside the existing permanent API: the router owns scoped leases,
application `RegisterAction` owners remain registered, and same-name/same-GUID execution
instances remain distinct. A host omits legacy rebinder registration when its input source
owns foreign clones and the service has no release contract. Application-owned direct-router
registrations keep the legacy behavior.

`MenuOpenRequest.GameplayActions` lists actions that must stop while the menu is open.
The shared ownership coordinator restores each prior enabled state and quarantines held
buttons until release. It does not infer a game's movement/fire maps or enable whole maps.
Choose native uGUI or semantic navigation in each profile, and wire the corresponding UI
module/actions. Use independent actions for independent operations.

Supply `anchorOverride` to preserve a legacy per-call anchor without mutating a shared
profile. Supply live `backInputAllowed` when an interaction/tool owner may consume the
same Back edge first. Custom native presenters should consult the presentation context's
`CanHandleBackInput`; do not send local submenu Back into MTK's full-screen navigation
stack. Hold activation uses the configured action plus `activationHeld:true`, not raw
controller button polling.

## Dirigible's compatibility path

The consumer retains `IContextMenuService` and its `ContextMenuItem` surface. Setting
`ContextMenuConfig.MenuHostConfig` selects the MTK-backed facade; leaving it unset retains
the shipped panel/item compatibility path. Existing prefab/config references and their
GUIDs remain fixtures. Readiness, prewarming failure, latest pending-open replacement,
close notifications and tool/context dismissal remain game-side lifecycle contracts.

`ContextMenuActionAdapter` projects the existing hierarchy and current availability into
MTK without replacing its callbacks. Canonical `ActionRequest.PlacementId` becomes the
stable placement key; older items without one receive snapshot-local index paths.
`IActionInvocationService`, `ActionTargetSnapshot`, the action catalog and concrete
MessagePipe handlers stay authoritative. The existing menu-bar `MenuActionSO` already
derives the game's canonical action definition; no parallel gameplay action enum is added.

`DirigibleContextMenuInputSource` preserves the directly executed canonical asset before
delegating MTK's independent source, and supplies Colony/ARPG execution actions for
suspension. `ActionInteractionService` retains high-priority cancellation ownership;
the menu's Back predicate prevents a consumed edge from navigating again. World
raycasting, actor/tile capture, build recipes, zones, harvesting, priority callbacks and
selection-command business logic remain in Dirigible.

Use the existing list as the initial integration profile, then change a per-use-case
profile to demonstrate the same actions in another presenter. A custom presenter or
submenu profile override changes composition without replacing domain execution.

## Validate the migrated integration

The imported sample already contains ten prefabs, six menu profiles, a host config and
`ActionMenuGallery.unity`. Open that saved scene and enter Play Mode; the generator is
only needed for another fresh destination. See the [sample README](../Samples~/HierarchicalActionMenus/README.md).

Automated action/navigation/geometry/input/lifecycle tests do not prove the saved sample
prefabs render or that a physical controller interaction works. Run the shipped gallery
in the consuming Unity project, then its declared runtime/player checks. Preserve native
evidence at the source/dependency revision where it ran. UI Extensions and Procedural UI
Tool were evaluated only; neither is a runtime dependency or a validated MTK adapter.
The [usage guide](ContextMenus.md#validate-an-integration) lists the package and sample
test assemblies and a focused command-line invocation. Include the application's existing
context-menu, build, priority and selection-command regressions when validating its facade.
