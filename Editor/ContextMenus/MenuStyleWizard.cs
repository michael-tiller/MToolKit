using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  public sealed class MenuStyleWizard : EditorWindow
  {
    public const string DefaultSourceFolder = "Assets/Editor/MToolKit/Menus";
    [SerializeField] private MenuStyleConfig configuration;
    [SerializeField] private MenuCommonSettings sharedCommon;
    private UnityEditor.Editor inspector;
    private Vector2 scroll;
    private string error;

    [MenuItem("Tools/MToolKit/Context Menus/Style Wizard")]
    public static void Open() => GetWindow<MenuStyleWizard>("Menu Style Wizard");

    private void OnEnable()
    {
      minSize = new Vector2(520, 600);
      if (Selection.activeObject is MenuStyleConfig config) configuration = config;
      if (Selection.activeObject is MenuCommonSettings common) sharedCommon = common;
    }
    private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    private void OnGUI()
    {
      EditorGUILayout.LabelField("Menu style authoring", EditorStyles.boldLabel);
      configuration = EditorGUILayout.ObjectField("Style configuration", configuration, typeof(MenuStyleConfig), false) as MenuStyleConfig;
      sharedCommon = EditorGUILayout.ObjectField("Common for new configuration", sharedCommon, typeof(MenuCommonSettings), false) as MenuCommonSettings;
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("New shared common settings")) CreateCommon();
      if (GUILayout.Button("New default style configuration")) CreateConfig();
      EditorGUILayout.EndHorizontal();
      if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
      if (configuration == null)
      {
        EditorGUILayout.HelpBox("Create a saved configuration, then share one Common settings asset across its styles or other configurations. Add your project input references or use its existing uGUI navigation module.", MessageType.Info);
        return;
      }
      scroll = EditorGUILayout.BeginScrollView(scroll);
      UnityEditor.Editor.CreateCachedEditor(configuration, typeof(MenuStyleConfigEditor), ref inspector);
      inspector.OnInspectorGUI();
      EditorGUILayout.EndScrollView();
    }

    /// <summary>Creates source assets only. Runtime outputs are created by explicit Preview/Rebuild.</summary>
    public static MenuStyleConfig CreateDefaults(string path, MenuCommonSettings common = null)
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before creating menu authoring assets.");
      if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
        || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains('\\')
        || Array.IndexOf(path.Split('/'), "..") >= 0 || Array.IndexOf(path.Split('/'), ".") >= 0)
        throw new InvalidOperationException("Choose a new .asset path beneath Assets.");
      if (Occupied(path))
        throw new InvalidOperationException("The configuration path already exists: " + path);
      string parent = path.Substring(0, path.LastIndexOf('/'));
      if (!AssetDatabase.IsValidFolder(parent)) throw new InvalidOperationException("Create the configuration's parent folder first.");
      string commonPath = path.Substring(0, path.Length - ".asset".Length) + "_Common.asset";
      if (common == null && Occupied(commonPath))
        throw new InvalidOperationException("The common settings path already exists. Select that asset explicitly before creating the configuration.");
      if (common != null && (!EditorUtility.IsPersistent(common) || !AssetDatabase.GetAssetPath(common).StartsWith("Assets/", StringComparison.Ordinal)))
        throw new InvalidOperationException("Select saved common settings under Assets.");
      if (common == null)
      {
        common = CreateInstance<MenuCommonSettings>();
        common.InputBindings.Configure(useNativeUguiNavigation: true);
        AssetDatabase.CreateAsset(common, commonPath);
      }
      var config = CreateInstance<MenuStyleConfig>();
      config.Common = common;
      config.OutputFolder = "Assets/MToolKit/Menus/" + Path.GetFileNameWithoutExtension(path);
      config.Styles = new MenuStyleRecipe[] {
        new SingleRingMenuStyleRecipe { AssetName = "WheelContextMenu", InnerRadius = 166, RingWidth = 100, AngularGap = 5 },
        new ReplacementMenuStyleRecipe { AssetName = "PieContextMenu", InnerRadius = 166, RingWidth = 120 },
        new ListMenuStyleRecipe { AssetName = "ListContextMenu", MaximumVisibleItems = 10 },
        new ConcentricMenuStyleRecipe { AssetName = "NestedWheelContextMenu", InnerRadius = 166, RingWidth = 60, ChildRingWidth = 55 }
      };
      AssetDatabase.CreateAsset(config, path);
      return config;
    }

    private void CreateConfig()
    {
      try { EnsureSourceFolder(DefaultSourceFolder); }
      catch (Exception exception) { error = exception.Message; return; }
      string path = EditorUtility.SaveFilePanelInProject("Create menu style configuration", "MenuStyles", "asset",
        "Choose a new editor authoring source asset.", DefaultSourceFolder);
      if (string.IsNullOrEmpty(path)) return;
      try { configuration = CreateDefaults(path, sharedCommon); sharedCommon = configuration.Common; Selection.activeObject = configuration; error = null; }
      catch (Exception exception) { error = exception.Message; }
    }
    private void CreateCommon()
    {
      try { EnsureSourceFolder(DefaultSourceFolder); }
      catch (Exception exception) { error = exception.Message; return; }
      string path = EditorUtility.SaveFilePanelInProject("Create shared menu settings", "MenuCommonSettings", "asset",
        "Choose a new shared authoring source asset.", DefaultSourceFolder);
      if (string.IsNullOrEmpty(path)) return;
      try
      {
        if (Occupied(path))
          throw new InvalidOperationException("The asset path already exists.");
        sharedCommon = CreateInstance<MenuCommonSettings>();
        sharedCommon.InputBindings.Configure(useNativeUguiNavigation: true);
        AssetDatabase.CreateAsset(sharedCommon, path);
        error = null;
      }
      catch (Exception exception) { error = exception.Message; }
    }
    private static bool Occupied(string path)
      => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") ||
         Directory.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;

    private static void EnsureSourceFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      string parent = path.Substring(0, path.LastIndexOf('/'));
      if (parent != "Assets") EnsureSourceFolder(parent);
      if (File.Exists(path) || File.Exists(path + ".meta"))
        throw new InvalidOperationException("Cannot create the authoring folder over an existing file or metadata: " + path);
      AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
  }
}
