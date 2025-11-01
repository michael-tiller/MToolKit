using R3;

namespace MToolKit.Runtime.Settings.Interfaces
{
    /// <summary>
    /// Represents a generic reactive setting that encapsulates a default value,
    /// its reactive property, and provides methods for applying, canceling, and resetting changes.
    /// </summary>
    public interface IReactiveSetting<T>
    {
        /// <summary>
        /// The reactive property holding the current value.
        /// </summary>
        ReactiveProperty<T> Property { get; }
        string Name { get; }

        /// <summary>
        /// The default value of the setting.
        /// </summary>
        T Default { get; }

        /// <summary>
        /// Returns true if the current value differs from the last applied value.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Applies the current value as the new baseline.
        /// </summary>
        void OnApply();

        /// <summary>
        /// Reverts the value to the last applied state.
        /// </summary>
        void OnCancel();

        /// <summary>
        /// Resets the value to its default.
        /// </summary>
        void OnRevertToDefault();
    }
}