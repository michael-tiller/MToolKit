using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Persistence.Abstractions
{
  public abstract class AbstractSaveBehaviour<T> : MonoBehaviour, ISaveable where T : ISaveable
  {
    [InlineProperty]
    [LabelWidth(64)]
    [SerializeField]
    private T data;

    public T Data => data;

    public string Key => data.Key;
    public ISaveable AsSaveable => Data;
    public UniTask<object> SaveAsync() => data.SaveAsync();
    public UniTask LoadAsync(object state) => data.LoadAsync(state);
  }
}