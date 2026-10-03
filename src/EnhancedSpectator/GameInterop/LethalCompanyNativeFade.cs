using System;
using System.Collections.Generic;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Whole-model fade ownership. Never changes source materials or shaders.</summary>
internal sealed class LethalCompanyNativeFade : IDisposable
{
    private readonly Renderer[] _renderers;
    private readonly NativeFadeRendererScope _scope;
    private readonly NativeFadeRequestState _request = new NativeFadeRequestState();
    private bool _registered, _unsupported, _warned;
    private int _diagnosticSubmittedFrame = -1, _diagnosticCompleted, _diagnosticMissed;
    private int _diagnosticHidden, _diagnosticOffscreen;
    private float _nextDiagnostic;
    internal readonly string Key;
    internal uint Tag { get; private set; }
    internal float Opacity { get; private set; }
    internal Camera? Camera { get; private set; }
    internal int Frame => _request.Frame;
    internal float SortDistance { get; private set; }
    internal bool Ready => !_unsupported && _registered && NativeFadePass.CanTakeCamera(LethalCompanyFearViewCamera.ActiveView);
    internal LethalCompanyNativeFade(Renderer[] renderers, string key)
    { _renderers = renderers; _scope = new NativeFadeRendererScope(renderers); Key = key; }
    internal void Prepare()
    {
        if (_registered || _unsupported) return;
        var reported = ModLog.IsDebugEnabled ? new HashSet<Material>() : null;
        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (reported != null && reported.Add(material))
                    ModLog.Debug($"NativeFade source: model={Key}, material={material.name}, shader={material.shader.name}, queue={material.renderQueue}, passes={material.passCount}, originalForward={HasForwardPass(material)}");
                if (!material.shader.isSupported || !HasForwardPass(material)
                    || (material.renderQueue <= 2500 && !HasDepthPass(material)))
                {
                    _unsupported = true;
                    ModLog.Warning($"Native fade unsupported: model={Key}, material={material.name}, shader={material.shader.name}; keeping original rendering.");
                    return;
                }
            }
        }
        _registered = NativeFadePass.Register(this);
        if (_registered)
            ModLog.Debug($"NativeFade model prepared: model={Key}, renderers={_renderers.Length}; original materials retained, waiting for camera readiness.");
        if (!_registered) _unsupported = true;
    }
    private static bool HasForwardPass(Material material)
    {
        foreach (string pass in NativeFadePass.ForwardPassNames)
            if (material.FindPass(pass) >= 0 && material.GetShaderPassEnabled(pass)) return true;
        return false;
    }
    private static bool HasDepthPass(Material material)
    {
        foreach (string pass in NativeFadePass.DepthPassNames)
            if (material.FindPass(pass) >= 0 && material.GetShaderPassEnabled(pass)) return true;
        return false;
    }
    internal void Begin(float opacity)
    {
        NativeFadeDiagnostics.Begin(this, opacity);
        ClearRequest();
        // Do not isolate fully opaque models: post-processing must see their original depth.
        if (!Features.FearMode.FearModelAppearanceRules.NeedsComposite(opacity) || !Ready) return;
        var camera = LethalCompanyFearViewCamera.ActiveView;
        if (camera == null) return;
        NativeFadePass.ExpireRequests();
        Tag = NativeFadePass.Slots.Acquire();
        if (Tag == 0)
        {
            if (!_warned) { _warned = true; ModLog.Warning($"Native fade capacity reached: model={Key}; preserving original rendering."); }
            return;
        }
        if (!_scope.Begin(camera.cullingMask, NativeFadePass.IsolationLayer, Tag)) { ClearRequest(); return; }
        Camera = camera; _request.Begin(Time.frameCount, camera.GetInstanceID());
        Opacity = float.IsNaN(opacity) || float.IsInfinity(opacity) ? 1f : Mathf.Clamp01(opacity);
        SortDistance = (_scope.FirstBounds.center - camera.transform.position).sqrMagnitude;
        NativeFadeDiagnostics.Trace(this, "isolated", camera);
    }
    internal Rect RenderRegion(Camera camera, int width, int height)
    {
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in _renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
            if (found) bounds.Encapsulate(renderer.bounds); else { bounds = renderer.bounds; found = true; }
        }
        return found ? NativeFadeRenderRegion.Project(camera, bounds, width, height) : new Rect();
    }
    internal void Restore()
    {
        if (_scope.IsActive) NativeFadeDiagnostics.Trace(this, "restore");
        _scope.Restore();
    }
    internal void Trace(string stage, Camera? camera = null) => NativeFadeDiagnostics.Trace(this, stage, camera);
    internal bool DiagnosticVisible => _renderers.Length > 0 && _renderers[0] != null
        && _renderers[0].enabled && !_renderers[0].forceRenderingOff && _renderers[0].gameObject.activeInHierarchy;
    internal string DiagnosticState()
    {
        var r = _renderers.Length > 0 ? _renderers[0] : null;
        return r == null ? "no-renderer" : $"i={(IsIsolated ? 1 : 0)},L={r.gameObject.layer},R={r.renderingLayerMask:X},T={Tag:X},v={(r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff ? 1 : 0)}";
    }
    internal string DiagnosticPose()
    {
        var r = _renderers.Length > 0 ? _renderers[0] : null;
        return r == null ? "none" : $"pos={r.transform.position.ToString("F3")},bounds={r.bounds.size.ToString("F3")},material={r.sharedMaterial?.GetInstanceID()}";
    }
    internal string DiagnosticMaterial()
    {
        var r = _renderers.Length > 0 ? _renderers[0] : null;
        var m = r == null ? null : r.sharedMaterial;
        if (m == null) return "none";
        string result = $"{m.shader.name}/{m.name}/{m.GetInstanceID()},queue={m.renderQueue}";
        foreach (string property in new[] { "_ZTestDepthEqualForOpaque", "_AlphaCutoffEnable", "_ReceivesSSR", "_Smoothness" })
            if (m.HasProperty(property)) result += $",{property}={m.GetFloat(property)}";
        return result;
    }
    internal bool IsIsolated => _scope.IsActive;
    internal void SetLayer(Transform root, int layer) => _scope.SetLayer(root, layer);
    internal void ReapplyForDraw()
    {
        NativeFadeDiagnostics.Trace(this, "before-reapply");
        if (Camera != null && Tag != 0) _scope.Begin(Camera.cullingMask, NativeFadePass.IsolationLayer, Tag);
        NativeFadeDiagnostics.Trace(this, "after-reapply");
    }
    internal void EndCamera(Camera camera)
    {
        NativeFadeDiagnostics.Trace(this, "camera-end", camera);
        Restore();
        if (ModLog.IsDebugEnabled && _request.Matches(Time.frameCount, camera.GetInstanceID()))
        {
            _diagnosticCompleted++;
            if (_diagnosticSubmittedFrame != Time.frameCount) _diagnosticMissed++;
            if (Time.unscaledTime >= _nextDiagnostic)
            {
                _nextDiagnostic = Time.unscaledTime + 5f;
                ModLog.Debug($"NativeFade request window: model={Key}, instance={GetHashCode():X}, camera={camera.name}, completed={_diagnosticCompleted}, noPassSubmission={_diagnosticMissed}, fullyHidden={_diagnosticHidden}, offscreen={_diagnosticOffscreen}, alpha={Opacity:F5}; submission is not pixel validation.");
                _diagnosticCompleted = _diagnosticMissed = _diagnosticHidden = _diagnosticOffscreen = 0;
            }
        }
        else if (!ModLog.IsDebugEnabled)
        {
            _diagnosticCompleted = _diagnosticMissed = _diagnosticHidden = _diagnosticOffscreen = 0;
            _nextDiagnostic = 0f;
        }
        if (_request.EndCamera(camera.GetInstanceID())) ClearRequest();
    }
    internal void RecordSubmission(bool hidden, bool offscreen)
    {
        if (!ModLog.IsDebugEnabled) return;
        _diagnosticSubmittedFrame = Time.frameCount;
        if (hidden) _diagnosticHidden++;
        if (offscreen) _diagnosticOffscreen++;
    }
    internal void Disable() { ClearRequest(); Opacity = 1f; }
    internal void ClearRequest()
    {
        Restore(); NativeFadePass.Slots.Release(Tag); Tag = 0; Camera = null; _request.Clear();
    }
    public void Dispose()
    {
        ClearRequest();
        if (_registered) NativeFadePass.Unregister(this);
        _registered = false;
    }
}
