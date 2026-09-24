namespace EnhancedSpectator.GameInterop;

/// <summary>Only the mod-owned sound source's output controls, independent of playback and transport.</summary>
public interface IGameFearAudioOutput
{
    /// <summary>Native mute state.</summary>
    bool Muted { get; set; }
    /// <summary>Explicit source gain.</summary>
    float Volume { get; set; }
}
