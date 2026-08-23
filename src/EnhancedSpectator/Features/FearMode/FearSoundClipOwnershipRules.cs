using System;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Rejects clips whose asset names explicitly identify a different monster family.
/// </summary>
public static class FearSoundClipOwnershipRules
{
    private static readonly OwnershipMarker[] Markers =
    {
        new OwnershipMarker("baboon", "baboonhawk"),
        new OwnershipMarker("caw", "baboonhawk"),
        new OwnershipMarker("squeak", "baboonhawk"),
        new OwnershipMarker("nutcracker", "nutcracker"),
        new OwnershipMarker("flowerman", "flowerman", "bracken"),
        new OwnershipMarker("bracken", "flowerman", "bracken"),
        new OwnershipMarker("hoarder", "hoardingbug", "hoarderbug"),
        new OwnershipMarker("maneater", "maneater", "cavedweller"),
        new OwnershipMarker("cavedweller", "maneater", "cavedweller"),
        new OwnershipMarker("masked", "masked"),
        new OwnershipMarker("springman", "spring", "springman"),
        new OwnershipMarker("radmech", "radmech", "oldbird"),
        new OwnershipMarker("oldbird", "radmech", "oldbird"),
        new OwnershipMarker("crawler", "crawler", "thumper"),
        new OwnershipMarker("thumper", "crawler", "thumper"),
        new OwnershipMarker("spider", "spider"),
        new OwnershipMarker("jester", "jester"),
        new OwnershipMarker("butler", "butler"),
        new OwnershipMarker("centipede", "centipede"),
        new OwnershipMarker("claysurgeon", "claysurgeon"),
        new OwnershipMarker("forestgiant", "forestgiant"),
        new OwnershipMarker("puffer", "puffer"),
        new OwnershipMarker("blob", "blob")
    };

    /// <summary>Gets whether the clip name is compatible with the selected model family.</summary>
    public static bool IsAllowed(string modelKey, string clipName)
    {
        string normalizedModel = Normalize(modelKey);
        string normalizedClip = Normalize(clipName);
        for (int index = 0; index < Markers.Length; index++)
        {
            OwnershipMarker marker = Markers[index];
            if (!normalizedClip.Contains(marker.ClipMarker, StringComparison.Ordinal))
            {
                continue;
            }

            for (int ownerIndex = 0; ownerIndex < marker.AllowedOwners.Length; ownerIndex++)
            {
                if (normalizedModel.Contains(marker.AllowedOwners[ownerIndex], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int length = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer, 0, length);
    }

    private sealed class OwnershipMarker
    {
        public OwnershipMarker(string clipMarker, params string[] allowedOwners)
        {
            ClipMarker = clipMarker;
            AllowedOwners = allowedOwners;
        }

        public string ClipMarker { get; }
        public string[] AllowedOwners { get; }
    }
}
