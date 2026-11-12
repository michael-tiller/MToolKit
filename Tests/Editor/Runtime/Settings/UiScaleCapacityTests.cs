using System;
using System.Reflection;
using MToolKit.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class UiScaleCapacityTests
  {
    private static readonly Vector2 MinimumViewport = new(1024, 768);

    private static Type ScalingType(string name)
    {
      var type = typeof(SettingsSystem).Assembly.GetType("MToolKit.Runtime.UI.Scaling." + name);
      Assert.That(type, Is.Not.Null, "Requested/effective scale policy belongs in MTK Runtime.");
      return type;
    }

    private static object Resolve(float requested, Vector2 available, float resolutionFactor,
      Vector2? minimum = null)
    {
      var method = ScalingType("UiScaleCapacity").GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
      Assert.That(method, Is.Not.Null);
      return method.Invoke(null, new object[] { requested, available, resolutionFactor, minimum ?? MinimumViewport });
    }

    private static T Value<T>(object result, string name)
    {
      var property = result.GetType().GetProperty(name);
      Assert.That(property, Is.Not.Null, "The capacity result must expose " + name + ".");
      return (T)property.GetValue(result);
    }

    private static float ResolutionFactor(Vector2 rendering, Vector2 reference,
      CanvasScaler.ScreenMatchMode mode, float match = .5f)
    {
      var method = ScalingType("UiScalePolicy").GetMethod("ResolutionFactor", BindingFlags.Public | BindingFlags.Static);
      Assert.That(method, Is.Not.Null, "Capacity and rendering must use the same authored scaler model.");
      return (float)method.Invoke(null, new object[] { rendering, reference, mode, match });
    }

    [TestCase(.75f)]
    [TestCase(1f)]
    [TestCase(1.25f)]
    [TestCase(1.5f)]
    [TestCase(2f)]
    [TestCase(2.5f)]
    [TestCase(3f)]
    public void SupportedRequests_ArePreservedWithoutConstraints(float requested)
    {
      var result = Resolve(requested, MinimumViewport * 4, 1);
      Assert.That(Value<float>(result, "Requested"), Is.EqualTo(requested));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(requested));
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(4));
      Assert.That(Value<bool>(result, "HasValidViewport"), Is.True);
      Assert.That(Value<bool>(result, "SupportsMinimum"), Is.True);
      Assert.That(Value<bool>(result, "IsConstrained"), Is.False);
    }

    [TestCase(1.39f, 1.39f, false)]
    [TestCase(1.4f, 1.4f, false)]
    [TestCase(1.41f, 1.4f, true)]
    [TestCase(2.5f, 1.4f, true)]
    public void RequestsBelowAtAndAboveCapacity_SeparateRequestedAndApplied(float requested, float expected,
      bool constrained)
    {
      var result = Resolve(requested, MinimumViewport * 1.4f, 1);
      Assert.That(Value<float>(result, "Requested"), Is.EqualTo(requested));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(expected).Within(.000001f));
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(1.4f).Within(.000001f));
      Assert.That(Value<bool>(result, "IsConstrained"), Is.EqualTo(constrained));
      Assert.That(Value<float>(result, "Effective"), Is.LessThanOrEqualTo(requested));
    }

    [TestCase(1280, 720, 2f / 3f)]
    [TestCase(1920, 1080, 1f)]
    [TestCase(2048, 1152, 16f / 15f)]
    [TestCase(3840, 2160, 2f)]
    public void ResolutionScaledSixteenByNine_ExposesTheSameLogicalCapacity(int width, int height,
      float expectedBaseline)
    {
      var rendering = new Vector2(width, height);
      float baseline = ResolutionFactor(rendering, new Vector2(1920, 1080),
        CanvasScaler.ScreenMatchMode.MatchWidthOrHeight);
      Assert.That(baseline, Is.EqualTo(expectedBaseline).Within(.000001f));
      var result = Resolve(3, rendering, baseline);
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(1.40625f).Within(.000002f),
        "Doubling physical resolution also doubles the preserved resolution baseline.");
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(1.40625f).Within(.000002f));
      Assert.That(Value<float>(result, "Requested"), Is.EqualTo(3));
      Assert.That(Value<bool>(result, "IsConstrained"), Is.True);
    }

    [TestCase(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0f, 2f)]
    [TestCase(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, .5f, 1f)]
    [TestCase(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 1f, .5f)]
    [TestCase(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, -1f, 2f)]
    [TestCase(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 2f, .5f)]
    [TestCase(CanvasScaler.ScreenMatchMode.Expand, .5f, .5f)]
    [TestCase(CanvasScaler.ScreenMatchMode.Shrink, .5f, 2f)]
    public void ResolutionFactor_MatchesUnityScaleWithScreenSize(CanvasScaler.ScreenMatchMode mode,
      float match, float expected)
    {
      Assert.That(ResolutionFactor(new Vector2(3840, 540), new Vector2(1920, 1080), mode, match),
        Is.EqualTo(expected).Within(.000001f));
    }

    [Test]
    public void SafeAreaAndRenderingDimensions_RemainSeparate()
    {
      float baseline = ResolutionFactor(new Vector2(3840, 2160), new Vector2(1920, 1080),
        CanvasScaler.ScreenMatchMode.MatchWidthOrHeight);
      var result = Resolve(2, new Vector2(3200, 1600), baseline);
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(1600f / 1536f).Within(.000001f));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(1600f / 1536f).Within(.000001f));
    }

    [Test]
    public void ConstantPixelBaseline_ConstrainsThenRestoresTheRetainedRequest()
    {
      const float request = 2f;
      var high = Resolve(request, new Vector2(3840, 2160), 1);
      var low = Resolve(request, new Vector2(1920, 1080), 1);
      var restored = Resolve(Value<float>(low, "Requested"), new Vector2(3840, 2160), 1);
      Assert.That(Value<float>(high, "Effective"), Is.EqualTo(2));
      Assert.That(Value<float>(low, "Effective"), Is.EqualTo(1.40625f));
      Assert.That(Value<float>(restored, "Effective"), Is.EqualTo(2));
      Assert.That(Value<bool>(restored, "IsConstrained"), Is.False);
    }

    [TestCase(.75f, true)]
    [TestCase(.5f, false)]
    [TestCase(.1f, false)]
    public void InsufficientMinimum_UsesActualSupportedFallback(float capacity, bool supportsMinimum)
    {
      var result = Resolve(3, MinimumViewport * capacity, 1);
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(capacity).Within(.000001f));
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(capacity).Within(.000001f));
      Assert.That(Value<bool>(result, "HasValidViewport"), Is.True);
      Assert.That(Value<bool>(result, "SupportsMinimum"), Is.EqualTo(supportsMinimum));
      Assert.That(Value<bool>(result, "IsConstrained"), Is.True);
    }

    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(float.NegativeInfinity, 1f)]
    [TestCase(-100f, .75f)]
    [TestCase(100f, 3f)]
    public void InvalidRequestedPreference_IsNormalizedIndependently(float supplied, float normalized)
    {
      var result = Resolve(supplied, MinimumViewport * 4, 1);
      Assert.That(Value<float>(result, "Requested"), Is.EqualTo(normalized));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(normalized));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void InvalidViewportOrFactor_IsExplicitAndFinite(float invalid)
    {
      AssertInvalid(Resolve(3, new Vector2(invalid, 1080), 1));
      AssertInvalid(Resolve(3, new Vector2(1920, invalid), 1));
      AssertInvalid(Resolve(3, new Vector2(1920, 1080), invalid));
      AssertInvalid(Resolve(3, new Vector2(1920, 1080), 1, new Vector2(invalid, 768)));
      AssertInvalid(Resolve(3, new Vector2(1920, 1080), 1, new Vector2(1024, invalid)));
    }

    private static void AssertInvalid(object result)
    {
      Assert.That(Value<float>(result, "Requested"), Is.EqualTo(3));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(.75f));
      Assert.That(Value<float>(result, "MaximumSupported"), Is.Zero);
      Assert.That(Value<bool>(result, "HasValidViewport"), Is.False);
      Assert.That(Value<bool>(result, "SupportsMinimum"), Is.False);
      Assert.That(Value<bool>(result, "IsConstrained"), Is.True,
        "An unknown viewport must never be presented as satisfying layout requirements.");
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidRenderingOrReference_DoesNotReturnAValidBaseline(float invalid)
    {
      Assert.That(float.IsNaN(ResolutionFactor(new Vector2(invalid, 1080), new Vector2(1920, 1080),
        CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)), Is.True);
      Assert.That(float.IsNaN(ResolutionFactor(new Vector2(1920, 1080), new Vector2(1920, invalid),
        CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)), Is.True);
    }

    [Test]
    public void PrecisionBoundaries_NeverApplyMoreThanTheRequestOrKnownCapacity()
    {
      const float capacity = 1.40625f;
      foreach (float requested in new[] { capacity - .000001f, capacity, capacity + .000001f })
      {
        var result = Resolve(requested, new Vector2(1920, 1080), 1);
        Assert.That(Value<float>(result, "Effective"), Is.LessThanOrEqualTo(requested));
        Assert.That(Value<float>(result, "Effective"), Is.LessThanOrEqualTo(capacity));
        Assert.That(Value<float>(result, "Effective"), Is.EqualTo(Mathf.Min(requested, capacity)));
      }
    }

    [Test]
    public void FiniteExtremeInputs_DoNotOverflowTheIntermediateDenominator()
    {
      var result = Resolve(3, new Vector2(float.MaxValue, float.MaxValue), float.MaxValue);
      Assert.That(Value<bool>(result, "HasValidViewport"), Is.True);
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(1f / 1024).Within(.000000001f));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(1f / 1024).Within(.000000001f));
    }

    [Test]
    public void ExtremelySmallPositiveFactor_KeepsAnUnconstrainedFiniteResult()
    {
      var result = Resolve(3, new Vector2(float.MaxValue, float.MaxValue), float.Epsilon);
      Assert.That(Value<bool>(result, "HasValidViewport"), Is.True);
      Assert.That(Value<float>(result, "MaximumSupported"), Is.EqualTo(float.MaxValue));
      Assert.That(Value<float>(result, "Effective"), Is.EqualTo(3));
      Assert.That(Value<bool>(result, "IsConstrained"), Is.False);
    }

    [Test]
    public void UnrepresentableCapacity_DoesNotReturnZeroAsAnAppliedMultiplier()
    {
      AssertInvalid(Resolve(3, new Vector2(float.Epsilon, float.Epsilon), float.MaxValue));
    }
  }
}
