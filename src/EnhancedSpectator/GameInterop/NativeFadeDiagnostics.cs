using System;
using System.Globalization;
using System.Text;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop;

// A single default-head burst globally: 12 adjacent frames at most once per 8s.
// Normal play allocates no traces/readbacks. Pixel values are numeric summaries,
// not screenshots; the sampled patch may include background or a hole in the mesh.
internal static class NativeFadeDiagnostics
{
    private sealed class Row
    {
        internal int Frame;
        internal float Delta, Alpha = float.NaN;
        internal int Draws;
        internal string Pose = string.Empty, Camera = string.Empty, Region = string.Empty;
        internal readonly StringBuilder Events = new StringBuilder(256);
        internal readonly Color[][] Pixels = new Color[3][];
        internal readonly bool[] Requested = new bool[3];
        internal readonly string[] Errors = new string[3];
    }
    private static readonly NativeFadeDiagnosticSchedule Schedule = new NativeFadeDiagnosticSchedule();
    private static readonly Row?[] Rows = new Row?[NativeFadeDiagnosticSchedule.Frames];
    private static LethalCompanyNativeFade? _owner;
    private static int _lastFrame, _pending, _generation;
    private static float _started;
    private static string _scene = string.Empty;

    internal static void Begin(LethalCompanyNativeFade owner, float opacity)
    {
        if (!ModLog.IsDebugEnabled) { Cancel(); return; }
        Flush();
        if (_owner == null && owner.Key == "Default" && owner.DiagnosticVisible && opacity < .99999f && opacity > 0f
            && Schedule.TryStart(Time.frameCount, Time.unscaledTime))
        {
            _owner = owner; _started = Time.unscaledTime; _generation++;
            var round = StartOfRound.Instance;
            var camera = LethalCompanyFearViewCamera.ActiveView;
            _scene = $"build={NativeFadePass.ImplementationId}, owner={owner.GetHashCode():X}, orbit={(round != null ? round.inShipPhase.ToString() : "unknown")}, testRoom={(round != null && round.testRoom != null)}, camera={camera?.name}/{camera?.GetInstanceID()}, shader={owner.DiagnosticMaterial()}";
        }
        if (_owner != owner) return;
        var row = Current();
        if (row != null && !row.Requested[0]) row.Alpha = opacity;
        Trace(owner, "begin-request");
    }
    private static Row? Current()
    {
        if (!ModLog.IsDebugEnabled || _owner == null) return null;
        int slot = Schedule.Slot(Time.frameCount);
        if (slot < 0) return null;
        _lastFrame = Time.frameCount;
        return Rows[slot] ??= new Row { Frame = Time.frameCount, Delta = Time.unscaledDeltaTime, Pose = _owner.DiagnosticPose() };
    }
    internal static void CameraEvent(string stage, Camera camera)
    {
        if (_owner != null) Trace(_owner, stage, camera);
    }
    internal static void Trace(LethalCompanyNativeFade owner, string stage, Camera? camera = null)
    {
        if (!ModLog.IsDebugEnabled || _owner != owner) return;
        var row = Current();
        if (row == null || row.Events.Length > 1800) return;
        row.Events.Append(stage);
        if (camera != null) row.Events.Append('@').Append(camera.name).Append('/').Append(camera.GetInstanceID());
        row.Events.Append('[').Append(owner.DiagnosticState()).Append("]>");
    }
    internal static void Pixels(LethalCompanyNativeFade owner, CommandBuffer cmd, RenderTexture texture, Rect region, int stage)
    {
        if (!ModLog.IsDebugEnabled || _owner != owner) return;
        var row = Current();
        if (row == null) return;
        if (stage == 0) row.Draws++;
        if (row.Requested[stage]) return;
        if (stage == 0)
        {
            row.Alpha = owner.Opacity; row.Region = region.ToString();
            var camera = owner.Camera;
            if (camera != null) row.Camera = $"camPos={camera.transform.position.ToString("F3")},jitter=({F(camera.projectionMatrix.m02)},{F(camera.projectionMatrix.m12)})";
        }
        row.Requested[stage] = true;
        int generation = _generation; _pending++;
        try
        {
            NativeFadePixelProbe.Enqueue(cmd, texture, region, (pixels, error) =>
            {
                if (_generation != generation) return;
                row.Pixels[stage] = pixels; row.Errors[stage] = error; _pending--;
            });
        }
        catch (Exception ex) { row.Errors[stage] = ex.GetType().Name; _pending--; }
    }
    internal static void Flush()
    {
        if (!ModLog.IsDebugEnabled) { Cancel(); return; }
        if (_owner == null || Schedule.Slot(Time.frameCount) >= 0) return;
        if (_pending > 0 && Time.unscaledTime - _started < 3f) return;
        var text = new StringBuilder("NativeFade forensic burst: ");
        text.Append(_scene).Append(", throughFrame=").Append(_lastFrame).Append(", pending=").Append(_pending);
        text.Append("; RGB are linear patch means; scene/native/final hashes are pixel fingerprints. blendError tests final=(1-alpha)*scene+alpha*native, NOT post-processing.\n");
        foreach (var row in Rows)
        {
            if (row == null) continue;
            text.Append("f=").Append(row.Frame).Append(" dtMs=").Append(F(row.Delta * 1000f)).Append(" alpha=").Append(F(row.Alpha)).Append(" draws=").Append(row.Draws);
            for (int i = 0; i < 3; i++) text.Append(' ').Append(i == 0 ? "scene=" : i == 1 ? "native=" : "final=").Append(Summary(row.Pixels[i], row.Errors[i]));
            text.Append(" blendError=").Append(BlendError(row)).Append(' ').Append(row.Pose).Append(' ').Append(row.Camera).Append(" region=").Append(row.Region).Append(" events=").Append(row.Events);
            if (row.Events.Length > 1800) text.Append("TRUNCATED");
            text.Append('\n');
        }
        ModLog.Debug(text.ToString());
        Cancel();
    }
    private static string Summary(Color[]? pixels, string? error)
    {
        if (!string.IsNullOrEmpty(error)) return error!;
        if (pixels == null || pixels.Length == 0) return "unavailable";
        Color sum = Color.clear; uint hash = 2166136261;
        foreach (var p in pixels)
        {
            sum += p;
            unchecked { hash = (hash ^ (uint)(p.r.GetHashCode())) * 16777619; hash = (hash ^ (uint)p.g.GetHashCode()) * 16777619; hash = (hash ^ (uint)p.b.GetHashCode()) * 16777619; }
        }
        sum /= pixels.Length;
        return $"({F(sum.r)},{F(sum.g)},{F(sum.b)})#{hash:X8}";
    }
    private static string BlendError(Row row)
    {
        var a = row.Pixels[0]; var b = row.Pixels[1]; var c = row.Pixels[2];
        if (a == null || b == null || c == null || a.Length == 0 || a.Length != b.Length || b.Length != c.Length) return "unavailable";
        float error = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            Color expected = a[i] * (1f - row.Alpha) + b[i] * row.Alpha;
            error += Mathf.Abs(c[i].r - expected.r) + Mathf.Abs(c[i].g - expected.g) + Mathf.Abs(c[i].b - expected.b);
        }
        return F(error / (a.Length * 3));
    }
    private static string F(float value) => value.ToString("F5", CultureInfo.InvariantCulture);
    internal static void Cancel()
    {
        if (_owner == null) return;
        _generation++; _owner = null; _pending = 0; Array.Clear(Rows, 0, Rows.Length);
    }
}
