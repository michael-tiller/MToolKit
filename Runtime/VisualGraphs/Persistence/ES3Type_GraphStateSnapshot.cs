using MToolKit.Runtime.VisualGraphs.Persistence;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using UnityEngine;
using UnityEngine.Scripting;
using System.Collections.Generic;
using ES3Internal;

namespace ES3Types
{
  [Preserve]
  [ES3Properties("GraphId", "LastSequenceId", "Data")]
  public class ES3Type_GraphStateSnapshot : ES3Type
  {
    public static ES3Type Instance;

    public ES3Type_GraphStateSnapshot() : base(typeof(GraphStateSnapshot))
    {
      Instance = this;
    }

    public override void Write(object obj, ES3Writer writer)
    {
      GraphStateSnapshot casted = (GraphStateSnapshot)obj;
      writer.WriteProperty("GraphId", casted.GraphId, ES3Type_string.Instance);
      writer.WriteProperty("LastSequenceId", casted.LastSequenceId, ES3Type_long.Instance);
      // 9.0.4: unserializable values fail loud (warn + skip) at save time, never silently drop on load.
      writer.WriteProperty("Data", GraphSnapshotSchemaSanitizer.FilterUnserializable(casted.Data, casted.GraphId),
        ES3TypeMgr.GetOrCreateES3Type(typeof(Dictionary<string, object>)));
    }

    public override object Read<T>(ES3Reader reader)
    {
      var graphId = reader.ReadProperty<string>(ES3Type_string.Instance);
      var lastSequenceId = reader.ReadProperty<long>(ES3Type_long.Instance);
      var data = reader.ReadProperty<Dictionary<string, object>>(ES3TypeMgr.GetOrCreateES3Type(typeof(Dictionary<string, object>)));

      return new GraphStateSnapshot
      {
        GraphId = graphId,
        LastSequenceId = lastSequenceId,
        Data = data ?? new Dictionary<string, object>()
      };
    }
  }
}

