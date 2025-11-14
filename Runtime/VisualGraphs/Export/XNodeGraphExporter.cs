using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Dialogue.Graphs;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using UnityEngine;
using XNode;
using Object = UnityEngine.Object;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace MToolKit.Runtime.VisualGraphs.Export
{
  /// <summary>
  ///   Exports xNode authoring graphs to runtime graph definitions.
  ///   Validates graphs and extracts parameters using Odin serialization.
  /// </summary>
  public sealed class XNodeGraphExporter
  {
    private readonly NodeExecutorRegistry nodeRegistry;

    public XNodeGraphExporter(NodeExecutorRegistry nodeRegistry)
    {
      this.nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
    }

    /// <summary>
    ///   Export a NodeGraph to a runtime definition.
    /// </summary>
    public RuntimeGraphDefinition Export(NodeGraph graphAsset)
    {
      if (graphAsset == null)
        throw new ArgumentNullException(nameof(graphAsset));

      Validate(graphAsset);

      var def = new RuntimeGraphDefinition
      {
        GraphId = graphAsset.name,
        GraphDomain = DetectDomain(graphAsset),
        MaxExecutionSteps = 1024, // Default, overridden below if graph specifies
        Subscriptions = new List<RuntimeSubscriptionDefinition>(),
        Nodes = new List<RuntimeNodeDefinition>(),
        Connections = new List<RuntimeConnectionDefinition>()
      };

      // Extract per-graph configuration
      if (graphAsset is QuestGraphAsset questGraph)
      {
        // Copy execution limit
        def.MaxExecutionSteps = questGraph.MaxExecutionSteps;

        // Extract subscriptions from graph asset (explicit subscriptions)
        if (questGraph.Subscriptions != null)
        {
          foreach (var subscription in questGraph.Subscriptions)
          {
            if (subscription?.MessageType == null || !subscription.MessageType.IsValid)
              continue;

            def.Subscriptions.Add(new RuntimeSubscriptionDefinition
            {
              MessageType = subscription.MessageType,
              DomainFilter = subscription.DomainFilter,
              Required = subscription.Required
            });
          }
        }
      }
      else if (graphAsset is DialogueGraphAsset dialogueGraph)
      {
        // Copy execution limit
        def.MaxExecutionSteps = dialogueGraph.MaxExecutionSteps;

        // Extract subscriptions
        if (dialogueGraph.Subscriptions != null)
        {
          foreach (var subscription in dialogueGraph.Subscriptions)
          {
            if (subscription?.MessageType == null || !subscription.MessageType.IsValid)
              continue;

            def.Subscriptions.Add(new RuntimeSubscriptionDefinition
            {
              MessageType = subscription.MessageType,
              DomainFilter = subscription.DomainFilter,
              Required = subscription.Required
            });
          }
        }
      }

      // Extract nodes
      var nodesByType = new Dictionary<string, List<Node>>();
      foreach (var node in graphAsset.nodes)
      {
        if (node == null) continue;

        var runtimeNode = new RuntimeNodeDefinition
        {
          NodeId = GetNodeId(node),
          NodeType = node.GetType().Name,
          Parameters = ExtractParameters(node)
        };
        def.Nodes.Add(runtimeNode);
      }

      // Extract connections
      foreach (var node in graphAsset.nodes)
      {
        if (node == null) continue;

        foreach (var port in node.Ports)
        {
          if (!port.IsOutput) continue;

          foreach (var conn in port.GetConnections())
          {
            if (conn?.node == null) continue;

            def.Connections.Add(new RuntimeConnectionDefinition
            {
              FromNodeId = GetNodeId(node),
              ToNodeId = GetNodeId(conn.node),
              PortName = port.fieldName
            });
          }
        }
      }

      def.BuildLookupCaches();
      return def;
    }

    /// <summary>
    ///   Validate a graph before export.
    /// </summary>
    private void Validate(NodeGraph graph)
    {
      var errors = new List<string>();

      // Check for entry nodes
      var entryNodes = graph.nodes.OfType<EntryNodeBase>().ToList();
      if (!entryNodes.Any())
        errors.Add($"Graph '{graph.name}' has no entry node. Add a QuestOnEventNode, DialogueStartNode, or custom EntryNodeBase.");

      // Check node types and GUIDs
      foreach (var node in graph.nodes)
      {
        if (node == null)
        {
          errors.Add($"Graph '{graph.name}' contains a null node reference.");
          continue;
        }

        var nodeTypeName = node.GetType().Name;

        // Entry nodes don't need executors - they're subscription points, not execution nodes
        if (node is EntryNodeBase)
          continue;

        // Check if executor exists
        if (nodeRegistry != null && !nodeRegistry.HasExecutor(nodeTypeName))
          errors.Add($"No executor registered for node type '{nodeTypeName}' in graph '{graph.name}'. Register an IGraphNodeExecutor for this type.");

        // Check GUID presence
        if (node is VisualGraphNodeBase vgNode && string.IsNullOrEmpty(vgNode.Guid))
          errors.Add($"Node '{node.name}' (type: {nodeTypeName}) is missing GUID in graph '{graph.name}'.");

        // Validate asset references
        ValidateAssetReferences(node, graph.name, errors);
      }

      if (errors.Any())
        throw new InvalidGraphException($"Graph validation failed:\n{string.Join("\n", errors)}");
    }

    /// <summary>
    ///   Validate AssetReference fields in a node.
    /// </summary>
    private void ValidateAssetReferences(Node node, string graphName, List<string> errors)
    {
#if UNITY_ADDRESSABLES
      var type = node.GetType();
      var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

      foreach (var field in fields)
      {
        var value = field.GetValue(node);
        if (value == null) continue;

        // Check if field is an AssetReference
        if (!value.GetType().IsSubclassOf(typeof(AssetReference)))
          continue;

        var assetRef = value as AssetReference;

        // Validate AssetReference is assigned
        if (string.IsNullOrEmpty(assetRef.AssetGUID))
        {
          errors.Add($"Node '{node.name}' in graph '{graphName}' has unassigned AssetReference field '{field.Name}'");
          continue;
        }

        // Validate asset exists and is loadable
        if (!assetRef.RuntimeKeyIsValid())
        {
          errors.Add($"Node '{node.name}' in graph '{graphName}' has invalid AssetReference '{field.Name}' (GUID: {assetRef.AssetGUID}). Asset may be deleted or not marked as Addressable.");
        }
      }
#endif
    }

    /// <summary>
    ///   Detect graph domain from asset type.
    /// </summary>
    private string DetectDomain(NodeGraph graph)
    {
      if (graph is QuestGraphAsset) return "Quest";
      if (graph is DialogueGraphAsset) return "Dialogue";
      return string.Empty;
    }

    /// <summary>
    ///   Get stable node ID from node (uses GUID if available, otherwise falls back to node name).
    /// </summary>
    private string GetNodeId(Node node)
    {
      if (node is VisualGraphNodeBase vgNode && !string.IsNullOrEmpty(vgNode.Guid))
        return vgNode.Guid;
      return node.name;
    }

    /// <summary>
    ///   Extract parameters from node using reflection to get all serialized fields.
    /// </summary>
    private NodeParametersDictionary ExtractParameters(Node node)
    {
      var dict = new NodeParametersDictionary();

      try
      {
        // Get all serialized fields using reflection (public and private with SerializeField attribute)
        var type = node.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
          .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

        foreach (var field in fields)
          try
          {
            // Skip Unity internal fields
            if (field.Name.StartsWith("m_") && field.DeclaringType == typeof(Object))
              continue;

            var value = field.GetValue(node);

#if UNITY_ADDRESSABLES
            // Handle AssetReference types (they're NOT UnityEngine.Object)
            if (value != null && value.GetType().IsSubclassOf(typeof(AssetReference)))
            {
              var assetRef = value as AssetReference;
              var serialized = SerializableAssetReference.FromAssetReference(assetRef);
              
              if (serialized == null)
              {
                Debug.LogWarning($"Node '{node.name}' field '{field.Name}' has null or invalid AssetReference");
              }
              else if (!string.IsNullOrEmpty(serialized.AssetGuid) && !assetRef.RuntimeKeyIsValid())
              {
                Debug.LogWarning($"Node '{node.name}' field '{field.Name}' AssetReference (GUID: {serialized.AssetGuid}) is not a valid Addressable. Asset may not load at runtime.");
              }
              
              dict[field.Name] = serialized;
            }
            else
#endif
            // Normalize UnityEngine.Object references
            if (value is Object unityObj)
            {
              dict[field.Name] = NormalizeUnityObject(unityObj);
            }
            else
            {
              dict[field.Name] = value;
            }
          }
          catch (Exception ex)
          {
            Debug.LogWarning($"Failed to extract parameter '{field.Name}' from node '{node.name}': {ex.Message}");
          }
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to extract parameters from node '{node.name}': {ex}");
      }

      return dict;
    }

    /// <summary>
    ///   Normalize UnityEngine.Object references to IDs/keys/names for runtime serialization.
    ///   Handles GuidScriptableObject by extracting GUID for safe, type-safe references.
    /// </summary>
    private object NormalizeUnityObject(Object obj)
    {
      if (obj == null) return null;

      // Handle GuidScriptableObject - extract GUID for safe references
      if (obj is GuidScriptableObject guidObj)
      {
        if (string.IsNullOrEmpty(guidObj.Guid))
        {
          Debug.LogWarning($"GuidScriptableObject '{obj.name}' has empty GUID. This may cause runtime errors.");
        }
        return guidObj.Guid;
      }

      // For regular Unity objects, return the instance ID or name
      // These would need to be scene references or resources
      return new
      {
        Name = obj.name,
        InstanceId = obj.GetInstanceID(),
        Type = obj.GetType().Name
      };
    }
  }

  /// <summary>
  ///   Exception thrown when graph validation fails.
  /// </summary>
  public sealed class InvalidGraphException : Exception
  {
    public InvalidGraphException(string message) : base(message) { }
  }
}