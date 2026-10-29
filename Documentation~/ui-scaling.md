# User UI scale

`GraphicsSettingsModule.UiScale` is the authoritative `ReactiveSetting<float>`.
Its multiplier defaults to `1`, accepts `0.75` through `3`, clamps out-of-range
values, and replaces non-finite values with the default. The INI key is
`[Graphics] UiScale`; missing or malformed values use the existing settings
initialization and persistence machinery.

## Migrating custom graphics settings implementations

`IGraphicsSettings` now requires `ReactiveSetting<float> UiScale { get; }`.
This is a source compatibility change for custom implementations: they must add
that member before recompiling against this package. Consumers using the supplied
`GraphicsSettingsModule` already receive it and need no replacement module.

Initialize one setting in the custom module's existing constructor, using its
existing settings controller for dirty tracking:

```csharp
public ReactiveSetting<float> UiScale { get; }

// Inside the existing module constructor:
UiScale = new ReactiveSetting<float>(UiScalePolicy.Default, "UI Scale", settingsController);
```

Use `UiScalePolicy.Normalize` for live and loaded values, retaining the validation
subscription with the module's other subscriptions. In the existing lifecycle,
call `UiScale.OnApply()` when applying a dirty scale, `UiScale.OnCancel()` when
cancelling, and `UiScale.OnRevertToDefault()` when resetting a nondefault scale.
Dispose the validation subscription and setting during module shutdown.

Keep the module's current registration and the existing `SettingsSystem` and
`IniService` persistence path. The scale uses `[Graphics] UiScale` and the applied
`LastValue` when queued writes run; unrelated preview drafts must remain unsaved.
Bind canvases and controls to this same setting. Do not register another settings
provider or create a second scale preference to satisfy the new interface member.

## Canvas integration

Opt in each player-facing **screen-space root** with `UserUiScaleCanvas` from
`MToolKit.Runtime.UI.Scaling`. Configure it with the existing setting:

```csharp
binding.Configure(settings.GraphicsSettings.UiScale);
// Or let VContainer invoke binding.Bind(ISettingsSystem).
```

For a `ScaleWithScreenSize` scaler:

```text
effective reference resolution = authored reference resolution / user multiplier
rendered scale = Unity's authored resolution scale × user multiplier
```

Both reference axes change together. The authored match mode and width/height
match remain intact. Unity continues calculating `Canvas.scaleFactor`; assigning
`CanvasScaler.scaleFactor` cannot override `ScaleWithScreenSize` reliably.
For an explicitly opted-in `ConstantPixelSize` root, the scaler's authored scale
factor is multiplied instead. `ConstantPhysicalSize` requires an explicit
authoring decision and is diagnosed rather than silently reinterpreted.

The component retains its original values and restores them on disable. Enabling
it subscribes to the current preference again. Nested canvases inherit their
root's scale and are excluded from a second multiplier. World-space canvases are
excluded. Cursors, debug UI and camera overlays require individual ownership
decisions; this feature never discovers and scales every canvas globally.

`AttachToRoot(content, preference)` is an explicit creator-side helper. Navigation,
save-status, error and bootstrap lifecycles use it for their owned UI. Content
instantiated beneath an already bound root inherits the current scale immediately.
An Addressables-created independent root must include the binding and receive
VContainer injection through its normal creation lifecycle, or have its creator
call `Configure` after instantiation. Addressables alone does not inject settings.

`ScaleChanged(float)` fires after changing the scaler, before geometry necessarily
settles. `ViewportChanged` follows Unity's root dimension notifications. Consumers
can queue one bounded layout operation for rendering. `Attached` is a discovery
notification for late-added bindings, not global scale state; it resets at subsystem
registration. Dispose subscriptions when views are disabled or destroyed.

## Settings and recovery

`UserUiScaleSlider` binds the same setting. Its integer slider positions are
percentages, `75..300`, giving native uGUI mouse, keyboard and controller movement
one percentage-point steps. Serialized fields are `slider`, `percentage` and the
optional `label`. `Configure(Slider, TMP_Text, ReactiveSetting<float>)` supports
creator-owned controls; `GraphicsSettingsInitializer.uiScaleSlider` supports
authored settings prefabs.

Changes preview live. Apply saves the preference and advances its cancellation
baseline. Cancel restores that baseline. `ResetToDefault()` on the slider sets a
100% draft, so ordinary Apply/Cancel semantics still apply. The existing confirmed
all-settings reset persists defaults through `SettingsSystem.DefaultSettings()`.

The fixed **Ctrl + Shift + 0** recovery chord uses an owned Input System action
and runs only while the application is focused. `SettingsSystem.RecoverUiScale()`
is also callable directly and returns `UniTask`. It restores 100%, advances only
the scale baseline and persists only the UI-scale INI key. Other drafts and their
applied baselines remain intact. It waits for an existing INI write to finish;
it does not invoke all-settings Apply, Cancel or FinishApply. The action and
pending-write cancellation lifetime are disposed with the settings system.

## Typography, layout and input conventions

Resolution scaling, user scaling and TMP auto-sizing are separate policies.
`TypesetPreset` applies semantic fonts and optional `Typeset.AccessibleFontSizeMin`
through runtime TMP fields. It follows its owning scale root, including late root
attachment and pooled-view reactivation. Authored typeset assets are never mutated
to apply a preference. Leave the optional accessible floor at zero when ordinary
semantic sizing is sufficient; choose other floors from actual rendering evidence.
TMP font size and geometry estimates alone do not prove rendered glyph height.

Responsive content should wrap and grow vertically, reflow into fewer columns and
scroll when necessary. Keep navigation, dismissal and recovery actions reachable.
Let the reading viewport own width: horizontal preferred-size fitting on a child
can defeat wrapping even when the parent has a vertical ScrollRect. Preserve the
authored baseline when applying temporary compact policies.

`ScrollIntoViewOnSelect.Install(root)` is a creator responsibility for selectable
content. Call it when constructing a view, or author its relay on templates so
runtime-created controls inherit it. Selection reveals actual scroll content;
it does not replace native uGUI navigation or hit testing.

Scale handling has no per-frame hierarchy polling. Use reactive preference changes,
canvas dimension notifications, localized-text/layout invalidation and explicit
creation lifecycles. Avoid continuous forced canvas or full-hierarchy rebuilds.
