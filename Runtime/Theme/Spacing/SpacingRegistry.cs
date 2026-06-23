using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Theme.Spacing
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Spacing/SpacingRegistry", fileName = "SpacingRegistry")]
  [InlineEditor]
  public class SpacingRegistry : ScriptableObject
  {
    [field: SerializeField]
    public Spacing[] Spacings { get; private set; }

    public Spacing GetSpacing(string id)
    {
      return Spacings.FirstOrDefault(s => s.Id == id);
    }
  }
}