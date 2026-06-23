using Sirenix.OdinInspector;
using UnityEngine;

namespace Theme.Swatch
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Swatch/Swatch", fileName = "NewSwatch")]
  public class Swatch : ScriptableObject
  {
    public string Id => name;

    [field: SerializeField]
    public ESwatchType Type { get; private set; }

    [field: SerializeField]
    [field: ShowIf(nameof(Type), ESwatchType.Color)]
    public Color Color { get; private set; } = Color.white;

    [field: SerializeField]
    [field: ShowIf(nameof(Type), ESwatchType.Gradient)]
    public Gradient Gradient { get; private set; } = new();
  }
}