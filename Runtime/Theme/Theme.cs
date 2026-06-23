using Sirenix.OdinInspector;
using Theme.Spacing;
using Theme.Swatch;
using Theme.Typeset;
using UnityEngine;

namespace Theme
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Theme/Theme", fileName = "NewTheme")]
  public class Theme : ScriptableObject
  {
    public string Id => name;

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