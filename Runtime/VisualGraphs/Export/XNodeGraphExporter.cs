using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Message;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.State;
using MToolKit.Runtime.VisualGraphs.Dialogue.Graphs;
using MToolKit.Runtime.VisualGraphs.Event.Graphs;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Quest.Nodes;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using UnityEngine;
using XNode;
using Object = UnityEngine.Object;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

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
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<XNodeGraphExporter>().ForFeature("VisualGraphs.Export"));
    private static ILogger log => logLazy.Value ?? Logger.None;

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
      else if (graphAsset is EventGraphAsset eventGraph)
      {
        // Copy execution limit
        def.MaxExecutionSteps = eventGraph.MaxExecutionSteps;

        // Extract subscriptions
        if (eventGraph.Subscriptions != null)
        {
          foreach (var subscription in eventGraph.Subscriptions)
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
      var nodeIdSet = new HashSet<string>();
      foreach (var node in graphAsset.nodes)
      {
        if (node == null) continue;

        var nodeId = GetNodeId(node);

        // Validate: Check for duplicate node IDs
        if (nodeIdSet.Contains(nodeId))
        {
          Debug.LogError($"Graph '{graphAsset.name}': Duplicate node ID '{nodeId}' found! Node '{node.name}' ({node.GetType().Name}) has the same ID as another node. This will cause connection issues. Please regenerate the GUID for this node in Unity.");
          continue; // Skip this node to prevent corruption
        }
        nodeIdSet.Add(nodeId);

        var runtimeNode = new RuntimeNodeDefinition
        {
          NodeId = nodeId,
          NodeType = node.GetType().Name,
          Parameters = ExtractParameters(node)
        };

        // Special handling for DialogueChoiceNode to properly serialize nested Choices
        if (node is Dialogue.Nodes.DialogueChoiceNode choiceNode)
        {
          ExtractChoiceNodeParameters(choiceNode, runtimeNode);
        }

        def.Nodes.Add(runtimeNode);
      }

      // Extract connections
      foreach (var node in graphAsset.nodes)
      {
        if (node == null) continue;

        // Special handling for DialogueChoiceNode to extract choice indices
        if (node is Dialogue.Nodes.DialogueChoiceNode choiceNode)
        {
          ExtractChoiceNodeConnections(choiceNode, def, graphAsset);
          continue;
        }

        // Standard connection extraction for other nodes
        var fromNodeId = GetNodeId(node);
        var fromNodeName = node.name;
        var fromNodeType = node.GetType().Name;

        foreach (var port in node.Ports)
        {
          if (!port.IsOutput) continue;

          var connections = port.GetConnections();
          log.Verbose("[Export] Node '{NodeName}' ({NodeType}, ID: {NodeId}) - Port '{PortName}' has {ConnectionCount} connection(s)",
            fromNodeName, fromNodeType, fromNodeId, port.fieldName, connections.Count);

          foreach (var conn in connections)
          {
            if (conn?.node == null)
            {
              Debug.LogWarning($"[Export] Node '{fromNodeName}' port '{port.fieldName}' has null connection");
              continue;
            }

            var toNodeId = GetNodeId(conn.node);
            var toNodeName = conn.node.name;
            var toNodeType = conn.node.GetType().Name;

            // Get text for DialogueLineNode for debugging
            var toNodeText = "";
            if (conn.node is Dialogue.Nodes.DialogueLineNode lineNode)
            {
              toNodeText = $" (Text: '{lineNode.Text}')";
            }

            log.Verbose("[Export] Connection: '{FromNode}' ({FromId}) -> '{ToNode}' ({ToId}) via port '{Port}'{Text}",
              fromNodeName, fromNodeId, toNodeName, toNodeId, port.fieldName, toNodeText);

            // Validate connection - warn about self-loops
            if (fromNodeId == toNodeId)
            {
              Debug.LogWarning($"Graph '{graphAsset.name}': Node '{node.name}' ({fromNodeId}) has a self-loop connection on port '{port.fieldName}'. This may cause infinite loops. Check the graph connections in Unity.");
              continue; // Skip self-loops
            }

            def.Connections.Add(new RuntimeConnectionDefinition
            {
              FromNodeId = fromNodeId,
              ToNodeId = toNodeId,
              PortName = port.fieldName
            });
          }
        }
      }

      def.BuildLookupCaches();
      return def;
    }

    /// <summary>
    ///   Extract and serialize Choices from a DialogueChoiceNode.
    ///   Handles nested Choice objects with LocalizedString fields that don't serialize well.
    /// </summary>
    private void ExtractChoiceNodeParameters(
      Dialogue.Nodes.DialogueChoiceNode choiceNode,
      RuntimeNodeDefinition runtimeNode)
    {
      var choices = choiceNode.Choices ?? new List<Dialogue.Nodes.DialogueChoiceNode.Choice>();
      var serializedChoices = new List<Dictionary<string, object>>();

      foreach (var choice in choices)
      {
        if (choice == null) continue;

        var choiceDict = new Dictionary<string, object>
        {
          ["Text"] = choice.Text ?? ""
        };
        serializedChoices.Add(choiceDict);
      }

      // Replace the Choices parameter with our serialized version
      runtimeNode.Parameters["Choices"] = serializedChoices;

      log.Verbose("[Export] DialogueChoiceNode '{NodeName}': Serialized {ChoiceCount} choices", choiceNode.name, serializedChoices.Count);
    }

    /// <summary>
    ///   Extract connections from a DialogueChoiceNode, storing choice index in port name.
    ///   This ensures reliable matching between choice selection and target nodes.
    /// </summary>
    private void ExtractChoiceNodeConnections(
      Dialogue.Nodes.DialogueChoiceNode choiceNode,
      RuntimeGraphDefinition def,
      NodeGraph graphAsset)
    {
      var nodeId = GetNodeId(choiceNode);
      var choices = choiceNode.Choices ?? new List<Dialogue.Nodes.DialogueChoiceNode.Choice>();

      // Iterate through choices and find their output ports
      // xNode's dynamicPortList creates ports named "ChoiceOutputs {index}" (e.g., "ChoiceOutputs 0", "ChoiceOutputs 1")
      for (var choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
      {
        // Look for ports matching xNode's dynamic port naming scheme: "ChoiceOutputs {index}"
        var expectedPortName = $"ChoiceOutputs {choiceIndex}";

        foreach (var port in choiceNode.Ports)
        {
          if (!port.IsOutput) continue;

          // Check if this port matches our expected name (exact match or starts with it)
          if (port.fieldName == expectedPortName || port.fieldName.StartsWith(expectedPortName + " "))
          {
            // Found the port for this choice - extract all connections
            foreach (var conn in port.GetConnections())
            {
              if (conn?.node == null) continue;

              // Store with a predictable port name format: "Choice_{index}"
              // This makes it easy to match in the executor
              def.Connections.Add(new RuntimeConnectionDefinition
              {
                FromNodeId = nodeId,
                ToNodeId = GetNodeId(conn.node),
                PortName = $"Choice_{choiceIndex}"
              });
            }
          }
        }
      }

      // Fallback: if we didn't find any connections using the pattern matching,
      // try to match by order (xNode maintains order for dynamic ports)
      // This handles edge cases where port naming might be different
      var connectionsForThisNode = def.Connections.Count(c => c.FromNodeId == nodeId);
      if (connectionsForThisNode == 0)
      {
        // No connections found with pattern matching - try order-based matching
        var allOutputPorts = choiceNode.Ports
          .Where(p => p.IsOutput)
          .OrderBy(p => p.fieldName) // Sort to ensure consistent ordering
          .ToList();

        if (allOutputPorts.Count > 0)
        {
          // Use connection order as fallback - assumes ports are in choice order
          var connectionIndex = 0;
          foreach (var port in allOutputPorts)
          {
            foreach (var conn in port.GetConnections())
            {
              if (conn?.node == null) continue;
              if (connectionIndex < choices.Count)
              {
                def.Connections.Add(new RuntimeConnectionDefinition
                {
                  FromNodeId = nodeId,
                  ToNodeId = GetNodeId(conn.node),
                  PortName = $"Choice_{connectionIndex}"
                });
                connectionIndex++;
              }
            }
          }
        }
      }
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

      ValidateVariableDeclarations(graph, GetDeclaredVariables(graph), errors);

      if (errors.Any())
        throw new InvalidGraphException($"Graph validation failed:\n{string.Join("\n", errors)}");
    }

    /// <summary>
    ///   The graph asset's optional declared-variables block, if its type carries one.
    /// </summary>
    private static GraphVariableSet GetDeclaredVariables(NodeGraph graph)
    {
      return graph switch
      {
        QuestGraphAsset quest => quest.DeclaredVariables,
        DialogueGraphAsset dialogue => dialogue.DeclaredVariables,
        EventGraphAsset evt => evt.DeclaredVariables,
        _ => null
      };
    }

    /// <summary>
    ///   Validate node state-key references against the graph's declared variables. Entirely skipped when no
    ///   declaration set is attached (opt-in — undeclared graphs export exactly as before). Unknown keys are
    ///   warnings (dynamic/mod keys stay legal at runtime); structural and type problems are errors.
    ///   All literal parsing uses the invariant culture (executors currently parse with the current culture —
    ///   tracked as technical debt).
    /// </summary>
    private static void ValidateVariableDeclarations(NodeGraph graph, GraphVariableSet set, List<string> errors)
    {
      if (set == null || set.entries == null || set.entries.Count == 0) return;

      var seenKeys = new HashSet<string>(StringComparer.Ordinal);
      foreach (var entry in set.entries)
      {
        if (entry == null) continue;

        if (string.IsNullOrWhiteSpace(entry.key))
        {
          errors.Add($"Graph '{graph.name}': declared variable with an empty key.");
          continue;
        }

        if (!Enum.IsDefined(typeof(EGraphVariableType), entry.type))
        {
          errors.Add($"Graph '{graph.name}': declared variable '{entry.key}' has out-of-range type value {(int)entry.type}.");
          continue;
        }

        if (!seenKeys.Add(entry.key))
          errors.Add($"Graph '{graph.name}': duplicate declared variable key '{entry.key}'.");
      }

      foreach (var node in graph.nodes)
      {
        switch (node)
        {
          case GenericStateSetNode setNode:
            ValidateSetNode(graph, set, setNode, errors);
            break;
          case GenericStateCheckNode checkNode:
            ValidateCheckNode(graph, set, checkNode, errors);
            break;
          case GenericStateGetNode getNode:
            ValidateGetNode(graph, set, getNode, errors);
            break;
          case MessageFieldGetNode fieldGetNode:
            WarnIfUndeclared(graph, set, fieldGetNode.StateKey, fieldGetNode.name);
            break;
          case QuestEmitEventNode emitNode:
            if (emitNode.Payload == null) break;
            foreach (var parameter in emitNode.Payload)
            {
              if (parameter != null && parameter.ValueType == ParameterValueType.Variable)
                WarnIfUndeclared(graph, set, parameter.VariableName, emitNode.name);
            }
            break;
        }
      }
    }

    /// <summary>
    ///   Unknown key = warning, not error: undeclared keys remain legal at runtime for dynamic/mod use.
    /// </summary>
    private static bool WarnIfUndeclared(NodeGraph graph, GraphVariableSet set, string key, string nodeName)
    {
      if (string.IsNullOrEmpty(key) || set.Find(key) != null) return false;

      Debug.LogWarning($"Graph '{graph.name}': node '{nodeName}' references undeclared state key '{key}'. " +
                       "Declare it in DeclaredVariables, or ignore if intentionally dynamic.");
      return true;
    }

    private static void ValidateSetNode(NodeGraph graph, GraphVariableSet set, GenericStateSetNode node, List<string> errors)
    {
      if (string.IsNullOrEmpty(node.StateKey)) return; // empty key is the existing runtime warning's job
      if (WarnIfUndeclared(graph, set, node.StateKey, node.name)) return;

      // The ValueType dropdown only offers these four; anything else is corrupted authoring data.
      if (!TryMapValueType(node.ValueType, out var nodeType))
      {
        errors.Add($"Graph '{graph.name}': node '{node.name}' has unknown ValueType '{node.ValueType}'.");
        return;
      }

      var declaration = set.Find(node.StateKey);
      if (declaration.type != nodeType)
      {
        errors.Add($"Graph '{graph.name}': node '{node.name}' writes '{node.StateKey}' as {nodeType} " +
                    $"but it is declared {declaration.type}.");
        return;
      }

      // The executor parses Value at runtime and only warns on failure — catch it at author time instead.
      // Empty Value mirrors the executor's default-to-"0" behavior and is always parseable.
      if (!string.IsNullOrEmpty(node.Value) && !LiteralParsesAs(node.Value, nodeType))
        errors.Add($"Graph '{graph.name}': node '{node.name}' Value '{node.Value}' does not parse as {nodeType} " +
                    $"for declared key '{node.StateKey}'.");
    }

    private static void ValidateCheckNode(NodeGraph graph, GraphVariableSet set, GenericStateCheckNode node, List<string> errors)
    {
      if (string.IsNullOrEmpty(node.StateKey)) return;
      if (WarnIfUndeclared(graph, set, node.StateKey, node.name)) return;

      var declaration = set.Find(node.StateKey);
      var isOrderingOperator = node.ComparisonOperator != "Equals" && node.ComparisonOperator != "NotEquals";

      switch (declaration.type)
      {
        case EGraphVariableType.Int:
        case EGraphVariableType.Float:
          if (!double.TryParse(node.ExpectedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            errors.Add($"Graph '{graph.name}': node '{node.name}' ExpectedValue '{node.ExpectedValue}' " +
                        $"is not numeric but '{node.StateKey}' is declared {declaration.type}.");
          break;

        case EGraphVariableType.Bool:
          // The executor compares via Convert.ToBoolean, which accepts only true/false — "1"/"0" throw at runtime
          // (the Set-node parser accepts them; the Check-node comparison does not).
          if (!bool.TryParse(node.ExpectedValue, out _))
            errors.Add($"Graph '{graph.name}': node '{node.name}' ExpectedValue '{node.ExpectedValue}' " +
                        $"must be 'true' or 'false' for bool key '{node.StateKey}'.");
          if (isOrderingOperator)
            errors.Add($"Graph '{graph.name}': node '{node.name}' uses ordering operator '{node.ComparisonOperator}' " +
                        $"on bool key '{node.StateKey}'.");
          break;

        case EGraphVariableType.Vector3:
        case EGraphVariableType.Vector2:
        case EGraphVariableType.Color:
          // The executor falls back to ToString comparison for structs — culture/precision-unstable. No struct
          // checks until typed literals/comparers exist.
          errors.Add($"Graph '{graph.name}': node '{node.name}' checks '{node.StateKey}' declared {declaration.type}; " +
                      "state checks do not support struct types.");
          break;
      }
    }

    private static void ValidateGetNode(NodeGraph graph, GraphVariableSet set, GenericStateGetNode node, List<string> errors)
    {
      var sourceDeclared = !string.IsNullOrEmpty(node.SourceStateKey) &&
                           !WarnIfUndeclared(graph, set, node.SourceStateKey, node.name) &&
                           set.Find(node.SourceStateKey) != null;
      var destinationDeclared = !string.IsNullOrEmpty(node.DestinationStateKey) &&
                                !WarnIfUndeclared(graph, set, node.DestinationStateKey, node.name) &&
                                set.Find(node.DestinationStateKey) != null;

      var sourceType = sourceDeclared ? set.Find(node.SourceStateKey).type : (EGraphVariableType?)null;
      var destinationType = destinationDeclared ? set.Find(node.DestinationStateKey).type : (EGraphVariableType?)null;

      if (sourceType.HasValue && destinationType.HasValue && sourceType.Value != destinationType.Value)
        errors.Add($"Graph '{graph.name}': node '{node.name}' copies '{node.SourceStateKey}' ({sourceType}) " +
                    $"into '{node.DestinationStateKey}' ({destinationType}).");

      // The default path writes the inferred default into the DESTINATION key, so the destination declaration
      // is what a bad default corrupts; fall back to the source declaration when only that is available.
      var defaultTarget = destinationType ?? sourceType;
      if (!defaultTarget.HasValue) return;

      if (defaultTarget.Value is EGraphVariableType.Vector3 or EGraphVariableType.Vector2 or EGraphVariableType.Color)
      {
        errors.Add($"Graph '{graph.name}': node '{node.name}' default path writes to a {defaultTarget} key; " +
                    "default-value inference only produces bool/int/float/string.");
        return;
      }

      var inferred = InferDefaultValueType(node.DefaultValue);
      if (inferred != defaultTarget.Value)
        errors.Add($"Graph '{graph.name}': node '{node.name}' DefaultValue '{node.DefaultValue}' infers as {inferred} " +
                    $"but the target key is declared {defaultTarget}.");
    }

    private static bool TryMapValueType(string valueType, out EGraphVariableType mapped)
    {
      switch (valueType?.ToLowerInvariant())
      {
        case "string": mapped = EGraphVariableType.String; return true;
        case "int": mapped = EGraphVariableType.Int; return true;
        case "float": mapped = EGraphVariableType.Float; return true;
        case "bool": mapped = EGraphVariableType.Bool; return true;
        default: mapped = default; return false;
      }
    }

    private static bool LiteralParsesAs(string value, EGraphVariableType type)
    {
      return type switch
      {
        EGraphVariableType.String => true,
        EGraphVariableType.Int => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        EGraphVariableType.Float => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        // Mirrors the Set executor's parser: 1/0 and (case-insensitive) true/false.
        EGraphVariableType.Bool => value == "1" || value == "0" || bool.TryParse(value, out _),
        _ => false
      };
    }

    /// <summary>
    ///   Mirror of the Get executor's default-value inference order: bool → int → float → string.
    /// </summary>
    private static EGraphVariableType InferDefaultValueType(string defaultValue)
    {
      if (bool.TryParse(defaultValue, out _)) return EGraphVariableType.Bool;
      if (int.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return EGraphVariableType.Int;
      if (float.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return EGraphVariableType.Float;
      return EGraphVariableType.String;
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
      if (graph is EventGraphAsset) return "Event";
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