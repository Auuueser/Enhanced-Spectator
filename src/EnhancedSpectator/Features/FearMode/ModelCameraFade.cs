using System;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Whole-model fading; failures preserve native surfaces without material replacement.</summary>
internal sealed class ModelCameraFade : IDisposable
{
    private readonly LethalCompanyNativeFade _native;
    internal bool Ready => _native.Ready;
    internal ModelCameraFade(Renderer[] renderers, string key) => _native = new LethalCompanyNativeFade(renderers, key);
    internal void Prepare() => _native.Prepare();
    internal void Begin(float opacity) { Restore(); _native.Begin(opacity); }
    internal void Restore() => _native.Restore();
    internal void EndCamera(Camera camera) => _native.EndCamera(camera);
    internal void Disable() => _native.Disable();
    public void Dispose() => _native.Dispose();
}
