using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   ES3-based saveable that works with POCO data objects
    /// </summary>
    public class ES3Saveable<T> : ISaveable where T : class, new()
  {
    private readonly T data;
    private readonly ES3Settings es3Settings;

    public ES3Saveable(string key, T data = null, ES3Settings es3Settings = null)
    {
      this.Key = key ?? throw new ArgumentNullException(nameof(key));
      this.data = data ?? new T();
      this.es3Settings = es3Settings ?? new ES3Settings();
    }

    #region ISaveable Members

    public string Key { get; }

    public UniTask<object> SaveAsync()
    {
      // Save data directly to ES3
      ES3.Save(Key, data, es3Settings);
      return UniTask.FromResult<object>(data);
    }

    public UniTask LoadAsync(object state)
    {
      // Load data from ES3
      if (ES3.KeyExists(Key, es3Settings))
      {
        T loadedData = ES3.Load<T>(Key, es3Settings);
        CopyData(loadedData, data);
      }
      return UniTask.CompletedTask;
    }

    #endregion

    private void CopyData(T source, T target)
    {
      // Simple property copying - can be enhanced with reflection or mapping libraries
      Type sourceType = typeof(T);
      Type targetType = typeof(T);

      foreach (PropertyInfo sourceProperty in sourceType.GetProperties())
        if (sourceProperty.CanRead && sourceProperty.CanWrite)
        {
          PropertyInfo targetProperty = targetType.GetProperty(sourceProperty.Name);
          if (targetProperty != null && targetProperty.CanWrite)
          {
            object value = sourceProperty.GetValue(source);
            targetProperty.SetValue(target, value);
          }
        }
    }

    public T GetData()
    {
      return data;
    }
  }
}