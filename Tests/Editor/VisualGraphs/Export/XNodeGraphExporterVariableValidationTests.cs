using System.Collections.Generic;
using System.Text.RegularExpressions;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Message;
using MToolKit.Runtime.VisualGraphs.Authoring.Nodes.State;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Quest.Nodes;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XNode;

namespace MToolKit.Tests.Editor.VisualGraphs.Export
{
  /// <summary>
  ///   Pins the 9.0.1 declared-variable validation pass in <see cref="XNodeGraphExporter" />: entirely skipped
  ///   without a declaration set (opt-in), declaration self-validation (duplicate/empty keys, out-of-range enum),
  ///   the per-node type matrix over Set/Check/Get state nodes, and warn-only coverage of
  ///   MessageFieldGetNode.StateKey and QuestEmitEventNode Variable payloads. Unknown keys WARN (dynamic/mod
  ///   keys stay legal at runtime); structural/type problems are errors via InvalidGraphException.
  /// </summary>
  [TestFixture]
  public sealed class XNodeGraphExporterVariableValidationTests : UnityObjectCleanup
  {
    // Like XNodeGraphExporterTests, this fixture consumes expected Debug logs via LogAssert.Expect instead of
    // suppressing them — an unconsumed warning would leak into LogAssert's queue and fail an unrelated test.

    private QuestGraphAsset graph;

    [SetUp]
    public void SetUp()
    {
      graph = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      graph.name = "VarGraph";
      Track(graph.AddNode<TestEntryNode>());
      graph.DeclaredVariables = Track(ScriptableObject.CreateInstance<GraphVariableSet>());
    }

    private T AddNode<T>() where T : Node
    {
      return Track(graph.AddNode<T>());
    }

    private void Declare(string key, EGraphVariableType type)
    {
      graph.DeclaredVariables.entries.Add(new GraphVariableDeclaration { key = key, type = type });
    }

    private static XNodeGraphExporter NewExporter()
    {
      var registry = new NodeExecutorRegistry();
      registry.Register(new RecordingExecutor("GenericStateSetNode"));
      registry.Register(new RecordingExecutor("GenericStateCheckNode"));
      registry.Register(new RecordingExecutor("GenericStateGetNode"));
      registry.Register(new RecordingExecutor("MessageFieldGetNode"));
      registry.Register(new RecordingExecutor("QuestEmitEventNode"));
      return new XNodeGraphExporter(registry);
    }

    private void AssertExportThrowsWith(string messageFragment)
    {
      var ex = Assert.Throws<InvalidGraphException>(() => NewExporter().Export(graph));
      Assert.That(ex.Message, Does.Contain(messageFragment));
    }

    // ---- Opt-in gate ----

    [Test]
    public void Export_NoDeclaredVariableSet_SkipsValidation()
    {
      graph.DeclaredVariables = null;
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "anything_goes";

      Assert.DoesNotThrow(() => NewExporter().Export(graph), "no declaration set = no validation, no warnings");
    }

    [Test]
    public void Export_EmptyDeclarationList_SkipsValidation()
    {
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "anything_goes";

      Assert.DoesNotThrow(() => NewExporter().Export(graph), "an attached but empty set behaves like no set");
    }

    // ---- Declaration self-validation ----

    [Test]
    public void Export_DuplicateDeclarationKey_ThrowsInvalidGraph()
    {
      Declare("hp", EGraphVariableType.Int);
      Declare("hp", EGraphVariableType.Float);

      AssertExportThrowsWith("duplicate declared variable key 'hp'");
    }

    [Test]
    public void Export_EmptyDeclarationKey_ThrowsInvalidGraph()
    {
      Declare("  ", EGraphVariableType.Int);

      AssertExportThrowsWith("empty key");
    }

    [Test]
    public void Export_OutOfRangeEnumDeclarationType_ThrowsInvalidGraph()
    {
      Declare("hp", (EGraphVariableType)999);

      AssertExportThrowsWith("out-of-range type value 999");
    }

    [Test]
    public void Export_NullEntryInsideList_ToleratedRestStillValidated()
    {
      graph.DeclaredVariables.entries.Add(null);
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "hp";
      node.ValueType = "int";
      node.Value = "5";

      Assert.DoesNotThrow(() => NewExporter().Export(graph), "a null slot in the list is skipped, not fatal");
    }

    // ---- Unknown keys warn ----

    [Test]
    public void Export_UndeclaredKey_LogsWarning_StillExports()
    {
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "typo_key";
      node.ValueType = "int";
      node.Value = "5";

      LogAssert.Expect(LogType.Warning, new Regex("undeclared state key 'typo_key'"));
      Assert.DoesNotThrow(() => NewExporter().Export(graph));
    }

    // ---- SetNode matrix ----

    [Test]
    public void Export_SetNodeValueTypeMismatch_ThrowsInvalidGraph()
    {
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "hp";
      node.ValueType = "bool";
      node.Value = "true";

      AssertExportThrowsWith("declared Int");
    }

    [Test]
    public void Export_SetNodeIntVsFloat_IsStrictMismatch()
    {
      Declare("speed", EGraphVariableType.Float);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "speed";
      node.ValueType = "int";
      node.Value = "5";

      AssertExportThrowsWith("declared Float");
    }

    [Test]
    public void Export_SetNodeUnknownValueTypeString_ThrowsInvalidGraph()
    {
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "hp";
      node.ValueType = "decimal"; // dropdown-backed field with an unknown value = corrupted authoring data

      AssertExportThrowsWith("unknown ValueType 'decimal'");
    }

    [Test]
    public void Export_SetNodeUnparseableDeclaredPrimitiveValue_ThrowsInvalidGraph()
    {
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "hp";
      node.ValueType = "int";
      node.Value = "abc"; // passes the type match, would only fail (warn) at runtime without this check

      AssertExportThrowsWith("does not parse as Int");
    }

    [Test]
    public void Export_SetNodeTargetingVectorDeclaredKey_ThrowsInvalidGraph()
    {
      Declare("position", EGraphVariableType.Vector3);
      var node = AddNode<GenericStateSetNode>();
      node.StateKey = "position";
      node.ValueType = "string";
      node.Value = "1,2,3";

      AssertExportThrowsWith("declared Vector3");
    }

    // ---- CheckNode matrix ----

    [Test]
    public void Export_CheckNodeUnparseableNumericExpectedValue_ThrowsInvalidGraph()
    {
      Declare("speed", EGraphVariableType.Float);
      var node = AddNode<GenericStateCheckNode>();
      node.StateKey = "speed";
      node.ExpectedValue = "banana";

      AssertExportThrowsWith("is not numeric");
    }

    [Test]
    public void Export_CheckNodeBoolExpectedValueOne_ThrowsInvalidGraph()
    {
      Declare("alive", EGraphVariableType.Bool);
      var node = AddNode<GenericStateCheckNode>();
      node.StateKey = "alive";
      node.ExpectedValue = "1"; // the Set parser accepts 1/0; the Check executor's Convert.ToBoolean throws on them

      AssertExportThrowsWith("must be 'true' or 'false'");
    }

    [Test]
    public void Export_CheckNodeOrderingOperatorOnBoolKey_ThrowsInvalidGraph()
    {
      Declare("alive", EGraphVariableType.Bool);
      var node = AddNode<GenericStateCheckNode>();
      node.StateKey = "alive";
      node.ExpectedValue = "true";
      node.ComparisonOperator = "GreaterThan"; // the executor silently degrades this to Equals

      AssertExportThrowsWith("ordering operator");
    }

    [Test]
    public void Export_CheckNodeOnVectorDeclaredKey_ThrowsInvalidGraph()
    {
      Declare("position", EGraphVariableType.Vector3);
      var node = AddNode<GenericStateCheckNode>();
      node.StateKey = "position";
      node.ExpectedValue = "(1, 2, 3)";
      node.ComparisonOperator = "Equals"; // even Equals: runtime falls back to culture-unstable ToString comparison

      AssertExportThrowsWith("do not support struct types");
    }

    // ---- GetNode matrix ----

    [Test]
    public void Export_GetNodeSourceAndDestinationTypeMismatch_ThrowsInvalidGraph()
    {
      Declare("kills", EGraphVariableType.Int);
      Declare("label", EGraphVariableType.String);
      var node = AddNode<GenericStateGetNode>();
      node.SourceStateKey = "kills";
      node.DestinationStateKey = "label";
      node.DefaultValue = "0";

      AssertExportThrowsWith("copies 'kills'");
    }

    [Test]
    public void Export_GetNodeDefaultValueInvalidForDeclaredDestinationType_ThrowsInvalidGraph()
    {
      Declare("speed_snapshot", EGraphVariableType.Float);
      var node = AddNode<GenericStateGetNode>();
      node.SourceStateKey = "undeclared_source"; // destination declaration is what the default path can corrupt
      node.DestinationStateKey = "speed_snapshot";
      node.DefaultValue = "0"; // executor inference order bool→int→float: "0" infers Int, not Float

      LogAssert.Expect(LogType.Warning, new Regex("undeclared state key 'undeclared_source'"));
      AssertExportThrowsWith("infers as Int");
    }

    [Test]
    public void Export_GetNodeVectorDeclaredDestination_ThrowsInvalidGraph()
    {
      Declare("position", EGraphVariableType.Vector3);
      var node = AddNode<GenericStateGetNode>();
      node.SourceStateKey = "position";
      node.DestinationStateKey = "position";
      node.DefaultValue = "0";

      AssertExportThrowsWith("default-value inference only produces bool/int/float/string");
    }

    // ---- Warn-only surfaces ----

    [Test]
    public void Export_MessageFieldGetUndeclaredStateKey_LogsWarning()
    {
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<MessageFieldGetNode>();
      node.StateKey = "untracked_field_target";

      LogAssert.Expect(LogType.Warning, new Regex("undeclared state key 'untracked_field_target'"));
      Assert.DoesNotThrow(() => NewExporter().Export(graph));
    }

    [Test]
    public void Export_QuestEmitEventUndeclaredVariablePayload_LogsWarning_NonVariableEntriesSilent()
    {
      Declare("hp", EGraphVariableType.Int);
      var node = AddNode<QuestEmitEventNode>();
      node.Payload = new List<MessagePayloadParameter>
      {
        new() { ParameterName = "p1", ValueType = ParameterValueType.Variable, VariableName = "untracked_var" },
        new() { ParameterName = "p2", ValueType = ParameterValueType.String, VariableName = "stale_var_name" }
      };

      LogAssert.Expect(LogType.Warning, new Regex("undeclared state key 'untracked_var'"));
      Assert.DoesNotThrow(() => NewExporter().Export(graph),
        "only Variable-kind payload entries read state; stale VariableName on other kinds produces no warning");
    }

    // ---- Clean path ----

    [Test]
    public void Export_MatchingDeclarations_ExportsClean()
    {
      Declare("hp", EGraphVariableType.Int);
      Declare("alive", EGraphVariableType.Bool);
      Declare("hp_snapshot", EGraphVariableType.Int);

      var setNode = AddNode<GenericStateSetNode>();
      setNode.StateKey = "hp";
      setNode.ValueType = "int";
      setNode.Value = "100";

      var checkNode = AddNode<GenericStateCheckNode>();
      checkNode.StateKey = "alive";
      checkNode.ExpectedValue = "true";
      checkNode.ComparisonOperator = "Equals";

      var getNode = AddNode<GenericStateGetNode>();
      getNode.SourceStateKey = "hp";
      getNode.DestinationStateKey = "hp_snapshot";
      getNode.DefaultValue = "0";

      Assert.DoesNotThrow(() => NewExporter().Export(graph), "a consistent declared graph exports with no errors or warnings");
    }
  }
}
