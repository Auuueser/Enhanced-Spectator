using System.Collections.Generic;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnhancedSpectator.Features.FearMode;

public sealed partial class FearModeVisualService
{
    // Inactive clean renderer clones only. No enemy/item behaviours or shared mutable fade materials.
    private readonly VisualReusePool<(string Model, int Hierarchy, int Renderers, int Mask), RuntimeEnemyVisual> _idleVisuals =
        new VisualReusePool<(string, int, int, int), RuntimeEnemyVisual>(4);

    private void SceneUnloaded(Scene scene) => ClearAll();

    private sealed class PendingVisual
    {
        internal readonly (string Model, int Hierarchy, int Renderers, int Mask) Key;
        internal readonly RuntimeVisualBuild Build;
        internal bool Ready;
        internal PendingVisual((string, int, int, int) key, RuntimeVisualBuild build) { Key = key; Build = build; }
    }
    private readonly Dictionary<ulong, PendingVisual> _pendingVisuals = new Dictionary<ulong, PendingVisual>();
    private readonly HashSet<ulong> _requestedBuildOwners = new HashSet<ulong>();
    private readonly List<ulong> _cancelBuildOwners = new List<ulong>();
    private int _buildCursor;

    private bool TryAcquireVisual(ulong owner, FearVisualSource source, string model, int mask, out RuntimeEnemyVisual? visual, out string reason)
    {
        visual = null;
        reason = "preparing visual across frames";
        if (source.HierarchyRoot == null || source.RendererRoot == null) { reason = "source was unloaded"; return false; }
        _requestedBuildOwners.Add(owner);
        var key = (model, source.HierarchyRoot.GetInstanceID(), source.RendererRoot.GetInstanceID(), mask);
        if (_pendingVisuals.TryGetValue(owner, out var pending) && !pending.Key.Equals(key))
        { pending.Build.Dispose(); _pendingVisuals.Remove(owner); pending = null; }
        if (_idleVisuals.TryTake(key, out visual))
        {
            pending?.Build.Dispose(); _pendingVisuals.Remove(owner);
            FearVisualWorkSchedule.Shared.ModelChanged(Time.frameCount);
            reason = "reused cached visual"; return true;
        }
        if (pending == null)
        {
            var build = _visualFactory.BeginCreate(source, model, _config.FearModelTargetHeight.Value,
                _config.FearModelUseOriginalScale.Value, _config.FearModelScaleMultiplier.Value, mask);
            _pendingVisuals.Add(owner, new PendingVisual(key, build));
            return false;
        }
        if (!pending.Ready) return false;
        visual = pending.Build.Take();
        reason = pending.Build.Reason;
        if (visual == null) return false; // Failed source stays suppressed until changed; no per-frame retry storm.
        visual.ReuseKey = key;
        pending.Build.Dispose(); _pendingVisuals.Remove(owner);
        return true;
    }

    private void AdvancePendingVisuals()
    {
        _cancelBuildOwners.Clear();
        foreach (var pair in _pendingVisuals)
            if (!_requestedBuildOwners.Contains(pair.Key)) _cancelBuildOwners.Add(pair.Key);
        foreach (ulong id in _cancelBuildOwners) { _pendingVisuals[id].Build.Dispose(); _pendingVisuals.Remove(id); }
        int runnable = 0;
        foreach (var pending in _pendingVisuals.Values) if (!pending.Ready) runnable++;
        if (runnable == 0) return;
        int skip = _buildCursor % runnable;
        _buildCursor = (skip + 1) % runnable;
        foreach (var pair in _pendingVisuals)
        {
            var pending = pair.Value;
            if (pending.Ready || skip-- > 0) continue;
            FearVisualWorkSchedule.Shared.ModelChanged(Time.frameCount);
            if (!pending.Build.Work.Done) pending.Build.Work.Advance();
            else
            {
                // Material capability checks are separated from clone/bake work by a frame.
                if (pending.Build.Work.Failure == null && pending.Build.Visual != null)
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (_config.Camera.FadeModelsNearby.Value) pending.Build.Visual.PrepareFadeMaterials();
                    double prepareMs = (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
                    var work = pending.Build.Work;
                    pending.Build.Reason += $", cloneMs={work.TotalMilliseconds:F2}, maxSliceMs={work.MaxStepMilliseconds:F2}, slices={work.Slices}, fadePrepareMs={prepareMs:F2}";
                }
                else
                {
                    pending.Build.Visual?.Dispose(); pending.Build.Visual = null;
                    ModLog.Warning($"Fear visual build failed: model={pending.Key.Model}, reason={pending.Build.Reason}, error={pending.Build.Work.Failure?.GetType().Name}.");
                }
                pending.Ready = true;
            }
            break; // One shared frame budget, not a full budget for every spectator.
        }
    }

    private void ClearPendingVisuals()
    {
        foreach (var pending in _pendingVisuals.Values) pending.Build.Dispose();
        _pendingVisuals.Clear(); _requestedBuildOwners.Clear(); _buildCursor = 0;
    }

    private void ParkVisual(RuntimeEnemyVisual visual)
    {
        visual.ParkForReuse();
        _idleVisuals.Return(visual.ReuseKey, visual);
    }
}
