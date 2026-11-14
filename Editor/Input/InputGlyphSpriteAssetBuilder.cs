using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.Input
{
  /// <summary>Editor-only authoring of native TMP resources. Players only consume the saved assets.</summary>
  public static class InputGlyphSpriteAssetBuilder
  {
    public readonly struct NamedSprite
    {
      public NamedSprite(string name, Sprite sprite) { Name = name; Sprite = sprite; }
      public string Name { get; }
      public Sprite Sprite { get; }
    }

    /// <summary>
    /// Packs sprites into a native TMP sprite asset, material and PNG atlas. Source import settings
    /// are preserved. Generated Sprite subassets support Image consumers without retaining source art.
    /// Rebaking retains the asset/atlas GUIDs and refreshes the owned generated Sprite subassets.
    /// </summary>
    public static TMP_SpriteAsset Bake(string assetPath, IReadOnlyList<NamedSprite> sources)
    {
      ValidateDestination(assetPath);
      if (sources == null || sources.Count == 0)
        throw new ArgumentException("At least one named sprite is required.", nameof(sources));
      var ordered = sources.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
      var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var source in ordered)
      {
        if (source.Sprite == null || string.IsNullOrEmpty(source.Name)
            || !Regex.IsMatch(source.Name, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || !unique.Add(source.Name))
          throw new ArgumentException("Sprites require distinct semantic names containing lowercase letters, digits and hyphens.", nameof(sources));
      }

      string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
      EnsureFolder(directory);
      var previous = AssetDatabase.LoadMainAssetAtPath(assetPath);
      string atlasPath = Path.ChangeExtension(assetPath, ".png").Replace('\\', '/');
      var existingSprites = previous == null ? new Dictionary<string, Sprite>(StringComparer.Ordinal)
        : AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
      var shader = Shader.Find("TextMeshPro/Sprite");
      if (shader == null) throw new InvalidOperationException("Import TMP Essential Resources before authoring glyphs.");

      var textures = new List<Texture2D>();
      Texture2D atlas = null;
      try
      {
        foreach (var source in ordered) textures.Add(ReadPixels(source.Sprite));
        atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var packed = atlas.PackTextures(textures.ToArray(), 2, 4096, false);
        if (packed.Length != ordered.Length)
          throw new InvalidOperationException("The input glyphs did not fit into the atlas.");
        File.WriteAllBytes(PhysicalPath(atlasPath), atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

        var asset = previous as TMP_SpriteAsset;
        if (asset == null)
        {
          asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
          AssetDatabase.CreateAsset(asset, assetPath);
        }
        asset.name = Path.GetFileNameWithoutExtension(assetPath);
        // TMP exposes the version setter only to its own assembly; author its serialized field.
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.hashCode = TMP_TextUtilities.GetHashCode(asset.name);
        asset.faceInfo = new FaceInfo
        {
          familyName = asset.name, pointSize = 64, scale = 1, lineHeight = 64,
          ascentLine = 51.2f, capLine = 51.2f, meanLine = 32, baseline = 0, descentLine = -12.8f
        };
        asset.spriteSheet = savedAtlas;
        asset.spriteCharacterTable.Clear();
        asset.spriteGlyphTable.Clear();

        for (int i = 0; i < ordered.Length; i++)
        {
          var normalized = packed[i];
          var rect = new Rect(Mathf.RoundToInt(normalized.x * atlas.width), Mathf.RoundToInt(normalized.y * atlas.height),
            Mathf.RoundToInt(normalized.width * atlas.width), Mathf.RoundToInt(normalized.height * atlas.height));
          var sprite = Sprite.Create(savedAtlas, rect, new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
          sprite.name = ordered[i].Name;
          if (existingSprites.TryGetValue(sprite.name, out var retained))
          {
            // Copy native Sprite serialization while retaining its real persistent object/local file id.
            EditorUtility.CopySerialized(sprite, retained);
            Object.DestroyImmediate(sprite);
            sprite = retained;
            EditorUtility.SetDirty(sprite);
            existingSprites.Remove(sprite.name);
          }
          else AssetDatabase.AddObjectToAsset(sprite, asset);
          // Each glyph occupies one em, independent of the source artwork's pixel resolution.
          float scale = 64f / rect.height;
          var metrics = new GlyphMetrics(rect.width * scale, 64, 0, 51.2f, rect.width * scale);
          var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(rect), 1, 0, sprite);
          asset.spriteGlyphTable.Add(glyph);
          asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph)
          { name = ordered[i].Name, scale = 1 });
        }
        foreach (var removed in existingSprites.Values) Object.DestroyImmediate(removed, true);
        if (asset.material == null)
        {
          asset.material = new Material(shader) { name = asset.name + " Material" };
          AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        asset.material.shader = shader;
        asset.material.mainTexture = savedAtlas;
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset.material);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        TMPro_EventManager.ON_SPRITE_ASSET_PROPERTY_CHANGED(true, asset);
        return asset;
      }
      finally
      {
        foreach (var texture in textures) Object.DestroyImmediate(texture);
        if (atlas != null) Object.DestroyImmediate(atlas);
      }
    }

    /// <summary>Checks existing destination ownership without creating folders or writing assets.</summary>
    public static void ValidateDestination(string assetPath)
    {
      if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
          || !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
          || assetPath.IndexOf('\\') >= 0 || assetPath.Split('/').Any(part => part == ".." || part == "."))
        throw new ArgumentException("Choose a project Assets/.../*.asset destination.", nameof(assetPath));
      var previous = AssetDatabase.LoadMainAssetAtPath(assetPath);
      if (previous != null && !(previous is TMP_SpriteAsset))
        throw new InvalidOperationException("The destination already contains a different asset type: " + assetPath);
      if (previous is TMP_SpriteAsset existing && existing.material != null
          && AssetDatabase.GetAssetPath(existing.material) != assetPath)
        throw new InvalidOperationException("The TMP material is shared outside this generated asset. Choose a dedicated output: " + assetPath);
      string atlasPath = Path.ChangeExtension(assetPath, ".png").Replace('\\', '/');
      if (File.Exists(PhysicalPath(atlasPath)) && (!(previous is TMP_SpriteAsset owned)
          || owned.spriteSheet == null || AssetDatabase.GetAssetPath(owned.spriteSheet) != atlasPath))
        throw new InvalidOperationException("The atlas sidecar is existing artwork not owned by this TMP asset: " + atlasPath);
      if (Shader.Find("TextMeshPro/Sprite") == null)
        throw new InvalidOperationException("Import TMP Essential Resources before authoring glyphs.");
    }

    public static Sprite GetSprite(TMP_SpriteAsset asset, string semanticName)
    {
      int index = asset.GetSpriteIndexFromName(semanticName);
      if (index < 0) return null;
      uint glyphIndex = asset.spriteCharacterTable[index].glyphIndex;
      return asset.spriteGlyphTable.First(glyph => glyph.index == glyphIndex).sprite;
    }

    public static void EnsureFolder(string path)
    {
      if (string.IsNullOrEmpty(path) || (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
          || path.IndexOf('\\') >= 0
          || path.Split('/').Any(part => part == ".." || part == "."))
        throw new ArgumentException("Expected an Assets folder.", nameof(path));
      string current = "Assets";
      foreach (string part in path.Split('/').Skip(1))
      {
        string child = current + "/" + part;
        if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, part);
        current = child;
      }
    }

    private static Texture2D ReadPixels(Sprite sprite)
    {
      Texture2D source = sprite.texture;
      Texture2D readable = null;
      try
      {
        if (!source.isReadable)
        {
          string path = PhysicalPath(AssetDatabase.GetAssetPath(source));
          readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
          if (string.IsNullOrEmpty(path) || !File.Exists(path) || !readable.LoadImage(File.ReadAllBytes(path), false))
            throw new InvalidOperationException("Supply a readable sprite or PNG/JPG source asset: " + sprite.name);
          source = readable;
        }
        var rect = sprite.rect;
        var copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        copy.SetPixels(source.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
        copy.Apply(false, false);
        return copy;
      }
      finally
      {
        if (readable != null) Object.DestroyImmediate(readable);
      }
    }

    private static string PhysicalPath(string assetPath)
    {
      if (string.IsNullOrEmpty(assetPath)) return "";
      if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
      {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
        if (package != null)
          return Path.Combine(package.resolvedPath, assetPath.Substring(("Packages/" + package.name).Length).TrimStart('/'));
      }
      return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", assetPath));
    }
  }
}
