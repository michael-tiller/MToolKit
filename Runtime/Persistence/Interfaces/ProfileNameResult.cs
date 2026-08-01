namespace MToolKit.Runtime.Persistence.Interfaces
{
    /// <summary>Result of a profile name operation: success flag and the actual profile name used.</summary>
    public readonly struct ProfileNameResult
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; }

        /// <summary>Actual profile name used (may differ from requested name if a unique name was generated).</summary>
        public string ActualProfileName { get; }

        /// <summary>Initializes a profile name result.</summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="actualProfileName">Actual profile name used.</param>
        public ProfileNameResult(bool success, string actualProfileName) { Success = success; ActualProfileName = actualProfileName; }

        /// <summary>Deconstructs for positional tuple syntax.</summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="actualProfileName">Actual profile name used.</param>
        public void Deconstruct(out bool success, out string actualProfileName) { success = Success; actualProfileName = ActualProfileName; }
    }
}
