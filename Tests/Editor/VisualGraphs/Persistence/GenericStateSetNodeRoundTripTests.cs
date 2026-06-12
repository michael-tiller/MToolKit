using ES3Internal;
using MToolKit.Runtime.VisualGraphs.Executors;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using System.Collections.Generic;

namespace MToolKit.Tests.Editor.VisualGraphs.Persistence
{
  /// <summary>
  ///   9.0.4 spec leg: values written by a REAL <see cref="GenericStateSetNodeExecutor" /> round-trip
  ///   ExportState → real ES3 file → ImportState into a fresh runner, with typed equality. Covers exactly the
  ///   four text-parseable types (bool/int/float/string) — <c>GenericStateSetNode</c> cannot author
  ///   Vector3/Vector2/Color (no text parser; export validation rejects struct targets until 9.5 typed
  ///   comparers), so the struct legs are covered by the direct-Set tests in
  ///   <see cref="ES3SnapshotFileRoundTripTests" /> instead.
  /// </summary>
  [TestFixture]
  public sealed class GenericStateSetNodeRoundTripTests
  {
    private const string FileName = "mtoolkit_vg_nodeset_roundtrip_test.es3";
    private const string Key = "snapshot";

    [SetUp]
    public void SetUp()
    {
      DeleteFile();
      ES3TypeMgr.GetOrCreateES3Type(typeof(GraphStateSnapshot));
    }

    [TearDown]
    public void TearDown()
    {
      DeleteFile();
    }

    private static void DeleteFile()
    {
      if (ES3.FileExists(FileName))
        ES3.DeleteFile(FileName);
    }

    private static GraphRunnerHarness HarnessWithSetNode(string stateKey, string value, string valueType)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1")
        .EntryNode("e")
        .Node("set", "GenericStateSetNode", new NodeParametersDictionary
        {
          { "stateKey", stateKey },
          { "value", value },
          { "valueType", valueType }
        })
        .Connect("e", "set")
        .Build());
      h.Executors.Register(new GenericStateSetNodeExecutor());
      return h;
    }

    private static void AssertNodeWrittenValueRoundTrips<T>(string value, string valueType, T expected)
    {
      var writer = HarnessWithSetNode("k", value, valueType);
      writer.Run(new TestMessageA());

      var snapshot = writer.Runner.ExportState();
      ES3.Save(Key, snapshot, FileName);
      var loaded = ES3.Load<GraphStateSnapshot>(Key, FileName);

      var reader = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());
      reader.Runner.ImportState(loaded);

      Assert.That(reader.State.TryGet<T>("k", out var roundTripped), Is.True,
        $"the node-written {valueType} survives export → ES3 file → import with its type intact");
      Assert.That(roundTripped, Is.EqualTo(expected));
    }

    [Test]
    public void NodeWrittenBool_RoundTripsThroughEs3File() =>
      AssertNodeWrittenValueRoundTrips("true", "bool", true);

    [Test]
    public void NodeWrittenInt_RoundTripsThroughEs3File() =>
      AssertNodeWrittenValueRoundTrips("42", "int", 42);

    [Test]
    public void NodeWrittenFloat_RoundTripsThroughEs3File() =>
      AssertNodeWrittenValueRoundTrips("1.5", "float", 1.5f);

    [Test]
    public void NodeWrittenString_RoundTripsThroughEs3File() =>
      AssertNodeWrittenValueRoundTrips("hello", "string", "hello");
  }
}
