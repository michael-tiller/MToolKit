using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>Checks an Addressables key without starting a failing load operation.</summary>
  internal static class QuestObjectiveAddressableLocator
  {
    public static bool Exists(object key, System.Type assetType)
    {
      if (key == null)
        return false;

#if UNITY_EDITOR
      // EditMode tests intentionally use synthetic GUIDs. Never start an Addressables load for
      // those references; doing so emits an InvalidKeyException before the task can be observed.
      if (!UnityEngine.Application.isPlaying)
        return false;
#endif

      string keyText = key.ToString();
      foreach (IResourceLocator locator in Addressables.ResourceLocators)
        if (locator.Locate(key, assetType, out IList<IResourceLocation> locations) && locations != null)
          foreach (IResourceLocation location in locations)
            if (location != null &&
                (string.Equals(location.PrimaryKey, keyText, System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(location.InternalId, keyText, System.StringComparison.OrdinalIgnoreCase)))
              return true;

      return false;
    }
  }
}
