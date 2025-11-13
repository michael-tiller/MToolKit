// Navigation/Config/ViewConfig.cs

using System;
using MToolKit.Runtime.Navigation.Views;
using Sirenix.OdinInspector;

namespace MToolKit.Runtime.Navigation.Config
{
  [Serializable]
  public class ViewConfig
  {
    [Required]
    public View ViewPrefab;
  }
}