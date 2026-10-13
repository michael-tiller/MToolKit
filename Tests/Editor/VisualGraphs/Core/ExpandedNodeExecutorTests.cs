using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Core
{
  [TestFixture]
  public sealed class ExpandedNodeExecutorTests
  {
    private sealed class ScopedServices : IServiceProvider
    {
      public GraphContextRegistry Registry;
      public ScopedKeyResolver Resolver;
      public object GetService(Type type) => type == typeof(GraphContextRegistry) ? (object)Registry :
        type == typeof(ScopedKeyResolver) ? Resolver : null;
    }

    private static GraphRunnerHarness Create(string family, string name, NodeParametersDictionary parameters)
    {
      var assembly = typeof(AddNode).Assembly;
      var type = assembly.GetType("MToolKit.Runtime.VisualGraphs.Executors." + family + "." + name + "Executor");
      Assert.That(type, Is.Not.Null, name + " must have an executable runtime implementation");
      var harness = new GraphRunnerHarness(GraphDefBuilder.New().EntryNode("entry")
        .Node("operation", name, parameters).Node("after", "Record").Connect("entry", "operation")
        .Connect("operation", "after").Build());
      harness.Executors.Register((IGraphNodeExecutor)Activator.CreateInstance(type));
      harness.RegisterExecutor("Record");
      return harness;
    }

    private static NodeParametersDictionary Binary(EGraphVariableType type = EGraphVariableType.Float)
    {
      return new NodeParametersDictionary { { "AKey", "a" }, { "BKey", "b" },
        { "ResultKey", "result" }, { "ValueType", type } };
    }

    [TestCase("Math", "SubtractNode", 8f, 2f, 6f)]
    [TestCase("Math", "DivideNode", 8f, 2f, 4f)]
    [TestCase("Math", "AddNode", 8f, 2f, 10f)]
    [TestCase("Math", "MultiplyNode", 8f, 2f, 16f)]
    public void BinaryFloat_ComputesAndContinues(string family, string name, float a, float b, float expected)
    {
      var h = Create(family, name, Binary());
      h.State.Set("a", a); h.State.Set("b", b); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("AddNode", 10)] [TestCase("SubtractNode", 6)]
    [TestCase("MultiplyNode", 16)] [TestCase("DivideNode", 4)]
    public void BinaryInt_PreservesIntegerType(string name, int expected)
    {
      var h = Create("Math", name, Binary(EGraphVariableType.Int));
      h.State.Set("a", 8); h.State.Set("b", 2); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<int>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Divide_ZeroUsesDeclaredFallback()
    {
      var p = Binary(); p["Fallback"] = 12f;
      var h = Create("Math", "DivideNode", p);
      h.State.Set("a", 5f); h.State.Set("b", 0f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(12f));
    }

    [TestCase("GreaterThan", true)] [TestCase("GreaterOrEqual", true)]
    [TestCase("LessThan", false)] [TestCase("LessOrEqual", false)]
    [TestCase("Equal", false)] [TestCase("NotEqual", true)]
    public void Compare_SixOperators(string comparison, bool expected)
    {
      var p = Binary(); p["Operator"] = comparison;
      var h = Create("Logic", "CompareNode", p);
      h.State.Set("a", 8f); h.State.Set("b", 2f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("AllTrueNode", false)] [TestCase("AnyTrueNode", true)]
    public void BooleanFolds_UseAllOperands(string name, bool expected)
    {
      var h = Create("Logic", name, new NodeParametersDictionary {
        { "Keys", new List<string> { "a", "b" } }, { "ResultKey", "result" } });
      h.State.Set("a", true); h.State.Set("b", false); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("Math", "AddNode", 10f)] [TestCase("Math", "SubtractNode", 6f)]
    [TestCase("Math", "MultiplyNode", 16f)] [TestCase("Math", "DivideNode", 4f)]
    [TestCase("Math", "ClampNode", 5f)] [TestCase("Math", "LerpNode", 5f)]
    [TestCase("Logic", "AndNode", false)] [TestCase("Logic", "OrNode", true)]
    [TestCase("Logic", "NotNode", true)] [TestCase("Logic", "XorNode", true)]
    [TestCase("Logic", "AllTrueNode", false)] [TestCase("Logic", "AnyTrueNode", true)]
    [TestCase("Logic", "CompareNode", true)]
    public void ScopedOperandsAndResults_PreserveTypesUnderFrenchCulture(string family, string name, object expected)
    {
      var previous = CultureInfo.CurrentCulture;
      try
      {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var p = new NodeParametersDictionary {
          { "AKey", "world.a" }, { "BKey", "world.b" }, { "ResultKey", "world.result" },
          { "ValueKey", "world.value" }, { "MinKey", "world.min" }, { "MaxKey", "world.max" },
          { "TKey", "world.t" }, { "InputMinKey", "world.min" }, { "InputMaxKey", "world.max" },
          { "OutputMinKey", "world.low" }, { "OutputMaxKey", "world.high" },
          { "ConditionKey", "world.true" }, { "TrueKey", "world.a" }, { "FalseKey", "world.b" },
          { "LeftKey", "world.true" }, { "RightKey", "world.false" }, { "Key", "world.false" },
          { "SourceKey", "world.vector" }, { "DestinationKey", "world.result" },
          { "Keys", new List<string> { "world.true", "world.false" } },
          { "Operator", "GreaterThan" }, { "Component", "Y" }, { "Format", "Value={0:F1}" },
          { "ValueType", name == "CombineValuesNode" || name == "ExtractValueNode" ?
              EGraphVariableType.Vector3 : EGraphVariableType.Float } };
        if (name == "CombineValuesNode") p["Keys"] = new List<string> { "world.x", "world.y", "world.z" };
        if (name == "FormatStringNode") p["Keys"] = new List<string> { "world.a" };
        var h = Create(family, name, p);
        var registry = new GraphContextRegistry(h.Emitter);
        registry.GetOrCreate(EGraphContextScope.Graph, h.Definition.GraphId, h.State);
        var world = registry.GetOrCreate(EGraphContextScope.World, null).Variables;
        world.Set("a", 8f); world.Set("b", 2f); world.Set("value", 5f);
        world.Set("min", 0f); world.Set("max", 10f); world.Set("t", 0.5f);
        world.Set("low", 10f); world.Set("high", 20f); world.Set("true", true); world.Set("false", false);
        world.Set("x", 1f); world.Set("y", 2f); world.Set("z", 3f); world.Set("vector", new Vector3(1, 2, 3));
        var services = new ScopedServices { Registry = registry, Resolver = new ScopedKeyResolver(registry) };
        var runner = new GraphRunner(h.Definition, h.State, h.Executors, services, h.Emitter);
        runner.HandleMessageAsync(new TestMessageA()).GetAwaiter().GetResult();
        Assert.That(world.Contains("result"), Is.True, "result must be written into World storage");
        Assert.That(world.Get<object>("result"), Is.EqualTo(expected is string s && s == "vector" ?
          (object)new Vector3(1, 2, 3) : expected));
        Assert.That(h.State.Contains("world.result"), Is.False, "scoped writes must not create a literal local key");
      }
      finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestCase("AddNode", 2f)] [TestCase("SubtractNode", -2f)]
    [TestCase("MultiplyNode", 0f)] [TestCase("DivideNode", 0f)]
    public void MathWrongOperand_FailsSoftToZero(string name, float expected)
    {
      var h = Create("Math", name, Binary());
      h.State.Set("a", "wrong-type"); h.State.Set("b", 2f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CompareWrongOperand_DoesNotTurnMissingEqualityIntoTrue()
    {
      var p = Binary(); p["Operator"] = "Equal";
      var h = Create("Logic", "CompareNode", p);
      h.State.Set("a", "wrong-type"); h.State.Set("b", 0f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var actual), Is.True);
      Assert.That(actual, Is.False);
    }

    [TestCase("AllTrueNode", false)] [TestCase("AnyTrueNode", false)]
    public void ExplicitBooleanFolds_MissingInputIsFalse(string name, bool expected)
    {
      var h = Create("Logic", name, new NodeParametersDictionary {
        { "Keys", new List<string> { "missing" } }, { "ResultKey", "result" } });
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("Math", "SubtractNode")] [TestCase("Math", "DivideNode")]
    [TestCase("Logic", "CompareNode")]
    [TestCase("Logic", "AllTrueNode")] [TestCase("Logic", "AnyTrueNode")]
    public void CatalogNode_IsAuthorableAndUsesClosedTypeDropdown(string family, string name)
    {
      var type = typeof(AddNode).Assembly.GetType("MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core." + family + "." + name);
      Assert.That(type, Is.Not.Null);
      var node = ScriptableObject.CreateInstance(type);
      try
      {
        Assert.That(node, Is.Not.Null);
        var valueType = type.GetField("ValueType");
        if (valueType != null) Assert.That(valueType.FieldType, Is.EqualTo(typeof(EGraphVariableType)));
      }
      finally { UnityEngine.Object.DestroyImmediate(node); }
    }
  }
}
