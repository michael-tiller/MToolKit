using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.ContextMenus.Tests
{
  public sealed class MenuStyleAuthoringTests
  {
    private string root;
    private MenuCommonSettings common;
    private MenuStyleConfig config;
    private readonly List<Object> transient = new List<Object>();

    [SetUp]
    public void SetUp()
    {
      EnsureFolder("Assets/Temp");
      EnsureFolder("Assets/Temp/automation-scratch");
      root = "Assets/Temp/automation-scratch/MenuStyleAuthoring_" + Guid.NewGuid().ToString("N").Substring(0, 8);
      EnsureFolder(root);
      common = Save<MenuCommonSettings>("Common");
      config = Save<MenuStyleConfig>("Styles");
      config.Common = common;
      config.OutputFolder = root + "/Generated";
      AssetDatabase.SaveAssetIfDirty(config);
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var value in transient) if (value != null && !EditorUtility.IsPersistent(value)) Object.DestroyImmediate(value);
      transient.Clear();
      if (root != null && root.StartsWith("Assets/Temp/automation-scratch/MenuStyleAuthoring_", StringComparison.Ordinal))
        AssetDatabase.DeleteAsset(root);
    }

    [Test]
    public void SharedCommonRebuildsExistingProfilesWithoutChangingTheirIdentity()
    {
      var first = Save<MenuProfile>("First");
      var second = Save<MenuProfile>("Second");
      var skin = Save<MenuSkin>("Skin");
      common.Skin = skin;
      common.MinimumTargetSize = 40;
      common.Anchor = MenuAnchorPolicy.BottomCenter;
      var controls = Save<InputActionAsset>("Controls");
      var action = controls.AddActionMap("Menu").AddAction("Confirm", InputActionType.Button);
      var reference = InputActionReference.Create(action);
      transient.Add(reference);
      AssetDatabase.AddObjectToAsset(reference, controls);
      common.InputBindings.Configure(submit: reference);
      var a = new SingleRingMenuStyleRecipe { Output = first, AssetName = "First", RingWidth = 81 };
      var b = new ConcentricMenuStyleRecipe { Output = second, AssetName = "Second", ChildRingWidth = 57 };
      config.Styles = new MenuStyleRecipe[] { a, b };
      string firstGuid = GuidOf(first), secondGuid = GuidOf(second);

      var outputs = MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(outputs, Is.EqualTo(new[] { first, second }));
      Assert.That(GuidOf(first), Is.EqualTo(firstGuid));
      Assert.That(GuidOf(second), Is.EqualTo(secondGuid));
      Assert.That(a.Output, Is.SameAs(first));
      Assert.That(b.Output, Is.SameAs(second));
      Assert.That(first.Skin, Is.SameAs(skin));
      Assert.That(second.Skin, Is.SameAs(skin));
      Assert.That(first.MinimumTargetSize, Is.EqualTo(40));
      Assert.That(second.Anchor, Is.EqualTo(MenuAnchorPolicy.BottomCenter));
      Assert.That(first.InputBindings.Submit, Is.SameAs(reference));
      Assert.That(second.InputBindings.Submit, Is.SameAs(reference));
      Assert.That(first.InputBindings, Is.Not.SameAs(second.InputBindings), "Runtime containers must not share mutable binding arrays.");
      Assert.That(first.RingWidth, Is.EqualTo(81));
      Assert.That(second.ChildRingWidth, Is.EqualTo(57));
    }

    [Test]
    public void CommonChangesApplyOnlyWhenTheCallerExplicitlyRebuilds()
    {
      var output = Save<MenuProfile>("Existing");
      output.AnalogDeadZone = .2f;
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { Output = output } };
      common.AnalogDeadZone = .6f;
      var plan = MenuStyleAuthoring.Preview(config);
      Assert.That(output.AnalogDeadZone, Is.EqualTo(.2f), "Preview must not make a live runtime edit.");

      MenuStyleAuthoring.Rebuild(plan);

      Assert.That(output.AnalogDeadZone, Is.EqualTo(.6f));
      common.AnalogDeadZone = .4f;
      Assert.That(output.AnalogDeadZone, Is.EqualTo(.6f), "Sharing is an authoring relationship, not runtime inheritance.");
    }

    [Test]
    public void SpecializedRecipesDoNotOverwriteUnrelatedExistingGeometry()
    {
      var single = Save<MenuProfile>("Single");
      single.ChildRingWidth = 63;
      single.ChildArcDegrees = 77;
      var concentric = Save<MenuProfile>("Concentric");
      config.Styles = new MenuStyleRecipe[] {
        new SingleRingMenuStyleRecipe { Output = single, InnerRadius = 110, RingWidth = 85 },
        new ConcentricMenuStyleRecipe { Output = concentric, InnerRadius = 130, RingWidth = 70,
          ChildRingWidth = 54, ChildArcDegrees = 160, RingGap = 18 }
      };

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(single.Navigation, Is.EqualTo(MenuNavigationPolicy.Replacement));
      Assert.That(single.ChildRingWidth, Is.EqualTo(63));
      Assert.That(single.ChildArcDegrees, Is.EqualTo(77));
      Assert.That(concentric.Navigation, Is.EqualTo(MenuNavigationPolicy.Concentric));
      Assert.That(concentric.InnerRadius, Is.EqualTo(130));
      Assert.That(concentric.ChildRingWidth, Is.EqualTo(54));
      Assert.That(concentric.ChildArcDegrees, Is.EqualTo(160));
      Assert.That(concentric.RingGap, Is.EqualTo(18));
    }

    [Test]
    public void EmptyProjectConfigurationCreatesUsableNativeOutputsWithoutSamples()
    {
      config.Styles = new MenuStyleRecipe[] {
        new ListMenuStyleRecipe { AssetName = "List" },
        new SingleRingMenuStyleRecipe { AssetName = "Wheel" }
      };
      var plan = MenuStyleAuthoring.Preview(config);
      Assert.That(plan.CanBuild, Is.True, string.Join("\n", plan.Diagnostics));
      Assert.That(plan.Entries.Count, Is.EqualTo(2));
      Assert.That(plan.Entries.All(value => value.CreatesProfile), Is.True);

      var outputs = MenuStyleAuthoring.Rebuild(plan);

      Assert.That(outputs[0].PresenterPrefab.GetComponent<ListMenuPresenter>(), Is.Not.Null);
      Assert.That(outputs[1].PresenterPrefab.GetComponent<RadialMenuPresenter>(), Is.Not.Null);
      foreach (var profile in outputs)
      {
        Assert.That(profile.Validate(out string diagnostic), Is.True, diagnostic);
        Assert.That(profile.ItemPrefab, Is.Null, "Complete native runtime widgets should remain usable without a sample import.");
        Assert.That(profile.PresenterPrefab.GetComponent<RectTransform>(), Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(profile), Does.StartWith(config.OutputFolder + "/"));
      }
      Assert.That(config.Styles[0].Output, Is.SameAs(outputs[0]));
      Assert.That(config.Styles[1].Output, Is.SameAs(outputs[1]));
      string[] firstGuids = outputs.Select(GuidOf).ToArray();
      string[] presenterGuids = outputs.Select(value => GuidOf(value.PresenterPrefab)).ToArray();
      var rebuilt = MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));
      Assert.That(rebuilt.Select(GuidOf).ToArray(), Is.EqualTo(firstGuids));
      Assert.That(rebuilt.Select(value => GuidOf(value.PresenterPrefab)).ToArray(), Is.EqualTo(presenterGuids));
    }

    [Test]
    public void PreviewDoesNotCreateFoldersOrDirtyTheSourceOrOutput()
    {
      var output = Save<MenuProfile>("Existing");
      config.Styles = new MenuStyleRecipe[] { new ConcentricMenuStyleRecipe { Output = output, RingWidth = 74 } };
      EditorUtility.SetDirty(config);
      AssetDatabase.SaveAssetIfDirty(config);
      string beforeConfig = EditorJsonUtility.ToJson(config), beforeProfile = EditorJsonUtility.ToJson(output);
      byte[] bytes = File.ReadAllBytes(AssetDatabase.GetAssetPath(output));

      var plan = MenuStyleAuthoring.Preview(config);

      Assert.That(plan.Entries[0].ExistingProfile, Is.SameAs(output));
      Assert.That(plan.Entries[0].CreatesProfile, Is.False);
      Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(beforeConfig));
      Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(beforeProfile));
      Assert.That(EditorUtility.IsDirty(output), Is.False);
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(output)), Is.EqualTo(bytes));
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [TestCase("config")]
    [TestCase("common")]
    [TestCase("target")]
    public void ChangedSerializedStateInvalidatesThePlanBeforeWritingAnyOutput(string changed)
    {
      var output = Save<MenuProfile>("Existing");
      config.Styles = new MenuStyleRecipe[] { new ListMenuStyleRecipe { Output = output, ListWidth = 510 } };
      string path = AssetDatabase.GetAssetPath(output);
      byte[] before = File.ReadAllBytes(path);
      var plan = MenuStyleAuthoring.Preview(config);
      if (changed == "config") config.Styles[0].MaximumVisibleItems++;
      else if (changed == "common") common.AnalogDeadZone = .4f;
      else output.RowHeight = 93;

      var error = Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));

      Assert.That(error.Message, Does.Contain("changed").IgnoreCase);
      Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [Test]
    public void AForeignAssetAtAPlannedOutputPathIsNeverAdoptedOrOverwritten()
    {
      EnsureFolder(config.OutputFolder);
      var foreign = ScriptableObject.CreateInstance<MenuProfile>();
      string path = config.OutputFolder + "/Wheel.asset";
      AssetDatabase.CreateAsset(foreign, path);
      string guid = GuidOf(foreign);
      byte[] before = File.ReadAllBytes(path);
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { AssetName = "Wheel" } };

      var plan = MenuStyleAuthoring.Preview(config);

      Assert.That(plan.CanBuild, Is.False);
      Assert.That(string.Join("\n", plan.Diagnostics), Does.Contain(path));
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));
      Assert.That(GuidOf(foreign), Is.EqualTo(guid));
      Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
      Assert.That(config.Styles[0].Output, Is.Null);
    }

    [Test]
    public void AnInvalidSecondStylePreventsTheFirstStyleFromBeingWritten()
    {
      var output = Save<MenuProfile>("Existing");
      byte[] before = File.ReadAllBytes(AssetDatabase.GetAssetPath(output));
      float previousWidth = output.ListWidth;
      config.Styles = new MenuStyleRecipe[] {
        new ListMenuStyleRecipe { Output = output, ListWidth = 610 },
        new ConcentricMenuStyleRecipe { AssetName = "Invalid", ChildArcDegrees = -5 }
      };

      var plan = MenuStyleAuthoring.Preview(config);

      Assert.That(plan.CanBuild, Is.False);
      Assert.That(string.Join("\n", plan.Diagnostics), Does.Contain("ChildArcDegrees"));
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));
      Assert.That(output.ListWidth, Is.EqualTo(previousWidth));
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(output)), Is.EqualTo(before));
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [Test]
    public void ExistingCustomPresenterAndWidgetPrefabsRetainTheirContentsAndReferences()
    {
      var output = Save<MenuProfile>("Existing");
      var presenter = SavePresenter("CustomPresenter");
      var widgetRoot = new GameObject("Private widget root", typeof(RectTransform), typeof(MToolKit.ContextMenus.Widgets.MenuActionWidget));
      transient.Add(widgetRoot);
      var privateChild = new GameObject("Private child", typeof(RectTransform));
      privateChild.transform.SetParent(widgetRoot.transform, false);
      privateChild.GetComponent<RectTransform>().sizeDelta = new Vector2(177, 91);
      var widget = PrefabUtility.SaveAsPrefabAsset(widgetRoot, root + "/CustomWidget.prefab");
      output.PresenterPrefab = presenter;
      output.ItemPrefab = widget;
      output.CenterPrefab = widget;
      output.SecondaryPrefab = widget;
      var skin = Save<MenuSkin>("ExistingSkin");
      output.Skin = skin;
      config.Styles = new MenuStyleRecipe[] { new ReplacementMenuStyleRecipe { Output = output, DrawSegments = false } };
      byte[] presenterBefore = File.ReadAllBytes(AssetDatabase.GetAssetPath(presenter));
      byte[] widgetBefore = File.ReadAllBytes(AssetDatabase.GetAssetPath(widget));

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(output.PresenterPrefab, Is.SameAs(presenter));
      Assert.That(output.ItemPrefab, Is.SameAs(widget));
      Assert.That(output.CenterPrefab, Is.SameAs(widget));
      Assert.That(output.SecondaryPrefab, Is.SameAs(widget));
      Assert.That(output.Skin, Is.SameAs(skin));
      Assert.That(output.DrawSegments, Is.False);
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(presenter)), Is.EqualTo(presenterBefore));
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(widget)), Is.EqualTo(widgetBefore));
      Assert.That(widget.transform.Find("Private child").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(177, 91)));
    }

    [Test]
    public void RebuildPreservesTheCallersSceneAndUnrelatedDirtyNativeAsset()
    {
      var foreign = Save<MenuSkin>("ForeignDirtySkin");
      byte[] foreignDisk = File.ReadAllBytes(AssetDatabase.GetAssetPath(foreign));
      foreign.OverrideTheme = true;
      EditorUtility.SetDirty(foreign);
      var caller = SceneManager.GetActiveScene();
      bool wasDirty = caller.isDirty;
      int sceneCount = SceneManager.sceneCount;
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { AssetName = "Wheel" } };

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(caller));
      Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
      Assert.That(caller.isDirty, Is.EqualTo(wasDirty), "Do not save or dirty the caller, including an untitled test-runner scene.");
      Assert.That(foreign.OverrideTheme, Is.True);
      Assert.That(EditorUtility.IsDirty(foreign), Is.True);
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(foreign)), Is.EqualTo(foreignDisk));
    }

    [Test]
    public void RebuildSelectedStyleLeavesOtherOutputsUntouched()
    {
      var first = Save<MenuProfile>("First");
      var second = Save<MenuProfile>("Second");
      var selected = new ListMenuStyleRecipe { Output = first, ListWidth = 620 };
      config.Styles = new MenuStyleRecipe[] { selected, new ListMenuStyleRecipe { Output = second, ListWidth = 730 } };
      string secondBefore = EditorJsonUtility.ToJson(second);

      var outputs = MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config, selected.Id));

      Assert.That(outputs, Is.EqualTo(new[] { first }));
      Assert.That(first.ListWidth, Is.EqualTo(620));
      Assert.That(EditorJsonUtility.ToJson(second), Is.EqualTo(secondBefore));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DuplicateIdentitiesOrOutputTargetsAreRejected(bool duplicateId)
    {
      var output = Save<MenuProfile>("Existing");
      var first = new ListMenuStyleRecipe { Output = output };
      var second = new SingleRingMenuStyleRecipe { AssetName = "Other" };
      if (duplicateId) second.Id = first.Id;
      else second.Output = output;
      config.Styles = new MenuStyleRecipe[] { first, second };

      var plan = MenuStyleAuthoring.Preview(config);

      Assert.That(plan.CanBuild, Is.False);
      Assert.That(string.Join("\n", plan.Diagnostics), Does.Contain("duplicate").IgnoreCase);
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));
    }

    [Test]
    public void ACustomRecipeRequiresAnExplicitPresenterAndReportsTheStyleName()
    {
      config.Styles = new MenuStyleRecipe[] { new CustomMenuStyleRecipe { AssetName = "SocialCards" } };

      var plan = MenuStyleAuthoring.Preview(config);

      Assert.That(plan.CanBuild, Is.False);
      Assert.That(string.Join("\n", plan.Diagnostics), Does.Contain("SocialCards").And.Contain("presenter").IgnoreCase);
    }

    [Test]
    public void AddressableOnlyPresenterReferencesAreRetainedWithoutCreatingANativeReplacement()
    {
      var output = Save<MenuProfile>("AddressableOnly");
      var presenter = SavePresenter("AddressablePresenter");
      string guid = GuidOf(presenter);
      output.PresenterReference = new ContextMenuPrefabReference(guid);
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { Output = output } };

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(output.PresenterPrefab, Is.Null);
      Assert.That(output.PresenterReference.AssetGUID, Is.EqualTo(guid));
      Assert.That(config.GeneratedRadialPresenter, Is.Null);
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [Test]
    public void ASharedFallbackCannotPointBackToTheActualOutputBeingRebuilt()
    {
      var output = Save<MenuProfile>("Existing");
      common.FallbackProfile = output;
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { Output = output } };
      byte[] before = File.ReadAllBytes(AssetDatabase.GetAssetPath(output));

      var plan = MenuStyleAuthoring.Preview(config);

      Assert.That(plan.CanBuild, Is.False, "Validating a temporary clone must not hide a self-reference to the real output.");
      Assert.That(string.Join("\n", plan.Diagnostics), Does.Contain("FallbackProfile"));
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(output)), Is.EqualTo(before));
    }

    [Test]
    public void PerStyleInputAndWidgetOverridesTakePrecedenceWithoutMutatingTheSharedSettings()
    {
      var controls = Save<InputActionAsset>("Bindings");
      var actions = controls.AddActionMap("Menu");
      var shared = InputActionReference.Create(actions.AddAction("Shared", InputActionType.Button));
      var specialized = InputActionReference.Create(actions.AddAction("Specialized", InputActionType.Button));
      AssetDatabase.AddObjectToAsset(shared, controls);
      AssetDatabase.AddObjectToAsset(specialized, controls);
      common.InputBindings.Configure(shared, shared, shared, shared, shared, shared,
        shared, shared, shared, shared, new[] { shared }, routePointerClick: true);
      var first = Save<MenuProfile>("First");
      var second = Save<MenuProfile>("Second");
      var recipe = new SingleRingMenuStyleRecipe { Output = second, OverrideInputBindings = true };
      recipe.InputBindings.Configure(hold: specialized, secondary: new[] { specialized }, useNativeUguiNavigation: true);
      config.Styles = new MenuStyleRecipe[] { new SingleRingMenuStyleRecipe { Output = first }, recipe };
      var skin = Save<MenuSkin>("SpecializedSkin");
      recipe.Skin = skin;

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(first.InputBindings.Navigate, Is.SameAs(shared));
      Assert.That(first.InputBindings.Point, Is.SameAs(shared));
      Assert.That(first.InputBindings.Click, Is.SameAs(shared));
      Assert.That(first.InputBindings.Submit, Is.SameAs(shared));
      Assert.That(first.InputBindings.Back, Is.SameAs(shared));
      Assert.That(first.InputBindings.Cancel, Is.SameAs(shared));
      Assert.That(first.InputBindings.PageForward, Is.SameAs(shared));
      Assert.That(first.InputBindings.PageBackward, Is.SameAs(shared));
      Assert.That(first.InputBindings.SwitchRing, Is.SameAs(shared));
      Assert.That(first.InputBindings.Hold, Is.SameAs(shared));
      Assert.That(first.InputBindings.Secondary[0], Is.SameAs(shared));
      Assert.That(first.InputBindings.RoutePointerClick, Is.True);
      Assert.That(second.InputBindings.Hold, Is.SameAs(specialized));
      Assert.That(second.InputBindings.UseNativeUguiNavigation, Is.True);
      Assert.That(second.InputBindings.RoutePointerClick, Is.False);
      Assert.That(second.Skin, Is.SameAs(skin));
      Assert.That(common.InputBindings.Hold, Is.SameAs(shared));
      Assert.That(common.Skin, Is.Null);
    }

    [TestCase("config-meta")]
    [TestCase("common-meta")]
    [TestCase("config-directory")]
    public void WizardSourceCreationRefusesForeignMetadataAndDirectoryIdentity(string collision)
    {
      string sourcePath = root + "/NewStyles.asset";
      string blockedPath = collision == "common-meta"
        ? root + "/NewStyles_Common.asset.meta" : collision == "config-meta"
          ? sourcePath + ".meta" : sourcePath;
      byte[] before = null;
      if (collision == "config-directory") Directory.CreateDirectory(blockedPath);
      else
      {
        File.WriteAllText(blockedPath, "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n");
        before = File.ReadAllBytes(blockedPath);
      }

      Assert.Throws<InvalidOperationException>(() => MenuStyleWizard.CreateDefaults(sourcePath));

      Assert.That(File.Exists(sourcePath), Is.False, "The wizard must not adopt an orphan asset identity.");
      Assert.That(AssetDatabase.LoadAssetAtPath<MenuCommonSettings>(root + "/NewStyles_Common.asset"), Is.Null,
        "Preflight every source destination before creating the companion common settings.");
      if (before != null) Assert.That(File.ReadAllBytes(blockedPath), Is.EqualTo(before));
      else Assert.That(Directory.Exists(blockedPath), Is.True);
    }

    [Test]
    public void CenterVisibilityOverridePreservesInteractionNoneAlongsideTheSharedWheelCenter()
    {
      var wheel = Save<MenuProfile>("Wheel");
      var interaction = Save<MenuProfile>("Interaction");
      interaction.ShowCenter = false;
      common.ShowCenter = true;
      config.Styles = new MenuStyleRecipe[] {
        new SingleRingMenuStyleRecipe { Output = wheel },
        new ReplacementMenuStyleRecipe { Output = interaction, OverrideShowCenter = true, ShowCenter = false }
      };

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(wheel.ShowCenter, Is.True);
      Assert.That(interaction.ShowCenter, Is.False, "Rebuilding shared radial settings must retain a style's explicit None center.");
      Assert.That(common.ShowCenter, Is.True);
    }

    [Test]
    public void WizardDefaultsCreateExactlyTheFourCanonicalStyleNames()
    {
      var created = MenuStyleWizard.CreateDefaults(root + "/CanonicalStyles.asset", common);
      string[] expected = { "WheelContextMenu", "PieContextMenu", "ListContextMenu", "NestedWheelContextMenu" };

      Assert.That(created.Common, Is.SameAs(common));
      Assert.That(created.Styles.Length, Is.EqualTo(4), "One wheel implementation should have one default recipe.");
      Assert.That(created.Styles.Select(value => value.AssetName), Is.EquivalentTo(expected));
      Assert.That(created.Styles.Select(value => value.DisplayName), Is.EquivalentTo(expected));
    }

    private T Save<T>(string name) where T : ScriptableObject
    {
      var value = ScriptableObject.CreateInstance<T>();
      AssetDatabase.CreateAsset(value, root + "/" + name + ".asset");
      return value;
    }

    private GameObject SavePresenter(string name)
    {
      var value = new GameObject(name, typeof(RectTransform), typeof(ListMenuPresenter));
      transient.Add(value);
      return PrefabUtility.SaveAsPrefabAsset(value, root + "/" + name + ".prefab");
    }

    private static string GuidOf(Object value) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));

    private static void EnsureFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      string parent = path.Substring(0, path.LastIndexOf('/'));
      EnsureFolder(parent);
      AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
  }
}
