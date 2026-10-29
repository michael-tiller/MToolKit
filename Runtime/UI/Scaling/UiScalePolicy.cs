using System.Globalization;
using UnityEngine;

namespace MToolKit.Runtime.UI.Scaling
{
  /// <summary>Resolution scaling and user preference remain independent multipliers.</summary>
  public static class UiScalePolicy
  {
    public const float Minimum = 0.75f;
    public const float Maximum = 3f;
    public const float Default = 1f;

    public static float Normalize(float scale)
    {
      return float.IsNaN(scale) || float.IsInfinity(scale) ? Default : Mathf.Clamp(scale, Minimum, Maximum);
    }

    public static Vector2 ReferenceResolution(Vector2 authored, float scale) => authored / Normalize(scale);
    public static int Percentage(float scale) => Mathf.RoundToInt(Normalize(scale) * 100f);
    public static string Serialize(float scale) => Normalize(scale).ToString("R", CultureInfo.InvariantCulture);
  }
}
