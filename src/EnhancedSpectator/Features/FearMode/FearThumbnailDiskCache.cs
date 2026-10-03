using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Local raw RGBA cache. File I/O and validation never run on the Unity thread.</summary>
internal sealed class FearThumbnailDiskCache
{
    internal const int Size = 256;
    internal const int PixelBytes = Size * Size * 4;
    private readonly string _root;
    private readonly Func<string> _environment;
    private Task<string>? _directory;

    internal FearThumbnailDiskCache(string root, Func<string> environment)
    { _root = root; _environment = environment; }

    // Lazy: constructing the menu service during save loading performs no cache work.
    private Task<string> DirectoryTask => _directory ??= Task.Run(() =>
        Path.Combine(_root, Hash("rgba-v1-pose-20260930\n" + _environment())));

    internal async Task<byte[]?> Read(string key)
    {
        try
        {
            string directory = await DirectoryTask.ConfigureAwait(false);
            return await Task.Run(() =>
            {
                string path = Path.Combine(directory, Hash(key) + ".rgba");
                if (!File.Exists(path) || new FileInfo(path).Length != PixelBytes + 32) return null;
                byte[] file = File.ReadAllBytes(path);
                if (file.Length != PixelBytes + 32) return null;
                var pixels = new byte[PixelBytes];
                Buffer.BlockCopy(file, 32, pixels, 0, PixelBytes);
                using var sha = SHA256.Create();
                byte[] digest = sha.ComputeHash(pixels);
                for (int i = 0; i < digest.Length; i++) if (digest[i] != file[i]) return null;
                return pixels;
            }).ConfigureAwait(false);
        }
        catch { return null; } // A missing, obsolete or damaged cache is regenerated from game data.
    }

    internal async Task<bool> Write(string key, byte[] pixels)
    {
        if (pixels.Length != PixelBytes) return false;
        string? temporary = null;
        try
        {
            string directory = await DirectoryTask.ConfigureAwait(false);
            return await Task.Run(() =>
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, Hash(key) + ".rgba");
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var output = File.Create(temporary))
                {
                    using var sha = SHA256.Create();
                    byte[] digest = sha.ComputeHash(pixels);
                    output.Write(digest, 0, digest.Length);
                    output.Write(pixels, 0, pixels.Length);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }).ConfigureAwait(false);
        }
        catch { return false; } // Read-only caches must never disable the model menu.
        finally
        {
            if (temporary != null) try { File.Delete(temporary); } catch { }
        }
    }

    internal static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }

    internal static string EnvironmentStamp(string gameAssembly, string pluginDirectory)
    {
        var stamp = new StringBuilder();
        AppendFile(stamp, gameAssembly);
        string[] files = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
            if (!string.Equals(Path.GetFileName(file), "EnhancedSpectator.dll", StringComparison.OrdinalIgnoreCase))
                AppendFile(stamp, file);
        return stamp.ToString();
    }

    private static void AppendFile(StringBuilder stamp, string path)
    {
        var file = new FileInfo(path);
        stamp.Append(path).Append('|').Append(file.Length).Append('|').Append(file.LastWriteTimeUtc.Ticks).Append('\n');
    }
}
