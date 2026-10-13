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

    [Test]
    public void MapValue_MapsRanges()
    {
      var h = Create("Transform", "MapValueNode", new NodeParametersDictionary {
        { "ValueKey", "value" }, { "InputMinKey", "min" }, { "InputMaxKey", "max" },
        { "OutputMinKey", "low" }, { "OutputMaxKey", "high" }, { "ResultKey", "result" } });
      h.State.Set("value", 5f); h.State.Set("min", 0f); h.State.Set("max", 10f);
      h.State.Set("low", 10f); h.State.Set("high", 20f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(15f));
    }

    [Test]
    public void CombineValues_ProducesVector3()
    {
      var h = Create("Transform", "CombineValuesNode", new NodeParametersDictionary {
        { "Keys", new List<string> { "x", "y", "z" } }, { "ValueType", EGraphVariableType.Vector3 },
        { "ResultKey", "result" } });
      h.State.Set("x", 1f); h.State.Set("y", 2f); h.State.Set("z", 3f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<Vector3>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(new Vector3(1f, 2f, 3f)));
    }

    [Test]
    public void ExtractValue_ProducesSelectedComponent()
    {
      var h = Create("Transform", "ExtractValueNode", new NodeParametersDictionary {
        { "SourceKey", "vector" }, { "ValueType", EGraphVariableType.Vector3 },
        { "Component", "Y" }, { "ResultKey", "result" } });
      h.State.Set("vector", new Vector3(1f, 2f, 3f)); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(2f));
    }

    [Test]
    public void FormatString_UsesInvariantCulture()
    {
      var previous = CultureInfo.CurrentCulture;
      try
      {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var h = Create("Transform", "FormatStringNode", new NodeParametersDictionary {
          { "Format", "Value={0:F1}" }, { "Keys", new List<string> { "value" } }, { "ResultKey", "result" } });
        h.State.Set("value", 1.5f); h.Run(new TestMessageA());
        Assert.That(h.State.TryGet<string>("result", out var actual), Is.True);
        Assert.That(actual, Is.EqualTo("Value=1.5"));
      }
      finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestCase("ConditionalAddNode", true, 10f)] [TestCase("ConditionalAddNode", false, 8f)]
    [TestCase("ConditionalMultiplyNode", true, 16f)] [TestCase("ConditionalMultiplyNode", false, 8f)]
    public void ConditionalMath_PassesThroughWhenFalse(string name, bool condition, float expected)
    {
      var p = Binary(); p["ConditionKey"] = "condition";
      var h = Create("Math", name, p);
      h.State.Set("a", 8f); h.State.Set("b", 2f); h.State.Set("condition", condition); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(true, "yes")] [TestCase(false, "no")]
    public void Select_PreservesSelectedType(bool condition, string expected)
    {
      var h = Create("Logic", "SelectNode", new NodeParametersDictionary {
        { "ConditionKey", "condition" }, { "TrueKey", "yes" }, { "FalseKey", "no" },
        { "ValueType", EGraphVariableType.String }, { "ResultKey", "result" } });
      h.State.Set("condition", condition); h.State.Set("yes", "yes"); h.State.Set("no", "no"); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<string>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("Math", "AddNode", 10f)] [TestCase("Math", "SubtractNode", 6f)]
    [TestCase("Math", "MultiplyNode", 16f)] [TestCase("Math", "DivideNode", 4f)]
    [TestCase("Math", "ClampNode", 5f)] [TestCase("Math", "LerpNode", 5f)]
    [TestCase("Math", "ConditionalAddNode", 10f)] [TestCase("Math", "ConditionalMultiplyNode", 16f)]
    [TestCase("Logic", "AndNode", false)] [TestCase("Logic", "OrNode", true)]
    [TestCase("Logic", "NotNode", true)] [TestCase("Logic", "XorNode", true)]
    [TestCase("Logic", "AllTrueNode", false)] [TestCase("Logic", "AnyTrueNode", true)]
    [TestCase("Logic", "CompareNode", true)] [TestCase("Logic", "SelectNode", 8f)]
    [TestCase("Transform", "MapValueNode", 15f)] [TestCase("Transform", "ExtractValueNode", 2f)]
    [TestCase("Transform", "FormatStringNode", "Value=8.0")]
    [TestCase("Transform", "CombineValuesNode", "vector")]
    [TestCase("Transform", "PositionNode", "vector")]
    [TestCase("Transform", "RotationNode", "vector")]
    [TestCase("Transform", "ScaleNode", "vector")]
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

    [Test]
    public void MapZeroRange_UsesFallback()
    {
      var h = Create("Transform", "MapValueNode", new NodeParametersDictionary {
        { "ValueKey", "v" }, { "InputMinKey", "same" }, { "InputMaxKey", "same" },
        { "OutputMinKey", "low" }, { "OutputMaxKey", "high" }, { "Fallback", 7f }, { "ResultKey", "result" } });
      h.State.Set("v", 5f); h.State.Set("same", 1f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(7f));
    }

    [Test]
    public void FormatMalformedTemplate_FailsSoft()
    {
      var h = Create("Transform", "FormatStringNode", new NodeParametersDictionary {
        { "Format", "{99}" }, { "Keys", new List<string> { "a" } }, { "ResultKey", "result" } });
      h.State.Set("a", 1f); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<string>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractWrongType_FailsSoft()
    {
      var h = Create("Transform", "ExtractValueNode", new NodeParametersDictionary {
        { "SourceKey", "a" }, { "ValueType", EGraphVariableType.Vector3 }, { "Component", "Y" }, { "ResultKey", "result" } });
      h.State.Set("a", "not-vector"); h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var actual), Is.True);
      Assert.That(actual, Is.EqualTo(0f));
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
    [TestCase("Math", "ConditionalAddNode")] [TestCase("Math", "ConditionalMultiplyNode")]
    [TestCase("Logic", "CompareNode")] [TestCase("Logic", "SelectNode")]
    [TestCase("Logic", "AllTrueNode")] [TestCase("Logic", "AnyTrueNode")]
    [TestCase("Transform", "MapValueNode")] [TestCase("Transform", "CombineValuesNode")]
    [TestCase("Transform", "ExtractValueNode")] [TestCase("Transform", "FormatStringNode")]
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
