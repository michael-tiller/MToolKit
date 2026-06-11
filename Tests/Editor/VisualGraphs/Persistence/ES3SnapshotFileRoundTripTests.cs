using System.Collections.Generic;
using ES3Internal;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Persistence
{
  /// <summary>
  ///   Pins the CURRENT ES3 wire behavior for the graph-state snapshot — the exact path 9.0.4 will extend.
  ///   Saves a Dictionary&lt;string, GraphStateSnapshot&gt; to a real temp ES3 file and reloads it, asserting a
  ///   non-zero LastSequenceId survives (the runner never sets it today, but the persisted type carries it) plus
  ///   GraphId and each supported primitive in Data. Exercises <c>ES3Type_GraphStateSnapshot.Write/Read</c>.
  /// </summary>
  [TestFixture]
  public sealed class ES3SnapshotFileRoundTripTests
  {
    private const string FileName = "mtoolkit_vg_snapshot_roundtrip_test.es3";
    private const string Key = "graph_states";

    [SetUp]
    public void SetUp()
    {
      DeleteFile();
      // Warm the custom ES3 types so Save/Load resolve them deterministically.
      ES3TypeMgr.GetOrCreateES3Type(typeof(GraphStateSnapshot));
      ES3TypeMgr.GetOrCreateES3Type(typeof(Dictionary<string, GraphStateSnapshot>));
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

    [Test]
    public void SnapshotDictionary_RoundTripsThroughEs3File()
    {
      var original = new Dictionary<string, GraphStateSnapshot>
      {
        ["g1"] = new GraphStateSnapshot
        {
          GraphId = "g1",
          LastSequenceId = 42,
          Data = new Dictionary<string, object>
          {
            { "s", "x" },
            { "i", 7 },
            { "f", 1.5f },
            { "b", true }
          }
        }
      };

      ES3.Save(Key, original, FileName);
      var loaded = ES3.Load<Dictionary<string, GraphStateSnapshot>>(Key, FileName);

      Assert.That(loaded.ContainsKey("g1"), Is.True);
      var snapshot = loaded["g1"];
      Assert.That(snapshot.GraphId, Is.EqualTo("g1"));
      Assert.That(snapshot.LastSequenceId, Is.EqualTo(42L),
        "a non-zero LastSequenceId survives the ES3 round-trip (the persisted field is real even though the runner never sets it today)");
      Assert.That(snapshot.Data["s"], Is.EqualTo("x"));
      Assert.That(snapshot.Data["i"], Is.EqualTo(7));
      Assert.That(snapshot.Data["f"], Is.EqualTo(1.5f));
      Assert.That(snapshot.Data["b"], Is.EqualTo(true));
    }
  }
}
