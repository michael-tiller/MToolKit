using System;
using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.ContextMenus.Hosting
{
  [Serializable]
  public sealed class NamedMenuProfile
  {
    [field: SerializeField] public string Name { get; set; }
    [field: SerializeField] public MenuProfile Profile { get; set; }
  }

  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Host Config", fileName = "ContextMenuHostConfig")]
  public sealed class MenuHostConfig : ScriptableObject
  {
    [field: SerializeField] public MenuProfile DefaultProfile { get; set; }
    [field: SerializeField] public NamedMenuProfile[] Profiles { get; set; } = Array.Empty<NamedMenuProfile>();
    public MenuProfile Resolve(string name)
    {
      if (string.IsNullOrEmpty(name)) return DefaultProfile;
      MenuProfile match = null;
      bool found = false;
      foreach (var item in Profiles ?? Array.Empty<NamedMenuProfile>())
      {
        if (item == null || !string.Equals(item.Name, name, StringComparison.Ordinal)) continue;
        if (found) throw new ArgumentException("Duplicate context-menu profile name: " + name);
        found = true; match = item.Profile;
      }
      return match != null ? match : throw new ArgumentException("Unknown context-menu profile: " + name);
    }
  }
}
