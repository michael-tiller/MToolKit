using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.State;

namespace MToolKit.Runtime.VisualGraphs.Persistence
{
  /// <summary>
  ///   Save provider for graph state. Integrates with the existing save system.
  ///   Note: Implement ICustomSaveProvider or adapt to your save system interface.
  /// </summary>
  public sealed class GraphStateSaveProvider
  {
    private readonly GraphEventRouter router;

    public GraphStateSaveProvider(GraphEventRouter router)
    {
      this.router = router ?? throw new ArgumentNullException(nameof(router));
    }

    public string Domain => "Graphs";

    /// <summary>
    ///   Capture all graph states for saving.
    /// </summary>
    public object Capture()
    {
      var map = new Dictionary<string, GraphStateSnapshot>();

      foreach (var runner in router.GetRunners())
      {
        var snapshot = runner.ExportState();
        if (snapshot != null)
          map[runner.GraphId] = snapshot;
      }

      return map;
    }

    /// <summary>
    ///   Restore graph states from saved data.
    /// </summary>
    public void Restore(object data)
    {
      if (data is not Dictionary<string, GraphStateSnapshot> map)
        return;

      foreach (var kv in map)
      {
        var runner = router.GetRunners().FirstOrDefault(r => r.GraphId == kv.Key);
        runner?.ImportState(kv.Value);
      }
    }
  }
}