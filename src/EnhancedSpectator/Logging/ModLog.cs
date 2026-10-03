using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace EnhancedSpectator.Logging;

/// <summary>
/// Central logging facade for the mod.
/// </summary>
public static class ModLog
{
    private static ManualLogSource? _source;
    private static volatile bool _debugEnabled;
    private static readonly object Gate = new object();
    private static readonly LogRateLimiter DebugLimit = new LogRateLimiter(20, 1, 1);
    private static readonly LogRateLimiter FaultLimit = new LogRateLimiter(1, 30, 256);
    private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    /// <summary>
    /// Initializes the logging facade with the plugin logger.
    /// </summary>
    public static void Initialize(ManualLogSource source)
    {
        _source = source;
        lock (Gate) { DebugLimit.Clear(); FaultLimit.Clear(); _debugEnabled = false; }
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
        lock (Gate)
        {
            if (_debugEnabled != enabled) DebugLimit.Clear();
            _debugEnabled = enabled;
        }
    }

    /// <summary>
    /// Writes a debug message.
    /// </summary>
    public static void Debug(string message)
    {
        lock (Gate)
        {
            if (!_debugEnabled || !DebugLimit.Allow("debug", Now, out int skipped)) return;
            Source.LogInfo($"[Debug] {message}" + Suppressed(skipped));
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
    public static void Warning(string message, [CallerFilePath] string site = "", [CallerLineNumber] int line = 0)
    {
        lock (Gate)
            if (FaultLimit.Allow("warning:" + site + ":" + line, Now, out int skipped))
                Source.LogWarning(message + Suppressed(skipped));
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public static void Error(string message, [CallerFilePath] string site = "", [CallerLineNumber] int line = 0)
    {
        lock (Gate)
            if (FaultLimit.Allow("error:" + site + ":" + line, Now, out int skipped))
                Source.LogError(message + Suppressed(skipped));
    }

    private static string Suppressed(int count) => count == 0 ? string.Empty : $" [suppressed {count} additional messages]";

    private static ManualLogSource Source =>
        _source ?? throw new InvalidOperationException("ModLog has not been initialized.");
}
