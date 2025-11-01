using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Input;
using R3;

namespace MToolKit.Runtime.Settings.Interfaces
{
    /// <summary>
    /// Central manager for all settings modules.
    /// </summary>
    public interface ISettingsSystem
    {
        /// <summary>
        /// Reference to the audio settings module.
        /// </summary>
        AudioSettingsModule AudioSettings { get; }

        /// <summary>
        /// Reference to the graphics settings module.
        /// </summary>
        GraphicsSettingsModule GraphicsSettings { get; }

        /// <summary>
        /// Reference to the game settings module.
        /// </summary>
        GameSettingsModule GameSettings { get; }
        /// <summary>
        /// Reference to the input settings module.
        /// </summary>
        InputSettingsModule InputSettings { get; }

        /// <summary>
        /// Reactive property indicating if settings have been modified.
        /// </summary>
        ReactiveProperty<bool> IsDirty { get; }

        /// <summary>
        /// Apply current settings.
        /// </summary>
        void Apply(bool autoFinish = true, bool gotoMenu = true);

        /// <summary>
        /// Finish applying settings and optionally return to menu.
        /// </summary>
        void FinishApply(bool gotoMenu = true);

        /// <summary>
        /// Revert to default settings.
        /// </summary>
        void DefaultSettings();

        /// <summary>
        /// Cancel settings changes.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Set the dirty state.
        /// </summary>
        void SetDirty(bool isDirty);
    }
}