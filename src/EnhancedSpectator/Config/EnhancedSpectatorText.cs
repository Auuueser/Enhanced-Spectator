namespace EnhancedSpectator.Config;

/// <summary>
/// Selects cached English or Chinese text without runtime localization lookups.
/// </summary>
public static class EnhancedSpectatorText
{
    /// <summary>
    /// Returns the text for the resolved config language.
    /// </summary>
    public static string Select(bool useChinese, string english, string chinese)
    {
        return useChinese ? chinese : english;
    }
}
