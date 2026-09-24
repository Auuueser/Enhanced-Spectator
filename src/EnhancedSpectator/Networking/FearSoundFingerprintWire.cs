using System;
using System.Buffers.Binary;
using System.Text;

namespace EnhancedSpectator.Networking;

/// <summary>Netcode FixedString digest tail: a four-byte length followed by UTF8 bytes, without struct padding.</summary>
internal static class FearSoundFingerprintWire
{
    internal const int DigestLength = 44;
    internal const int WireLength = sizeof(int) + DigestLength;

    internal static bool TryDecode(ReadOnlySpan<byte> payload, out string digest)
    {
        digest = string.Empty;
        if (payload.Length != WireLength || BinaryPrimitives.ReadInt32LittleEndian(payload) != DigestLength) return false;
        string candidate = Encoding.UTF8.GetString(payload.Slice(sizeof(int)));
        Span<byte> decoded = stackalloc byte[32];
        if (!Convert.TryFromBase64String(candidate, decoded, out int count) || count != 32) return false;
        digest = candidate;
        return true;
    }
}
