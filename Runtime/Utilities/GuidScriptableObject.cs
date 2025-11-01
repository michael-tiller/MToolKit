using System;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Runtime.Utilities
{
    public interface IDisplayName
    {
        string DisplayName { get; }
    }
    
    public class GuidScriptableObject : ScriptableObject, IDisplayName
    {
        [field:SerializeField] 
        public string DisplayName { get; protected set; }

        [field:SerializeField, ReadOnly, PropertyOrder(-9999)] 
        public string Guid { get; protected set; }
        
        [field:SerializeField, ReadOnly, PropertyOrder(-9998)] 
        public long Timestamp { get; protected set; }

#if UNITY_EDITOR
        protected virtual void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(Guid)) 
                return;

            RefreshGuid();
        }
#endif
        
        [Button]
        protected virtual void RefreshGuid()
        {
#if UNITY_EDITOR
            Guid = System.Guid.NewGuid().ToString();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            EditorUtility.SetDirty(this);
#endif
        }
  
        public override bool Equals(object obj)
        {
            if (obj is not GuidScriptableObject other)
                return false;
            return this.Guid == other.Guid; // or any property that uniquely identifies an accessory
        }

        public override int GetHashCode() => Guid.GetHashCode();
    }
}
