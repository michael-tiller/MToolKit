using System.Collections.Generic;
using System.Linq;

namespace MToolKit.Runtime.VisualGraphs.Persistence
{
    /// <summary>
    /// Save provider for graph state. Integrates with the existing save system.
    /// Note: Implement ICustomSaveProvider or adapt to your save system interface.
    /// </summary>
    public sealed class GraphStateSaveProvider
    {
        private readonly GraphEventRouter _router;

        public string Domain => "Graphs";

        public GraphStateSaveProvider(GraphEventRouter router)
        {
            _router = router ?? throw new System.ArgumentNullException(nameof(router));
        }

        /// <summary>
        /// Capture all graph states for saving.
        /// </summary>
        public object Capture()
        {
            var map = new Dictionary<string, GraphStateSnapshot>();
            
            foreach (var runner in _router.GetRunners())
            {
                var snapshot = runner.ExportState();
                if (snapshot != null)
                    map[runner.GraphId] = snapshot;
            }
            
            return map;
        }

        /// <summary>
        /// Restore graph states from saved data.
        /// </summary>
        public void Restore(object data)
        {
            if (data is not Dictionary<string, GraphStateSnapshot> map)
                return;

            foreach (var kv in map)
            {
                var runner = _router.GetRunners().FirstOrDefault(r => r.GraphId == kv.Key);
                runner?.ImportState(kv.Value);
            }
        }
    }
}

