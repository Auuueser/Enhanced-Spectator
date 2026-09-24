namespace EnhancedSpectator.Features.FearMode;

/// <summary>Shared frame budget keeps preparation independent of how many visuals enter the fade radius.</summary>
internal sealed class FrameWorkBudget
{
    private readonly int _limit;
    private int _frame = -1, _spent;
    internal FrameWorkBudget(int limit) => _limit = limit;
    internal bool TrySpend(int frame)
    {
        if (_frame != frame) { _frame = frame; _spent = 0; }
        if (_spent >= _limit) return false;
        _spent++;
        return true;
    }
}
