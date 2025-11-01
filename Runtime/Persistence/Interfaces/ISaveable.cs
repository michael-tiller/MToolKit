using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  public interface ISaveable
  {
    string Key { get; }
    UniTask<object> SaveAsync();
    UniTask LoadAsync(object state);
  }
}