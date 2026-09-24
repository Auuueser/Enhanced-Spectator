using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace EnhancedSpectator.GameInterop;

// Layout metadata only. Geometry, textures, colors and transforms stay in the user's game files.
// Every bounded range is verified before use; other game revisions fail closed to the scene source.
[DataContract]
internal sealed class V81DropshipData
{
    [DataMember] public Chunk[] chunks = Array.Empty<Chunk>();
    [DataMember] public Texture[] textures = Array.Empty<Texture>();
    [DataMember] public Material[] materials = Array.Empty<Material>();
    [DataMember] public Mesh[] meshes = Array.Empty<Mesh>();
    internal byte[][] Bytes = Array.Empty<byte[]>();

    [DataContract] internal sealed class Chunk
    {
        [DataMember] public string file = "";
        [DataMember] public long offset;
        [DataMember] public int size;
        [DataMember] public string sha256 = "";
    }
    [DataContract] internal sealed class Texture
    {
        [DataMember] public int chunk, width, height, format, mips;
    }
    [DataContract] internal sealed class Material
    {
        [DataMember] public int color, emission, texture, normal, uv;
    }
    [DataContract] internal sealed class Mesh
    {
        [DataMember] public string name = "";
        [DataMember] public int transform, vertices, indices, vertexCount;
        [DataMember] public int[] materials = Array.Empty<int>();
        [DataMember] public Submesh[] submeshes = Array.Empty<Submesh>();
    }
    [DataContract] internal sealed class Submesh
    {
        [DataMember] public int start, count;
    }

    internal static V81DropshipData Read(string gameDataDirectory)
    {
        using var manifest = typeof(V81DropshipData).Assembly.GetManifestResourceStream("EnhancedSpectator.GameInterop.V81DropshipLayout.json")
            ?? throw new InvalidDataException("Missing V81 visual layout metadata");
        var data = (V81DropshipData)new DataContractJsonSerializer(typeof(V81DropshipData)).ReadObject(manifest)!;
        data.Bytes = new byte[data.chunks.Length][];
        for (int i = 0; i < data.chunks.Length; i++) data.Bytes[i] = ReadChunk(gameDataDirectory, data.chunks[i]);
        return data;
    }

    internal static byte[] ReadChunk(string directory, Chunk chunk)
    {
        if (Path.GetFileName(chunk.file) != chunk.file || chunk.size < 1 || chunk.size > 2 * 1024 * 1024 || chunk.offset < 0)
            throw new InvalidDataException("Invalid bounded V81 resource range");
        using var input = new FileStream(Path.Combine(directory, chunk.file), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (chunk.offset > input.Length - chunk.size) throw new InvalidDataException("V81 resource range outside file");
        input.Position = chunk.offset;
        var bytes = new byte[chunk.size];
        int position = 0;
        while (position < bytes.Length)
        {
            int count = input.Read(bytes, position, bytes.Length - position);
            if (count == 0) throw new EndOfStreamException();
            position += count;
        }
        using var hash = SHA256.Create();
        string actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        if (!string.Equals(actual, chunk.sha256, StringComparison.Ordinal))
            throw new InvalidDataException("V81 resource layout differs: " + chunk.file + " @ " + chunk.offset);
        return bytes;
    }

    internal static float Half(byte[] bytes, int offset)
    {
        int bits = bytes[offset] | bytes[offset + 1] << 8;
        int exponent = (bits >> 10) & 31, fraction = bits & 1023;
        float value = exponent == 0 ? fraction / 16777216f
            : exponent == 31 ? (fraction == 0 ? float.PositiveInfinity : float.NaN)
            : (float)Math.Pow(2, exponent - 15) * (1 + fraction / 1024f);
        return (bits & 32768) == 0 ? value : -value;
    }
}
