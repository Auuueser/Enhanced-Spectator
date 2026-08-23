namespace EnhancedSpectator.Config;

/// <summary>
/// Selects the language used by Enhanced Spectator config descriptions and runtime UI.
/// </summary>
public enum EnhancedSpectatorLanguageMode
{
    /// <summary>
    /// Uses Chinese when LC Chinese Project is installed; otherwise uses English.
    /// </summary>
    Auto,

    /// <summary>
    /// Always uses English.
    /// </summary>
    English,

    /// <summary>
    /// Always uses Simplified Chinese.
    /// </summary>
    Chinese,
}

/// <summary>
/// Pure language selection rules.
/// </summary>
public static class EnhancedSpectatorLanguageRules
{
    /// <summary>
    /// Resolves whether Chinese text should be used.
    /// </summary>
    public static bool UseChinese(EnhancedSpectatorLanguageMode mode, bool lcChineseProjectInstalled)
    {
        return mode == EnhancedSpectatorLanguageMode.Chinese
            || (mode == EnhancedSpectatorLanguageMode.Auto && lcChineseProjectInstalled);
    }
}
