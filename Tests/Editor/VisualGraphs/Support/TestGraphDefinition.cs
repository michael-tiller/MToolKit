using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Core.Types;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Hand-rolled <see cref="IRuntimeGraphDefinition" /> used to drive <c>GraphRunner</c> in EditMode
  ///   without building xNode assets. Mirrors the production lookup contract (GetNodeById /
  ///   GetConnectionsFrom) over plain lists so tests can pin runner behavior deterministically.
  /// </summary>
  public sealed class TestRuntimeGraphDefinition : IRuntimeGraphDefinition
  {
    public string GraphId { get; set; } = "TestGraph";
    public string GraphDomain { get; set; } = "Quest";
    public int MaxExecutionSteps { get; set; } = 100;
    public IReadOnlyList<RuntimeSubscriptionDefinition> Subscriptions { get; set; } = new List<RuntimeSubscriptionDefinition>();
    public IReadOnlyList<RuntimeNodeDefinition> Nodes { get; set; } = new List<RuntimeNodeDefinition>();
    public IReadOnlyList<RuntimeConnectionDefinition> Connections { get; set; } = new List<RuntimeConnectionDefinition>();

    public RuntimeNodeDefinition GetNodeById(string nodeId)
    {
      return Nodes?.FirstOrDefault(n => n.NodeId == nodeId);
    }

    public IEnumerable<RuntimeConnectionDefinition> GetConnectionsFrom(string nodeId)
    {
      return Connections?.Where(c => c.FromNodeId == nodeId) ?? Enumerable.Empty<RuntimeConnectionDefinition>();
    }
  }

  /// <summary>
  ///   Fluent builder for <see cref="TestRuntimeGraphDefinition" />. Keeps runner/router fixtures readable.
  /// </summary>
  public sealed class GraphDefBuilder
  {
    private readonly List<RuntimeConnectionDefinition> connections = new();
    private readonly List<RuntimeNodeDefinition> nodes = new();
    private readonly List<RuntimeSubscriptionDefinition> subscriptions = new();
    private string graphDomain = "Quest";
    private string graphId = "TestGraph";
    private int maxSteps = 100;

    public static GraphDefBuilder New()
    {
      return new GraphDefBuilder();
    }

    public GraphDefBuilder Id(string id)
    {
      graphId = id;
      return this;
    }

    public GraphDefBuilder Domain(string domain)
    {
      graphDomain = domain;
      return this;
    }

    public GraphDefBuilder MaxSteps(int n)
    {
      maxSteps = n;
      return this;
    }

    public GraphDefBuilder Node(string id, string type, NodeParametersDictionary parameters = null)
    {
      nodes.Add(new RuntimeNodeDefinition
      {
        NodeId = id,
        NodeType = type,
        Parameters = parameters ?? new NodeParametersDictionary()
      });
      return this;
    }

    /// <summary>Adds a node whose type is recognised as an entry node by GraphRunner.IsEntryNode.</summary>
    public GraphDefBuilder EntryNode(string id, string type = "QuestOnEventNode")
    {
      return Node(id, type);
    }

    public GraphDefBuilder Connect(string fromNodeId, string toNodeId, string portName = "Next")
    {
      connections.Add(new RuntimeConnectionDefinition
      {
        FromNodeId = fromNodeId,
        ToNodeId = toNodeId,
        PortName = portName
      });
      return this;
    }

    public GraphDefBuilder Subscribe(Type messageType, string domain = null)
    {
      subscriptions.Add(new RuntimeSubscriptionDefinition
      {
        MessageType = new MessageTypeReference(messageType),
        DomainFilter = domain
      });
      return this;
    }

    /// <summary>Adds a subscription whose MessageType is invalid (Type == null), to pin the skip path.</summary>
    public GraphDefBuilder SubscribeInvalid(string domain = null)
    {
      subscriptions.Add(new RuntimeSubscriptionDefinition
      {
        MessageType = new MessageTypeReference { Type = null },
        DomainFilter = domain
      });
      return this;
    }

    public TestRuntimeGraphDefinition Build()
    {
      return new TestRuntimeGraphDefinition
      {
        GraphId = graphId,
        GraphDomain = graphDomain,
        MaxExecutionSteps = maxSteps,
        Nodes = nodes,
        Connections = connections,
        Subscriptions = subscriptions
      };
    }
  }
}
