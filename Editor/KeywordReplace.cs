// Assets/Editor/KeywordReplace.cs

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor
{
    public class KeywordReplace : AssetModificationProcessor
    {
        public static void OnWillCreateAsset(string metaPath)
        {
            if (!metaPath.EndsWith(".meta")) return;

            string assetPath = metaPath.Substring(0, metaPath.Length - 5);
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            if (extension != ".cs" && extension != ".js" && extension != ".boo")
                return;

            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("Assets"));
            string fullAssetPath = Path.Combine(projectRoot, assetPath);
            string fullMetaPath = fullAssetPath + ".meta";

            if (!File.Exists(fullAssetPath)) return;

            string content = File.ReadAllText(fullAssetPath);
            string modified = content
                .Replace("#CREATIONDATE#", DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("#PROJECT#", Sanitize(PlayerSettings.productName))
                .Replace("#COMPANY#", Sanitize(PlayerSettings.companyName));

            if (extension == ".cs")
            {
                var dirs = assetPath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Split(Path.DirectorySeparatorChar)
                    .ToList();
                if (dirs.Contains("Source")) dirs.Remove("Source");
                if (dirs.Contains("Scripts")) dirs.Remove("Scripts");

                string rootNs = EditorSettings.projectGenerationRootNamespace;
                string genNs = dirs.Count() > 2
                    ? string.Join(".", dirs.Skip(1).Take(dirs.Count() - 2))
                    : "";

                string feature = dirs.Count() > 2
                    ? string.Join(".", dirs.Skip(3).Take(dirs.Count() - 4))
                    : "";
                string finalNs = string.IsNullOrEmpty(genNs) ? rootNs : rootNs + genNs;
                modified = modified.Replace("#NAMESPACE#", finalNs);

                if (string.IsNullOrWhiteSpace(feature))
                {
                    feature = "Unassigned";
                }
                
                modified = modified.Replace("#FEATURE#", feature);
            }

            if (modified != content)
            {
                File.WriteAllText(fullAssetPath, modified);
                AssetDatabase.Refresh();
            }
        }

        static string Sanitize(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(input
                .Where(c => !char.IsWhiteSpace(c) && !invalid.Contains(c))
                .ToArray());
        }
    }
}
#endif
