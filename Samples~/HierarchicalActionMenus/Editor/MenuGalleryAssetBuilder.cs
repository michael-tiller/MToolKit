using System;
using System.Collections.Generic;
using System.IO;
using MToolKit.ContextMenus.Authoring;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.UI.Scaling;
using MToolKit.Theme;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Swatch;
using MToolKit.Theme.Typeset;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.ContextMenus.Samples.Editor
{
  /// <summary>Creates editable Unity assets; generated assets contain no editor-only factory dependency.</summary>
  public static class MenuGalleryAssetBuilder
  {
    public static string DefaultOutputRoot
      => "Assets/Samples/MToolKit/" + (PackageInfo.FindForAssembly(typeof(MenuSession).Assembly)?.version ?? "Local")
        + "/HierarchicalActionMenus";

    [MenuItem("Tools/MToolKit/Samples/Create Action Menu Gallery")]
    public static void CreateGallery() => Build(DefaultOutputRoot);

    /// <summary>Creates a gallery in an unused destination without replacing authored assets or saving caller work.</summary>
    public static string Build(string outputRoot)
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode)
        throw new InvalidOperationException("Create the menu gallery outside Play Mode.");
      outputRoot = outputRoot?.Replace('\\', '/').TrimEnd('/');
      if (string.IsNullOrWhiteSpace(outputRoot) || !outputRoot.StartsWith("Assets/", StringComparison.Ordinal) ||
          outputRoot.Contains("..")) throw new ArgumentException("The gallery output must be a child of Assets.", nameof(outputRoot));
      string scenePath = outputRoot + "/ActionMenuGallery.unity";
      for (int i = 0; i < SceneManager.sceneCount; i++)
      {
        if (SceneManager.GetSceneAt(i).path == scenePath)
          throw new InvalidOperationException("Close the existing ActionMenuGallery scene before regenerating its assets.");
        if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
          throw new InvalidOperationException("Save open untitled scenes before generating the action-menu gallery; the builder does not save caller scenes.");
      }
      EnsureUnusedDestination(outputRoot);
      EnsureNoUnrelatedDirtyAssets(outputRoot);
      var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
      if (font == null) throw new InvalidOperationException("Import TMP Essentials (LiberationSans SDF) before creating this sample.");
      Folder(outputRoot);
      string stagingPath = outputRoot + "/__MenuGalleryAuthoring.unity";
      var previous = SceneManager.GetActiveScene();
      var staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
      try
      {
        SceneManager.SetActiveScene(staging);
        if (!EditorSceneManager.SaveScene(staging, stagingPath))
          throw new InvalidOperationException("Unity could not save the private menu authoring scene.");
        return BuildOwned(outputRoot, scenePath, font);
      }
      finally
      {
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        if (staging.IsValid() && staging.isLoaded) EditorSceneManager.CloseScene(staging, true);
        AssetDatabase.DeleteAsset(stagingPath);
      }
    }

    private static readonly string[] GeneratedFolders = { "Actions", "Input", "Prefabs", "Profiles", "Theme", "Icons" };
    private static void EnsureUnusedDestination(string root)
    {
      foreach (string folder in GeneratedFolders)
        if (Directory.Exists(root + "/" + folder) || File.Exists(root + "/" + folder))
          throw new InvalidOperationException("Generated gallery data already exists at '" + root + "/" + folder
            + "'. Edit the existing assets or choose a new destination; generation never overwrites them.");
      foreach (string file in new[] { "ActionMenuGallery.unity", "__MenuGalleryAuthoring.unity" })
        if (File.Exists(root + "/" + file))
          throw new InvalidOperationException("A gallery scene already exists at '" + root + "/" + file + "'. Choose a new destination.");
    }

    private static void EnsureNoUnrelatedDirtyAssets(string root)
    {
      // Unity SaveScene flushes dirty persistent assets even when they are not referenced
      // by the scene being saved. Refuse before writing instead of saving caller edits.
      var paths = new HashSet<string>(StringComparer.Ordinal);
      foreach (var asset in Resources.FindObjectsOfTypeAll<Object>())
      {
        if (asset == null || !EditorUtility.IsPersistent(asset) || !EditorUtility.IsDirty(asset)) continue;
        // Imported shader/font/texture objects carry dirty internal caches. Scene saving
        // cannot serialize those foreign objects back to their source file. Keep actual
        // native asset edits and importer settings protected without blocking on caches.
        if (!AssetDatabase.IsNativeAsset(asset) && !(asset is AssetImporter)) continue;
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path) || !(path.StartsWith("Assets/", StringComparison.Ordinal)
            || path.StartsWith("Packages/", StringComparison.Ordinal))) continue;
        bool owned = false;
        foreach (string folder in GeneratedFolders)
          if (path.StartsWith(root + "/" + folder + "/", StringComparison.Ordinal)) { owned = true; break; }
        if (!owned) paths.Add(path);
      }
      if (paths.Count == 0) return;
      var sorted = new List<string>(paths); sorted.Sort(StringComparer.Ordinal);
      throw new InvalidOperationException("Save or discard unrelated asset edits before generating the gallery. Unity scene saving"
        + " would otherwise write these dirty assets: " + string.Join(", ", sorted));
    }

    private static void SaveOwnedAssets(string root)
    {
      foreach (string folder in GeneratedFolders)
        foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root + "/" + folder }))
        {
          var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
          if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
        }
    }

    private static string BuildOwned(string outputRoot, string scenePath, TMP_FontAsset font)
    {
      Folder(outputRoot);
      foreach (string folder in GeneratedFolders) Folder(outputRoot + "/" + folder);
      var input = CreateInput(outputRoot);
      var refs = CreateReferences(outputRoot, input);
      var icons = CreateIcons(outputRoot);
      var theme = CreateThemes(outputRoot, font);
      var secondary = SavePrefab(outputRoot, "SecondaryAction", () => ActionWidget("Secondary action", font, theme.Label, true, true, false));
      var row = SavePrefab(outputRoot, "ListRow", () => ActionWidget("List row", font, theme.Label, true, true, false));
      var radial = SavePrefab(outputRoot, "RadialLabelIcon", () => ActionWidget("Radial label and icon", font, theme.Label, true, true, true));
      var iconOnly = SavePrefab(outputRoot, "IconOnly", () => ActionWidget("Icon only", font, theme.Label, false, true, true));
      var sims = SavePrefab(outputRoot, "InteractionButton", () => ActionWidget("Interaction button", font, theme.Label, true, true, false));
      var center = SavePrefab(outputRoot, "CenterInformation", () => Center(font, theme.Label, theme.Description, secondary));
      var customCenter = SavePrefab(outputRoot, "CustomCenter", () => CustomCenter(font));
      var listPresenter = Presenter<ListMenuPresenter>(outputRoot, "ListPresenter");
      var radialPresenter = Presenter<RadialMenuPresenter>(outputRoot, "RadialPresenter");
      var customPresenter = Presenter<GalleryCustomPresenter>(outputRoot, "CustomCardPresenter");
      var bindings = Bindings(refs);
      var list = Profile(outputRoot, "TraditionalList", listPresenter, row, null, theme.Skin, bindings);
      list.Navigation = MenuNavigationPolicy.Inline; list.ShowCenter = false; list.MaximumVisibleItems = 10;
      list.ListWidth = 380; list.RowHeight = 48; list.Anchor = MenuAnchorPolicy.Centered;
      var rust = Profile(outputRoot, "RustBuildWheel", radialPresenter, radial, center, theme.Skin, bindings);
      rust.CenterPrefab = customCenter; rust.SecondaryPrefab = secondary;
      var gta = Profile(outputRoot, "GtaEquipmentWheel", radialPresenter, iconOnly, center, theme.Skin, bindings);
      gta.SecondaryPrefab = secondary; gta.AngularGap = 5;
      var interactions = Profile(outputRoot, "SimsInteractions", radialPresenter, sims, null, theme.Skin, bindings);
      interactions.DrawSegments = false; interactions.ShowCenter = false; interactions.WidgetSize = new Vector2(144, 56);
      interactions.InnerRadius = 90; interactions.RingWidth = 150;
      var concentric = Profile(outputRoot, "ExcalidrawConcentric", radialPresenter, radial, center, theme.Skin, bindings);
      concentric.Navigation = MenuNavigationPolicy.Concentric; concentric.RingWidth = 60; concentric.RingGap = 10;
      concentric.ChildRingWidth = 55; concentric.ChildArcDegrees = 120; concentric.SecondaryPrefab = secondary;
      var custom = Profile(outputRoot, "ReplacementCards", customPresenter, sims, null, theme.LocalSkin, bindings);
      custom.Navigation = MenuNavigationPolicy.Replacement; custom.ListWidth = 380; custom.RowHeight = 80;
      custom.MaximumVisibleItems = 8; custom.ShowCenter = false;
      foreach (var profile in new[] { rust, gta, interactions, concentric, custom }) profile.FallbackProfile = list;
      foreach (var profile in new[] { list, rust, gta, interactions, concentric, custom })
      {
        EditorUtility.SetDirty(profile);
        if (!profile.Validate(out string diagnostic)) throw new InvalidOperationException(profile.name + ": " + diagnostic);
      }
      var actions = CreateActions(outputRoot, icons, refs, custom);
      var config = Asset<MenuHostConfig>(outputRoot + "/Profiles/GalleryHost.asset");
      config.DefaultProfile = concentric;
      config.Profiles = new[]
      {
        new NamedMenuProfile { Name = MenuGalleryController.Rust, Profile = rust },
        new NamedMenuProfile { Name = MenuGalleryController.Gta, Profile = gta },
        new NamedMenuProfile { Name = MenuGalleryController.Sims, Profile = interactions },
        new NamedMenuProfile { Name = MenuGalleryController.Concentric, Profile = concentric },
        new NamedMenuProfile { Name = MenuGalleryController.List, Profile = list }
      };
      EditorUtility.SetDirty(config);
      SaveOwnedAssets(outputRoot);
      CreateScene(scenePath, font, config, actions, input, theme);
      SaveOwnedAssets(outputRoot);
      Debug.Log("Created editable action-menu gallery at " + scenePath + ". Open this scene and enter Play Mode to validate it.");
      return scenePath;
    }

    private static MenuProfile Profile(string root, string name, GameObject presenter, GameObject item,
      GameObject center, MenuSkin skin, MenuInputBindings bindings)
    {
      var profile = Asset<MenuProfile>(root + "/Profiles/" + name + ".asset");
      profile.PresenterPrefab = presenter; profile.PresenterReference = null;
      profile.ItemPrefab = item; profile.CenterPrefab = center; profile.SecondaryPrefab = null;
      profile.Skin = skin; profile.InputBindings = bindings; profile.Navigation = MenuNavigationPolicy.Replacement;
      profile.InnerRadius = 165; profile.RingWidth = 80; profile.RingGap = 12; profile.ChildRingWidth = 70;
      profile.StartAngle = 90; profile.Clockwise = true; profile.AngularGap = 3; profile.ChildArcDegrees = 120;
      profile.MaximumVisibleItems = 8; profile.ListWidth = 360; profile.RowHeight = 48;
      profile.WidgetSize = new Vector2(96, 64); profile.MinimumTargetSize = 40;
      profile.ShowCenter = center != null; profile.Backdrop = true; profile.DrawSegments = true;
      profile.ReducedMotion = true; profile.AnalogDeadZone = .25f; profile.AnalogHysteresis = 6;
      profile.Anchor = MenuAnchorPolicy.Centered;
      return profile;
    }

    private static InputActionAsset CreateInput(string root)
    {
      string path = root + "/Input/ActionMenuGallery.inputactions";
      var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
      if (existing != null)
      {
        foreach (string required in new[] { "UI/Point", "UI/Click", "UI/Navigate", "UI/AnyKey", "Shared/Pause",
          "Shared/OpenContext", "Shared/HoldWheel", "Shared/Theme", "Shared/ScaleUp", "Shared/ScaleDown", "Shared/NextStyle",
          "Player/Fire", "Player/Move", "Menu/Navigate", "Menu/Submit", "Menu/Back", "Menu/Secondary" })
          if (existing.FindAction(required) == null)
            throw new InvalidOperationException("Existing gallery input asset is missing " + required + ". Choose another output folder to create a fresh sample.");
        return existing;
      }
      var asset = ScriptableObject.CreateInstance<InputActionAsset>();
      try
      {
        asset.name = "ActionMenuGallery";
        var player = asset.AddActionMap("Player");
        var movement = player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        movement.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
          .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        movement.AddBinding("<Gamepad>/leftStick");
        Button(player, "Fire", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
        var ui = asset.AddActionMap("UI");
        ui.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position", expectedControlLayout: "Vector2");
        ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
        ui.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton", expectedControlLayout: "Button");
        ui.AddAction("Scroll", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
        var uiNavigate = ui.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
        Directions(uiNavigate, false);
        Button(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
        Button(ui, "Cancel"); // Semantic Menu/Back owns Back while the menu is open.
        Button(ui, "AnyKey", "<Keyboard>/anyKey");
        var shared = asset.AddActionMap("Shared");
        Button(shared, "Pause");
        Button(shared, "OpenContext", "<Mouse>/rightButton", "<Keyboard>/f1", "<Gamepad>/select");
        Button(shared, "HoldWheel", "<Keyboard>/tab", "<Gamepad>/leftShoulder");
        Button(shared, "Theme", "<Keyboard>/f8");
        Button(shared, "ScaleUp", "<Keyboard>/equals"); Button(shared, "ScaleDown", "<Keyboard>/minus");
        Button(shared, "NextStyle", "<Keyboard>/f2", "<Gamepad>/rightStickPress");
        var menu = asset.AddActionMap("Menu");
        Directions(menu.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2"), true);
        Button(menu, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
        Button(menu, "Back", "<Keyboard>/escape", "<Gamepad>/buttonEast");
        Button(menu, "Cancel", "<Keyboard>/backspace", "<Gamepad>/start");
        Button(menu, "PageForward", "<Keyboard>/pageDown", "<Gamepad>/rightTrigger");
        Button(menu, "PageBackward", "<Keyboard>/pageUp", "<Gamepad>/leftTrigger");
        Button(menu, "SwitchRing", "<Keyboard>/r", "<Gamepad>/rightShoulder");
        Button(menu, "Secondary", "<Keyboard>/q", "<Gamepad>/buttonWest");
        Button(menu, "Inspect", "<Keyboard>/e", "<Gamepad>/buttonNorth");
        File.WriteAllText(path, asset.ToJson());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path)
          ?? throw new InvalidOperationException("Unity could not import the gallery input asset.");
      }
      finally { Object.DestroyImmediate(asset); }
    }

    private static void Directions(InputAction action, bool wasd)
    {
      action.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
        .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
      if (wasd) action.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
      action.AddBinding("<Gamepad>/dpad"); action.AddBinding("<Gamepad>/leftStick");
    }
    private static void Button(InputActionMap map, string name, params string[] paths)
    {
      var action = map.AddAction(name, InputActionType.Button, expectedControlLayout: "Button");
      foreach (string path in paths) action.AddBinding(path);
    }
    private static Dictionary<string, InputActionReference> CreateReferences(string root, InputActionAsset asset)
    {
      var result = new Dictionary<string, InputActionReference>(StringComparer.Ordinal);
      foreach (var map in asset.actionMaps) foreach (var action in map.actions)
      {
        string key = map.name + "/" + action.name;
        string path = root + "/Input/" + map.name + "_" + action.name + ".asset";
        var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
        if (reference == null) { reference = InputActionReference.Create(action); AssetDatabase.CreateAsset(reference, path); }
        else reference.Set(action);
        EditorUtility.SetDirty(reference); result.Add(key, reference);
      }
      return result;
    }
    private static MenuInputBindings Bindings(IReadOnlyDictionary<string, InputActionReference> refs)
    {
      var bindings = new MenuInputBindings();
      bindings.Configure(navigate: refs["Menu/Navigate"], point: refs["UI/Point"], click: refs["UI/Click"],
        submit: refs["Menu/Submit"], back: refs["Menu/Back"], cancel: refs["Menu/Cancel"],
        pageForward: refs["Menu/PageForward"], pageBackward: refs["Menu/PageBackward"], switchRing: refs["Menu/SwitchRing"],
        hold: refs["Shared/HoldWheel"], secondary: new[] { refs["Menu/Secondary"], refs["Menu/Inspect"] },
        useNativeUguiNavigation: false, routePointerClick: true);
      return bindings;
    }

    private static MenuActionSetSO CreateActions(string root, Sprite[] icons,
      IReadOnlyDictionary<string, InputActionReference> refs, MenuProfile replacement)
    {
      T Definition<T>(string id, string label, int icon = 0) where T : MenuActionDefinitionSO
      {
        var item = Asset<T>(root + "/Actions/" + id + ".asset");
        item.Id = "gallery." + id; item.Label = label; item.LabelKey = null;
        item.DescriptionKey = null; item.Description = label + ": the same action in every presentation.";
        item.Icon = icons[icon % icons.Length]; item.BindingHint = refs["Menu/Submit"];
        item.SecondaryActions = Array.Empty<MenuActionDefinitionSO>(); EditorUtility.SetDirty(item); return item;
      }
      GalleryCommandDefinitionSO Command(string id, string label, string cost = null, MenuActionOutcome outcome = MenuActionOutcome.Close)
      {
        var action = Definition<GalleryCommandDefinitionSO>(id, label);
        action.CostText = cost; action.Outcome = outcome; action.UnavailableReason = null; return action;
      }
      MenuSubmenuDefinitionSO Submenu(string id, string label, params MenuActionDefinitionSO[] children)
      {
        var submenu = Definition<MenuSubmenuDefinitionSO>(id, label, 1);
        submenu.Children = children; submenu.Provider = null; submenu.Primary = null; submenu.ProfileOverride = null; return submenu;
      }
      var variant = Definition<GalleryVariantDefinitionSO>("cycle-variant", "Cycle variant", 2);
      variant.BindingHint = refs["Menu/Secondary"];
      var inspect = Command("inspect", "Inspect selected item", outcome: MenuActionOutcome.Refresh);
      inspect.BindingHint = refs["Menu/Inspect"];
      var revolver = Command("equip-revolver", "Revolver", "Equipment: sidearm");
      revolver.SecondaryActions = new MenuActionDefinitionSO[] { variant, inspect };
      revolver.Description = "Equip this item. Cycle its variant independently without closing the menu.";
      var bow = Command("equip-bow", "Bow", "Equipment: ranged"); bow.SecondaryActions = new MenuActionDefinitionSO[] { variant, inspect };
      var equipment = Submenu("equipment", "Equipment", revolver, bow, Command("equip-tools", "Tools"));
      var reinforced = Command("reinforced-door", "Reinforced door", "Cost: 4 metal + 2 timber");
      var metal = Submenu("metal-doors", "Metal doors", reinforced, Command("sliding-door", "Sliding door", "Cost: 6 metal"));
      var doors = Submenu("doors", "Doors", Command("wood-door", "Wooden door", "Cost: 3 timber"), metal);
      var build = Submenu("build", "Build", doors, Command("wall", "Wall", "Cost: 2 stone"),
        Command("floor", "Floor", "Cost: 2 timber"), Command("lamp", "Lamp", "Cost: 1 metal"));
      var social = Submenu("interactions", "Interactions", Command("greet", "Greet"), Command("chat", "Chat"),
        Submenu("conversation", "Conversation", Command("ask", "Ask a question"), Command("share", "Share an idea")));
      social.ProfileOverride = replacement;
      social.Description = "Opening this branch replaces the complete radial presenter with a custom card presenter.";
      var toggle = Definition<GalleryToggleDefinitionSO>("snap", "Snap to grid", 2);
      var careful = Definition<GalleryChoiceDefinitionSO>("careful", "Careful mode", 2); careful.Group = "mode"; careful.Value = "careful";
      var quick = Definition<GalleryChoiceDefinitionSO>("quick", "Quick mode", 2); quick.Group = "mode"; quick.Value = "quick";
      var header = Definition<MenuHeaderDefinitionSO>("preferences-header", "Settings and live state"); header.BindingHint = null; header.Icon = null;
      var provider = Asset<GalleryChildrenProviderSO>(root + "/Actions/StockProvider.asset"); provider.Icon = icons[2]; provider.IdPrefix = "gallery.stock";
      EditorUtility.SetDirty(provider);
      var inventory = Submenu("inventory", "Available inventory"); inventory.Provider = provider;
      var settings = Submenu("settings", "Preferences", header, toggle, careful, quick, inventory);
      var disabled = Command("unavailable", "Unavailable recipe", "Cost: unavailable"); disabled.UnavailableReason = "Requires an unavailable ingredient";
      var set = Asset<MenuActionSetSO>(root + "/Actions/GalleryActions.asset");
      set.Actions = new MenuActionDefinitionSO[] { build, equipment, social, settings, disabled };
      EditorUtility.SetDirty(set);
      return set;
    }

    private sealed class GalleryTheme
    {
      public MenuSkin Skin, LocalSkin;
      public Typeset Label, Description;
      public ThemeAsset Dark, Light;
      public ThemeRegistry Registry;
    }
    private static GalleryTheme CreateThemes(string root, TMP_FontAsset font)
    {
      string themeRoot = root + "/Theme";
      var label = Asset<Typeset>(themeRoot + "/Label.asset");
      var description = Asset<Typeset>(themeRoot + "/Description.asset");
      foreach (var typeset in new[] { label, description })
      {
        Set(typeset, "semId", p => p.stringValue = typeset == label ? "menu.label" : "menu.description");
        Set(typeset, Backing("Font"), p => p.objectReferenceValue = font);
        Set(typeset, Backing("FontSize"), p => p.floatValue = typeset == label ? 18 : 16);
        Set(typeset, Backing("FontSizeMin"), p => p.floatValue = 16);
        Set(typeset, Backing("AccessibleFontSizeMin"), p => p.floatValue = 16);
        Set(typeset, Backing("AutoSizeFont"), p => p.boolValue = true);
      }
      var typography = Asset<TypesetStyleRegistry>(themeRoot + "/Typesets.asset");
      SetArray(typography, Backing("Typesets"), new Object[] { label, description });
      var spacing = Asset<Spacing>(themeRoot + "/Spacing.asset");
      Set(spacing, "semId", p => p.stringValue = "menu.spacing");
      Set(spacing, Backing("Gap"), p => p.floatValue = 6);
      var spacings = Asset<SpacingRegistry>(themeRoot + "/Spacings.asset");
      SetArray(spacings, Backing("Spacings"), new Object[] { spacing });
      var skin = Asset<MenuSkin>(themeRoot + "/SemanticSkin.asset");
      skin.Colors = Array.Empty<MenuSkinColor>(); skin.OverrideTheme = false;
      skin.LabelTypeset = label; skin.DescriptionTypeset = description; skin.Spacing = spacing;
      ThemeAsset MakeTheme(string name, bool light)
      {
        var swatches = new List<Object>();
        foreach (MenuVisualRole role in Enum.GetValues(typeof(MenuVisualRole)))
        {
          var swatch = Asset<Swatch>(themeRoot + "/" + name + role + ".asset");
          Set(swatch, "semId", p => p.stringValue = "menu." + role.ToString().ToLowerInvariant());
          Set(swatch, Backing("Type"), p => p.enumValueIndex = (int)ESwatchType.Color);
          Color color = MenuSkin.DefaultColor(role);
          if (light)
          {
            if (role == MenuVisualRole.Label || role == MenuVisualRole.Icon) color = new Color(.08f, .12f, .18f);
            else if (role == MenuVisualRole.Description) color = new Color(.18f, .22f, .28f);
            else if (role == MenuVisualRole.Normal || role == MenuVisualRole.Center) color = new Color(.9f, .93f, .97f, .98f);
            else if (role == MenuVisualRole.Background) color = new Color(.65f, .7f, .8f, .9f);
            else if (role == MenuVisualRole.Disabled) color = new Color(.7f, .72f, .75f, .75f);
            else if (role == MenuVisualRole.Selected) color = new Color(.4f, .72f, .95f);
          }
          Set(swatch, Backing("Color"), p => p.colorValue = color);
          swatches.Add(swatch); if (!light) skin.SetSwatch(role, swatch);
        }
        var registry = Asset<SwatchRegistry>(themeRoot + "/" + name + "Swatches.asset");
        SetArray(registry, Backing("Swatches"), swatches.ToArray());
        var theme = Asset<ThemeAsset>(themeRoot + "/" + name + ".asset");
        Set(theme, "semId", p => p.stringValue = "gallery." + name.ToLowerInvariant());
        Set(theme, Backing("SwatchRegistry"), p => p.objectReferenceValue = registry);
        Set(theme, Backing("TypesetStyleRegistry"), p => p.objectReferenceValue = typography);
        Set(theme, Backing("SpacingRegistry"), p => p.objectReferenceValue = spacings);
        return theme;
      }
      var dark = MakeTheme("Dark", false); var lightTheme = MakeTheme("Light", true);
      var registryThemes = Asset<ThemeRegistry>(themeRoot + "/Themes.asset");
      SetArray(registryThemes, Backing("Themes"), new Object[] { dark, lightTheme });
      var localSkin = Asset<MenuSkin>(themeRoot + "/LocalOverrideSkin.asset");
      localSkin.Colors = Array.Empty<MenuSkinColor>(); localSkin.LabelTypeset = label; localSkin.DescriptionTypeset = description;
      localSkin.Spacing = spacing; localSkin.OverrideTheme = false;
      foreach (var entry in skin.Colors) localSkin.SetSwatch(entry.Role, entry.Swatch);
      localSkin.SetColor(MenuVisualRole.Selected, new Color(.7f, .35f, .75f));
      EditorUtility.SetDirty(skin); EditorUtility.SetDirty(localSkin);
      return new GalleryTheme { Skin = skin, LocalSkin = localSkin, Label = label, Description = description,
        Dark = dark, Light = lightTheme, Registry = registryThemes };
    }

    private static Sprite[] CreateIcons(string root)
    {
      var result = new Sprite[3];
      for (int icon = 0; icon < result.Length; icon++)
      {
        string path = root + "/Icons/Symbol" + icon + ".png";
        if (!File.Exists(path))
        {
          var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
          try
          {
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
              float dx = x - 31.5f, dy = y - 31.5f, radius = Mathf.Sqrt(dx * dx + dy * dy);
              bool solid = icon == 0 ? Mathf.Abs(dx) < 5 || Mathf.Abs(dy) < 5
                : icon == 1 ? Mathf.Abs(dx) + Mathf.Abs(dy) < 25 && Mathf.Abs(dx) + Mathf.Abs(dy) > 16
                : radius < 24 && radius > 16;
              texture.SetPixel(x, y, solid ? Color.white : Color.clear);
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
          }
          finally { Object.DestroyImmediate(texture); }
          AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
          var importer = (TextureImporter)AssetImporter.GetAtPath(path);
          importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 64;
          importer.spriteImportMode = SpriteImportMode.Single;
          importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
        }
        result[icon] = AssetDatabase.LoadAssetAtPath<Sprite>(path)
          ?? throw new InvalidOperationException("The generated icon is not a sprite: " + path);
      }
      return result;
    }

    private static GameObject ActionWidget(string name, TMP_FontAsset font, Typeset labelTypeset,
      bool withLabel, bool withIcon, bool radial)
    {
      var root = Rect(name, null); root.sizeDelta = new Vector2(radial ? 96 : 240, radial ? 64 : 48);
      var background = root.gameObject.AddComponent<Image>(); background.color = new Color(.12f, .14f, .18f, radial ? 0 : .95f);
      background.raycastTarget = !radial; // Native wedges own hit testing for icon/label sectors.
      var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = background; button.transition = Selectable.Transition.None;
      var widget = root.gameObject.AddComponent<MenuActionWidget>();
      widget.Button = button; widget.Background = radial ? null : background;
      if (withIcon)
      {
        var icon = Rect("Icon", root).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.preserveAspect = true;
        Place(icon.rectTransform, radial ? new Vector2(0, withLabel ? 10 : 0) : new Vector2(22, 0), new Vector2(28, 28));
        if (!radial) icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
        widget.Icon = icon;
      }
      if (withLabel)
      {
        var label = Text("Label", root, font, 18); widget.Label = label;
        if (radial) Place(label.rectTransform, new Vector2(0, -20), new Vector2(96, 28));
        else { Stretch(label.rectTransform, new Vector2(withIcon ? 44 : 8, 4), new Vector2(12, 4)); label.alignment = TextAlignmentOptions.MidlineLeft; }
        var preset = label.gameObject.AddComponent<TypesetPreset>();
        Set(preset, Backing("Text"), p => p.objectReferenceValue = label);
        preset.SetOverrideTheme(true); preset.SetStart(labelTypeset);
      }
      var selected = Rect("Selection marker", root).gameObject.AddComponent<Image>(); selected.raycastTarget = false;
      selected.rectTransform.anchorMin = new Vector2(0, 0); selected.rectTransform.anchorMax = new Vector2(0, 1);
      selected.rectTransform.pivot = new Vector2(0, .5f); selected.rectTransform.sizeDelta = new Vector2(4, 0); widget.Highlight = selected;
      var check = Rect("Checked marker", root).gameObject.AddComponent<Image>(); check.raycastTarget = false;
      check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(1, 1);
      check.rectTransform.anchoredPosition = new Vector2(-5, -5); check.rectTransform.sizeDelta = new Vector2(8, 8); widget.Check = check;
      root.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
      return root.gameObject;
    }
    private static GameObject Center(TMP_FontAsset font, Typeset labelTypeset, Typeset descriptionTypeset, GameObject secondary)
    {
      var root = Rect("Center information", null); root.sizeDelta = new Vector2(160, 250);
      var background = root.gameObject.AddComponent<Image>(); background.raycastTarget = false;
      var title = Text("Title", root, font, 19); Place(title.rectTransform, new Vector2(0, 84), new Vector2(152, 40));
      var description = Text("Description", root, font, 16); Place(description.rectTransform, new Vector2(0, 42), new Vector2(152, 44));
      var unavailable = Text("Availability", root, font, 16); Place(unavailable.rectTransform, new Vector2(0, 5), new Vector2(152, 24));
      var cost = Text("Cost", root, font, 16); Place(cost.rectTransform, new Vector2(0, -18), new Vector2(152, 20));
      var variant = Text("Variant", root, font, 16); Place(variant.rectTransform, new Vector2(0, -38), new Vector2(152, 20));
      var hint = Text("Binding", root, font, 16); Place(hint.rectTransform, new Vector2(0, -58), new Vector2(152, 20));
      foreach (var text in new[] { title, description })
      {
        var preset = text.gameObject.AddComponent<TypesetPreset>(); Set(preset, Backing("Text"), p => p.objectReferenceValue = text);
        preset.SetOverrideTheme(true); preset.SetStart(text == title ? labelTypeset : descriptionTypeset);
      }
      var container = Rect("Secondary actions", root); Place(container, new Vector2(0, -98), new Vector2(152, 84));
      var layout = container.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 4;
      layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
      var widget = root.gameObject.AddComponent<MenuCenterWidget>(); widget.Configure(title, description, disabledReason: unavailable,
        bindingHint: hint, secondaryContainer: container, secondaryPrefab: secondary);
      widget.Cost = cost; widget.Variant = variant; widget.Background = background;
      return root.gameObject;
    }
    private static GameObject CustomCenter(TMP_FontAsset font)
    {
      var root = Rect("Custom information", null); root.sizeDelta = new Vector2(176, 180);
      var widget = root.gameObject.AddComponent<GalleryCenterWidget>();
      widget.Title = Text("Custom title", root, font, 19); Place(widget.Title.rectTransform, new Vector2(0, 58), new Vector2(172, 40));
      widget.Details = Text("Custom details", root, font, 16); Place(widget.Details.rectTransform, Vector2.zero, new Vector2(172, 80));
      widget.Hint = Text("Custom hint", root, font, 16); Place(widget.Hint.rectTransform, new Vector2(0, -64), new Vector2(172, 30));
      return root.gameObject;
    }
    private static GameObject Presenter<T>(string root, string name) where T : Component
      => SavePrefab(root, name, () => { var go = Rect(name, null).gameObject; go.AddComponent<T>(); return go; });
    private static GameObject SavePrefab(string root, string name, Func<GameObject> create)
    {
      GameObject temporary = null;
      try
      {
        string path = root + "/Prefabs/" + name + ".prefab";
        if (File.Exists(path)) throw new InvalidOperationException("A prefab already exists at '" + path + "'. Generation never overwrites it.");
        temporary = create();
        var saved = PrefabUtility.SaveAsPrefabAsset(temporary, path);
        return saved;
      }
      finally { if (temporary != null) Object.DestroyImmediate(temporary); }
    }

    private static void CreateScene(string path, TMP_FontAsset font, MenuHostConfig config,
      MenuActionSetSO actions, InputActionAsset input, GalleryTheme theme)
    {
      var previous = SceneManager.GetActiveScene();
      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
      try
      {
        SceneManager.SetActiveScene(scene);
        var canvasObject = new GameObject("Action Menu Gallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
        var scaling = canvasObject.AddComponent<UserUiScaleCanvas>(); scaling.MinimumLogicalViewport = new Vector2(480, 320);
        var host = canvasObject.AddComponent<ContextMenuHost>();
        var controller = canvasObject.AddComponent<MenuGalleryController>();
        controller.Host = host; controller.HostConfig = config; controller.ActionSet = actions; controller.InputActions = input;
        controller.Canvas = canvas; controller.ScaleCanvas = scaling; controller.DarkTheme = theme.Dark; controller.LightTheme = theme.Light;
        var eventObject = new GameObject("Gallery Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        controller.InputModule = eventObject.GetComponent<InputSystemUIInputModule>();
        var themeObject = new GameObject("Gallery Theme"); themeObject.SetActive(false);
        var current = themeObject.AddComponent<MenuGalleryTheme>();
        Set(current, Backing("ThemeRegistry"), p => p.objectReferenceValue = theme.Registry);
        Set(current, Backing("DefaultTheme"), p => p.objectReferenceValue = theme.Dark);
        controller.StandaloneTheme = current; controller.OwnStandaloneTheme = true; themeObject.SetActive(true);
        var controls = Rect("Gallery controls", canvas.transform);
        controls.anchorMin = Vector2.zero; controls.anchorMax = Vector2.one; controls.offsetMin = controls.offsetMax = Vector2.zero;
        var overlay = controls.gameObject.AddComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 1000;
        controls.gameObject.AddComponent<GraphicRaycaster>();
        var title = Text("Title", controls, font, 24); title.text = "One action hierarchy - five presentations";
        AnchorTop(title.rectTransform, new Vector2(0, -24), new Vector2(900, 40));
        string[] styles = { MenuGalleryController.Concentric, MenuGalleryController.Rust, MenuGalleryController.Gta,
          MenuGalleryController.Sims, MenuGalleryController.List };
        for (int i = 0; i < styles.Length; i++)
        {
          var button = Control(controls, font, styles[i], new Vector2((i - 2) * 230, -66), new Vector2(220, 40));
          UnityEventTools.AddStringPersistentListener(button.onClick, controller.OpenStyle, styles[i]);
        }
        var callbacks = new UnityAction[] { controller.ToggleCommands, controller.ToggleSecondary, controller.InvalidateTarget,
          controller.RestoreTarget, controller.ToggleTheme, controller.DecreaseScale, controller.IncreaseScale, controller.OpenAtEdge };
        string[] names = { "Toggle commands", "Toggle secondary", "Invalidate target", "Restore target", "Theme (F8)", "UI scale -", "UI scale +", "Open at edge" };
        for (int i = 0; i < callbacks.Length; i++)
        {
          var button = Control(controls, font, names[i], new Vector2(120, -180 - i * 48), new Vector2(200, 40));
          var buttonRect = (RectTransform)button.transform;
          buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1);
          UnityEventTools.AddPersistentListener(button.onClick, callbacks[i]);
        }
        controller.Status = Text("Status", controls, font, 16);
        controller.Status.rectTransform.anchorMin = controller.Status.rectTransform.anchorMax = new Vector2(1, .5f);
        Place(controller.Status.rectTransform, new Vector2(-150, -100), new Vector2(260, 320), false);
        controller.Hints = Text("Input hints", controls, font, 16);
        controller.Hints.rectTransform.anchorMin = controller.Hints.rectTransform.anchorMax = new Vector2(1, .5f);
        Place(controller.Hints.rectTransform, new Vector2(-150, 220), new Vector2(260, 240), false);
        EnsureNoUnrelatedDirtyAssets(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Unity could not save the action-menu gallery scene.");
      }
      finally
      {
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
      }
    }
    private static Button Control(Transform parent, TMP_FontAsset font, string label, Vector2 position, Vector2 size)
    {
      var rect = Rect(label, parent); AnchorTop(rect, position, size);
      var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.16f, .21f, .3f, .98f);
      var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
      var text = Text("Label", rect, font, 16); text.text = label; Stretch(text.rectTransform, new Vector2(6, 2), new Vector2(6, 2));
      return button;
    }
    private static RectTransform Rect(string name, Transform parent)
    {
      var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size)
    {
      var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
      text.font = font; text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
      text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.Normal;
      text.overflowMode = TextOverflowModes.Ellipsis; return text;
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size, bool centered = true)
    {
      if (centered) rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
      rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void AnchorTop(RectTransform rect, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); Place(rect, position, size, false); }
    private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = minimum; rect.offsetMax = -maximum; }
    private static T Asset<T>(string path) where T : ScriptableObject
    {
      if (File.Exists(path)) throw new InvalidOperationException("Another asset type already occupies " + path);
      var asset = ScriptableObject.CreateInstance<T>(); asset.name = Path.GetFileNameWithoutExtension(path);
      AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static void Folder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
      if (string.IsNullOrEmpty(parent)) throw new ArgumentException("Invalid asset folder " + path);
      Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static string Backing(string property) => "<" + property + ">k__BackingField";
    private static void Set(Object target, string property, Action<SerializedProperty> assign)
    {
      var serialized = new SerializedObject(target);
      var field = serialized.FindProperty(property)
        ?? throw new InvalidOperationException(target.GetType().Name + " does not serialize '" + property + "'.");
      assign(field); serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
    private static void SetArray(Object target, string property, Object[] values)
      => Set(target, property, p => { p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; });
  }
}
