using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

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

    /// <summary>
    /// Matches CanvasScaler's Scale With Screen Size baseline using its rendering-display dimensions
    /// and the authored reference resolution. The available safe area is a separate capacity input.
    /// Invalid inputs return NaN so callers cannot mistake an unknown baseline for a valid viewport.
    /// </summary>
    public static float ResolutionFactor(Vector2 renderingPixels, Vector2 authoredReference,
      CanvasScaler.ScreenMatchMode mode, float match)
    {
      if (!PositiveFinite(renderingPixels.x) || !PositiveFinite(renderingPixels.y) ||
          !PositiveFinite(authoredReference.x) || !PositiveFinite(authoredReference.y)) return float.NaN;

      double width = (double)renderingPixels.x / authoredReference.x;
      double height = (double)renderingPixels.y / authoredReference.y;
      double factor;
      switch (mode)
      {
        case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
          if (float.IsNaN(match) || float.IsInfinity(match)) return float.NaN;
          double weight = Mathf.Clamp01(match);
          factor = Math.Exp(Math.Log(width) * (1 - weight) + Math.Log(height) * weight);
          break;
        case CanvasScaler.ScreenMatchMode.Expand:
          factor = Math.Min(width, height);
          break;
        case CanvasScaler.ScreenMatchMode.Shrink:
          factor = Math.Max(width, height);
          break;
        default:
          return float.NaN;
      }
      float result = (float)factor;
      return PositiveFinite(result) ? result : float.NaN;
    }

    private static bool PositiveFinite(float value) =>
      value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    public static int Percentage(float scale) => Mathf.RoundToInt(Normalize(scale) * 100f);
    public static string Serialize(float scale) => Normalize(scale).ToString("R", CultureInfo.InvariantCulture);
  }
}
