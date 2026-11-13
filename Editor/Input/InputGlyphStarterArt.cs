using System;
using UnityEngine;

namespace MToolKit.Editor.Input
{
  /// <summary>
  /// Optional neutral starter artwork for editor authoring. The returned temporary sprites must be
  /// baked and disposed by the caller. No artwork generation is included in player assemblies.
  /// </summary>
  public static class InputGlyphStarterArt
  {
    public static Sprite Keycap(string control)
    {
      var texture = Canvas();
      Line(texture, 6, 8, 58, 8); Line(texture, 58, 8, 58, 56);
      Line(texture, 58, 56, 6, 56); Line(texture, 6, 56, 6, 8);
      switch (control)
      {
        case "enter":
          Line(texture, 45, 43, 45, 25); Line(texture, 45, 25, 19, 25);
          Line(texture, 19, 25, 28, 34); Line(texture, 19, 25, 28, 16);
          break;
        case "space":
          Line(texture, 17, 37, 17, 25); Line(texture, 17, 25, 47, 25); Line(texture, 47, 25, 47, 37);
          break;
        case "escape":
          Line(texture, 19, 44, 19, 27); Line(texture, 19, 44, 36, 44); Line(texture, 19, 44, 44, 19);
          break;
        case "tab":
          Line(texture, 16, 32, 43, 32); Line(texture, 43, 32, 34, 41);
          Line(texture, 43, 32, 34, 23); Line(texture, 48, 22, 48, 42);
          break;
        case "e":
          Line(texture, 22, 44, 22, 20); Line(texture, 22, 44, 43, 44);
          Line(texture, 22, 32, 39, 32); Line(texture, 22, 20, 43, 20);
          break;
        default:
          UnityEngine.Object.DestroyImmediate(texture);
          throw new ArgumentException("Supported starter keycaps: enter, space, escape, tab, e.", nameof(control));
      }
      return Finish(texture, "keyboard-" + control);
    }

    public static Sprite FacePosition(string control)
    {
      Vector2 selected;
      switch (control)
      {
        case "buttonSouth": selected = new Vector2(32, 18); break;
        case "buttonEast": selected = new Vector2(46, 32); break;
        case "buttonWest": selected = new Vector2(18, 32); break;
        case "buttonNorth": selected = new Vector2(32, 46); break;
        default: throw new ArgumentException("Expected one of the four logical face buttons.", nameof(control));
      }
      var texture = Canvas();
      Circle(texture, new Vector2(32, 32), 28, false);
      foreach (var center in new[] { new Vector2(32, 18), new Vector2(46, 32), new Vector2(18, 32), new Vector2(32, 46) })
        Circle(texture, center, 5, center == selected);
      return Finish(texture, "generic-" + control);
    }

    public static Sprite MouseButton(string control)
    {
      var texture = Canvas();
      // A neutral mouse silhouette with the selected button filled.
      for (int y = 7; y <= 57; y++)
      for (int x = 16; x <= 48; x++)
      {
        bool border = x <= 18 || x >= 46 || y <= 9 || y >= 55 || y == 35 || (x == 32 && y >= 35);
        bool selected = (control == "leftButton" && x >= 21 && x <= 28 && y >= 39 && y <= 51)
          || (control == "rightButton" && x >= 36 && x <= 43 && y >= 39 && y <= 51)
          || (control == "middleButton" && x >= 29 && x <= 35 && y >= 42 && y <= 49);
        if (border || selected) texture.SetPixel(x, y, Color.white);
      }
      if (control != "leftButton" && control != "rightButton" && control != "middleButton")
      {
        UnityEngine.Object.DestroyImmediate(texture);
        throw new ArgumentException("Expected a logical mouse button.", nameof(control));
      }
      return Finish(texture, "mouse-" + control);
    }

    private static Texture2D Canvas()
    {
      var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
      texture.SetPixels(new Color[64 * 64]);
      return texture;
    }

    private static Sprite Finish(Texture2D texture, string name)
    {
      texture.Apply(false, false);
      var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
      sprite.name = name;
      return sprite;
    }

    private static void Circle(Texture2D texture, Vector2 center, int radius, bool filled)
    {
      for (int y = 0; y < 64; y++)
      for (int x = 0; x < 64; x++)
      {
        float distance = Vector2.Distance(new Vector2(x, y), center);
        if (distance <= radius && (filled || distance >= radius - 2)) texture.SetPixel(x, y, Color.white);
      }
    }

    private static void Line(Texture2D texture, int x0, int y0, int x1, int y1)
    {
      int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
      for (int step = 0; step <= steps; step++)
      {
        float t = steps == 0 ? 0 : (float)step / steps;
        int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
        int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
          texture.SetPixel(x + dx, y + dy, Color.white);
      }
    }
  }
}
