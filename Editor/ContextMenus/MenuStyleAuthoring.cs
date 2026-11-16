using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.ContextMenus
{
  public sealed class MenuStyleBuildEntry
  {
    public string StyleId { get; internal set; }
    public string Name { get; internal set; }
    public string OutputPath { get; internal set; }
    public bool CreatesProfile { get; internal set; }
    public MenuProfile ExistingProfile { get; internal set; }
    public string PresenterPath { get; internal set; }
    internal MenuStyleRecipe Recipe;
    internal Type DefaultPresenterType;
  }

  public sealed class MenuStyleBuildPlan
  {
    public IReadOnlyList<MenuStyleBuildEntry> Entries { get; internal set; }
    public IReadOnlyList<string> Diagnostics { get; internal set; }
    public bool CanBuild => Diagnostics != null && Diagnostics.Count == 0;
    internal MenuStyleConfig Config;
    internal string StyleId;
    internal string Fingerprint;
  }

  public static class MenuStyleAuthoring
  {
    /// <summary>Creates a new runtime MenuSkin override asset without adopting existing paths or metadata.</summary>
    public static MenuSkin CreateThemeOverrideAsset(string path)
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode)
        throw new InvalidOperationException("Stop Play Mode before creating theme override assets.");
      if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
        || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains('\\'))
        throw new InvalidOperationException("Choose a new runtime .asset path under Assets, outside Editor folders.");
      string parent = Parent(path);
      if ((parent != "Assets" && !ValidFolder(parent)) || !ValidName(Path.GetFileNameWithoutExtension(path))
        || path.Split('/').Any(part => string.Equals(part, "Editor", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Theme overrides are runtime assets. Choose a normal Assets folder, outside Editor folders.");
      if (Exists(path))
        throw new InvalidOperationException("Refusing to adopt or overwrite an existing asset, directory or metadata at " + path + ".");
      if (!AssetDatabase.IsValidFolder(parent))
        throw new InvalidOperationException("Create the runtime theme asset's parent folder first.");
      MenuSkin asset = null;
      try
      {
        asset = ScriptableObject.CreateInstance<MenuSkin>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
      }
      catch
      {
        if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
        throw;
      }
    }

    /// <summary>Read-only validation and exact proposed output paths. A null style id selects all styles.</summary>
    public static MenuStyleBuildPlan Preview(MenuStyleConfig config, string styleId = null)
    {
      var diagnostics = new List<string>();
      var entries = new List<MenuStyleBuildEntry>();
      var plan = new MenuStyleBuildPlan { Config = config, StyleId = styleId, Entries = entries, Diagnostics = diagnostics };
      if (config == null) { diagnostics.Add("Select a saved MenuStyleConfig asset."); return plan; }
      if (!SavedAsset(config)) diagnostics.Add("Save the style configuration under Assets before rebuilding.");
      if (config.Common == null) diagnostics.Add("Assign a shared MenuCommonSettings asset.");
      else if (!SavedAsset(config.Common)) diagnostics.Add("Save the common settings under Assets before rebuilding.");
      if (!ValidFolder(config.OutputFolder)) diagnostics.Add("OutputFolder must be a project folder beneath Assets without relative path segments.");
      var styles = config.Styles ?? Array.Empty<MenuStyleRecipe>();
      if (styles.Length == 0) diagnostics.Add("Add at least one style recipe.");
      var ids = new HashSet<string>(StringComparer.Ordinal);
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      bool selected = styleId == null;
      foreach (var recipe in styles)
      {
        if (recipe == null) { diagnostics.Add("Remove the missing style recipe."); continue; }
        string name = string.IsNullOrWhiteSpace(recipe.AssetName) ? recipe.DisplayName : recipe.AssetName;
        if (string.IsNullOrWhiteSpace(recipe.Id) || !ids.Add(recipe.Id)) diagnostics.Add(name + ": duplicate or empty style identity.");
        string path = recipe.Output != null ? AssetDatabase.GetAssetPath(recipe.Output)
          : (config.OutputFolder ?? "").TrimEnd('/') + "/" + recipe.AssetName + ".asset";
        if (!paths.Add(path)) diagnostics.Add(name + ": duplicate output target " + path + ".");
        if (styleId != null && recipe.Id != styleId) continue;
        selected = true;
        var entry = new MenuStyleBuildEntry { Recipe = recipe, StyleId = recipe.Id, Name = name,
          OutputPath = path, ExistingProfile = recipe.Output, CreatesProfile = recipe.Output == null };
        entries.Add(entry);
        ValidateParentFolders(path, diagnostics);
        if (recipe.Output != null)
        {
          if (!SavedAsset(recipe.Output) || !AssetDatabase.IsMainAsset(recipe.Output))
            diagnostics.Add(name + ": output must be a saved main MenuProfile asset under Assets.");
        }
        else
        {
          if (!ValidName(recipe.AssetName)) diagnostics.Add(name + ": AssetName must be a plain file name without an extension.");
          if (Exists(path)) diagnostics.Add(name + ": refusing to adopt or overwrite an existing asset at " + path + ". Assign that profile explicitly if it is yours.");
        }
        if (config.Common == null) continue;
        MenuProfile candidate = null;
        try
        {
          candidate = CreateCandidate(config.Common, recipe);
          if (recipe.Output != null && candidate.FallbackProfile == recipe.Output)
            diagnostics.Add(name + ": FallbackProfile cannot reference the output itself.");
          if (!candidate.Validate(out string diagnostic, false)) diagnostics.Add(name + ": " + diagnostic);
          ValidatePrefab(candidate.PresenterPrefab, name + " presenter", diagnostics);
          ValidatePrefab(candidate.ItemPrefab, name + " item", diagnostics);
          ValidatePrefab(candidate.CenterPrefab, name + " center", diagnostics);
          ValidatePrefab(candidate.SecondaryPrefab, name + " secondary", diagnostics);
          bool addressable = candidate.PresenterReference != null && Guid.TryParseExact(candidate.PresenterReference.AssetGUID, "N", out _);
          if (candidate.PresenterPrefab == null && !addressable)
          {
            entry.DefaultPresenterType = recipe.DefaultPresenterType;
            if (entry.DefaultPresenterType == null) diagnostics.Add(name + ": custom style requires an explicit presenter prefab or an existing output presenter.");
            else
            {
              GameObject existing = entry.DefaultPresenterType == typeof(MToolKit.ContextMenus.Presentation.ListMenuPresenter)
                ? config.GeneratedListPresenter : config.GeneratedRadialPresenter;
              entry.PresenterPath = existing != null ? AssetDatabase.GetAssetPath(existing)
                : config.OutputFolder.TrimEnd('/') + "/MTK_" + entry.DefaultPresenterType.Name + ".prefab";
              ValidateParentFolders(entry.PresenterPath, diagnostics);
              if (existing == null && Exists(entry.PresenterPath)) diagnostics.Add(name + ": refusing to adopt an existing presenter at " + entry.PresenterPath + ".");
              else if (existing != null && (existing.GetComponent(entry.DefaultPresenterType) == null || !SavedAsset(existing)))
                diagnostics.Add(name + ": the owned default presenter reference is invalid.");
            }
          }
        }
        catch (Exception error) { diagnostics.Add(name + ": " + error.Message); }
        finally { if (candidate != null) Object.DestroyImmediate(candidate); }
      }
      if (!selected) diagnostics.Add("The selected style no longer exists. Refresh the preview.");
      plan.Fingerprint = Fingerprint(plan);
      return plan;
    }

    /// <summary>Refuses changed source/target identities before writing any selected output.</summary>
    public static MenuProfile[] Rebuild(MenuStyleBuildPlan plan)
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before rebuilding menu styles.");
      if (plan == null || plan.Config == null) throw new InvalidOperationException("Preview a saved style configuration before rebuilding.");
      var current = Preview(plan.Config, plan.StyleId);
      if (!string.Equals(current.Fingerprint, plan.Fingerprint, StringComparison.Ordinal))
        throw new InvalidOperationException("The configuration, common settings or output assets changed after preview. Refresh the preview before rebuilding.");
      if (!current.CanBuild) throw new InvalidOperationException(string.Join("\n", current.Diagnostics));
      var config = plan.Config;
      var outputs = new List<MenuProfile>();
      var candidates = new List<MenuProfile>();
      GameObject list = config.GeneratedListPresenter, radial = config.GeneratedRadialPresenter;
      Undo.IncrementCurrentGroup();
      int undoGroup = Undo.GetCurrentGroup();
      Undo.SetCurrentGroupName("Rebuild menu styles");
      try
      {
        // Build every proposed profile before creating folders, prefabs, or changing saved objects.
        foreach (var entry in current.Entries) candidates.Add(CreateCandidate(config.Common, entry.Recipe));
        for (int i = 0; i < current.Entries.Count; i++)
        {
          var entry = current.Entries[i];
          var candidate = candidates[i];
          if (entry.DefaultPresenterType != null)
          {
            bool isList = entry.DefaultPresenterType == typeof(MToolKit.ContextMenus.Presentation.ListMenuPresenter);
            var presenter = isList ? list : radial;
            if (presenter == null)
            {
              EnsureFolder(Parent(entry.PresenterPath));
              presenter = MenuDefaultPrefabBuilder.Create(entry.DefaultPresenterType, entry.PresenterPath);
              if (isList) list = presenter; else radial = presenter;
            }
            candidate.PresenterPrefab = presenter;
          }
          MenuProfile output = entry.ExistingProfile;
          if (output == null)
          {
            EnsureFolder(Parent(entry.OutputPath));
            candidate.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(candidate, entry.OutputPath);
            output = candidate;
          }
          else
          {
            Undo.RecordObject(output, "Rebuild menu style");
            candidate.hideFlags = output.hideFlags;
            EditorUtility.CopySerialized(candidate, output);
            EditorUtility.SetDirty(output);
          }
          outputs.Add(output);
          AssetDatabase.SaveAssetIfDirty(output);
        }
        Undo.RecordObject(config, "Save menu style outputs");
        for (int i = 0; i < current.Entries.Count; i++) current.Entries[i].Recipe.Output = outputs[i];
        config.SetGeneratedPresenters(list, radial);
        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(config.Common);
        AssetDatabase.SaveAssetIfDirty(config.Common);
        AssetDatabase.SaveAssetIfDirty(config);
        Undo.CollapseUndoOperations(undoGroup);
        return outputs.ToArray();
      }
      finally
      {
        foreach (var candidate in candidates)
          if (candidate != null && !EditorUtility.IsPersistent(candidate)) Object.DestroyImmediate(candidate);
      }
    }

    internal static MenuProfile CreateCandidate(MenuCommonSettings common, MenuStyleRecipe recipe)
    {
      var profile = recipe.Output != null ? Object.Instantiate(recipe.Output) : ScriptableObject.CreateInstance<MenuProfile>();
      try
      {
        profile.hideFlags = HideFlags.HideAndDontSave;
        profile.name = recipe.Output != null ? recipe.Output.name : recipe.AssetName;
        profile.SharedThemeOverrides = common.ThemeOverrides;
      profile.ThemeOverrides = recipe.ThemeOverrides;
      if (common.Skin != null) profile.Skin = common.Skin;
        if (common.ItemPrefab != null) profile.ItemPrefab = common.ItemPrefab;
        if (common.CenterPrefab != null) profile.CenterPrefab = common.CenterPrefab;
        if (common.SecondaryPrefab != null) profile.SecondaryPrefab = common.SecondaryPrefab;
        if (common.FallbackProfile != null) profile.FallbackProfile = common.FallbackProfile;
        profile.InputBindings = Clone(common.InputBindings);
        profile.Transition = common.Transition;
        profile.Anchor = common.Anchor;
        profile.ShowCenter = common.ShowCenter; profile.Backdrop = common.Backdrop;
        profile.ReducedMotion = common.ReducedMotion; profile.MinimumTargetSize = common.MinimumTargetSize;
        profile.AnalogDeadZone = common.AnalogDeadZone; profile.AnalogHysteresis = common.AnalogHysteresis;
        profile.MaximumVisibleItems = recipe.MaximumVisibleItems; profile.WidgetSize = recipe.WidgetSize;
        if (recipe.PresenterPrefab != null) profile.PresenterPrefab = recipe.PresenterPrefab;
        if (recipe.ItemPrefab != null) profile.ItemPrefab = recipe.ItemPrefab;
        if (recipe.CenterPrefab != null) profile.CenterPrefab = recipe.CenterPrefab;
        if (recipe.SecondaryPrefab != null) profile.SecondaryPrefab = recipe.SecondaryPrefab;
        if (recipe.Skin != null) profile.Skin = recipe.Skin;
        if (recipe.OverrideInputBindings) profile.InputBindings = Clone(recipe.InputBindings);
        // Unity materializes an unassigned serialized AssetReference as an empty wrapper when cloning assets.
        // It is not an authored addressable choice; nonempty invalid GUIDs must still produce diagnostics.
        if (profile.PresenterPrefab == null && profile.PresenterReference != null &&
            string.IsNullOrEmpty(profile.PresenterReference.AssetGUID)) profile.PresenterReference = null;
        recipe.ApplySpecialized(profile);
        if (recipe.OverrideShowCenter) profile.ShowCenter = recipe.ShowCenter;
        return profile;
      }
      catch
      {
        Object.DestroyImmediate(profile);
        throw;
      }
    }

    private static MenuInputBindings Clone(MenuInputBindings bindings)
    {
      if (bindings == null) return null;
      var copy = new MenuInputBindings();
      var secondary = new InputActionReference[bindings.Secondary.Count];
      for (int i = 0; i < secondary.Length; i++) secondary[i] = bindings.Secondary[i];
      copy.Configure(bindings.Navigate, bindings.Point, bindings.Click, bindings.Submit, bindings.Back, bindings.Cancel,
        bindings.PageForward, bindings.PageBackward, bindings.SwitchRing, bindings.Hold, secondary,
        bindings.UseNativeUguiNavigation, bindings.RoutePointerClick);
      return copy;
    }

    private static string Fingerprint(MenuStyleBuildPlan plan)
    {
      var text = new StringBuilder();
      AppendObject(text, plan.Config);
      AppendObject(text, plan.Config.Common);
      AppendObject(text, plan.Config.Common != null ? plan.Config.Common.ThemeOverrides : null);
      AppendObject(text, plan.Config.GeneratedListPresenter);
      AppendObject(text, plan.Config.GeneratedRadialPresenter);
      text.Append('|').Append(plan.StyleId);
      foreach (var entry in plan.Entries)
      {
        AppendObject(text, entry.ExistingProfile);
        AppendObject(text, entry.Recipe.ThemeOverrides);
        text.Append('|').Append(entry.OutputPath); AppendFile(text, entry.OutputPath);
        text.Append('|').Append(entry.PresenterPath); AppendFile(text, entry.PresenterPath);
      }
      using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static void AppendObject(StringBuilder text, Object value)
    {
      if (value == null) { text.Append("|null"); return; }
      string path = AssetDatabase.GetAssetPath(value);
      text.Append('|').Append(value.GetInstanceID()).Append('|').Append(path)
        .Append('|').Append(AssetDatabase.AssetPathToGUID(path)).Append('|').Append(EditorJsonUtility.ToJson(value));
      AppendFile(text, path);
    }

    private static void AppendFile(StringBuilder text, string path)
    {
      if (string.IsNullOrEmpty(path)) return;
      foreach (var file in new[] { path, path + ".meta" })
      {
        text.Append('|').Append(file).Append(':');
        if (!File.Exists(file)) { text.Append("missing"); continue; }
        using (var hash = SHA256.Create()) text.Append(Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(file))));
      }
    }

    private static void ValidatePrefab(GameObject prefab, string name, ICollection<string> diagnostics)
    {
      if (prefab != null && (!EditorUtility.IsPersistent(prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab)))
        diagnostics.Add(name + " must reference a saved prefab, not a scene object.");
    }

    private static bool SavedAsset(Object value)
      => value != null && EditorUtility.IsPersistent(value) && AssetDatabase.GetAssetPath(value).StartsWith("Assets/", StringComparison.Ordinal);
    private static bool Exists(string path) => !string.IsNullOrEmpty(path) && (File.Exists(path) || File.Exists(path + ".meta")
      || Directory.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null);
    private static void ValidateParentFolders(string path, ICollection<string> diagnostics)
    {
      if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) return;
      string parent = Parent(path);
      while (parent != "Assets")
      {
        if (File.Exists(parent) || (!Directory.Exists(parent) && File.Exists(parent + ".meta")))
        { diagnostics.Add("A file or orphan metadata blocks the generated folder: " + parent); return; }
        parent = Parent(parent);
      }
    }
    private static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) && value != "." && value != ".."
      && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !value.Contains('/') && !value.Contains('\\')
      && !value.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && value.TrimEnd(' ', '.') == value;
    private static bool ValidFolder(string value) => !string.IsNullOrWhiteSpace(value)
      && value.StartsWith("Assets/", StringComparison.Ordinal) && !value.Contains('\\')
      && value.Split('/').All(segment => ValidName(segment));
    private static string Parent(string path) => path.Substring(0, path.LastIndexOf('/'));
    private static void EnsureFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      if (!ValidFolder(path)) throw new InvalidOperationException("Invalid generated asset folder: " + path);
      string parent = Parent(path);
      if (parent != "Assets") EnsureFolder(parent);
      AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
  }
}
