namespace EnhancedSpectator.Features.FearMode;

/// <summary>Keeps synchronous thumbnail render/readback work away from model selection frames.</summary>
public sealed class FearVisualWorkSchedule
{
    internal static readonly FearVisualWorkSchedule Shared = new FearVisualWorkSchedule();
    private int _resumeAt, _lastThumbnail = -4;
    /// <summary>Reserves six frames around a model change for visible world work.</summary>
    public void ModelChanged(int frame) => _resumeAt = frame + 6;
    /// <summary>Allows at most one synchronous thumbnail every four frames outside that window.</summary>
    public bool TryThumbnail(int frame)
    {
        if (frame < _resumeAt || frame - _lastThumbnail < 4) return false;
        _lastThumbnail = frame; return true;
    }
}
