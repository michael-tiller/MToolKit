using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Theme.Spacing
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Spacing/Spacing", fileName = "NewSpacing")]
  [InlineEditor]
  public class Spacing : ScriptableObject
  {
    public string Id => name;

    // Maps 1:1 to Figma auto-layout: Padding == frame padding, Gap == spacing between items.
    [field: SerializeField]
    public RectOffset Padding { get; private set; } = new();

    [field: SerializeField]
    public float Gap { get; private set; }
  }
}