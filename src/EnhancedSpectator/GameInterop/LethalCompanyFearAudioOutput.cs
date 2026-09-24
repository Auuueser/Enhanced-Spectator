using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal sealed class LethalCompanyFearAudioOutput : IGameFearAudioOutput
{
    private readonly AudioSource _source;
    internal LethalCompanyFearAudioOutput(AudioSource source) => _source = source;
    public bool Muted { get => _source != null && _source.mute; set { if (_source != null) _source.mute = value; } }
    public float Volume { get => _source != null ? _source.volume : 0f; set { if (_source != null) _source.volume = value; } }
}
