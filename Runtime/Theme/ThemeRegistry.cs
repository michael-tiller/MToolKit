using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Theme
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Theme/ThemeRegistry", fileName = "ThemeRegistry")]
  [InlineEditor]
  public class ThemeRegistry : ScriptableObject
  {
    [field: SerializeField]
    public Theme[] Themes { get; private set; }

    public Theme GetTheme(string id)
    {
      return Themes.FirstOrDefault(s => s.Id == id);
    }
  }
}