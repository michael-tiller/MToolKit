using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Variables
{
    /// <summary>
    /// Global/project-wide graph variables mapped by graph ID.
    /// Applied during bootstrap before definition initial variables and saved state.
    /// </summary>
    [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Global Variables", fileName = "GlobalGraphVariables", order = 201)]
    public sealed class GlobalGraphVariables : ScriptableObject
    {
        [InfoBox("Global variables are applied in this order:\n1. Global variables (this asset)\n2. Definition initial variables\n3. Restored save state (wins)")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<GraphEntry> graphs = new();

        /// <summary>
        /// Get variable set for a specific graph ID.
        /// </summary>
        public GraphVariableSet GetFor(string graphId)
        {
            if (string.IsNullOrEmpty(graphId)) return null;

            foreach (var entry in graphs)
            {
                if (entry.graphId == graphId)
                    return entry.variables;
            }

            return null;
        }

        [Serializable]
        public sealed class GraphEntry
        {
            [HorizontalGroup("Entry", Width = 250)]
            [Tooltip("Graph ID to apply variables to")]
            public string graphId = "";

            [HorizontalGroup("Entry")]
            [InlineEditor(InlineEditorModes.GUIOnly)]
            [Tooltip("Variables to apply to this graph")]
            public GraphVariableSet variables;
        }
    }
}

