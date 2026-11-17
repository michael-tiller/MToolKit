# Hierarchical action menus

Import **Hierarchical Action Menus** from the MToolKit Package Manager samples, import TMP Essentials if needed, open the included `ActionMenuGallery.unity` scene and enter Play Mode. The saved gallery ships ten presenter/widget prefabs, five menu profiles, a host config, semantic themes, reusable actions and Input System assets. Four root styles share one action set; the fifth profile supplies the custom child presenter. You do not need to run the generator to open the imported sample.

To create another editable gallery, call `MenuGalleryAssetBuilder.Build("Assets/MyMenuGallery")` with a fresh destination. The **Tools > MToolKit > Samples > Create Action Menu Gallery** command targets `Assets/Samples/MToolKit/<version>/HierarchicalActionMenus`; it refuses that folder once it contains the shipped gallery. Edit the imported assets directly or use a new destination instead of generating over them.

Save any open untitled scene and save or discard existing asset edits before generating. Unity's scene-saving API also writes unrelated dirty persistent assets; the builder therefore refuses those edits before creating output, identifying their paths and preserving their in-memory and on-disk state. Generation creates its temporary objects in a private saved authoring scene, restores the caller's active scene, and explicitly saves only assets in its generated folders. A destination containing generated data is refused before mutation. Importing the Runtime, Editor, and Tests source folders into a fresh destination is supported.

The refusal protects serializable native asset and importer edits. Imported shader/font/texture objects' internal dirty caches are excluded; Unity does not serialize those foreign objects back to their source files.

The five buttons open the same authored hierarchy with these configurations:

| Profile | Composition |
| --- | --- |
| WheelContextMenu | Native ring with icons and artwork, a standard information center and independently enabled variant/inspect actions. Hold/release uses the configured input action. |
| PieContextMenu | Individual button prefabs with no wedge background and no center. Interactions opens the custom child presenter. |
| ListContextMenu | Accessible list with inline foldouts, checked-state indicators, bounded pagination and shared Back navigation. |
| NestedWheelContextMenu | Default; independent parent/child rings, a 120-degree child arc, center information and secondary controls. Build > Doors > Metal doors demonstrates depth beyond two rings. |

The wheel's center is a configuration choice. Assign `Prefabs/CustomCenter.prefab` to its profile's `CenterPrefab` for the fully custom `GalleryCenterWidget`, which displays description, resource cost, variant and binding hints. It uses the same wheel profile and action hierarchy.

**Interactions** carries an authored child-profile override in every profile. It replaces the complete presenter with `GalleryCustomPresenter`, whose own two-column card layout uses the shared session and execution path. The child skin supplies a local selected-color override while retaining semantic theme tokens. Returning to the parent restores its presentation. This branch demonstrates actual presenter replacement; the other branches retain each chosen style.

The state is a plain `MenuGalleryState` stored in `MenuContext.Target`. Reusable ScriptableObjects contain definitions only. Preferences includes a noninteractive header, toggle, exclusive choices, and an asynchronous inventory provider. Taking the last inventory item removes that choice on refresh. The disabled recipe displays a structured availability reason. Equipment has independently enabled variant actions, whose refresh updates the center without dismissing the menu.

The side controls change command/secondary availability, invalidate/restore the target, switch light/dark MTK themes, adjust MTK UI scale, and open at a screen edge. Invalidating the target retires the session. Restore the target before opening another menu. At constrained viewport sizes the radial profiles use the configured list fallback; text and targets are not silently shrunk. The scene uses a 1600 x 1000 CanvasScaler reference; that viewport accommodates the authored two-ring composition. `CanvasScaler` and `UserUiScaleCanvas` own resolution and preference scaling.

| Operation | Keyboard/mouse default | Controller default |
| --- | --- | --- |
| Open/close | Right click or F1 | Select |
| Hold wheel; release to confirm | Tab | Left shoulder |
| Navigate | Arrows/WASD or hover | Left stick or D-pad |
| Confirm / Back / Cancel | Enter / Escape / Backspace | South / East / Start |
| Ring focus | R | Right shoulder |
| Secondary variant / inspect | Q / E | West / North |
| Next / previous page | Page Down / Page Up | Right / left trigger |
| Next style | F2 | Right stick press |
| Theme / larger / smaller UI | F8 / = / - | Top controls |

All operations use authored Input System actions and runtime clones. The table describes defaults, not action-definition keys. The existing MTK prompt service resolves device-specific hints and falls back to binding display names when no glyph exists. The scene deliberately binds `Player/Fire` to the same control as menu confirm and `Player/Move` to the stick/WASD; its status counters help inspect gameplay suspension and restoration. Confirming while a menu owns input must not increment those gameplay counters. Physical controls held through dismissal remain quarantined until release.

The saved gallery assigns its own `Input/Glyphs/MenuPromptSettings.asset` with glyph-only output. Its editable keyboard recipe reuses MTK's keycap baker; mouse and neutral controller artwork uses MTK's native TMP atlas builder. Atlas names contain the glyph folder's persistent GUID so independent galleries can coexist. Rebake through the saved recipe and preserve those names. Injected application prompt services remain authoritative. Menu items have no confirmation hints. The selected action alone owns the center confirmation glyph; clearing selection unbinds it. Header, center, auxiliary and navigation hints follow runtime rebinding/device changes; action words remain text. Missing artwork uses the prompt service's readable fallback.

This sample's controller creates and disposes its MTK `InputService`, prompt service and menu host. Its scene-owned `MenuGalleryTheme` implements `IThemeService` without creating or replacing the application's `CurrentTheme` singleton, so additive scene loading does not take global theme ownership. In a game, register `ContextMenuHostService` through the framework plugin and use the application's existing input, prompt, theme and localization services. The sample does not replace an application's rebinding settings UI: all its bindings can be wired into MTK's existing rebinder by the consuming project.

`GalleryCommandDefinitionSO`, `GalleryToggleDefinitionSO`, `GalleryChoiceDefinitionSO` and `GalleryVariantDefinitionSO` demonstrate the protected `CreateAction` extension point. `GalleryChildrenProviderSO` demonstrates cancellation-aware runtime children. `GalleryCenterWidget` intentionally owns its own visuals without global theme opt-in. `GalleryCustomPresenter` demonstrates a complete behavioral/layout replacement. Every optional visual is assigned through public component contracts, rather than framework access to private child transforms.

Every root style renders the same media examples: **Build** has both a compact icon and a full-color image, **Equipment** has only an icon, **Interactions** has only an image, and **Preferences** has neither. The disabled recipe also has both. The same independent image appears in the selected equipment center, an auxiliary variant action, and dynamically generated inventory choices. Icons consume the semantic icon color; images retain their original colors. Both are optional, independent sprite references on action definitions and runtime descriptors. Default item and center prefabs expose separate targets; custom widgets can opt into either.

The icons and supply-crate artwork are generated original PNG sprites. No Asset Store content, UI Extensions or Procedural UI Tool is required. The sample requires the same Unity Input System, TMP and MTK dependencies as the context-menu module.

The installed package's `Documentation~/ContextMenus.md` and `Documentation~/ContextMenusMigration.md` describe authoring, customization and migration. Run `MToolKit.ContextMenus.Samples.Tests.Editor` in Unity's EditMode Test Runner and `MToolKit.ContextMenus.Samples.Tests.PlayMode` in PlayMode. Inspect the saved scene at different viewport sizes and UI scales, and exercise the mouse, keyboard and physical-controller controls above. Include a development-player check for the input and rendering paths you ship. Compiling the source or generating assets alone does not exercise those interactions.
