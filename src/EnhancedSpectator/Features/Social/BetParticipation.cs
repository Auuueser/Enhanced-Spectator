namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Whether the local player skipped the bets. A real game's choice lasts for that game (its session); the local
/// preview keeps its own, which goes with the preview: skipping in a test never hides a real game's bets, and a real
/// game's choice never stops the test from trying them.
/// </summary>
internal sealed class BetParticipation
{
    private int _skippedSession;
    private bool _skippedInPreview;

    internal bool Skipped(bool preview, int session) => preview ? _skippedInPreview : _skippedSession != 0 && _skippedSession == session;

    internal void Skip(bool preview, int session)
    {
        if (preview) _skippedInPreview = true;
        else _skippedSession = session;
    }

    /// <summary>The preview ended (or started): its choice is forgotten.</summary>
    internal void ResetPreview() => _skippedInPreview = false;
}
