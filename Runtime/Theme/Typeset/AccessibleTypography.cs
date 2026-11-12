using MToolKit.Runtime.UI.Scaling;
using UnityEngine;

namespace MToolKit.Theme.Typeset
{
  /// <summary>Separates semantic readability policy from resolution and user scaling.</summary>
  public static class AccessibleTypography
  {
    public static float ResolveMinimum(float authoredMinimum, float authoredMaximum, float userScale) =>
      ResolveMinimum(authoredMinimum, authoredMaximum, userScale, 0);

    public static float ResolveMinimum(float authoredMinimum, float authoredMaximum, float userScale,
      float accessibleMinimum)
    {
      float baseline = Mathf.Max(0, authoredMinimum);
      if (float.IsNaN(accessibleMinimum) || float.IsInfinity(accessibleMinimum) || accessibleMinimum <= baseline)
        return baseline;
      float progress = Mathf.InverseLerp(UiScalePolicy.Default, UiScalePolicy.Maximum, UiScalePolicy.Normalize(userScale));
      return Mathf.Lerp(baseline, accessibleMinimum, progress);
    }

    /// <summary>
    /// Reaches the semantic accessibility floor at the supported viewport's endpoint.
    /// Ordinary and smaller scales retain their authored minimum; an invalid endpoint
    /// cannot establish an accessibility range. The four-argument policy retains its
    /// original maximum-user-scale endpoint for existing callers.
    /// </summary>
    public static float ResolveMinimum(float authoredMinimum, float authoredMaximum, float userScale,
      float accessibleMinimum, float accessibilityScale)
    {
      float baseline = Mathf.Max(0, authoredMinimum);
      if (float.IsNaN(accessibleMinimum) || float.IsInfinity(accessibleMinimum) || accessibleMinimum <= baseline ||
          float.IsNaN(accessibilityScale) || float.IsInfinity(accessibilityScale) || accessibilityScale <= UiScalePolicy.Default)
        return baseline;
      float normalized = UiScalePolicy.Normalize(userScale);
      if (normalized <= UiScalePolicy.Default) return baseline;
      float endpoint = Mathf.Min(UiScalePolicy.Maximum, accessibilityScale);
      float progress = Mathf.InverseLerp(UiScalePolicy.Default, endpoint, normalized);
      return Mathf.Lerp(baseline, accessibleMinimum, progress);
    }
  }
}
