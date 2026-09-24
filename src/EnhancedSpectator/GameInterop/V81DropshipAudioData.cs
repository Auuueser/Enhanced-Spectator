using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace EnhancedSpectator.GameInterop;

// Original compressed audio is read locally. Embedded setup packets are generic Vorbis codec tables
// from Fmod5Sharp (MIT), not game recordings. See THIRD_PARTY_NOTICES.md.
[DataContract]
internal sealed class V81DropshipAudioData
{
    [DataMember] public Clip[] clips = Array.Empty<Clip>();
    [DataMember] public Setup[] setups = Array.Empty<Setup>();
    [DataContract] internal sealed class Clip
    {
        [DataMember] public string name = "";
        [DataMember] public V81DropshipData.Chunk range = null!;
        [DataMember] public int frequency, channels, samples, packetOffset, setup;
        internal string CachePath = "";
    }
    [DataContract] internal sealed class Setup
    {
        [DataMember] public uint crc;
        [DataMember] public string header = "";
        [DataMember] public int[] flags = Array.Empty<int>();
    }

    internal static V81DropshipAudioData Prepare(string gameData, string cacheDirectory)
    {
        using var resource = typeof(V81DropshipAudioData).Assembly.GetManifestResourceStream("EnhancedSpectator.GameInterop.V81DropshipAudio.json")
            ?? throw new InvalidDataException("Missing V81 audio layout metadata");
        var data = (V81DropshipAudioData)new DataContractJsonSerializer(typeof(V81DropshipAudioData)).ReadObject(resource)!;
        Directory.CreateDirectory(cacheDirectory);
        foreach (var clip in data.clips)
        {
            byte[] original = V81DropshipData.ReadChunk(gameData, clip.range);
            byte[] ogg = Rewrap(original, clip, data.setups[clip.setup]);
            clip.CachePath = Path.Combine(cacheDirectory, clip.name + "-" + clip.range.sha256.Substring(0, 16) + ".ogg");
            bool same = File.Exists(clip.CachePath) && new FileInfo(clip.CachePath).Length == ogg.Length
                && Equal(File.ReadAllBytes(clip.CachePath), ogg);
            if (!same) File.WriteAllBytes(clip.CachePath, ogg);
        }
        return data;
    }

    private static bool Equal(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }

    internal static byte[] Rewrap(byte[] fsb, Clip clip, Setup setup)
    {
        if (fsb.Length < 68 || Encoding.ASCII.GetString(fsb, 0, 4) != "FSB5"
            || BitConverter.ToUInt32(fsb, 4) != 1 || BitConverter.ToUInt32(fsb, 8) != 1
            || BitConverter.ToUInt32(fsb, 24) != 15 || clip.packetOffset < 68 || clip.packetOffset >= fsb.Length)
            throw new InvalidDataException("Unsupported original audio bank layout");
        byte[] setupPacket = Convert.FromBase64String(setup.header);
        if (setup.flags.Length < 1 || setup.flags.Length > 16 || setupPacket.Length < 7
            || setupPacket[0] != 5 || Encoding.ASCII.GetString(setupPacket, 1, 6) != "vorbis")
            throw new InvalidDataException("Invalid Vorbis setup metadata");
        var packets = new List<byte[]>();
        using (var input = new BinaryReader(new MemoryStream(fsb, false)))
        {
            input.BaseStream.Position = clip.packetOffset;
            while (input.BaseStream.Position + 2 <= fsb.Length)
            {
                int length = input.ReadUInt16();
                if (length == 0 || length == ushort.MaxValue) break;
                if (length > fsb.Length - input.BaseStream.Position || packets.Count >= 10000)
                    throw new InvalidDataException("Original audio packet out of range");
                packets.Add(input.ReadBytes(length));
            }
        }
        if (packets.Count == 0) throw new InvalidDataException("Original audio has no packets");
        using var output = new MemoryStream();
        uint sequence = 0;
        WritePage(output, InfoPacket(clip), 0, 2, sequence++);
        WritePage(output, CommentPacket(), 0, 0, sequence++);
        WritePage(output, setupPacket, 0, 0, sequence++);
        int modeBits = 0;
        while ((1 << modeBits) < setup.flags.Length) modeBits++;
        long granule = 0;
        int previousBlock = 0;
        for (int i = 0; i < packets.Count; i++)
        {
            byte[] packet = packets[i];
            int mode = (packet[0] >> 1) & ((1 << modeBits) - 1);
            if ((packet[0] & 1) != 0 || mode >= setup.flags.Length) throw new InvalidDataException("Invalid Vorbis audio mode");
            int block = setup.flags[mode] == 0 ? 256 : 2048;
            if (previousBlock != 0) granule += (block + previousBlock) / 4;
            previousBlock = block;
            bool last = i == packets.Count - 1 || granule >= clip.samples;
            granule = Math.Min(granule, clip.samples);
            WritePage(output, packet, last ? clip.samples : granule, last ? (byte)4 : (byte)0, sequence++);
            if (last) break;
        }
        return output.ToArray();
    }

    private static byte[] InfoPacket(Clip clip)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write((byte)1); writer.Write(Encoding.ASCII.GetBytes("vorbis")); writer.Write(0);
        writer.Write((byte)clip.channels); writer.Write(clip.frequency);
        writer.Write(0); writer.Write(0); writer.Write(0); writer.Write((byte)0xB8); writer.Write((byte)1);
        return output.ToArray();
    }

    private static byte[] CommentPacket()
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write((byte)3); writer.Write(Encoding.ASCII.GetBytes("vorbis"));
        byte[] vendor = Encoding.ASCII.GetBytes("Enhanced Spectator local original audio");
        writer.Write(vendor.Length); writer.Write(vendor); writer.Write(0); writer.Write((byte)1);
        return output.ToArray();
    }

    // One complete packet per Ogg page. All original packets and selected setup headers fit in one page.
    private static void WritePage(Stream destination, byte[] packet, long granule, byte flags, uint sequence)
    {
        int segments = packet.Length / 255 + 1;
        if (segments > 255) throw new InvalidDataException("Vorbis packet exceeds bounded page size");
        using var page = new MemoryStream(); using var writer = new BinaryWriter(page);
        writer.Write(Encoding.ASCII.GetBytes("OggS")); writer.Write((byte)0); writer.Write(flags);
        writer.Write(granule); writer.Write(1u); writer.Write(sequence); writer.Write(0u); writer.Write((byte)segments);
        for (int i = 0; i < segments; i++) writer.Write((byte)Math.Min(255, packet.Length - i * 255));
        writer.Write(packet);
        byte[] bytes = page.ToArray();
        uint crc = 0;
        foreach (byte value in bytes)
        {
            crc ^= (uint)value << 24;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
        }
        Array.Copy(BitConverter.GetBytes(crc), 0, bytes, 22, 4);
        destination.Write(bytes, 0, bytes.Length);
    }
}
