using System;
using System.Text;

namespace EnhancedSpectator.GameInterop;

/// <summary>LC-Chinese-Project name policy: preserve real digits, never infer a suffix from digits alone.</summary>
public static class PlayerNameRepairRules
{
    /// <summary>Preserves ordinary spaces and real digits like the internal upstream policy; false reproduces vanilla.</summary>
    public static string Sanitize(string? value, bool preserveDigits)
    {
        var output = new StringBuilder();
        if (value != null)
            foreach (char ch in value)
                if (char.IsLetter(ch) || preserveDigits && (char.IsDigit(ch) || ch == ' ')) output.Append(ch);
        string name = output.ToString();
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
    }

    /// <summary>Bounds optional, untrusted lobby member metadata before using it as a name source.</summary>
    public static string Advertised(string? value) => value == null || value.Length > 128 ? string.Empty : Sanitize(value, true);

    /// <summary>Accepts only proven vanilla output, placeholders, or an already-correct name.</summary>
    public static bool TryRepair(string? current, string? steamName, out string repaired)
    {
        repaired = Sanitize(steamName, true);
        if (repaired.Length == 0) return false;
        string vanilla = Sanitize(steamName, false);
        vanilla = vanilla.Length == 0 ? "Nameless" : vanilla.Length <= 2 ? vanilla + "0" : vanilla;
        string oldSanitized = repaired.Replace(" ", string.Empty);
        return Matches(current, repaired) || Matches(current, vanilla) || Matches(current, oldSanitized)
            || string.IsNullOrWhiteSpace(current) || PlayerDisplayNameRules.IsGenericPlayerNumber(current!);
    }

    /// <summary>Recognizes vanilla duplicate numbering on display surfaces using an independent source name.</summary>
    public static bool CanRepairSurface(string current, string raw)
    {
        return TryRepair(current, raw, out _);
    }

    private static bool Matches(string? current, string source) => !string.IsNullOrEmpty(source)
        && (string.Equals(current, source, StringComparison.Ordinal)
            || PlayerDisplayNameRules.HasSyntheticNumericSuffix(current!, source));
}
