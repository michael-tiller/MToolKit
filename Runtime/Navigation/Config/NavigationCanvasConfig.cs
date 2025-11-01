// Navigation/Config/NavigationCanvasConfig.cs

using System.Linq;
using MToolKit.Runtime.Navigation.DataStructures;
using MToolKit.Runtime.Navigation.Enums;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Navigation.Config
{
  [CreateAssetMenu(fileName = "New NavigationCanvasConfig", menuName = "_MTools/Navigation/Navigation Canvas Config")]
  [InlineEditor]
  public class NavigationCanvasConfig : ScriptableObject
  {
    [ValidateInput(nameof(ValidateCanvasConfigsDict), "CanvasConfigDict must contain at least one entry and include a CanvasConfig with CanvasType == ECanvasType.Main.")]
    public CanvasConfigDict CanvasConfigDict = new();

    private bool ValidateCanvasConfigsDict(CanvasConfigDict configs)
    {
      if (configs == null || configs.Count == 0) return false;

      bool hasMainCanvas = false;
      for (int i = 0; i < configs.Count; i++)
      {
        ECanvasType configKey = configs.ElementAt(i).Key;
        if (CanvasConfigDict == null) return false;

        if (configKey == ECanvasType.Main) hasMainCanvas = true;
      }

      if (!hasMainCanvas) return false;

      return true;
    }
  }
}