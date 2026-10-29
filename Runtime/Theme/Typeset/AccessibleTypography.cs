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
  }
}
