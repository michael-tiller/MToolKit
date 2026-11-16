# Menu style authoring

Open **Tools → MToolKit → Context Menus → Style Wizard**. The tool edits saved authoring assets and explicitly bakes their settings into runtime `MenuProfile` assets.

The Inspector and wizard use native Unity Editor controls, MTK theme asset tooling, existing input references and the shared widget factories.

## Create shared settings and styles

1. Choose **New default style configuration**. Source assets default to `Assets/Editor/MToolKit/Menus`. The wizard creates one `MenuStyleConfig` and, unless one was selected, a companion `MenuCommonSettings`.
2. Expand **Shared common settings**. Configure **MTK theme overrides (shared)**, optional **Fallback skin**, input bindings, center/item/secondary prefabs, placement, accessibility and transitions.
3. Expand a style to edit its specialized layout. The four defaults are `WheelContextMenu`, `PieContextMenu`, `ListContextMenu` and `NestedWheelContextMenu`.
4. Choose **Preview all changes** or **Preview changes** for one style. Inspect diagnostics and the exact proposed runtime asset paths.
5. Choose **Rebuild all styles** or **Rebuild this style**. A rebuild button without a current preview first creates the preview; press it again after reviewing the paths.
6. Assign the generated profiles to the project's existing menu host configuration or per-use-case requests.

Runtime profiles and native presenter prefabs default to `Assets/MToolKit/Menus/<configuration name>`. They do not reference the Editor authoring assets.

The geometry panel is a layout sketch with up to twelve illustrative actions. It uses native radial geometry and shows item/center rectangles. It does not render custom prefab graphics, actual actions, text metrics, animations or runtime viewport fitting. Validate those through the existing menu host and project UI.

## Sharing and overrides

Multiple configurations can reference the same Common settings asset. Changes to its references and layout settings reach runtime profiles through explicit rebuilds. Referenced theme assets remain shared assets; their update timing is described below.

Common settings supply theme, input, accessibility, placement, transitions and optional composition. The generic recipe classes keep their serialized identity while naming four style families:

| Style | Recipe | Presentation |
| --- | --- | --- |
| WheelContextMenu | `SingleRingMenuStyleRecipe` | One native ring with configurable items and center. |
| PieContextMenu | `ReplacementMenuStyleRecipe` | Replacement navigation and individually composed buttons. |
| ListContextMenu | `ListMenuStyleRecipe` | List rows and inline foldouts. |
| NestedWheelContextMenu | `ConcentricMenuStyleRecipe` | Parent ring and partial child arc. |

A custom recipe retains a project-defined presenter. Custom cards are a child-presentation example. Icon, label and richer center variations remain configurations of the wheel; they do not add another default style.

Per-style presenter, item, center, secondary and fallback skin references override Common. An empty override inherits Common, then retains an existing output reference. To return an output to native defaults, clear the shared/style override and clear the output reference. Existing custom prefab contents and private child transforms are never regenerated.

Enable **Override show center** to give a radial or custom style its own center visibility. Replacement interaction recipes default to None; uncheck the override to inherit Common. The default list omits center information.

Enable **Override input bindings** for a style that needs distinct hold, ring-switch, secondary or other action references. The complete binding set is copied; referenced Input System actions retain their identity. Dead zone and hysteresis remain shared Common settings.

New wizard Common settings use the project's native uGUI navigation module. Configure existing semantic action references and turn off native uGUI navigation when the project needs analog sector selection, hold/release or bound secondary operations. The tool does not create another input asset or rebinding system.

## MTK theme overrides

The primary theme controls are inline in Common and in each expanded style. They edit a sparse runtime `MenuSkin` asset containing references to existing MTK `Swatch`, `Typeset` and `Spacing` assets. **Create overrides** creates a new runtime asset under a normal `Assets/` folder; Editor folders, occupied paths, directories and orphan metadata are refused. Authoring Common/configuration assets can remain under `Assets/Editor`.

The inline Swatch fields use MTK's existing color-chip/asset drawer. Typography fields reference MTK Typesets, including their font, sizing, style and spacing settings. **Open MTK Theme tools** opens the existing Theme Configurator for editing those assets. Each layer has one **Spacing (all states)** token for padding and gap; spacing is independent of interaction state.

Colors and typography have these foldouts:

- **Default (Any state)** provides values for every state without its own entry in that layer.
- **Enabled (Normal)**, **Hovered**, **Selected**, **Disabled** and **Checked** provide state-specific Swatches and label/description Typesets.

Unset properties inherit in this order, resolving each property independently:

1. Per-style exact state, then per-style Default.
2. Shared exact state, then shared Default.
3. Fallback skin exact state, then fallback Default.
4. Native widget defaults or the preset's restored authored baseline.

A per-style Default value therefore wins over a shared state-specific value. Swatches and Typesets resolve their semantic IDs through the supplied `IThemeService`, unless that layer disables **Resolve active MTK theme**. An explicit legacy local-color override wins over its Swatch, including transparent colors. The UI labels active local overrides; assigning a different Swatch clears that local-color mode.

Interaction state and visual role are separate. Default item and native wedge fills request `Normal` for normal/checked state, `Hovered` for pointer hover, `Selected` for selection and `Disabled` for unavailable actions. Center and auxiliary/footer fills keep the `Center` and `Auxiliary` roles. Labels, descriptions and icons use their named roles with the active state; check markers use `Checked`. Disabled takes precedence over hover, selection and checked state. Custom presenters can consume any role.

`Backdrop` is the separate outside-overlay role; its native default is black at 35% opacity. It uses Default/Any tokens, follows theme refreshes, and remains transparent when `MenuProfile.Backdrop` is false. `Background` remains available for custom panel/background composition; an opaque legacy Background token does not become a full-screen overlay.

Common's `ThemeOverrides` reference bakes into `MenuProfile.SharedThemeOverrides`; a recipe's `ThemeOverrides` bakes into `MenuProfile.ThemeOverrides`. These remain separate from `MenuProfile.Skin`, the optional fallback. Clearing either authoring override reference and rebuilding clears that layer on the selected output, allowing inheritance from the next layer. Existing fallback skin retention continues to follow the composition rules above.

Changing authoring references requires **Preview/Rebuild**. Editing a `MenuSkin` after preview invalidates the plan, so refresh the preview before rebuilding. An `IThemeService` theme update refreshes open menus. Ordinary MenuSkin/Swatch/Typeset edits do not necessarily emit that event; their values take effect on the next bind, refresh or theme update. The tool does not promise an immediate rerender from a raw asset edit.

Native widgets consume typography through MTK `TypesetPreset` and spacing through explicitly wired `SpacingPreset` targets. Custom widgets can provide their own targets or set `UseSkin = false` to retain their visual ownership. The framework does not add layout controllers to arbitrary custom prefab children. The geometry sketch resolves layered colors but remains a layout illustration, not a font/prefab preview.

## Existing profiles and identity

Assign an existing `MenuProfile` to a recipe's **Output** to rebuild it in place. Its GUID and existing host/action references remain valid. **Asset Name** names a new output; changing that name does not rename an assigned profile.

Direct custom presenters and valid typed Addressables presenter references are retained unless a direct presenter override is supplied. If a built-in recipe has no presenter, the tool creates a minimal native List or Radial presenter prefab. Null widget references continue using runtime widget defaults. A custom recipe requires an explicit or existing presenter.

Preview and rebuild reject duplicate IDs, duplicate output targets, invalid profiles, self-referencing fallbacks and unexpected existing assets at new output paths. Rebuild also refuses changed source or target state after preview. Refresh the preview after intentional external edits.

The tool saves only its selected profiles and authoring sources, plus newly created native presenter outputs. It does not rebuild scenes, overwrite existing prefab contents, call global `SaveAssets`, or save unrelated dirty assets/scenes. Removing a recipe removes the authoring entry; it does not delete its runtime assets.

## Editor API

- `MenuStyleAuthoring.Preview(config, optionalStyleId)` returns diagnostics and a proposed asset plan without writing.
- `MenuStyleAuthoring.Rebuild(plan)` revalidates the plan and returns updated runtime profiles.
- `MenuStyleWizard.CreateDefaults(newAssetPath, optionalSharedCommon)` creates source assets only. Runtime outputs still require explicit preview/rebuild.
- `MenuStyleAuthoring.CreateThemeOverrideAsset(newAssetPath)` creates a new runtime MenuSkin override asset with collision and destination checks.

Authoring types live in Editor-only `MToolKit.Editor.ContextMenus`. Outputs use the standard runtime `MenuProfile`, presenter and widget contracts; game code does not depend on the authoring assembly.

## Authoring validation

Run `MToolKit.ContextMenus.Authoring.Tests.Editor` in Unity Test Runner, or select the `MToolKit.Editor.ContextMenus.Tests` namespace. Its fixtures cover shared settings, layer baking/clearing, in-place GUID retention, custom composition, Addressables-only presenters, stale plans, runtime-asset destination checks, foreign metadata/directory refusal, center visibility, selected-style rebuilds and caller scene/dirty-asset preservation.

Validate rendering, input, transitions and viewport fitting through the supplied gallery PlayMode tests and the consuming project's menu host. Authoring unit tests do not establish those runtime results.
