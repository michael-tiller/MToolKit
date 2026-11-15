# Input glyphs and localized TMP prompts

The runtime pipeline is `InputAction/effective binding → InputBindingResolver → InputPromptResolver → native TMP rich text`. `InputGlyphCatalog` supplies presentation assets; `InputPromptService` adds player context, localization and reactive bindings. The runtime assembly never calls the editor baker.

## Configuration

Create **MToolKit / Input / Input Glyph Catalog** and **Prompt Settings** assets. Assign the settings to `GlobalConstantsConfigAsset.InputPrompts` for the standard VContainer installation, or pass them to a separately owned `InputPromptService`.

Each catalog entry has a kind, device family, optional named TMP sprite asset/reference, optional symbol, localization key, readable fallback and optional `ImageSprite`. `InputGlyphEntryKind.Binding = 0` is the compatible default and uses a logical control path (`enter`, `buttonSouth`, `dpad/up`). `Device = 1` uses `DeviceLayout` instead of a control path to describe the input device itself. Multiple families can use separate atlases. Printable keyboard characters do not require catalog entries.

Presentation styles have stable serialized values: `Auto = 0`, `Character = 1`, `LocalizedName = 2`, `Glyph = 3`, `GlyphAndName = 4`. Device families are `KeyboardAndMouse = 0`, `Xbox = 1`, `PlayStation = 2`, `Nintendo = 3`, `Generic = 4`.

`Auto` uses printable keyboard text and available special-key/controller glyphs. `Glyph` falls back to a readable name when no authored glyph is available. `GlyphAndName` combines the native sprite/symbol and localized name. `Character` retains compact mouse labels such as `LMB`; `LocalizedName` uses full names. A font-aware binding checks configured symbols against the target font and its native fallbacks.

The settings asset supplies a default: `Auto` follows that default, while an explicit style overrides it for that resolve call or UI instance. Choose `Glyph` for a compact hotbar, `LocalizedName` for a label, or `GlyphAndName` for an explanatory hint. Changing one view's style does not change shared settings or another view's style.

When an action has no eligible binding in the requested context, choose `InputPromptUnboundBehavior`: `ShowUnbound = 0` preserves the localized Unbound label, `KeyboardAndMouse = 1` tries the same action's configured desktop binding, and `Hide = 2` returns an empty prompt. This policy concerns missing bindings; missing controller artwork uses the glyph fallback below while retaining the configured controller binding. A valid controller binding always takes precedence over desktop fallback.

```csharp
var context = prompts.Context.WithUnboundBehavior(InputPromptUnboundBehavior.KeyboardAndMouse);
string shortcut = prompts.Resolve(action, InputPromptStyle.Glyph, context);
hotbarPrompt.ConfigureUnboundBehavior(InputPromptUnboundBehavior.Hide);
```

Desktop fallback uses effective keyboard/mouse paths, including overrides and composites, without assuming a scheme name. It preserves action/map/asset masks, player device restrictions and an explicitly selected binding index; it does not enable actions, pair devices or change gameplay routing. If no eligible desktop binding exists, it retains Unbound. An empty explicit path has no action to search; Hide returns empty and other policies retain Unbound. A nonempty `ResolvePath` remains the explicitly requested control.

Configure a Unity Localization collection name on the settings asset. MTK does not assume a game-specific table. `InputPromptSettings.Configure(...)`, `NotifyChanged()`, and catalog setters publish changes. Inspector changes also refresh live bindings. The optional preview binding group explicitly selects bindings; a family override changes only their appearance. For example, a Gamepad group plus PlayStation family can preview the configured controller binding without changing gameplay routing.

## Direct resolution and composition

```csharp
using MToolKit.Runtime.Input.Prompts;

var prompts = new InputPromptService(settings, actions: runtimeActions);
InputAction confirm = runtimeActions.FindAction("Player/Confirm");
string representation = prompts.Resolve(confirm, InputPromptStyle.GlyphAndName);
// Example: <sprite="MyKeyboardGlyphs" name="keyboard-enter"> Enter

string sentence = prompts.Compose("ui.confirm", "Press {confirm} to confirm {name}",
    new InputPromptArgument("confirm", confirm),
    InputPromptArgument.PlainText("name", externallySuppliedPlayerName));
```

`Compose` passes a named dictionary to native Unity SmartFormat. Translators control sentence order. `PlainText` escapes external text for TMP, including attempts to close a `noparse` tag and backslash-encoded Unicode delimiters. Generated prompts retain their sprite tags. The lower-level `Localize` and existing `LocalizationHelper.GetLocalizedString` accept trusted formatting arguments; escape external text or use `Compose` at that boundary.

`InputPromptResolver` is the synchronous, non-MonoBehaviour implementation. Inject `IInputPromptLocalizer` for tests or a different key-name source. `InputBindingResolver.Resolve` exposes the selected index, actual action and logical controls. Disabled actions are inspected without enabling them. Selection respects effective overrides, empty overrides, action/map/asset masks, binding-group tokens, control-scheme names, explicit indices, device restrictions and composite roles.

When a context supplies a device, candidates containing controls on that device rank before keyboard/mouse companions, regardless of binding order. Equal candidates retain authored order. Companions remain eligible for mixed keyboard/mouse chords and keyboard-only shortcuts after mouse activity; this preference never bypasses masks, allowed devices or an explicitly selected binding index.

For duplicate composite roles, prompts show the first eligible authored alternative per role after override, mask, group and device filtering. For example, a WASD/arrows composite normally displays WASD, then uses an eligible arrow when its earlier role binding is unbound or filtered. Gameplay retains all configured alternatives. One/two-modifier chords keep their required roles and order; directional composites label their selected representatives.

## Native Localization and Google Sheets authoring

Use semantic keys, text and Smart String templates in the existing Localization collection/Google Sheets extension:

| Key | English | French example |
| --- | --- | --- |
| `input.key.enter` | `Enter` | `Entrée` |
| `input.prompt.confirm` | `Press {confirm} to confirm` | `{confirm} : confirmer` |
| `ui.input.cancel` | `{0} Cancel` | `Annuler {0}` |

Mark templates with placeholders as **Smart**. Keep atlas names and numeric sprite indices out of the sheet. Catalog entries own the mapping from semantic controls to artwork. Existing `LocalizationHelper.GetLocalizedString("ui.confirm", representation)` composition remains available for positional templates. A custom SmartFormat formatter is unnecessary: native named arguments provide inline prompts without a second template language.

Names use `input.key.{controlPath}`, `input.mouse.{controlPath}`, and `input.gamepad.{family}.{controlPath}` by default. Composite role labels use `input.composite.{role}`; the unbound fallback uses `input.unbound`. Catalog localization keys can override these conventions. Missing entries use readable English/control-name fallbacks while asynchronous table loading completes, then refresh when the native operation completes.

## Binding TMP text

```csharp
// Keep both disposables for the lifetime of this UI/player context.
InputPromptService prompts = new(settings, actions: runtimeActions);
IDisposable binding = prompts.Bind(caption, () => prompts.Compose(
    "input.prompt.confirm", "Press {confirm} to confirm",
    new InputPromptArgument("confirm", confirm, InputPromptStyle.GlyphAndName)));

// OnDisable / owning scope disposal:
binding.Dispose();
prompts.Dispose();
```

`Bind` updates the existing `TMP_Text` in place and passes its font to symbol fallback selection. It does not toggle the GameObject or force layout rebuilds. TMP retains normal wrapping, sizing, fallback fonts and accessibility scaling. Sprite assets use semantic names and native metrics.

Alternatively, add `TMPInputPrompt` to an existing TMP text object. Assign an `InputActionReference`, style, optional native `LocalizedString` sentence, and named argument (default `input`). The component receives `IInputPromptService` through VContainer. Without injection it owns a standalone service using its optional settings. `Configure`, `ConfigureSentence`, and `Construct` also support runtime setup. It releases its input and localization bindings on disable and refreshes on re-enable. Each component owns its `LocalizedString` instance; do not share one mutable instance between components.

Two components can use the same service with different presentations:

```csharp
hotbarPrompt.Configure(confirmReference, InputPromptStyle.Glyph);
instructionPrompt.Configure(confirmReference, InputPromptStyle.GlyphAndName);
hotbarPrompt.Construct(sharedPrompts);
instructionPrompt.Construct(sharedPrompts);

// Reconfigure this instance alone; the hotbar stays glyph-only.
instructionPrompt.Configure(confirmReference, InputPromptStyle.LocalizedName);
```

Adapters that identify their own runtime actions by name can optionally implement `IInputPromptDisplaySource.GetDisplayString(string actionName, InputPromptStyle style)`. Forward the requested style to the shared resolver while preserving the adapter's player/action context, for example:

```csharp
public string GetDisplayString(string actionName, InputPromptStyle style)
    => prompts.Resolve(runtimeMap.FindAction(actionName, throwIfNotFound: false),
        style, playerContext);
```

This optional interface adds no members to existing action or hotkey interfaces. Consumers can use it when available and retain their existing adapter otherwise. It is a display seam; the owning service/view still manages `Bind` or `Changed` subscriptions and cleanup.

Adapters can additionally implement `IInputPromptFallbackDisplaySource` to accept a per-view unbound policy through its three-argument `GetDisplayString` overload. The original two-argument interface remains compatible. Pass the policy through `InputPromptContext.WithUnboundBehavior`; avoid detecting Unbound by comparing localized text.

## Runtime action identity and independent players

`Resolve(InputAction)` always inspects the supplied execution instance. `ResolveReference(InputActionReference)` uses optional `IInputActionSource.ResolveRuntimeAction` to map an authored reference to the clone owned by `InputService`. Foreign action instances and maps without an asset backreference retain their identity. `IInputService` remains source compatible; the action-source interface is additive.

For independent players, create separate services with their own runtime asset and/or explicit `PlayerInput`. Automatic context follows relevant action activity and that player's native InputUser/control-scheme changes. A merely connected controller does not become active. For explicit selection use `SetContext(new InputPromptContext(device, bindingGroup, familyOverride, bindingIndex))`, or pass a context to one resolve call. The four-argument context constructor does not require caller-side TMP references; rendered callers can use the five-argument overload with a target font.

The service observes native binding/device/user changes, existing rebinder notifications through additive `IInputBindingChangeSource`, and language changes through R3/native Localization events. It does not poll each frame. A caller that owns a service disposes it; a caller that owns a UI binding disposes that binding. Generic settings rows continue to display/rebind the exact action asset supplied by their existing initializer; projects executing InputService's clone should explicitly supply that runtime asset rather than silently changing an existing rebinding target.

## Controller mapping and extension

| Logical control | Xbox | PlayStation | Nintendo | Generic |
| --- | --- | --- | --- | --- |
| `buttonSouth` | A | Cross | B | South |
| `buttonEast` | B | Circle | A | East |
| `buttonWest` | X | Square | Y | West |
| `buttonNorth` | Y | Triangle | X | North |

Default layout inheritance covers XInput, DualShock/DualSense and Switch Pro families. Generic Gamepad stays generic. Add ordered catalog layout rules to support another family/layout without changing the resolver. Bindings keep their actual logical controls; glyph choice never swaps face-button behavior.

For controller bindings, glyph presentation tries usable original-family artwork, then Generic artwork for the same logical control, then the original family's localized name. `GlyphAndName` retains that original localized name beside either glyph. Missing or invalid native sprite geometry, atlas/material references, unsafe tag names, and failed TMP registration all follow this fallback. `Character` and `LocalizedName` remain text-only, and keyboard/mouse controls do not borrow Generic controller artwork.

## Device identity prompts

The optional `IInputDevicePromptService` adds `ResolveDevice(...)` without changing `IInputPromptService` or requiring action-only implementations to add methods. The standard VContainer installer exposes both interfaces on the same `InputPromptService` singleton. Use `Resolve(action, ...)` for a configured control and `ResolveDevice(...)` for the actual input device; device identity requires no invented action or binding.

```csharp
using System.Collections.Generic;

// devicePrompts is an injected IInputDevicePromptService; deviceCaption is TMP_Text.
IDisposable deviceBinding = devicePrompts.Bind(deviceCaption, () =>
    devicePrompts.Localize("input.prompt.device", "Using {device}",
        new Dictionary<string, object> {
            ["device"] = devicePrompts.ResolveDevice(InputPromptStyle.GlyphAndName)
        }));

// OnDisable: release this UI binding; the injected service belongs to its DI scope.
deviceBinding.Dispose();
```

Add `input.prompt.device` as a native Smart String, for example English `Using {device}` and French `Périphérique actif : {device}`. The argument above is trusted resolver output, so its native sprite tag remains intact. The same `Bind` observes device, locale and catalog changes and updates the existing widget. Standalone callers can cast their owned `InputPromptService` to `IInputDevicePromptService` and dispose the service with the owning context.

Automatic device identity follows the service's relevant input activity or its specified `PlayerInput`'s paired devices. Supply a runtime action asset, action source or `PlayerInput` for that context; merely connecting a controller does not select it. A context passed to `ResolveDevice` or retained with `SetContext` selects its explicit `Device`. `FamilyOverride` and preview binding groups affect action artwork/selection; they do not change the actual device's identity. The synchronous alternative is `InputPromptResolver.ResolveDevice(device, style, context)`.

Author a `Device` catalog/recipe row with a native `DeviceLayout`, such as `XInputController`, `DualShockGamepad` or `Gamepad`, and leave `ControlPath` empty. Lookup walks the actual layout and its closest native ancestors first, then applies platform and locale fallbacks within each layout. A PlayStation-specific `Any` entry therefore wins over a generic `Gamepad` entry for Windows. The row's `Family` organizes its atlas recipe; it does not create a gameplay binding or alter layout identity. Device identity is layout/locale/platform based, independent of keyboard keycap content metadata. Binding, generated-keycap and legacy Image lookups exclude device rows.

`Character` and `LocalizedName` return a localized device name. `Auto`/`Glyph` prefer available device artwork, and `GlyphAndName` appends its localized name. Missing artwork uses the authored fallback or native device display name; a null, removed or disabled device uses `input.device.none`. Default name keys use the `input.device.` prefix followed by `keyboard`, `mouse`, `xbox`, `playstation`, `nintendo`, `controller` or `generic`; a row's `LocalizationKey` overrides them.

Identity reflects the native layout/provider exposed to Unity, not verified physical hardware. A Steam-backed layout can have explicitly authored Steam artwork, including a registered non-Gamepad provider layout. This does not enable Steam support, guarantee a physical Steam Controller, or identify Steam hardware behind XInput emulation. Preview choices likewise provide no hardware-detection proof. Additional controller layouts and artwork remain configuration work.

The native Mouse's five button paths remain `leftButton`, `rightButton`, `middleButton`, `forwardButton` and `backButton`. Its `DeltaControl` supplies synthetic `scroll/up` and `scroll/down` axes for exact directional artwork; `scroll/y` remains an undirected axis. Authoring glyphs does not add buttons or change input routing.

## Existing GamepadIconsData and editor authoring

### Saved atlas recipes and generated keycaps

New profiles use a 64-point caption inside a 64-pixel cap, with two pixels of vertical layout padding. TMP renders the cap as one em, so matching `FontSize` to `SharedHeight` preserves the source font's ordinary letter proportions; a 32-point label inside a 64-pixel cap remains half-size at every UI scale. New standard named keys use FitLabel so Enter, Shift and other names fit at this size. Existing saved profiles retain their authored values and need an explicit rebake. Use the surrounding UI's semantic typography minimum and inspect caption ink at normal and accessibility scales; increasing only atlas resolution or the outer sprite size does not repair a small internal caption. Outline materials can reduce the visible white body; calibrate the profile's caption size and layout padding against the actual baked ink.

`InputKeycapBakeProfile.TextColor` tints artwork and, by default, captions. Enable `OverrideFontColor` and select `FontColor` to give captions an independent color without changing existing profiles. For example, set `FontColor` to white while retaining a configured artwork tint. Both settings are exposed in the saved-profile Inspector and Atlas Wizard.

Assign the optional `FontMaterial` to use a native TMP outline preset for the profile's `Font`. It must reference that font's atlas; validation rejects mismatched atlases before baking. The baker uses and releases a temporary material copy so native TMP calculations cannot alter the authored preset. A locale font override uses its own native material. Material choice, font padding and output size affect small outlines; inspect the actual saved cap at its intended display size.

Open **Tools / MToolKit / Input Glyphs / Atlas Wizard** to author glyph artwork. A saved atlas recipe records its destination, catalog mappings, semantic sprite names and source Sprites. Replace a source Sprite and rebuild from the recipe to update the native TMP atlas without editing consumer strings. Add new logical controls to the recipe rather than encoding numeric atlas indices in localization content.

Each `InputGlyphAtlasSource.Glyph.Tint` defaults to `Color.white`. Baking multiplies the source RGBA pixels by this tint before packing the native atlas, leaving original artwork and import settings unchanged. This supports, for example, baking white source icons as black. `ImportFromCatalog` treats generated pixels as already tinted and initializes the imported recipe's tint to white. Rebuilding from destination-backed generated artwork also resets its tint to white after applying it, so repeated bakes do not compound the tint. The lower-level editor `NamedSprite(name, sprite, tint)` overload provides the same behavior.

Save editor-only recipes and bake profiles under an `Editor` folder, outside runtime Addressables folders. Save the generated TMP assets/atlases in the runtime artwork folder. The runtime catalog references generated output, never its authoring profile.

For keyboard artwork, use an `InputKeycapBakeProfile`. It defines the TMP font/font size, border and background sprites/styles, text/fill/border colors, padding, shared height and minimum width. The editor baker uses UGUI Images and a centered TMP label with native preferred-size layout. Printable keys can share square dimensions; named keys such as Enter, Backspace, Shift and Space can use wider, per-key width rules. Fit-to-label and fixed-width rules make sizing repeatable across a whole set. Change the profile and rebake to regenerate the caps and native TMP atlas together; there is no need to draw an image for each key.

The sizing and image modes have stable serialized integers: `InputKeycapWidthRule.FitLabel = 0`, `Fixed = 1`; `InputKeycapImageMode.Simple = 0`, `Sliced = 1`. Widths include the configured border/padding; invalid dimensions, missing font characters and clipped labels produce authoring errors. The profile's native `LocalizationCollection` and `Locales` fields keep localized rebakes repeatable directly from the wizard.

Keep fonts and optional nine-sliced border/background source sprites referenced by the profile. Baked outputs are normal saved TMP sprite assets. Editor preview/rendering resources and source recipes are never required by a player. The resulting glyph metrics preserve each cap's width relative to the shared height, so inline text retains native wrapping and accessibility scaling.

Background and border image layers support both static (`Simple`) and native nine-sliced (`Sliced`) sprites. For nine-slicing, set the source Sprite's border in Unity's Sprite Editor; the baker keeps the source import settings. An optional authored UGUI background prefab supports procedural styles during baking. For example, a prefab can contain an independently installed Procedural UI Tool component. MTK instantiates the prefab only in its isolated editor preview and consumes the rendered result; it does not copy vendor code, require that package, or depend on it at runtime. Keep the ordinary image-based profile when that optional tool is unavailable.

The editor preview submits UGUI's generated Graphic meshes, native materials and textures through Unity's preview renderer. It preserves native UGUI layout, slicing and TMP label geometry; it adds no runtime renderer. Keep background prefabs self-contained and unmasked: `Mask` and `RectMask2D` effects are rejected instead of exporting an incorrect clipped result.

### Optional native sprite-slice renaming

With Unity's optional `com.unity.2d.sprite` editor package available, `MToolKit.Editor.SpriteAuthoring.SpriteRenameUtility.Rename(...)` renames saved multiple-sprite texture slices through the native Sprite Editor data providers. Pass `SpriteRename(sourceSprite, newName)` records. It validates all requested textures, sprite IDs and final names before applying changes, supports simultaneous name swaps, and skips no-op sheets. Existing sprite GUIDs/local file IDs, geometry and serialized Image references are retained; the source PNG is unchanged. Duplicate final names are rejected before an importer is written. The returned integer is the number of changed slices. This optional editor assembly introduces no runtime dependency.

### Keyboard cap content

For a special key, set the row's `ContentMode` to `Sprite`, assign its `ContentSprite`, and choose the maximum `SpriteSize`. Native UGUI centers the image and preserves its aspect ratio within the same background, padding and width rules as text caps. One symbol cap can serve every language. Keep `LocalizationKey` for its separate localized name; an unassigned source sprite uses the readable localized label until artwork is supplied. Ordinary text caps keep `ContentMode = Label`.

Choose `SpriteAndLabel` to put both the artwork and localized name inside the same border, such as a Shift arrow beside LShift or its translated short name. `SpriteLabelSpacing` controls their gap; `FitLabel` uses native preferred-size layout so translated captions can widen the cap. This mode contains text and therefore produces a separate cap in each language's sheet. The rendered result remains one ordinary TMP sprite. Baking controls what is inside the box; the caller's `GlyphAndName` style additionally requests a name outside it.

Keyboard profiles can cover the full Input System keyboard: letters/digits, punctuation, function keys, navigation/editing, lock/system keys, numpad and the additional keys exposed by the installed package. Numpad controls retain their own semantic identity even when their native display character matches a main keyboard key. Printable main-key artwork still follows the native printed character, including alternate keyboard layouts. Prefer `FitLabel` for translated captions such as Page Up or Backspace; each key remains independently configurable.

`InputKeycapBakeProfile.CreateStandardKeys()` reads the installed native layout without creating a device. New profiles initialize their defaults in `OnEnable`; loading a saved recipe preserves its serialized array, including an intentionally empty array. Call `profile.EnsureStandardKeys()` explicitly to append missing defaults to an edited recipe. It retains existing rows, ordering, artwork, labels, widths and styles, compares control paths without case, and returns whether it added anything. Regional printed characters outside the default literal set can have authored rows; a missing generated cap falls back to readable text.

The wizard's **Key to edit** selector exposes that row's sprite, content mode, fallback label, localization key and width rule; the optional full list supports adding/removing custom rows. **Add missing standard keys** supports Undo and leaves rebaking explicit. The same fields are editable on the saved ScriptableObject. For example, configure `numpad8` with fallback `Num 8` and localization key `input.key.numpad8`; generated-mode name resolution uses this configured fallback when a translation is missing. An imported translation still wins, and stale baked text remains ineligible until rebaked.

Platform variants use `InputGlyphPlatform.Any = 0`, `Windows = 1`, `MacOS = 2`, and `Linux = 3`. Add rows with the same logical control and different `Platform` values, with distinct semantic sprite names. The resolver reads Unity's `SystemInfo.operatingSystemFamily` on its first render that needs automatic platform selection, then retains the result. Construction uses no Unity API; a per-context override such as `new InputPromptContext().WithPlatform(InputGlyphPlatform.MacOS)` also avoids the OS query and supports previews and independent callers. Exact-platform entries take priority over `Any`, then use the existing locale and content rules. Neutral OS symbols share the neutral sheet; localized OS-specific captions still belong in their locale sheet. Existing catalog/Image lookup overloads retain `Any` behavior; typed platform overloads opt into platform-aware lookup. `EnsureStandardKeys()` completes universal `Any` rows without replacing authored platform variants.

The standard installed Unity Keyboard layout has no bindable Fn control. A supplemental `fn` catalog/profile row can provide its globe artwork to `ResolvePath` or a custom keyboard layout; it does not create hardware support or a gameplay binding.

### Separate sheets and enabled locales

`LocaleSource = ProjectEnabled` follows Unity Localization's configured project locales, with no English/French limit. `ProfileLocales = 0` retains the manual `Locales` list for existing profiles; `ProjectEnabled = 1` ignores that manual list. The wizard displays the resolved codes. Adding another enabled project locale includes it in the next bake.

Each bake writes the configured base asset for neutral printable characters and icon-only caps, and one sibling native asset/PNG per language containing localized captions. For example, `MTK_Keycaps.asset`, `MTK_Keycaps_en.asset` and `MTK_Keycaps_fr.asset` are distinct outputs. Localized languages are never packed together in one file. Names such as `keycap-left-shift-fr` remain semantic and stable; the runtime catalog points each locale entry to its own asset.

```csharp
// Editor authoring only; players consume the saved TMP assets.
profile.LocaleSource = InputKeycapLocaleSource.ProjectEnabled;
InputKeycapBakeResult result = InputKeycapBaker.BakeAll(profile);
foreach (InputKeycapAtlasOutput output in result.Atlases)
    Debug.Log($"{output.LocaleCode}: {output.SpriteAsset.name}");
foreach (string issue in result.Issues)
    Debug.LogWarning(issue);
```

`Bake(profile)` remains available and returns the neutral primary asset, or the first emitted locale asset when there is no neutral group. Use `BakeAll` to inspect all outputs and coverage issues. `GetLocaleCodes(profile)` and `GetSpriteAssetPath(profile, code)` expose the resolved list and destinations. Rebuilding retains the base GUID and surviving same-name sprite identities. Prior owned mappings are pruned without deleting old files; copied profiles do not inherit another profile's cleanup authority. All destinations are checked before writes, and the catalog publishes once after the groups finish; file writes themselves are sequential.

`LocaleFontOverrides` selects a native TMP font by exact locale, then parent language, then the profile font and its native fallback chain. Missing translations use the readable profile label and report a content gap. Unsupported glyph rows are reported and omitted rather than emitting incorrect glyphs; the prompt retains its normal readable fallback. Font overrides affect baked captions; configure the UI font/fallbacks for surrounding translated text as well. Enabling a locale does not supply translations or fonts. CJK sheets can use provisional readable captions while suitable CJK fonts and translated names are authored and verified.

After Google Sheets content changes, use this explicit authoring sequence:

1. Import through the collection's existing Unity Localization Google Sheets extension and wait for it to finish. Sheets contain semantic keys and translations; sprite names, atlas indices and controller mappings stay in the catalog/profile.
2. Select that native `LocalizationCollection` and choose `ProjectEnabled` or an explicit manual locale subset in the saved profile. Moving an existing table or collection preserves its Unity object reference. If an import creates a replacement collection, deliberately assign the replacement to the profile.
3. In the Atlas Wizard, select **Validate saved keycaps against imported tables**. `InputKeycapBaker.ValidateBakedContent(profile)` reports missing translations, stale text and missing expected native entries without writing tables or rebuilding assets. It compares labels, entry identities and content modes; it does not fingerprint artwork or styling.
4. Review the report, update fonts/widths or sprite assignments as needed, explicitly rebuild, and inspect the preview and a real TMP prompt. Changes to artwork or styling always require rebuilding. No remote upload or automatic table import occurs during baking.

Generated text caps record their locale and exact baked label. Runtime selection first uses an assigned locale-neutral symbol cap, otherwise the printed keyboard character or current localized key name and selected locale (exact locale, then parent language, then locale-neutral). A missing or stale translated text cap falls back to readable text rather than displaying a cap with the wrong label. Rebake the affected locale after updating its font, translation or keycap style. Custom localizers can optionally implement `IInputPromptLocaleProvider`; existing `IInputPromptLocalizer` implementations remain valid.

The catalog's keyboard glyph mode is explicit: `Authored = 0` preserves existing artwork and `GeneratedKeycaps = 1` selects generated caps. Character and LocalizedName styles remain text. Auto/Glyph select an eligible generated cap. GlyphAndName always appends the localized name outside the cap, including when that label is also printed inside it: `<sprite="MTK_Keycaps" name="keycap-e"> E`. Controller mappings retain their existing logical controls and artwork.

`InputKeycapContentMode` has explicit values `Label = 0`, `Sprite = 1`, and `SpriteAndLabel = 2`. Text-bearing Label and SpriteAndLabel caps use the existing locale and exact baked-label matching. SpriteAndLabel bakes the icon and localized label inside one cap; `Glyph` displays that cap and `GlyphAndName` also appends the external localized name. A generated Sprite cap contains shared artwork and is locale-neutral; its external name still uses Unity Localization. Keep `Entry.KeycapContent` at Label for existing authored glyphs. Compact modifier fallbacks preserve the side: `LCtrl`, `RCtrl`, `LShift`, `RShift`, `LAlt`, and `RAlt`, using the existing semantic keys `input.key.leftCtrl`, `input.key.rightCtrl`, and their Shift/Alt equivalents.

`GamepadIconsData` retains its script GUID, original Xbox/PS fields, serialization aliases and Sprite-returning APIs. `GetIcon(layout, controlPath)` and its typed family overload try original-family catalog sprites, still-valid original Xbox/PS serialized sprites, then Generic catalog sprites for the same control. Unknown and Nintendo layouts never borrow Xbox artwork. `InputGlyphCatalog.GetSprite` likewise tries original-family then Generic sprites for controllers. Image APIs return null after both image stages fail, so the caller can present the original family's localized name; a usable native Image sprite does not require a TMP material. UI.Image consumers remain supported. Reflection-based lookup and direct per-consumer family selection are unnecessary; prefer these typed methods and the prompt resolver. The original `XboxIcons`/`Ps4Icons` accessors remain compatibility surfaces rather than a new presentation pipeline.

Editor-only `InputGlyphSpriteAssetBuilder.Bake(assetPath, IReadOnlyList<NamedSprite>)` packs authored source sprites into native TMP assets, material, PNG atlas and atlas-backed Image sprites. It preserves source import settings. `InputGlyphCatalogAuthoring.Generate(legacyIcons, outputFolder, catalogName)` provides a migration/starter example for the existing Xbox/PS icons plus neutral keyboard/generic/Nintendo starters. Projects can replace the artwork/catalog entries without changing runtime code. Rebaking retains root asset/atlas GUIDs, the owned material, and the identity/local file ID of existing same-name Sprite subassets, while updating their native geometry. Removing or renaming a sprite needs corresponding consumer migration. An unrelated PNG sidecar or externally shared material is rejected before mutation; choose a dedicated atlas destination instead.

The existing `TryGetEntry(family, controlPath)` and `GetSprite` APIs continue to select locale-neutral authored artwork for Image compatibility. Use the locale overload for localized authored artwork, or `TryGetKeycapEntry(controlPath, localeCode, expectedLabel, out entry)` and its `ImageSprite` for an explicitly selected generated cap.

Sprites use `<sprite="AtlasName" name="semantic-name">`; ordinary native `<sprite name="semantic-name">` also works with a TMP text object's configured sprite asset. Use unique atlas names, valid sprite characters, materials and textures. Missing artwork or unsupported symbols remain readable through names. The editor baker is not included in player execution.

Atlas names are registered using the native case-insensitive TMP hash. Assets whose names collide with an existing native registration fall back to a symbol/name rather than rendering another atlas. Quotes, control characters, angle brackets and backslashes are rejected in generated tag attributes.

MTK's migration/starter generator supplies basic keyboard/three-button mouse artwork, existing Xbox/PlayStation controls and Nintendo/generic face buttons. Dirigible's separate configured recipes add D-pad/stick-axis artwork, five native mouse buttons, directional scroll glyphs and device identities. Coverage belongs to the consuming catalog; controls without artwork retain readable names and their configured input behavior.

Native integration tests inspect actual Sprite characters, asset references, materials, atlas UVs and geometry at two font sizes. In the consuming Unity 6000.3.10f1/uGUI package, the aggregate `TMP_TextInfo.spriteCount` is overwritten with zero; inspect actual `TMP_TextElementType.Sprite` characters when validating that version. No vendor modification is required for rendering.
