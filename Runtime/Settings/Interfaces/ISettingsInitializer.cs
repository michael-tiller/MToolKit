using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Settings.Interfaces
{
    /// <summary>
    /// Abstract base class for Config initializers that provides an asynchronous configuration hook.
    /// </summary>
    public interface ISettingsInitializer
    {
        /// <summary>
        /// Asynchronously configures the Config UI.
        /// </summary>
        UniTask ConfigureAsync();
    }
}