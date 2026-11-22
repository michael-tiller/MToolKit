using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MToolKit.ContextMenus.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.ContextMenus.Tests
{
  public sealed class MenuPresentationThemeSetAuthoringTests
  {
    private string root;
    private MenuCommonSettings common;
    private MenuStyleConfig config;
    private readonly List<Object> transient = new List<Object>();

    [SetUp]
    public void SetUp()
    {
      root = "Assets/Temp/automation-scratch/MenuPresentationThemeSet_" + Guid.NewGuid().ToString("N");
      EnsureFolder(root);
      common = Save<MenuCommonSettings>("Common"); config = Save<MenuStyleConfig>("Styles");
      config.Common = common; config.OutputFolder = root + "/Generated";
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ListOverflowIsAuthorableAndBakedWithoutChangingRowGeometryOrAssetIdentity(bool typedTheme)
    {
      var field = (typedTheme ? typeof(MenuListLayout) : typeof(ListMenuStyleRecipe)).GetField("Overflow");
      Assert.That(field, Is.Not.Null, "Both typed and inline list authoring must expose the optional scrolling policy.");
      Assert.That(field.FieldType.FullName, Is.EqualTo("MToolKit.ContextMenus.Configuration.MenuListOverflowPolicy"));
      var scrolling = Enum.Parse(field.FieldType, "Scrolling");
      var output = Save<MenuProfile>("ScrollableOutput");
      var recipe = new ListMenuStyleRecipe { Output = output, ListWidth = 466, RowHeight = 66 };
      string sourceBefore = null;
      if (typedTheme)
      {
        var source = Save<ListMenuThemeSet>("ScrollableTheme");
        object layout = new MenuListLayout { ListWidth = 466, RowHeight = 66 };
        field.SetValue(layout, scrolling); source.Layout = (MenuListLayout)layout;
        recipe.ThemeSet = source; sourceBefore = EditorJsonUtility.ToJson(source);
      }
      else field.SetValue(recipe, scrolling);
      config.Styles = new MenuStyleRecipe[] { recipe };
      string guid = GuidOf(output);

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      var overflow = typeof(MenuProfile).GetProperty("ListOverflow");
      Assert.That(overflow, Is.Not.Null);
      Assert.That(overflow.GetValue(output), Is.EqualTo(scrolling));
      Assert.That(output.ListWidth, Is.EqualTo(466)); Assert.That(output.RowHeight, Is.EqualTo(66));
      Assert.That(GuidOf(output), Is.EqualTo(guid));
      if (typedTheme) Assert.That(EditorJsonUtility.ToJson(recipe.ThemeSet), Is.EqualTo(sourceBefore));
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var item in transient) if (item != null && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
      transient.Clear();
      if (root != null && root.StartsWith("Assets/Temp/automation-scratch/MenuPresentationThemeSet_", StringComparison.Ordinal))
        AssetDatabase.DeleteAsset(root);
    }

    [TestCase("list")]
    [TestCase("wheel")]
    [TestCase("nested")]
    [TestCase("pie")]
    public void AssignedTypedThemeOwnsItsFamilyLayoutAndCompositionWhileRecipeInputRemains(string family)
    {
      var output = Save<MenuProfile>("Output");
      var shared = Save<MenuSkin>("Shared"); var local = Save<MenuSkin>("TypedLocal");
      var legacy = Save<MenuSkin>("LegacyLocal"); var fallback = Save<MenuSkin>("Fallback");
      common.ThemeOverrides = shared; common.Skin = fallback; common.AnalogDeadZone = .31f;
      common.ShowCenter = true; common.Backdrop = true;
      MenuPresentationThemeSet theme;
      MenuStyleRecipe recipe;
      if (family == "list")
      {
        var value = Save<ListMenuThemeSet>("ListTheme");
        value.Layout = new MenuListLayout { ListWidth = 466, RowHeight = 66 };
        theme = value;
        recipe = new ListMenuStyleRecipe { ThemeSet = value, ListWidth = 333, RowHeight = 44, Navigation = MenuNavigationPolicy.Flyout };
      }
      else if (family == "wheel")
      {
        var value = Save<WheelMenuThemeSet>("WheelTheme");
        value.Layout = WheelLayout(201, 88, true);
        theme = value;
        recipe = new SingleRingMenuStyleRecipe { ThemeSet = value, InnerRadius = 90, RingWidth = 70 };
      }
      else if (family == "nested")
      {
        var value = Save<NestedWheelMenuThemeSet>("NestedTheme");
        value.Layout = new MenuNestedWheelLayout { InnerRing = WheelLayout(201, 55, true), RingGap = 17,
          ChildRingWidth = 44, ChildArcDegrees = 96 };
        theme = value;
        recipe = new ConcentricMenuStyleRecipe { ThemeSet = value, InnerRadius = 90, RingWidth = 70,
          RingGap = 8, ChildRingWidth = 31, ChildArcDegrees = 84 };
      }
      else
      {
        var value = Save<PieMenuThemeSet>("PieTheme");
        value.Layout = WheelLayout(111, 155, false);
        theme = value;
        recipe = new ReplacementMenuStyleRecipe { ThemeSet = value, InnerRadius = 90, RingWidth = 70, DrawSegments = true };
      }
      theme.ThemeOverrides = local; theme.MaximumVisibleItems = 5; theme.WidgetSize = new Vector2(152, 68);
      theme.ShowCenter = false; theme.Backdrop = false;
      recipe.Output = output; recipe.ThemeOverrides = legacy; recipe.MaximumVisibleItems = 7;
      recipe.WidgetSize = new Vector2(100, 50); recipe.OverrideShowCenter = true; recipe.ShowCenter = true;
      recipe.OverrideInputBindings = true; recipe.InputBindings.Configure(useNativeUguiNavigation: false);
      config.Styles = new[] { recipe };
      string sourceBefore = EditorJsonUtility.ToJson(theme), guid = GuidOf(output);

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(output.SharedThemeOverrides, Is.SameAs(shared));
      Assert.That(output.ThemeOverrides, Is.SameAs(local));
      Assert.That(output.Skin, Is.SameAs(fallback));
      Assert.That(output.MaximumVisibleItems, Is.EqualTo(5));
      Assert.That(output.WidgetSize, Is.EqualTo(new Vector2(152, 68)));
      Assert.That(output.ShowCenter, Is.False); Assert.That(output.Backdrop, Is.False);
      Assert.That(output.InputBindings.UseNativeUguiNavigation, Is.False);
      Assert.That(output.AnalogDeadZone, Is.EqualTo(.31f));
      if (family == "list")
      {
        Assert.That(output.ListWidth, Is.EqualTo(466)); Assert.That(output.RowHeight, Is.EqualTo(66));
        Assert.That(output.Navigation, Is.EqualTo(MenuNavigationPolicy.Flyout));
      }
      else
      {
        Assert.That(output.InnerRadius, Is.EqualTo(family == "pie" ? 111 : 201));
        Assert.That(output.RingWidth, Is.EqualTo(family == "pie" ? 155 : family == "nested" ? 55 : 88));
        Assert.That(output.StartAngle, Is.EqualTo(31)); Assert.That(output.Clockwise, Is.False);
        Assert.That(output.AngularGap, Is.EqualTo(4)); Assert.That(output.DrawSegments, Is.EqualTo(family != "pie"));
        Assert.That(output.Navigation, Is.EqualTo(family == "nested" ? MenuNavigationPolicy.Concentric : MenuNavigationPolicy.Replacement));
        if (family == "nested")
        { Assert.That(output.RingGap, Is.EqualTo(17)); Assert.That(output.ChildRingWidth, Is.EqualTo(44)); Assert.That(output.ChildArcDegrees, Is.EqualTo(96)); }
      }
      Assert.That(GuidOf(output), Is.EqualTo(guid));
      Assert.That(EditorJsonUtility.ToJson(theme), Is.EqualTo(sourceBefore), "Baking must not mutate the reusable source.");
    }

    [Test]
    public void NativeSerializationRetainsConcreteRecipeNamesLegacyFieldsAndExactlyOneTypedThemeSlot()
    {
      var list = Save<ListMenuThemeSet>("ListTheme"); var wheel = Save<WheelMenuThemeSet>("WheelTheme");
      var nested = Save<NestedWheelMenuThemeSet>("NestedTheme"); var pie = Save<PieMenuThemeSet>("PieTheme");
      var output = Save<MenuProfile>("Existing"); string guid = GuidOf(output);
      config.Styles = new MenuStyleRecipe[] {
        new ListMenuStyleRecipe { ThemeSet = list, ListWidth = 417, RowHeight = 53, Output = output },
        new SingleRingMenuStyleRecipe { ThemeSet = wheel, InnerRadius = 137, RingWidth = 67, Output = output },
        new ConcentricMenuStyleRecipe { ThemeSet = nested, InnerRadius = 139, RingWidth = 69, RingGap = 13, ChildRingWidth = 57, ChildArcDegrees = 111, Output = output },
        new ReplacementMenuStyleRecipe { ThemeSet = pie, InnerRadius = 141, RingWidth = 71, WidgetSize = new Vector2(151, 61), Output = output }
      };
      var clone = ScriptableObject.CreateInstance<MenuStyleConfig>(); transient.Add(clone);

      EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(config), clone);

      var expected = new[] { typeof(ListMenuThemeSet), typeof(WheelMenuThemeSet), typeof(NestedWheelMenuThemeSet), typeof(PieMenuThemeSet) };
      for (int i = 0; i < clone.Styles.Length; i++)
      {
        var original = config.Styles[i]; var restored = clone.Styles[i];
        Assert.That(restored.GetType(), Is.EqualTo(original.GetType())); Assert.That(restored.Id, Is.EqualTo(original.Id));
        Assert.That(restored.Output, Is.SameAs(output));
        var slots = restored.GetType().GetFields().Where(field => field.Name == "ThemeSet").ToArray();
        Assert.That(slots.Length, Is.EqualTo(1), "A radial subtype must not inherit a second Wheel-only source slot.");
        Assert.That(slots[0].FieldType, Is.EqualTo(expected[i]));
        Assert.That(slots[0].GetValue(restored), Is.SameAs(original.GetType().GetField("ThemeSet").GetValue(original)));
      }
      Assert.That(((ListMenuStyleRecipe)clone.Styles[0]).ListWidth, Is.EqualTo(417));
      Assert.That(((SingleRingMenuStyleRecipe)clone.Styles[1]).InnerRadius, Is.EqualTo(137));
      var nestedRecipe = (ConcentricMenuStyleRecipe)clone.Styles[2];
      Assert.That(nestedRecipe.InnerRadius, Is.EqualTo(139)); Assert.That(nestedRecipe.RingWidth, Is.EqualTo(69));
      Assert.That(nestedRecipe.RingGap, Is.EqualTo(13)); Assert.That(nestedRecipe.ChildRingWidth, Is.EqualTo(57)); Assert.That(nestedRecipe.ChildArcDegrees, Is.EqualTo(111));
      Assert.That(((ReplacementMenuStyleRecipe)clone.Styles[3]).WidgetSize, Is.EqualTo(new Vector2(151, 61)));
      Assert.That(GuidOf(output), Is.EqualTo(guid));
    }

    [Test]
    public void OneReusableTypedSourceRebuildsTwoConfigurationsWithoutReplacingOutputsOrStateAssets()
    {
      var theme = Save<ListMenuThemeSet>("Reusable"); theme.Layout = new MenuListLayout { ListWidth = 492, RowHeight = 63 };
      var layer = Save<MenuSkin>("Layer"); var state = Save<MenuStyleSet>("PositiveInactive");
      layer.StateStyles = new[] { new MenuSkinStyle { State = MenuThemeState.Any, Style = state } }; theme.ThemeOverrides = layer;
      var first = Save<MenuProfile>("First"); var second = Save<MenuProfile>("Second");
      config.Styles = new MenuStyleRecipe[] { new ListMenuStyleRecipe { Output = first, ThemeSet = theme } };
      var other = Save<MenuStyleConfig>("Other"); other.Common = common; other.OutputFolder = root + "/OtherGenerated";
      other.Styles = new MenuStyleRecipe[] { new ListMenuStyleRecipe { Output = second, ThemeSet = theme } };
      string themeGuid = GuidOf(theme), layerGuid = GuidOf(layer), stateGuid = GuidOf(state), firstGuid = GuidOf(first), secondGuid = GuidOf(second);

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));
      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(other));

      Assert.That(first.ListWidth, Is.EqualTo(492)); Assert.That(second.ListWidth, Is.EqualTo(492));
      Assert.That(first.ThemeOverrides, Is.SameAs(layer)); Assert.That(second.ThemeOverrides, Is.SameAs(layer));
      Assert.That(first.ThemeOverrides.StateStyles[0].Style, Is.SameAs(state));
      Assert.That(GuidOf(first), Is.EqualTo(firstGuid)); Assert.That(GuidOf(second), Is.EqualTo(secondGuid));
      Assert.That(GuidOf(theme), Is.EqualTo(themeGuid)); Assert.That(GuidOf(layer), Is.EqualTo(layerGuid)); Assert.That(GuidOf(state), Is.EqualTo(stateGuid));
    }

    [Test]
    public void EditingTypedSourceAfterPreviewIsRejectedBeforeAnyRuntimeOutputWrite()
    {
      var output = Save<MenuProfile>("Existing"); var theme = Save<WheelMenuThemeSet>("Theme");
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { Output = output, ThemeSet = theme } };
      var plan = MenuStyleAuthoring.Preview(config);
      byte[] before = File.ReadAllBytes(AssetDatabase.GetAssetPath(output)); theme.Layout.InnerRadius += 10;

      var error = Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));

      Assert.That(error.Message, Does.Contain("changed").IgnoreCase);
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(output)), Is.EqualTo(before));
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [Test]
    public void TypedSourceCreationRefusesAbstractOrForeignTypesAndNeverAdoptsOrphanMetadata()
    {
      EnsureFolder(root + "/Editor");
      string path = root + "/Editor/WheelTheme.asset";
      var created = MenuStyleAuthoring.CreatePresentationThemeSetAsset(typeof(WheelMenuThemeSet), path);
      Assert.That(created, Is.TypeOf<WheelMenuThemeSet>()); Assert.That(EditorUtility.IsPersistent(created), Is.True);
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreatePresentationThemeSetAsset(typeof(MenuPresentationThemeSet), root + "/Abstract.asset"));
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreatePresentationThemeSetAsset(typeof(MenuProfile), root + "/Foreign.asset"));
      string blocked = root + "/NestedTheme.asset";
      File.WriteAllText(blocked + ".meta", "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n");
      byte[] metadata = File.ReadAllBytes(blocked + ".meta");
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreatePresentationThemeSetAsset(typeof(NestedWheelMenuThemeSet), blocked));
      Assert.That(File.Exists(blocked), Is.False); Assert.That(File.ReadAllBytes(blocked + ".meta"), Is.EqualTo(metadata));
    }

    private static MenuWheelLayout WheelLayout(float radius, float width, bool segments)
      => new MenuWheelLayout { InnerRadius = radius, RingWidth = width, StartAngle = 31, Clockwise = false, AngularGap = 4, DrawSegments = segments };
    private T Save<T>(string name) where T : ScriptableObject
    { var value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, root + "/" + name + ".asset"); return value; }
    private static string GuidOf(Object value) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
    private static void EnsureFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
      AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
  }
}
