using System;
using UnityEngine;

namespace MToolKit.Runtime.UI.Scaling
{
  /// <summary>One viewport's capacity; only Requested is a persistent player preference.</summary>
  public readonly struct UiScaleCapacityResult
  {
    public float Requested { get; }
    public float Effective { get; }
    public float MaximumSupported { get; }
    public bool HasValidViewport { get; }
    public bool SupportsMinimum { get; }
    public bool IsConstrained { get; }

    internal UiScaleCapacityResult(float requested, float effective, float maximumSupported,
      bool hasValidViewport, bool supportsMinimum, bool isConstrained)
    {
      Requested = requested;
      Effective = effective;
      MaximumSupported = maximumSupported;
      HasValidViewport = hasValidViewport;
      SupportsMinimum = supportsMinimum;
      IsConstrained = isConstrained;
    }
  }

  /// <summary>Pure capacity calculation in authored canvas logical units, before the user multiplier.</summary>
  public static class UiScaleCapacity
  {
    private const float ConstraintTolerance = .00001f;

    public static UiScaleCapacityResult Resolve(float requested, Vector2 availablePixels, float resolutionFactor,
      Vector2 minimumLogicalViewport)
    {
      requested = UiScalePolicy.Normalize(requested);
      if (!PositiveFinite(availablePixels.x) || !PositiveFinite(availablePixels.y) ||
          !PositiveFinite(resolutionFactor) || !PositiveFinite(minimumLogicalViewport.x) ||
          !PositiveFinite(minimumLogicalViewport.y)) return Invalid(requested);

      // Double intermediates avoid overflow in factor * minimum or in the division
      // when all supplied values are valid floats. Do not use the already-scaled canvas rect.
      double maximum = Math.Min((double)availablePixels.x / resolutionFactor / minimumLogicalViewport.x,
        (double)availablePixels.y / resolutionFactor / minimumLogicalViewport.y);
      float supported = (float)Math.Min(maximum, float.MaxValue);
      if (!PositiveFinite(supported)) return Invalid(requested);

      float effective = Math.Min(requested, supported);
      bool supportsMinimum = supported >= UiScalePolicy.Minimum;
      // Tolerance affects warning presentation only; application always respects the exact limit.
      bool constrained = !supportsMinimum || requested - supported > ConstraintTolerance;
      return new UiScaleCapacityResult(requested, effective, supported, true, supportsMinimum, constrained);
    }

    private static UiScaleCapacityResult Invalid(float requested) =>
      new(requested, UiScalePolicy.Minimum, 0, false, false, true);

    private static bool PositiveFinite(float value) =>
      value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
  }
}
