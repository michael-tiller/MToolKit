using MToolKit.Runtime.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Theme.Swatch
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Swatch/Swatch", fileName = "NewSwatch")]
  public class Swatch : SemanticScriptableObject
  {
    [field: SerializeField]
    public ESwatchType Type { get; private set; }

    [field: SerializeField]
    [field: ShowIf(nameof(Type), ESwatchType.Color)]
    public Color Color { get; private set; } = Color.white;

    [field: SerializeField]
    [field: ShowIf(nameof(Type), ESwatchType.Gradient)]
    public Gradient Gradient { get; private set; } = new();

    /// <summary>
    /// Implicit projection to a single flat <see cref="UnityEngine.Color"/>, so a swatch can feed any
    /// Color API directly (e.g. <c>image.color = swatch</c>) without a preset or manual unwrap. A
    /// <see cref="ESwatchType.Color"/> swatch yields its <see cref="Color"/>; a
    /// <see cref="ESwatchType.Gradient"/> swatch yields its gradient sampled at t=0; a null swatch
    /// yields <see cref="UnityEngine.Color.white"/> (no tint) rather than throwing.
    /// </summary>
    public static implicit operator Color(Swatch swatch)
    {
      if (swatch == null) return Color.white;
      return swatch.Type == ESwatchType.Gradient ? swatch.Gradient.Evaluate(0f) : swatch.Color;
    }

    /// <summary>
    /// Implicit projection to a <see cref="UnityEngine.Gradient"/>, the mirror of the Color operator so a
    /// swatch can feed a Gradient API directly. A <see cref="ESwatchType.Gradient"/> swatch yields its
    /// gradient; a <see cref="ESwatchType.Color"/> (or null) swatch yields a flat two-stop gradient of that
    /// color (white for null). Provided for completeness — no swatch in-project is Gradient-typed today.
    /// </summary>
    public static implicit operator Gradient(Swatch swatch)
    {
      if (swatch != null && swatch.Type == ESwatchType.Gradient) return swatch.Gradient;
      return FlatGradient(swatch == null ? Color.white : swatch.Color);
    }

    /// <summary>Build a constant-color gradient (two matching color/alpha stops) from a flat color.</summary>
    private static Gradient FlatGradient(Color color)
    {
      var gradient = new Gradient();
      gradient.SetKeys(
        new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
        new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
      return gradient;
    }
  }
}