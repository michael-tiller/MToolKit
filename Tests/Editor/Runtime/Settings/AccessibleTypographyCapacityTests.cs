using System;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.Settings.BoundSettings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class AccessibleTypographyCapacityTests
  {
    [TestCase(.75f, 2f, 20f)]
    [TestCase(1f, 2f, 20f)]
    [TestCase(1.25f, 2f, 23.75f)]
    [TestCase(1.5f, 2f, 27.5f)]
    [TestCase(2f, 2f, 35f)]
    [TestCase(3f, 2f, 35f)]
    [TestCase(1.25f, 1.5f, 27.5f)]
    [TestCase(2f, 3f, 27.5f)]
    [TestCase(3f, 3f, 35f)]
    public void SupportedEndpoint_ReachesSemanticFloorWithoutChangingBaseline(
      float effective, float endpoint, float expected)
    {
      Assert.That(Resolve(20, 24, effective, 35, endpoint), Is.EqualTo(expected).Within(.0001f));
    }

    [TestCase(.75f, 20f)]
    [TestCase(1f, 20f)]
    [TestCase(2f, 22f)]
    [TestCase(3f, 24f)]
    public void ExistingFourArgumentPolicy_KeepsItsOriginalThreeTimesEndpoint(float scale, float expected)
    {
      var method = RuntimeType("MToolKit.Theme.Typeset.AccessibleTypography").GetMethod("ResolveMinimum",
        new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
      Assert.That(method, Is.Not.Null);
      Assert.That((float)method.Invoke(null, new object[] { 20f, 24f, scale, 24f }),
        Is.EqualTo(expected).Within(.0001f));
    }

    [TestCase(0f)]
    [TestCase(19f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void MissingOrInvalidSemanticFloor_PreservesAuthoredMinimum(float floor)
    {
      Assert.That(Resolve(20, 24, 2, floor, 2), Is.EqualTo(20));
    }

    [Test]
    public void CapacityChange_ReappliesFloorWhenEffectiveScaleDoesNotChange()
    {
      using var fixture = new Fixture(1.5f);
      Assert.That(fixture.Effective, Is.EqualTo(1.5f));
      Assert.That(fixture.Minimum, Is.EqualTo(27.5f).Within(.001f));
      fixture.SetMinimumViewport(new Vector2(640, 360));
      Assert.That(fixture.Effective, Is.EqualTo(1.5f));
      Assert.That(fixture.Minimum, Is.EqualTo(23.75f).Within(.001f),
        "The same effective factor can have a different accessibility endpoint after a viewport-policy change.");
      fixture.SetMinimumViewport(new Vector2(960, 540));
      Assert.That(fixture.Minimum, Is.EqualTo(27.5f).Within(.001f));
      fixture.Preference.Value = 1;
      Assert.That(fixture.Minimum, Is.EqualTo(20f));
      fixture.AssertAssetUnchanged();
    }

    [Test]
    public void ConstrainedRequestOnlyChange_PreservesResolvedAutoSize()
    {
      using var fixture = new Fixture(2f);
      Assert.That(fixture.Minimum, Is.EqualTo(35f).Within(.001f));
      // An auto-sized value inside the authored range must survive an unchanged typography policy.
      fixture.Text.GetType().GetProperty("fontSize").SetValue(fixture.Text, 40f);
      fixture.Preference.Value = 3f;
      Assert.That(fixture.Preference.Value, Is.EqualTo(3f));
      Assert.That(fixture.Effective, Is.EqualTo(2f).Within(.0001f));
      Assert.That(fixture.Minimum, Is.EqualTo(35f).Within(.001f));
      Assert.That((float)fixture.Text.GetType().GetProperty("fontSize").GetValue(fixture.Text),
        Is.EqualTo(40f), "A request above an unchanged capacity must not reset the resolved TMP size.");
      fixture.AssertAssetUnchanged();
    }

    [Test]
    public void HiddenPreset_ReappliesCurrentEffectiveFloorOnEnable()
    {
      using var fixture = new Fixture(1f);
      Assert.That(fixture.Minimum, Is.EqualTo(20f));
      fixture.SetPresetActive(false);
      fixture.Preference.Value = 3f;
      fixture.SetPresetActive(true);
      Assert.That(fixture.Effective, Is.EqualTo(2f).Within(.0001f));
      Assert.That(fixture.Minimum, Is.EqualTo(35f).Within(.001f));
      fixture.AssertAssetUnchanged();
    }

    private static float Resolve(float minimum, float maximum, float effective, float floor, float endpoint)
    {
      var method = RuntimeType("MToolKit.Theme.Typeset.AccessibleTypography").GetMethod("ResolveMinimum",
        new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) });
      Assert.That(method, Is.Not.Null,
        "The semantic typography policy must support the actual viewport's accessibility endpoint.");
      return (float)method.Invoke(null, new object[] { minimum, maximum, effective, floor, endpoint });
    }

    private static Type RuntimeType(string name)
    {
      var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
        .FirstOrDefault(value => value != null);
      Assert.That(type, Is.Not.Null, name);
      return type;
    }

    private sealed class Fixture : IDisposable
    {
      internal readonly ReactiveSetting<float> Preference;
      internal readonly Component Text;
      private readonly GameObject root;
      private readonly GameObject cameraOwner;
      private readonly RenderTexture texture;
      private readonly Component binding;
      private readonly Component preset;
      private readonly ScriptableObject asset;

      internal Fixture(float requested)
      {
        Preference = new ReactiveSetting<float>(requested, "UI Scale");
        cameraOwner = new GameObject("Owned typography capacity camera", typeof(Camera));
        texture = new RenderTexture(1920, 1080, 0);
        var camera = cameraOwner.GetComponent<Camera>();
        camera.targetTexture = texture;
        root = new GameObject("Owned typography capacity canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        binding = root.AddComponent(RuntimeType("MToolKit.Runtime.UI.Scaling.UserUiScaleCanvas"));
        SetMinimumViewport(new Vector2(960, 540));
        Canvas.ForceUpdateCanvases();
        binding.GetType().GetMethod("Configure").Invoke(binding, new object[] { Preference });
        asset = ScriptableObject.CreateInstance(RuntimeType("MToolKit.Theme.Typeset.Typeset"));
        using (var serialized = new SerializedObject(asset))
        {
          serialized.FindProperty("<AutoSizeFont>k__BackingField").boolValue = true;
          serialized.FindProperty("<FontSizeMin>k__BackingField").floatValue = 20;
          serialized.FindProperty("<FontSize>k__BackingField").floatValue = 48;
          serialized.FindProperty("<AccessibleFontSizeMin>k__BackingField").floatValue = 35;
          serialized.FindProperty("<Font>k__BackingField").objectReferenceValue =
            RuntimeType("TMPro.TMP_Settings").GetProperty("defaultFontAsset").GetValue(null) as Object;
          serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Text = new GameObject("Owned semantic text", typeof(RectTransform))
          .AddComponent(RuntimeType("TMPro.TextMeshProUGUI"));
        Text.transform.SetParent(root.transform, false);
        preset = Text.gameObject.AddComponent(RuntimeType("MToolKit.Theme.Typeset.TypesetPreset"));
        preset.GetType().GetMethod("SetStart").Invoke(preset, new object[] { asset });
      }

      internal float Effective => (float)binding.GetType().GetProperty("EffectiveUserScale").GetValue(binding);
      internal float Minimum => (float)Text.GetType().GetProperty("fontSizeMin").GetValue(Text);
      internal void SetMinimumViewport(Vector2 minimum) =>
        binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(binding, minimum);

      internal void SetPresetActive(bool enabled)
      {
        // EditMode does not dispatch ordinary runtime MonoBehaviour enable callbacks.
        if (!enabled) preset.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(preset, null);
        Text.gameObject.SetActive(enabled);
        if (enabled) preset.GetType().GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(preset, null);
      }

      internal void AssertAssetUnchanged()
      {
        using var serialized = new SerializedObject(asset);
        Assert.That(serialized.FindProperty("<FontSizeMin>k__BackingField").floatValue, Is.EqualTo(20));
        Assert.That(serialized.FindProperty("<FontSize>k__BackingField").floatValue, Is.EqualTo(48));
        Assert.That(serialized.FindProperty("<AccessibleFontSizeMin>k__BackingField").floatValue, Is.EqualTo(35));
      }

      public void Dispose()
      {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraOwner);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(asset);
        Preference.Dispose();
      }
    }
  }
}
