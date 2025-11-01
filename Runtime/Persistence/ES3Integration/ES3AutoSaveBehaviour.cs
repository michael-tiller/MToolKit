using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    /// ES3-based saveable that works with POCO data objects
    /// </summary>
    public class ES3Saveable<T> : ISaveable where T : class, new()
    {
        private readonly string key;
        private readonly T data;
        private readonly ES3Settings es3Settings;

        public string Key => key;

        public ES3Saveable(string key, T data = null, ES3Settings es3Settings = null)
        {
            this.key = key ?? throw new System.ArgumentNullException(nameof(key));
            this.data = data ?? new T();
            this.es3Settings = es3Settings ?? new ES3Settings();
        }

        public UniTask<object> SaveAsync()
        {
            // Save data directly to ES3
            ES3.Save(key, data, es3Settings);
            return UniTask.FromResult<object>(data);
        }

        public UniTask LoadAsync(object state)
        {
            // Load data from ES3
            if (ES3.KeyExists(key, es3Settings))
            {
                var loadedData = ES3.Load<T>(key, es3Settings);
                CopyData(loadedData, data);
            }
            return UniTask.CompletedTask;
        }

        private void CopyData(T source, T target)
        {
            // Simple property copying - can be enhanced with reflection or mapping libraries
            var sourceType = typeof(T);
            var targetType = typeof(T);

            foreach (var sourceProperty in sourceType.GetProperties())
            {
                if (sourceProperty.CanRead && sourceProperty.CanWrite)
                {
                    var targetProperty = targetType.GetProperty(sourceProperty.Name);
                    if (targetProperty != null && targetProperty.CanWrite)
                    {
                        var value = sourceProperty.GetValue(source);
                        targetProperty.SetValue(target, value);
                    }
                }
            }
        }

        public T GetData() => data;
    }
}
