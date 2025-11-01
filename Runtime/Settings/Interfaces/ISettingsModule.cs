namespace MToolKit.Runtime.Settings.Interfaces
{
    
    /// <summary>
    /// Module for managing audio Config.
    /// </summary>
    public interface ISettingsModule
    {
        void RevertToDefaultSettings();
        void Apply();
        void Cancel();

        void OnShutdown();
    }
}