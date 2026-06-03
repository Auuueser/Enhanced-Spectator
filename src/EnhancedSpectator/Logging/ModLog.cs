using System;
using BepInEx.Logging;

namespace EnhancedSpectator.Logging;

/// <summary>
/// Central logging facade for the mod.
/// </summary>
public static class ModLog
{
    private static ManualLogSource? _source;
    private static bool _debugEnabled;

    /// <summary>
    /// Initializes the logging facade with the plugin logger.
    /// </summary>
    public static void Initialize(ManualLogSource source)
    {
        _source = source;
    }

    /// <summary>
    /// Gets whether verbose debug logging is currently enabled.
    /// </summary>
    public static bool IsDebugEnabled => _debugEnabled;

    /// <summary>
    /// Enables or disables verbose debug logging.
    /// </summary>
    public static void SetDebugEnabled(bool enabled)
    {
        _debugEnabled = enabled;
    }

    /// <summary>
    /// Writes a debug message.
    /// </summary>
    public static void Debug(string message)
    {
        if (_debugEnabled)
        {
            Source.LogInfo($"[Debug] {message}");
        }
    }

    /// <summary>
    /// Writes an informational message.
    /// </summary>
    public static void Info(string message)
    {
        Source.LogInfo(message);
    }

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    public static void Warning(string message)
    {
        Source.LogWarning(message);
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public static void Error(string message)
    {
        Source.LogError(message);
    }

    private static ManualLogSource Source =>
        _source ?? throw new InvalidOperationException("ModLog has not been initialized.");
}
