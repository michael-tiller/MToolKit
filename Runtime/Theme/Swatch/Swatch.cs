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
  }
}