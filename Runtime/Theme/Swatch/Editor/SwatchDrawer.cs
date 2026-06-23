#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MToolKit.Theme.Swatch.Editor
{
  [CustomPropertyDrawer(typeof(Swatch))]
  public class SwatchDrawer : PropertyDrawer
  {
    private const float EYEDROPPER_W = 20f; // right slice of the ColorField left uncovered for its eyedropper
    private static readonly string colorBacking = $"<{nameof(Swatch.Color)}>k__BackingField";
    private static readonly string gradientBacking = $"<{nameof(Swatch.Gradient)}>k__BackingField";

    private static SwatchEditorConfig config;

    private static SwatchEditorConfig GetConfig()
    {
      return config != null ? config : config = SwatchEditorConfig.Find();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
      EditorGUI.BeginProperty(position, label, property);

      Rect content = EditorGUI.PrefixLabel(position, label);
      int indent = EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;

      Swatch swatch = property.objectReferenceValue as Swatch;
      if (swatch == null)
      {
        EditorGUI.ObjectField(content, property, GUIContent.none);
      }
      else
      {
        // [ icon (= color bar, opens picker) + eyedropper ]  [ SO field ]
        float swatchW = content.height + EYEDROPPER_W;
        Rect swatchRect = new(content.x, content.y, swatchW, content.height);
        Rect nameRect = new(swatchRect.xMax + 2f, content.y, content.xMax - swatchRect.xMax - 2f, content.height);

        DrawSwatch(swatchRect, swatch);
        EditorGUI.ObjectField(nameRect, property, GUIContent.none);
      }

      EditorGUI.indentLevel = indent;
      EditorGUI.EndProperty();
    }

    // Native ColorField (click -> modal picker, plus eyedropper) with the icon painted over the color bar.
    private static void DrawSwatch(Rect rect, Swatch swatch)
    {
      switch (swatch.Type)
      {
        case ESwatchType.Color:
          EditorGUI.BeginChangeCheck();
          Color picked = EditorGUI.ColorField(rect, GUIContent.none, swatch.Color, true, true, false);
          if (EditorGUI.EndChangeCheck())
            WriteColor(swatch, picked);
          // cover only the bar; leave the eyedropper slice on the right
          PaintIcon(new Rect(rect.x, rect.y, rect.width - EYEDROPPER_W, rect.height), swatch.Color);
          break;
        case ESwatchType.Gradient:
          EditField(rect, swatch, gradientBacking); // gradient bar, opens gradient editor (no eyedropper)
          break;
        default: // None
          PaintIcon(rect, new Color(0.25f, 0.25f, 0.25f));
          break;
      }
    }

    private static void PaintIcon(Rect rect, Color tint)
    {
      EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.5f)); // border
      Rect inner = new(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
      Texture2D icon = GetConfig() != null ? GetConfig().DefaultIcon : null;
      if (icon != null)
      {
        Color prev = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(inner, icon, ScaleMode.ScaleToFit, true);
        GUI.color = prev;
      }
      else
      {
        EditorGUI.DrawRect(inner, tint);
      }
    }

    private static void WriteColor(Swatch swatch, Color value)
    {
      SerializedObject so = new(swatch);
      so.FindProperty(colorBacking).colorValue = value;
      so.ApplyModifiedProperties();
    }

    // Draws the swatch's serialized field (Gradient backing) as a native, editable widget.
    private static void EditField(Rect rect, Swatch swatch, string backing)
    {
      SerializedObject so = new(swatch);
      so.Update();
      EditorGUI.PropertyField(rect, so.FindProperty(backing), GUIContent.none);
      so.ApplyModifiedProperties();
    }
  }
}

#endif