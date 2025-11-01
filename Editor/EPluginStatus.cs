#if UNITY_EDITOR

namespace MToolKit.Runtime.Editor
{
    /// <summary>
    /// Enumeration of possible plugin statuses for diagnostic purposes.
    /// </summary>
    public enum EPluginStatus
    {
        Unknown = 0,
        Active = 1,
        Initializing = 2,
        Stopped = 3,
        Error = 4
    }
}

#endif
