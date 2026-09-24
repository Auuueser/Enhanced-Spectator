using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Confirmed original functional sounds only; never invokes any item behavior.</summary>
internal sealed class OriginalItemSoundCatalog
{
    private readonly Dictionary<Item, AudioClip[]> _catalogs = new Dictionary<Item, AudioClip[]>();
    private readonly Dictionary<AudioClip, string> _contentKeys = new Dictionary<AudioClip, string>();
    private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
    private readonly float[] _samples = new float[8192];
    private readonly byte[] _bytes = new byte[8192 * sizeof(float)];

    internal void Copy(Item item, List<AudioClip> clips)
    {
        if (_catalogs.TryGetValue(item, out var cached)) { clips.AddRange(cached); return; }
        var root = item.spawnPrefab;
        if (root == null) return;
        var noise = root.GetComponent<NoisemakerProp>();
        if (noise != null) Add(clips, noise.noiseSFX);
        var cushion = root.GetComponent<WhoopieCushionItem>();
        if (cushion != null) Add(clips, cushion.fartAudios);
        var shovel = root.GetComponent<Shovel>();
        if (shovel != null) { Add(clips, shovel.reelUp, shovel.swing); Add(clips, shovel.hitSFX); }
        var knife = root.GetComponent<KnifeItem>();
        if (knife != null) { Add(clips, knife.swingSFX); Add(clips, knife.hitSFX); }
        var shotgun = root.GetComponent<ShotgunItem>();
        if (shotgun != null) { Add(clips, shotgun.gunShootSFX); Add(clips, shotgun.gunReloadSFX, shotgun.noAmmoSFX); }
        var music = root.GetComponent<BoomboxItem>();
        if (music != null) { Add(clips, music.musicAudios); Add(clips, music.stopAudios); }
        var radio = root.GetComponent<WalkieTalkie>();
        if (radio != null) { Add(clips, radio.startTransmissionSFX); Add(clips, radio.stopTransmissionSFX); }
        var jet = root.GetComponent<JetpackItem>();
        if (jet != null) Add(clips, jet.startJetpackSFX, jet.jetpackSustainSFX, jet.jetpackWarningBeepSFX, jet.jetpackLowBatteriesSFX, jet.jetpackBrokenSFX);
        var radar = root.GetComponent<RadarBoosterItem>();
        if (radar != null) Add(clips, radar.pingSFX, radar.flashSFX);
        var spray = root.GetComponent<SprayPaintItem>();
        if (spray != null) { Add(clips, spray.spraySFX, spray.sprayCanEmptySFX); Add(clips, spray.sprayCanShakeSFX); }
        var zap = root.GetComponent<PatcherTool>();
        if (zap != null) { Add(clips, zap.activateClips); Add(clips, zap.beginShockClips); Add(clips, zap.overheatClips); Add(clips, zap.finishShockClips); }
        var ladder = root.GetComponent<ExtensionLadderItem>();
        if (ladder != null) Add(clips, ladder.ladderExtendSFX, ladder.ladderShrinkSFX, ladder.blinkWarningSFX);
        var gas = root.GetComponent<TetraChemicalItem>();
        if (gas != null) Add(clips, gas.releaseGasSFX, gas.outOfGasSFX);
        var grenade = root.GetComponent<StunGrenadeItem>();
        if (grenade != null) Add(clips, grenade.pullPinSFX, grenade.explodeSFX);
        if (item.name == "ComedyMask" || item.name == "TragedyMask")
        {
            var periodic = root.GetComponent<RandomPeriodicAudioPlayer>();
            if (periodic != null) Add(clips, periodic.randomClips);
            var mask = root.GetComponent<HauntedMaskItem>();
            if (mask != null) Add(clips, mask.maskAttachAudio); // World version only; local variant is the same sound.
        }
        if (item.name == "SeveredHeart")
        {
            var heartbeat = root.GetComponentInChildren<LoopShapeKey>(true);
            if (heartbeat != null) Add(clips, heartbeat.audioOn, heartbeat.audioOff);
        }
        // These confirmed per-item fields contain the toy/phone/bell sound itself, not generic handling.
        // Do not enroll grab/drop fields for any other item.
        var animated = root.GetComponent<AnimatedItem>();
        if (animated != null && ((item.name == "RobotToy" && animated.grabAudio?.name == "RobotToyCheer")
            || (item.name == "Phone" && animated.grabAudio?.name == "PhoneScream")
            || (item.name == "Dentures" && animated.grabAudio?.name == "ChatteringTeeth"))) Add(clips, animated.grabAudio);
        if (item.name == "Bell" && item.dropSFX?.name == "DropBell") Add(clips, item.dropSFX);
        // Equal PCM content requires equal sample count, channel count and frequency. Avoid
        // decoding/hashing entire songs on model selection when no duplicate can exist.
        var formats = new Dictionary<(int Samples, int Channels, int Frequency), int>();
        foreach (var clip in clips)
        {
            var format = (clip.samples, clip.channels, clip.frequency);
            formats.TryGetValue(format, out int count);
            formats[format] = count + 1;
        }
        _seen.Clear();
        for (int index = 0; index < clips.Count;)
        {
            var clip = clips[index];
            string key = formats[(clip.samples, clip.channels, clip.frequency)] > 1
                ? ContentKey(clip) : "unique:" + clip.GetInstanceID();
            if (!_seen.Add(key)) clips.RemoveAt(index);
            else index++;
        }
        _catalogs[item] = clips.ToArray();
    }

    // Protect index-based messages if another mod or different PCM availability changes the deduplicated list.
    internal static string Fingerprint(IReadOnlyList<AudioClip> clips)
    {
        var text = new StringBuilder();
        foreach (var clip in clips)
            text.Append(clip.name.Length).Append(':').Append(clip.name).Append(':').Append(clip.samples)
                .Append(':').Append(clip.channels).Append(':').Append(clip.frequency).Append(';');
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static void Add(List<AudioClip> clips, params AudioClip[] values)
    {
        if (values == null) return;
        foreach (var clip in values)
            if (clip != null && clip.samples > 0 && clip.length > 0.001f && !clips.Contains(clip)) clips.Add(clip);
    }

    private string ContentKey(AudioClip clip)
    {
        if (_contentKeys.TryGetValue(clip, out string key)) return key;
        // Unity cannot expose PCM for streamed/compressed clips. Preserve those distinct sources;
        // never infer identical audio merely from a matching name or duration.
        if (clip.loadState != AudioDataLoadState.Loaded || clip.loadType != AudioClipLoadType.DecompressOnLoad)
            return "source:" + clip.GetInstanceID();
        using (var sha = SHA256.Create())
        {
            int total = clip.samples * clip.channels;
            int stride = _samples.Length / clip.channels * clip.channels;
            if (stride == 0) return "source:" + clip.GetInstanceID();
            for (int offset = 0; offset < total; offset += stride)
            {
                if (!clip.GetData(_samples, offset / clip.channels)) return "source:" + clip.GetInstanceID();
                int bytes = Math.Min(stride, total - offset) * sizeof(float);
                Buffer.BlockCopy(_samples, 0, _bytes, 0, bytes);
                sha.TransformBlock(_bytes, 0, bytes, _bytes, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            key = clip.frequency + ":" + clip.channels + ":" + Convert.ToBase64String(sha.Hash!);
        }
        _contentKeys[clip] = key;
        return key;
    }
}
