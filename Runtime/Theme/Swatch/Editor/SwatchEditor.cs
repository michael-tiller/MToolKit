#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Theme.Swatch.Editor
{
  [CustomEditor(typeof(Swatch), true)]
  [CanEditMultipleObjects]
  public class SwatchEditor : OdinEditor
  {
    // ponytail: shape lives in the icon's alpha; we tint RGB and keep alpha as the mask.
    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
      if (target is not Swatch swatch || swatch.Type == ESwatchType.None)
        return base.RenderStaticPreview(assetPath, subAssets, width, height);

      SwatchEditorConfig config = SwatchEditorConfig.Find();
      Texture2D icon = config != null ? config.DefaultIcon : null;
      if (icon == null)
        return base.RenderStaticPreview(assetPath, subAssets, width, height);

      // Icon is isReadable=0, so blit it (resized to the preview) through a RenderTexture to read its pixels.
      RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
      Graphics.Blit(icon, rt);
      RenderTexture prev = RenderTexture.active;
      RenderTexture.active = rt;

      Texture2D preview = new(width, height, TextureFormat.RGBA32, false);
      preview.ReadPixels(new Rect(0, 0, width, height), 0, 0);

      RenderTexture.active = prev;
      RenderTexture.ReleaseTemporary(rt);

      Color[] pixels = preview.GetPixels();
      for (int y = 0; y < height; y++)
      {
        Color tint = swatch.Type == ESwatchType.Gradient
          ? swatch.Gradient.Evaluate(height <= 1 ? 0f : (float)y / (height - 1))
          : swatch.Color;
        for (int x = 0; x < width; x++)
        {
          float mask = pixels[y * width + x].a;
          pixels[y * width + x] = new Color(tint.r, tint.g, tint.b, tint.a * mask);
        }
      }
      preview.SetPixels(pixels);
      preview.Apply();
      return preview;
    }
  }
}

#endif