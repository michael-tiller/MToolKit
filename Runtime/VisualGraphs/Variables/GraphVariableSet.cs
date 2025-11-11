using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Variables
{
    /// <summary>
    /// Scriptable object containing a set of graph variables.
    /// Can be used for global variables or per-definition initial variables.
    /// </summary>
    [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Variable Set", fileName = "GraphVariables", order = 200)]
    public sealed class GraphVariableSet : ScriptableObject
    {
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<GraphVariableEntry> entries = new();

        /// <summary>
        /// Apply these variables to a graph state.
        /// </summary>
        public void ApplyTo(IGraphState state)
        {
            if (state == null) return;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.key)) continue;

                switch (entry.type)
                {
                    case GraphVariableType.String:
                        state.Set(entry.key, entry.stringValue);
                        break;
                    case GraphVariableType.Int:
                        state.Set(entry.key, entry.intValue);
                        break;
                    case GraphVariableType.Float:
                        state.Set(entry.key, entry.floatValue);
                        break;
                    case GraphVariableType.Bool:
                        state.Set(entry.key, entry.boolValue);
                        break;
                }
            }
        }

        [Serializable]
        public sealed class GraphVariableEntry
        {
            [HorizontalGroup("Entry", Width = 200)]
            [LabelWidth(30)]
            public string key = "variableName";

            [HorizontalGroup("Entry", Width = 100)]
            [LabelWidth(30)]
            public GraphVariableType type = GraphVariableType.Int;

            [HorizontalGroup("Entry")]
            [ShowIf("@type == GraphVariableType.String")]
            [LabelText("Value")]
            public string stringValue = "";

            [HorizontalGroup("Entry")]
            [ShowIf("@type == GraphVariableType.Int")]
            [LabelText("Value")]
            public int intValue = 0;

            [HorizontalGroup("Entry")]
            [ShowIf("@type == GraphVariableType.Float")]
            [LabelText("Value")]
            public float floatValue = 0f;

            [HorizontalGroup("Entry")]
            [ShowIf("@type == GraphVariableType.Bool")]
            [LabelText("Value")]
            public bool boolValue = false;
        }

        public enum GraphVariableType
        {
            String,
            Int,
            Float,
            Bool
        }
    }
}

