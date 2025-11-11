using System;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring
{
    /// <summary>
    /// Base class for all visual graph nodes. Provides stable GUID for runtime node identification.
    /// </summary>
    [Serializable]
    public abstract class VisualGraphNodeBase : Node
    {
        /// <summary>
        /// Stable GUID used as runtime NodeId. This ensures node identity persists across authoring changes.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private string _guid = string.Empty;

        /// <summary>
        /// Stable GUID for this node.
        /// </summary>
        public string Guid
        {
            get => _guid;
            private set => _guid = value;
        }

        protected override void Init()
        {
            base.Init();
            EnsureGuid();
        }

        protected virtual void OnValidate()
        {
            EnsureGuid();
        }

        private void EnsureGuid()
        {
            if (string.IsNullOrEmpty(_guid))
            {
                _guid = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }
    }
}

