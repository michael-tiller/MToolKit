using MToolKit.Runtime.VisualGraphs.Dialogue.Graphs;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Definitions
{
  /// <summary>
  ///   Dialogue definition asset linking a dialogue ID to a graph and configuration.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Dialogue Definition", fileName = "DialogueDef", order = 301)]
  public sealed class DialogueDefinition : ScriptableObject
  {
    [BoxGroup("Identity")]
    [Tooltip("Unique dialogue identifier")]
    public string DialogueId = "dialogue_001";

    [BoxGroup("Graph")]
    [Required]
    [Tooltip("Dialogue graph asset to execute. Required - dialogues cannot function without a graph.")]
    public DialogueGraphAsset GraphAsset;

    [BoxGroup("Variables")]
    [InlineEditor]
    [Tooltip("Initial variables for this dialogue (applied after global, before save)")]
    public GraphVariableSet InitialVariables;

    [BoxGroup("Configuration")]
    [Tooltip("Start this dialogue when NPC is interacted with")]
    public bool StartOnInteract = true;

    [BoxGroup("Configuration")]
    [Tooltip("NPC identifier for this dialogue")]
    public string NpcId = "npc_001";

    [BoxGroup("Addressable")]
    [Tooltip("Optional addressable key for dynamic loading")]
    public string AddressableKey;

#if UNITY_EDITOR
    [BoxGroup("Graph")]
    [Button("Create & Link Dialogue Graph", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    [ShowIf("@GraphAsset == null")]
    [Tooltip("Links to existing DialogueGraphAsset if found, otherwise creates a new one in the same folder with '_Graph' suffix")]
    private void CreateAndLinkDialogueGraph()
    {
      var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
      if (string.IsNullOrEmpty(assetPath))
      {
        UnityEngine.Debug.LogError("Cannot create graph: Asset must be saved to disk first.");
        return;
      }

      var folderPath = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
      var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
      var graphName = $"{assetName}_Graph";
      var graphPath = $"{folderPath}/{graphName}.asset";

      // Check if graph already exists - if so, just link it
      var existingGraph = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueGraphAsset>(graphPath);
      if (existingGraph != null)
      {
        GraphAsset = existingGraph;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEngine.Debug.Log($"Linked to existing dialogue graph: {graphPath}");
        return;
      }

      // Create new graph asset
      var graphAsset = ScriptableObject.CreateInstance<DialogueGraphAsset>();
      graphAsset.name = graphName;

      UnityEditor.AssetDatabase.CreateAsset(graphAsset, graphPath);
      UnityEditor.AssetDatabase.SaveAssets();
      UnityEditor.AssetDatabase.Refresh();

      // Link it
      GraphAsset = graphAsset;
      UnityEditor.EditorUtility.SetDirty(this);

      UnityEngine.Debug.Log($"Created and linked dialogue graph: {graphPath}");
    }
#endif
  }
}