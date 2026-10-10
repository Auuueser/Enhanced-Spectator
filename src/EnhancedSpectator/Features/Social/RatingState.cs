using System.Collections.Generic;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Dead players' reviews of living teammates for one round, in the Company's deadpan voice. Tags 0–3 are (grim)
/// praise, 4–7 teasing; a rater gives one person at most two different tags. Tallies stay with the host until the
/// round ends.
/// </summary>
internal sealed class RatingState
{
    internal const int TagCount = 8, PositiveCount = 4, PerTarget = 2;
    private static readonly string[] Chinese = { "公司好资产", "人形囤积虫", "耐用型员工", "命比较硬", "活体诱饵", "带薪留守", "尸体预备役", "跑得比队友快" },
        English = { "Valued asset", "Human Hoarding Bug", "Durable model", "Hard to kill", "Live bait", "Paid to stay aboard", "Corpse in training", "Outran the crew. Barely." };
    internal static string Tag(int tag, bool chinese) => (chinese ? Chinese : English)[tag];
    internal static bool Positive(int tag) => tag < PositiveCount;

    private readonly Dictionary<ulong, int[]> _tallies = new Dictionary<ulong, int[]>();
    private readonly HashSet<(ulong Rater, ulong Target, int Tag)> _given = new HashSet<(ulong, ulong, int)>();

    internal IReadOnlyDictionary<ulong, int[]> Tallies => _tallies;

    internal int GivenTo(ulong rater, ulong target)
    { int count = 0; foreach (var given in _given) if (given.Rater == rater && given.Target == target) count++; return count; }
    internal bool Has(ulong rater, ulong target, int tag) => _given.Contains((rater, target, tag));

    /// <summary>Adds or withdraws one tag; false when invalid, a self-rating, or over the per-person limit.</summary>
    internal bool Toggle(ulong rater, ulong target, int tag, bool on)
    {
        if (tag < 0 || tag >= TagCount || rater == target) return false;
        if (!on) { if (!_given.Remove((rater, target, tag))) return false; _tallies[target][tag]--; return true; }
        if (_given.Contains((rater, target, tag)) || GivenTo(rater, target) >= PerTarget) return false;
        _given.Add((rater, target, tag));
        if (!_tallies.TryGetValue(target, out var counts)) _tallies[target] = counts = new int[TagCount];
        counts[tag]++;
        return true;
    }

    internal void Clear() { _tallies.Clear(); _given.Clear(); }

    /// <summary>The overall verdict from praise against teasing, and the most given tag.</summary>
    internal static string Verdict(int[] counts, bool chinese, out int top)
    {
        int positive = 0, negative = 0; top = 0;
        for (int i = 0; i < TagCount; i++)
        {
            if (Positive(i)) positive += counts[i]; else negative += counts[i];
            if (counts[i] > counts[top]) top = i;
        }
        if (positive >= 3 && negative == 0) return chinese ? "本季度暂不裁员" : "Spared this quarter";
        if (negative >= 3 && positive == 0) return chinese ? "资产价值待核销" : "Asset pending write-off";
        if (positive > negative) return chinese ? "尚可继续压榨" : "Still worth squeezing";
        if (positive == negative) return chinese ? "标准耗材" : "Standard consumable";
        return chinese ? "已列入观察名单" : "Under review";
    }
}
