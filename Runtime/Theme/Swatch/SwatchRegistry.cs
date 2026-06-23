using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Theme.Swatch
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Swatch/SwatchRegistry", fileName = "SwatchRegistry")]
  [InlineEditor]
  public class SwatchRegistry : ScriptableObject
  {
    [field: SerializeField]
    public Swatch[] Swatches { get; private set; }

    public Swatch GetSwatch(string id)
    {
      return Swatches.FirstOrDefault(s => s.Id == id);
    }
  }
}