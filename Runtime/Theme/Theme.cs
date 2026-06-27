using MToolKit.Runtime.Utilities;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Swatch;
using MToolKit.Theme.Typeset;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Theme
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Theme/Theme", fileName = "NewTheme")]
  public class Theme : SemanticScriptableObject
  {
    [field: SerializeField]
    [field: Required]
    public TypesetStyleRegistry TypesetStyleRegistry { get; private set; }

    [field: SerializeField]
    [field: Required]
    public SwatchRegistry SwatchRegistry { get; private set; }

    [field: SerializeField]
    [field: Required]
    public SpacingRegistry SpacingRegistry { get; private set; }
  }
}