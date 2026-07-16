using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Theme.Swatch
{
  [RequireComponent(typeof(Graphic))]
  public class SwatchPreset : ThemePreset<Swatch>
  {
    [field: SerializeField]
    [field: Required]
    public Graphic Graphic { get; private set; }

    protected override void AutoWire()
    {
      Graphic = GetComponent<Graphic>();
    }

    protected override Swatch Resolve(Theme theme, string id)
    {
      return theme.SwatchRegistry.GetSwatch(id);
    }

    protected override void Apply(Swatch swatch)
    {
      if (Graphic == null || swatch == null) return;
      if (Graphic.color != swatch.Color) Graphic.color = swatch.Color;
    }
  }
}