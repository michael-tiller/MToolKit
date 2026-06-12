using System.Collections.Generic;
using ES3Internal;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

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

    [Test]
    public void SnapshotData_StructTypes_RoundTripTyped()
    {
      var original = new Dictionary<string, GraphStateSnapshot>
      {
        ["g1"] = new GraphStateSnapshot
        {
          GraphId = "g1",
          Data = new Dictionary<string, object>
          {
            { "v3", new Vector3(1.5f, -2f, 3.25f) },
            { "v2", new Vector2(0.5f, 9f) },
            { "c", new Color(0.1f, 0.2f, 0.3f, 0.4f) }
          }
        }
      };

      ES3.Save(Key, original, FileName);
      var loaded = ES3.Load<Dictionary<string, GraphStateSnapshot>>(Key, FileName);

      var data = loaded["g1"].Data;
      Assert.That(data["v3"], Is.TypeOf<Vector3>(), "boxed Vector3 must keep its struct type through the ES3 object envelope");
      Assert.That(data["v3"], Is.EqualTo(new Vector3(1.5f, -2f, 3.25f)));
      Assert.That(data["v2"], Is.TypeOf<Vector2>());
      Assert.That(data["v2"], Is.EqualTo(new Vector2(0.5f, 9f)));
      Assert.That(data["c"], Is.TypeOf<Color>());
      Assert.That(data["c"], Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
    }

    [Test]
    public void SingleSnapshot_AllSevenTypes_RoundTripsThroughEs3File()
    {
      // The exact wire path scope persistence uses: ONE GraphStateSnapshot under its own key
      // (the dictionary tests above exercise a different ES3 collection path).
      var original = new GraphStateSnapshot
      {
        GraphId = "player",
        Data = new Dictionary<string, object>
        {
          { "s", "x" },
          { "i", 7 },
          { "f", 1.5f },
          { "b", true },
          { "v3", new Vector3(1f, 2f, 3f) },
          { "v2", new Vector2(4f, 5f) },
          { "c", new Color(0.25f, 0.5f, 0.75f, 1f) }
        }
      };

      ES3.Save("scope_state", original, FileName);
      var loaded = ES3.Load<GraphStateSnapshot>("scope_state", FileName);

      Assert.That(loaded.GraphId, Is.EqualTo("player"));
      Assert.That(loaded.Data["s"], Is.EqualTo("x"));
      Assert.That(loaded.Data["i"], Is.EqualTo(7));
      Assert.That(loaded.Data["f"], Is.EqualTo(1.5f));
      Assert.That(loaded.Data["b"], Is.EqualTo(true));
      Assert.That(loaded.Data["v3"], Is.EqualTo(new Vector3(1f, 2f, 3f)));
      Assert.That(loaded.Data["v2"], Is.EqualTo(new Vector2(4f, 5f)));
      Assert.That(loaded.Data["c"], Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
    }

    [Test]
    public void UnsupportedValueType_SkippedAtSave_WithWarning()
    {
      // A delegate is genuinely unserializable by ES3 — this drives the REAL Dictionary<string, object>
      // write path, proving the save-time probe catches what the actual write would choke on (9.0.4
      // fail-loud: warn + skip at save, never a throw and never a silent drop on load).
      var original = new GraphStateSnapshot
      {
        GraphId = "g1",
        Data = new Dictionary<string, object>
        {
          { "ok", 7 },
          { "bad", new System.Action(() => { }) }
        }
      };

      using var sink = new SerilogSinkScope();
      Assert.DoesNotThrow(() => ES3.Save("snapshot", original, FileName),
        "an unserializable value must be skipped loudly at save time, not abort the save");
      Assert.That(sink.ContainsWarning("Skipping unserializable state value"), Is.True,
        "the skip must be loud — a warning naming the key and type");

      var loaded = ES3.Load<GraphStateSnapshot>("snapshot", FileName);
      Assert.That(loaded.Data.ContainsKey("bad"), Is.False, "the unserializable value must not resurface on load");
      Assert.That(loaded.Data["ok"], Is.EqualTo(7), "supported values in the same snapshot still round-trip");
    }
  }
}
