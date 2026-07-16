using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Persistence;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Persistence
{
  /// <summary>
  ///   Characterization of <see cref="GraphStateSaveController" /> via a <see cref="MemoryES3Service" /> fake +
  ///   <see cref="FakeGraphRunner" />s on a real <see cref="GraphEventRouter" />. Pins the OBSERVABLE behavior:
  ///   snapshots are written under the domain-prefixed key, one runner's export failure does not abort the save,
  ///   and on load each matching runner receives ImportState while a save entry with no runner is skipped
  ///   without throwing. The restored/missing counters are Serilog-only (and the static Lazy logger makes a
  ///   fixture sink unreliable), so they are deliberately NOT asserted — the restore/skip behavior is.
  /// </summary>
  [TestFixture]
  public sealed class GraphStateSaveControllerTests
  {
    private const string SaveKey = "graphs_graph_states";

    private static FakeGraphRunner RunnerWithState(string graphId, string key, object value)
    {
      return new FakeGraphRunner(graphId)
      {
        ExportSnapshot = new GraphStateSnapshot
        {
          GraphId = graphId,
          Data = new Dictionary<string, object> { { key, value } }
        }
      };
    }

    [Test]
    public void HasSaveData_ReflectsKeyPresence()
    {
      var router = new GraphEventRouter();
      router.RegisterRunner(RunnerWithState("g1", "k", 1));
      var es3 = new MemoryES3Service();
      var controller = new GraphStateSaveController(router, es3);

      Assert.That(controller.HasSaveData(), Is.False);
      controller.SaveAsync().GetAwaiter().GetResult();
      Assert.That(controller.HasSaveData(), Is.True);
    }

    [Test]
    public void SaveAsync_WritesSnapshotsUnderPrefixedKey()
    {
      var router = new GraphEventRouter();
      router.RegisterRunner(RunnerWithState("g1", "k", 1));
      var es3 = new MemoryES3Service();

      new GraphStateSaveController(router, es3).SaveAsync().GetAwaiter().GetResult();

      Assert.That(es3.KeyExists(SaveKey), Is.True, "graph states are saved under 'graphs_graph_states'");
    }

    [Test]
    public void SaveAsync_OneRunnerExportThrows_OthersStillSaved()
    {
      var router = new GraphEventRouter();
      var bad = RunnerWithState("g1", "k", 1);
      bad.ThrowOnExport = true;
      router.RegisterRunner(bad);
      router.RegisterRunner(RunnerWithState("g2", "k", 2));
      var es3 = new MemoryES3Service();

      new GraphStateSaveController(router, es3).SaveAsync().GetAwaiter().GetResult();

      var saved = es3.LoadAsync<Dictionary<string, GraphStateSnapshot>>(SaveKey).GetAwaiter().GetResult();
      Assert.That(saved.ContainsKey("g2"), Is.True, "the healthy runner's state is still saved");
      Assert.That(saved.ContainsKey("g1"), Is.False, "the throwing runner is skipped, not fatal");
    }

    [Test]
    public void SaveLoad_RoundTrip_RestoresToMatchingRunner()
    {
      var es3 = new MemoryES3Service();

      var saveRouter = new GraphEventRouter();
      saveRouter.RegisterRunner(RunnerWithState("g1", "hp", 99));
      new GraphStateSaveController(saveRouter, es3).SaveAsync().GetAwaiter().GetResult();

      var loadRouter = new GraphEventRouter();
      var loadRunner = new FakeGraphRunner("g1");
      loadRouter.RegisterRunner(loadRunner);
      new GraphStateSaveController(loadRouter, es3).LoadAsync().GetAwaiter().GetResult();

      Assert.That(loadRunner.Imported.Count, Is.EqualTo(1));
      Assert.That(loadRunner.Imported[0].Data["hp"], Is.EqualTo(99),
        "the saved snapshot is imported into the matching runner on load");
    }

    [Test]
    public void LoadAsync_SaveEntryWithNoRunner_SkippedWithoutThrow_MatchingStillRestored()
    {
      var es3 = new MemoryES3Service();

      var saveRouter = new GraphEventRouter();
      saveRouter.RegisterRunner(RunnerWithState("g1", "hp", 1));
      saveRouter.RegisterRunner(RunnerWithState("g2", "hp", 2));
      new GraphStateSaveController(saveRouter, es3).SaveAsync().GetAwaiter().GetResult();

      // Load against a router that only has g1 — g2's saved entry has no runner.
      var loadRouter = new GraphEventRouter();
      var g1 = new FakeGraphRunner("g1");
      loadRouter.RegisterRunner(g1);
      new GraphStateSaveController(loadRouter, es3).LoadAsync().GetAwaiter().GetResult();

      Assert.That(g1.Imported.Count, Is.EqualTo(1),
        "the matching runner is restored even though the orphan (g2) save entry is skipped without throwing");
    }

    [Test]
    public void LoadAsync_NoSaveData_NoOp()
    {
      var router = new GraphEventRouter();
      var runner = new FakeGraphRunner("g1");
      router.RegisterRunner(runner);

      new GraphStateSaveController(router, new MemoryES3Service()).LoadAsync().GetAwaiter().GetResult();

      Assert.That(runner.Imported, Is.Empty);
    }
  }
}
