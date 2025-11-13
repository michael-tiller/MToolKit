using System.Collections.Generic;
using UnityEngine;

namespace MToolKit.Runtime.Utilities.SerializableDictionary
{
  public abstract class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
  {
    [SerializeField]
    [HideInInspector]
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<TKey> keyData = new();

    [SerializeField]
    [HideInInspector]
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<TValue> valueData = new();

    #region ISerializationCallbackReceiver Members

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
      Clear();
      for (int i = 0; i < keyData.Count && i < valueData.Count; i++)
        this[keyData[i]] = valueData[i];
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
      keyData.Clear();
      valueData.Clear();

      foreach (KeyValuePair<TKey, TValue> item in this)
      {
        keyData.Add(item.Key);
        valueData.Add(item.Value);
      }
    }

    #endregion
  }
}