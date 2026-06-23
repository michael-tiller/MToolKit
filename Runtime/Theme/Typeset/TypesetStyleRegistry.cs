using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Theme.Typeset
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Typeset/TypesetStyleRegistry", fileName = "TypesetStyleRegistry")]
  [InlineEditor]
  public class TypesetStyleRegistry : ScriptableObject
  {
    [field: SerializeField]
    public Typeset[] Typesets { get; private set; }

    public Typeset GetTypeset(string id)
    {
      return Typesets.FirstOrDefault(s => s.Id == id);
    }
  }
}