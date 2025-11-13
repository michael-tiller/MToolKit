using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Authoring.Graphs;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Quest;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using UnityEngine;
using XNode;
using Object = UnityEngine.Object;

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
        Subscriptions = new List<RuntimeSubscriptionDefinition>(),
        Nodes = new List<RuntimeNodeDefinition>(),
        Connections = new List<RuntimeConnectionDefinition>()
      };

      // Extract nodes
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

        // Collect event subscriptions
        if (node is QuestOnEventNode questEventNode)
          def.Subscriptions.Add(new RuntimeSubscriptionDefinition
          {
            EventType = questEventNode.EventType,
            EventDomain = questEventNode.EventDomain
          });
        else if (node is IEventSubscribedNode subNode)
          def.Subscriptions.Add(new RuntimeSubscriptionDefinition
          {
            EventType = subNode.EventType,
            EventDomain = subNode.EventDomain
          });
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

        // Check if executor exists
        if (nodeRegistry != null && !nodeRegistry.HasExecutor(nodeTypeName))
          errors.Add($"No executor registered for node type '{nodeTypeName}' in graph '{graph.name}'. Register an IGraphNodeExecutor for this type.");

        // Check GUID presence
        if (node is VisualGraphNodeBase vgNode && string.IsNullOrEmpty(vgNode.Guid))
          errors.Add($"Node '{node.name}' (type: {nodeTypeName}) is missing GUID in graph '{graph.name}'.");
      }

      if (errors.Any())
        throw new InvalidGraphException($"Graph validation failed:\n{string.Join("\n", errors)}");
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

            // Normalize UnityEngine.Object references
            if (value is Object unityObj)
              dict[field.Name] = NormalizeUnityObject(unityObj);
            else
              dict[field.Name] = value;
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
    ///   Override or extend this method for project-specific addressable key extraction.
    /// </summary>
    private object NormalizeUnityObject(Object obj)
    {
      if (obj == null) return null;

      // TODO: Add addressable key extraction if needed
      // For now, use name as a simple identifier
      return obj.name;
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