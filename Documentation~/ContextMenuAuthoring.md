# Menu style authoring

Open **Tools → MToolKit → Context Menus → Style Wizard**. It edits saved authoring assets and explicitly bakes their settings into runtime `MenuProfile` assets. Existing profiles keep their GUIDs, and existing custom prefab contents are retained.

## Create shared settings and styles

1. Choose **New default style configuration**. Source assets default to `Assets/Editor/MToolKit/Menus`. A configuration references one reusable `MenuCommonSettings` asset.
2. Expand **Shared common settings** to assign shared state style assets. Input, accessibility, placement and fallback composition are in the separate collapsed section.
3. Expand a style and assign its correctly typed **Theme set**, or use **Create**. Open **Details** to edit that source asset. Each family owns its own dimensions and visual composition.
4. Choose **Preview all changes** or **Preview changes** for one style. Inspect diagnostics and the exact proposed runtime paths.
5. Choose **Rebuild all styles** or **Rebuild this style**. Without a current preview, the first press creates the preview; press again after reviewing it.
6. Assign the resulting profiles to the existing menu host configuration or per-use-case requests.

Runtime profiles and missing native presenter prefabs default to `Assets/MToolKit/Menus/<configuration name>`. They do not reference the Editor authoring sources. Creating a configuration or previewing it does not create theme sources or runtime outputs implicitly.

## Typed presentation themes

Each recipe accepts one specific source asset type through Unity's typed object picker:

| Style | Recipe | Theme source | Layout |
| --- | --- | --- | --- |
| `WheelContextMenu` | `SingleRingMenuStyleRecipe` | `WheelMenuThemeSet` | Radius, ring width, start angle, direction and gaps. |
| `PieContextMenu` | `ReplacementMenuStyleRecipe` | `PieMenuThemeSet` | Button orbit and size; optional background ring. |
| `ListContextMenu` | `ListMenuStyleRecipe` | `ListMenuThemeSet` | List width, row height and Pagination/Scrolling overflow. |
| `NestedWheelContextMenu` | `ConcentricMenuStyleRecipe` | `NestedWheelMenuThemeSet` | Independent parent/child widths, inter-ring gap and partial child arc. |

These derive from Editor-only `MenuPresentationThemeSet`. A source also owns visible-item capacity, item/button size, center visibility, backdrop and optional presenter/item/center/secondary prefabs. Its `ThemeOverrides` reference supplies the per-style runtime state layer. Empty visual references inherit Common, then retain an existing output reference.

An assigned theme source owns the family's visual geometry and composition. Existing recipe fields remain under **Advanced inline fallback** and apply when that source is absent. Input bindings, tool/gameplay behavior and recipe navigation policy are outside the theme source. Custom recipes retain their project-defined presenter and inline configuration.

The shared layout `ApplyTo` methods serve both typed sources and legacy inline recipes. Rebuild copies effective values into the existing `MenuProfile`; runtime presenters read that profile. State changes never select another radius, row height or hit-test geometry from a state asset.

Multiple configurations can reuse the same source. Editing it requires explicit Preview/Rebuild for each affected output. Changing the output's Asset Name does not rename an assigned profile. A selected-style rebuild leaves other outputs unchanged.

The geometry panel is a layout sketch with up to twelve illustrative actions. It shows effective family layout and layered colors, not actual actions, custom prefab graphics, text metrics, animation or runtime viewport fitting. Validate those through the menu host and gallery.

## Reusable state style sets

Common and each presentation source show six compact state slots: **Default (Any state)**, **Enabled (Normal)**, **Hovered**, **Selected**, **Disabled** and **Checked**. Each slot references one reusable runtime `MenuStyleSet`. Use **New** to create an asset and **Details** only when editing its contents. Empty slots inherit. Names such as `PositiveInactive` or `NegativeSelected` are asset-instance conventions, not a global enum.

`MenuStyleSet` derives from shared `MToolKit.Theme.ThemeStyleSet`. It references existing MTK Swatches for surface, foreground, description, icon and border, plus label/description Typesets and Spacing. Menu-specific channels are collapsed inside that asset's Inspector: explicit normal/hovered/selected/disabled fills, custom panel, center, auxiliary/footer, outside backdrop, inner/outer borders, separator and check marker. The ordinary slot editor does not repeat those individual fields.

Swatch fields reuse MTK's color-chip/asset drawer. **Open MTK Theme tools** opens the existing leaf-token configurator. Clearing a property permits inheritance. Empty fill channels use the asset's Surface; empty inner/outer border channels use its Border. Missing check-marker colors search all layers before finally using the resolved label color.

Each property resolves in this order:

1. Per-style exact state: direct legacy value, then state style asset.
2. Per-style Default: direct legacy value, then Default style asset.
3. Shared exact state and Default, in the same order.
4. Fallback skin exact state and Default, in the same order.
5. Native defaults or the preset's restored authored baseline.

A per-style Default therefore wins over a shared state-specific property. Disabled takes precedence over hover, selection and checked interaction state. Surface, text and border can vary independently. Swatches, Typesets and Spacing resolve semantic IDs through `IThemeService` unless the supplying asset or entire layer locks them to authored tokens.

State packs contain no family geometry. Their Spacing token applies to the widget's explicitly wired MTK layout targets; the legacy spacing API continues to resolve Default/Any. Keep consistent state spacing when padding/gap should remain stable. Typesets bind through `TypesetPreset`. The framework does not add layout controllers to arbitrary custom prefab children; custom widgets can opt out with `UseSkin = false`.

Center, auxiliary and outside-backdrop channels are independent of an item's Surface. The backdrop's native default is black at 35% opacity; `MenuProfile.Backdrop = false` keeps it transparent. A custom center or presenter can deliberately own its own visuals.

## Preserve or convert existing layers

The existing `MenuSkin` remains the runtime layer container. Common's `ThemeOverrides` bakes into `MenuProfile.SharedThemeOverrides`; the selected source's layer bakes into `MenuProfile.ThemeOverrides`. The optional `MenuProfile.Skin` remains the fallback. Clearing a layer reference and rebuilding clears that layer on the selected output.

**Advanced legacy overrides (count)** retains old direct Swatch/Typeset/Spacing fields and local raw colors. These direct properties win over the style asset in the same exact state, so the count identifies deliberate or inherited overrides that may mask a state asset.

Use **Preview legacy conversion** to choose an existing runtime Assets folder outside Editor folders. Inspect the exact proposed paths, then choose **Convert previewed tokens**. Conversion creates and verifies state assets before clearing converted direct entries. It preserves the layer/profile GUIDs and existing MTK token references; it does not duplicate Swatches, Typesets or Spacing. Occupied paths and already-assigned state slots are refused. Raw colors and ambiguous groups containing distinct inactive token references remain advanced rather than losing authored data. Unconverted values and existing style assets are not overwritten.

Runtime `MenuStyleSet` and `MenuSkin` assets belong outside Editor folders. Typed presentation theme sources and configurations are Editor-only and can live under `Assets/Editor`. Creation refuses foreign types, occupied files/directories and orphan metadata. The tool saves only its owned outputs and sources; it does not call global `SaveAssets`, rewrite custom prefab children or save unrelated scenes.

## Update timing and validation

Changing source references or layout requires Preview/Rebuild. Source, state-style content or output edits after preview invalidate that plan. An `IThemeService` update refreshes open menus. Ordinary raw asset edits do not necessarily emit that event; token values take effect on the next bind, refresh or theme update.

Run `MToolKit.ContextMenus.Authoring.Tests.Editor` in Unity Test Runner, or select `MToolKit.Editor.ContextMenus.Tests`. Coverage includes typed source serialization, family layout, in-place GUID retention, shared state assets, stale plans, guarded creation, token-preserving conversion, custom composition and existing Addressables-only presenters.

Authoring unit tests do not establish rendering or input behavior. Run the supplied gallery PlayMode tests and inspect its mouse, keyboard, controller, scaling and viewport behavior in the consuming project. Include the development-player checks required by that project's workflow.

## Editor API

- `MenuStyleAuthoring.Preview(config, optionalStyleId)` and `Rebuild(plan)` validate and apply selected outputs.
- `MenuStyleWizard.CreateDefaults(newPath, optionalCommon)` creates authoring sources only.
- `CreatePresentationThemeSetAsset(type, newPath)` creates a concrete typed Editor theme source.
- `CreateMenuStyleSetAsset(newPath)` creates a reusable runtime state pack.
- `CreateThemeOverrideAsset(newPath)` creates a runtime layer container.
- `PreviewStyleSetConversion(layer, outputFolder)` and `ConvertStyleSets(plan)` explicitly migrate representable legacy tokens.
