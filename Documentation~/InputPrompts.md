# Input glyphs and localized TMP prompts

The runtime pipeline is `InputAction/effective binding → InputBindingResolver → InputPromptResolver → native TMP rich text`. `InputGlyphCatalog` supplies presentation assets; `InputPromptService` adds player context, localization and reactive bindings. The runtime assembly never calls the editor baker.

## Configuration

Create **MToolKit / Input / Input Glyph Catalog** and **Prompt Settings** assets. Assign the settings to `GlobalConstantsConfigAsset.InputPrompts` for the standard VContainer installation, or pass them to a separately owned `InputPromptService`.

Each catalog entry has a device family, logical control path (`enter`, `buttonSouth`, `dpad/up`), optional named TMP sprite asset/reference, optional symbol, localization key, readable fallback and optional `ImageSprite`. Multiple families can use separate atlases. Printable keyboard characters do not require catalog entries.

Presentation styles have stable serialized values: `Auto = 0`, `Character = 1`, `LocalizedName = 2`, `Glyph = 3`, `GlyphAndName = 4`. Device families are `KeyboardAndMouse = 0`, `Xbox = 1`, `PlayStation = 2`, `Nintendo = 3`, `Generic = 4`.

`Auto` uses printable keyboard text and available special-key/controller glyphs. `Glyph` falls back to a readable name when no authored glyph is available. `GlyphAndName` combines the native sprite/symbol and localized name. `Character` retains compact mouse labels such as `LMB`; `LocalizedName` uses full names. A font-aware binding checks configured symbols against the target font and its native fallbacks.

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

`InputPromptResolver` is the synchronous, non-MonoBehaviour implementation. Inject `IInputPromptLocalizer` for tests or a different key-name source. `InputBindingResolver.Resolve` exposes the selected index, actual action and logical controls. Disabled actions are inspected without enabling them. Selection respects effective overrides, empty overrides, action/map/asset masks, binding-group tokens, control-scheme names, explicit indices, device restrictions and composite roles. Alternatives are selected deterministically; one/two-modifier chords retain their order, and directional composites label their parts.

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

## Existing GamepadIconsData and editor authoring

`GamepadIconsData` retains its script GUID, original Xbox/PS fields, serialization aliases and Sprite-returning APIs. Assigning a generalized catalog makes `GetIcon(layout, controlPath)` use it first, with the historical fallback retained for unmigrated assets. New callers can use `GetIcon(InputGlyphFamily, controlPath)` or `InputGlyphCatalog.GetSprite`. UI.Image consumers remain supported. Reflection-based lookup and direct per-consumer family selection are unnecessary; prefer these typed methods and the prompt resolver. The original `XboxIcons`/`Ps4Icons` accessors remain compatibility surfaces rather than a new presentation pipeline.

Editor-only `InputGlyphSpriteAssetBuilder.Bake(assetPath, IReadOnlyList<NamedSprite>)` packs authored source sprites into native TMP assets, material, PNG atlas and atlas-backed Image sprites. It preserves source import settings. `InputGlyphCatalogAuthoring.Generate(legacyIcons, outputFolder, catalogName)` provides a migration/starter example for the existing Xbox/PS icons plus neutral keyboard/generic/Nintendo starters. Projects can replace the artwork/catalog entries without changing runtime code. Rebaking refreshes generated subassets and retains root asset/atlas GUIDs; review references to generated subassets when reauthoring.

Sprites use `<sprite="AtlasName" name="semantic-name">`; ordinary native `<sprite name="semantic-name">` also works with a TMP text object's configured sprite asset. Use unique atlas names, valid sprite characters, materials and textures. Missing artwork or unsupported symbols remain readable through names. The editor baker is not included in player execution.

Atlas names are registered using the native case-insensitive TMP hash. Assets whose names collide with an existing native registration fall back to a symbol/name rather than rendering another atlas. Quotes, control characters, angle brackets and backslashes are rejected in generated tag attributes.

The Dirigible starter art includes keyboard/mouse, Xbox and PlayStation controls plus Nintendo/generic face buttons. Other controls remain readable names until their artwork is authored. This is a catalog/art coverage limit, not a change to configured input behavior. Native integration tests verify actual Sprite characters, asset references, materials, atlas UVs and geometry at two font sizes. In the consuming Unity 6000.3.10f1/uGUI package, the aggregate `TMP_TextInfo.spriteCount` is overwritten with zero; inspect actual `TMP_TextElementType.Sprite` characters when validating that version. No vendor modification is required for rendering.
