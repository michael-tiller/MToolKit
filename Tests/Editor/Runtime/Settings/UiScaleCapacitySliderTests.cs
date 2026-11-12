using System;
using System.Globalization;
using System.Linq;
using MToolKit.Runtime.Settings.BoundSettings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class UiScaleCapacitySliderTests
  {
    [Test]
    public void NativeKeyboardAndControllerMove_PreserveOnePercentRequestedStepsAboveCapacity()
    {
      using var fixture = new Fixture();
      fixture.Slider.value = 250;
      fixture.Slider.OnMove(new UnityEngine.EventSystems.AxisEventData(null)
        { moveDir = UnityEngine.EventSystems.MoveDirection.Right });
      Assert.That(fixture.Slider.value, Is.EqualTo(251));
      Assert.That(fixture.Preference.Value, Is.EqualTo(2.51f));
      Assert.That(fixture.Effective, Is.EqualTo(1.4f).Within(.001f));
      StringAssert.Contains("140%", fixture.Text(fixture.Warning));
    }

    [TestCase("fr-FR", "140,6%")]
    [TestCase("en-US", "140.6%")]
    public void AppliedPercentage_RemainsNumericForLocaleFormatting(string culture, string expected)
    {
      var previous = System.Globalization.CultureInfo.CurrentCulture;
      try
      {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        using var fixture = new Fixture();
        fixture.Binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(fixture.Binding, new Vector2(1024, 768));
        fixture.Slider.value = 300;
        StringAssert.Contains(expected, fixture.Text(fixture.Warning));
      }
      finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    public void ConstrainedRequest_KeepsThumbAndPreference_AndReportsAppliedPercentage()
    {
      using var fixture = new Fixture();
      fixture.Slider.value = 250;
      Assert.That(fixture.Preference.Value, Is.EqualTo(2.5f));
      Assert.That(fixture.Preference.LastValue, Is.EqualTo(1f), "Live movement must remain a draft.");
      Assert.That(fixture.Slider.value, Is.EqualTo(250));
      Assert.That(fixture.Effective, Is.EqualTo(1.4f).Within(.001f));
      Assert.That(fixture.Text(fixture.Percentage), Is.EqualTo("250%"));
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.True);
      StringAssert.Contains("140%", fixture.Text(fixture.Warning));
      StringAssert.Contains("available UI layout area", fixture.Text(fixture.Warning));
      Assert.That(fixture.Warning.gameObject.activeSelf, Is.True,
        "Hiding a warning must not collapse its reserved layout or move the slider.");

      fixture.Slider.value = 100;
      Assert.That(fixture.Effective, Is.EqualTo(1f).Within(.001f));
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.False);
      Assert.That(fixture.Warning.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void ViewportChanges_RestoreRequestedScaleWithoutNewPreferenceWrites()
    {
      using var fixture = new Fixture();
      fixture.Slider.value = 250;
      int notifications = 0;
      fixture.Slider.onValueChanged.AddListener(_ => notifications++);
      fixture.Binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(
        fixture.Binding, new Vector2(512, 300));
      Assert.That(fixture.Effective, Is.EqualTo(2.5f).Within(.001f));
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.False);
      Assert.That(fixture.Slider.value, Is.EqualTo(250));
      Assert.That(notifications, Is.Zero, "Canvas feedback must not feed a new slider change back into settings.");
      Assert.That(fixture.Preference.LastValue, Is.EqualTo(1f));

      fixture.Binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(
        fixture.Binding, Fixture.MinimumViewport);
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.True);
      StringAssert.Contains("140%", fixture.Text(fixture.Warning));
      Assert.That(fixture.Preference.Value, Is.EqualTo(2.5f));
      Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void RebindingAndDisabling_DetachOldPreferenceAndCanvasFeedback()
    {
      using var fixture = new Fixture();
      fixture.Slider.value = 250;
      using var replacement = new ReactiveSetting<float>(1f, "UI Scale");
      fixture.Control.GetType().GetMethod("Bind").Invoke(fixture.Control, new object[] { replacement });
      fixture.Preference.Value = 3f;
      Assert.That(fixture.Slider.value, Is.EqualTo(100));
      ((Behaviour)fixture.Control).enabled = false;
      // These runtime-only behaviours deliberately do not execute lifecycle callbacks in Edit Mode.
      fixture.Control.GetType().GetMethod("OnDisable", System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic).Invoke(fixture.Control, null);
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.False);
      fixture.Binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(
        fixture.Binding, new Vector2(512, 300));
      replacement.Value = 2f;
      Assert.That(fixture.Slider.value, Is.EqualTo(100), "Disabled controls must stop observing setting changes.");
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.False);
      ((Behaviour)fixture.Control).enabled = true;
      fixture.Control.GetType().GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic).Invoke(fixture.Control, null);
      Assert.That(fixture.Slider.value, Is.EqualTo(200));
    }

    [Test]
    public void LateCanvasBinding_UpdatesExistingSliderWithoutMovingItsRequestedPosition()
    {
      using var fixture = new Fixture(bindCanvas: false);
      fixture.Slider.value = 250;
      fixture.BindCanvas();
      Assert.That(fixture.Slider.value, Is.EqualTo(250));
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.True);
      StringAssert.Contains("140%", fixture.Text(fixture.Warning));
    }

    [Test]
    public void InsufficientViewport_KeepsWarningVisibleEvenAtMinimumRequestedScale()
    {
      using var fixture = new Fixture();
      fixture.Binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(
        fixture.Binding, new Vector2(10000, 10000));
      fixture.Slider.value = 75;
      Assert.That(fixture.Slider.value, Is.EqualTo(75));
      Assert.That(fixture.Preference.Value, Is.EqualTo(.75f));
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.True,
        "A below-minimum viewport must not silently claim that its minimum requirement is satisfied.");
      StringAssert.Contains((fixture.Effective * 100).ToString("0.#", CultureInfo.CurrentCulture) + "%", fixture.Text(fixture.Warning));
      StringAssert.Contains("minimum", fixture.Text(fixture.Warning).ToLowerInvariant());
    }

    [Test]
    public void FractionalCapacity_DistinguishesAppliedValueFromAdjacentIntegerRequest()
    {
      using var fixture = new Fixture();
      fixture.Binding.GetType().GetProperty("MinimumLogicalViewport").SetValue(
        fixture.Binding, new Vector2(1024, 768));
      fixture.Slider.value = 141;
      Assert.That(fixture.Slider.value, Is.EqualTo(141));
      Assert.That(fixture.Text(fixture.Percentage), Is.EqualTo("141%"));
      Assert.That(fixture.Effective, Is.EqualTo(1.40625f).Within(.00001f));
      Assert.That(((Behaviour)fixture.Warning).enabled, Is.True);
      StringAssert.Contains(140.625f.ToString("0.#", CultureInfo.CurrentCulture) + "%", fixture.Text(fixture.Warning));
      StringAssert.DoesNotContain("141%", fixture.Text(fixture.Warning));
    }

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
      .Select(assembly => assembly.GetType("MToolKit.Runtime.UI.Scaling." + name)).FirstOrDefault(type => type != null);

    private sealed class Fixture : IDisposable
    {
      internal static readonly Vector2 MinimumViewport = new(1024, 1080 / 1.4f);
      internal readonly ReactiveSetting<float> Preference = new(1f, "UI Scale");
      internal readonly Slider Slider;
      internal readonly Component Percentage;
      internal readonly Component Warning;
      internal readonly Component Control;
      internal Component Binding { get; private set; }
      private readonly GameObject root;
      private readonly GameObject cameraObject;
      private readonly RenderTexture texture;

      internal Fixture(bool bindCanvas = true)
      {
        Assert.That(RuntimeType("UserUiScaleCanvas")?.GetProperty("Capacity"), Is.Not.Null,
          "Requested/effective capacity feedback must be available from the existing scale component.");
        Type controlType = RuntimeType("UserUiScaleSlider");
        Assert.That(controlType?.GetMethod("ConfigureFeedback"), Is.Not.Null,
          "The existing settings control must bind its inline warning.");
        Type textType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("TMPro.TextMeshProUGUI"))
          .First(type => type != null);
        cameraObject = new GameObject("Owned capacity slider camera", typeof(Camera));
        texture = new RenderTexture(1920, 1080, 0);
        var camera = cameraObject.GetComponent<Camera>();
        camera.targetTexture = texture;
        root = new GameObject("Owned capacity slider canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var row = new GameObject("UI scale row", typeof(RectTransform), typeof(Slider));
        row.transform.SetParent(root.transform, false);
        Slider = row.GetComponent<Slider>();
        Percentage = new GameObject("Requested percentage", typeof(RectTransform)).AddComponent(textType);
        Percentage.transform.SetParent(row.transform, false);
        Warning = new GameObject("Inline warning", typeof(RectTransform), typeof(LayoutElement)).AddComponent(textType);
        Warning.transform.SetParent(root.transform, false);
        Warning.GetComponent<LayoutElement>().minHeight = 80;
        Control = row.AddComponent(controlType);
        controlType.GetMethod("ConfigureFeedback").Invoke(Control, new object[] { Warning });
        controlType.GetMethod("Configure").Invoke(Control, new object[] { Slider, Percentage, Preference });
        Canvas.ForceUpdateCanvases();
        if (bindCanvas) BindCanvas();
      }

      internal void BindCanvas()
      {
        Type bindingType = RuntimeType("UserUiScaleCanvas");
        Binding = root.AddComponent(bindingType);
        bindingType.GetProperty("MinimumLogicalViewport").SetValue(Binding, MinimumViewport);
        bindingType.GetMethod("Configure").Invoke(Binding, new object[] { Preference });
      }

      internal float Effective => (float)Binding.GetType().GetProperty("EffectiveUserScale").GetValue(Binding);
      internal string Text(Component text) => (string)text.GetType().GetProperty("text").GetValue(text);

      public void Dispose()
      {
        if (Control != null) Control.GetType().GetMethod("Bind").Invoke(Control, new object[] { null });
        if (root != null) Object.DestroyImmediate(root);
        if (cameraObject != null) Object.DestroyImmediate(cameraObject);
        if (texture != null) Object.DestroyImmediate(texture);
        Preference.Dispose();
      }
    }
  }
}
