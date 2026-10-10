using System;

namespace EnhancedSpectator.Features.Social;

/// <summary>Emotes to display, already filtered by the viewer's preference: sender client id and text.</summary>
internal static class SpectatorSocialEvents
{
    internal static event Action<ulong, string>? Emote;
    internal static void RaiseEmote(ulong sender, string text) => Emote?.Invoke(sender, text);
}
