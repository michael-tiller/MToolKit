#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Theme.Swatch.Editor
{
  /// Shared, editor-only config holding the default swatch-preview icon for ALL swatches.
  /// Create one via Assets > Create > Dirigible > Swatch Editor Config and wire DefaultIcon.
  [CreateAssetMenu(menuName = "Theme/Swatch/Swatch Editor Config", fileName = "SwatchEditorConfig")]
  public class SwatchEditorConfig : ScriptableObject
  {
    [field: SerializeField]
    public Texture2D DefaultIcon { get; private set; }

    /// First SwatchEditorConfig found anywhere in the project, or null if none exists.
    public static SwatchEditorConfig Find()
    {
      string[] guids = AssetDatabase.FindAssets($"t:{nameof(SwatchEditorConfig)}");
      return guids.Length == 0
        ? null
        : AssetDatabase.LoadAssetAtPath<SwatchEditorConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
  }
}

#endif