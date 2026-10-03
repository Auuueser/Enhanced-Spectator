using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

internal sealed class ModelTargetFadeState
{
    internal ulong? ClientId, SlotId;
    private readonly ModelOpacityTransition _transition = new ModelOpacityTransition();
    private float _nextStateDiagnostic;
    private readonly FadeFrameDiagnostics _diagnostics = new FadeFrameDiagnostics();

    internal float CurrentOpacity => _transition.Current;
    internal void SeedOpacity(float opacity) => _transition.Seed(opacity);

    internal float Update(Vector3 modelPosition, bool enabled, bool ready = true, string key = "unspecified", Transform? modelTransform = null, Bounds? localEnvelope = null)
    {
        enabled = enabled && NativeFadePass.ShouldFadeTarget(ClientId, SlotId);
        float target = 1f, distance = float.NaN;
        bool found = false;
        Bounds body = default;
        if (enabled && LethalCompanyWatchedBody.TryGetBounds(ClientId, SlotId, out body))
        {
            found = true;
            distance = modelTransform != null && localEnvelope.HasValue
                ? ModelFadeGeometry.Distance(localEnvelope.Value, modelTransform.localToWorldMatrix, modelTransform.worldToLocalMatrix, body)
                : Vector3.Distance(modelPosition, body.ClosestPoint(modelPosition));
            target = FearModelAppearanceRules.Opacity(distance, true, NativeFadePass.FadeRadius);
        }
        float opacity = _transition.Update(target, enabled && ready, Time.frameCount, Time.unscaledDeltaTime);
        if (ModLog.IsDebugEnabled)
        {
            _diagnostics.Sample(Time.frameCount, ready, found, opacity);
            if (Time.unscaledTime >= _nextStateDiagnostic)
            {
                _nextStateDiagnostic = Time.unscaledTime + 5f;
                ModLog.Debug($"NativeFade frame window: model={key}, instance={GetHashCode():X}, enabled={enabled}, ready={ready}, target={ClientId}/{SlotId}, body={found}, distance={distance:F3}, desired={target:F5}, current={opacity:F5}, frames={_diagnostics.Frames}, notReady={_diagnostics.NotReady}, missingBody={_diagnostics.MissingBody}, readyChanges={_diagnostics.ReadinessChanges}, alphaRange={_diagnostics.MinOpacity:F5}..{_diagnostics.MaxOpacity:F5}, largestStep={_diagnostics.LargestStep:F5}, modelPosition={modelPosition}, radius={NativeFadePass.FadeRadius:F2}, {LethalCompanyFearViewCamera.DescribeViewer()}");
                _diagnostics.Clear();
            }
        }
        else { _diagnostics.Clear(); _nextStateDiagnostic = 0f; }
        return opacity;
    }
}
