using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

internal sealed class ModelTargetFadeState
{
    internal ulong? ClientId, SlotId;
    private readonly ModelOpacityTransition _transition = new ModelOpacityTransition();
    private float _nextStateDiagnostic;
    private float _lastDiagnostic = -10f;
    private (bool enabled, bool ready, bool found, bool nearby, ulong? client, ulong? slot)? _lastChainState;

    internal float CurrentOpacity => _transition.Current;
    internal void SeedOpacity(float opacity) => _transition.Seed(opacity);

    internal float Update(Vector3 modelPosition, bool enabled, bool ready = true, string key = "unspecified", Transform? modelTransform = null, Bounds? localEnvelope = null)
    {
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
        {
            var state = (enabled, ready, found, target < .999f, ClientId, SlotId);
            if ((!_lastChainState.HasValue || !_lastChainState.Value.Equals(state)) && Time.unscaledTime >= _nextStateDiagnostic)
            {
                _lastChainState = state;
                _nextStateDiagnostic = Time.unscaledTime + 1f;
                ModLog.Info($"NativeFade target state: model={key}, enabled={enabled}, ready={ready}, target={ClientId}/{SlotId}, body={found}, distance={distance:F3}, desired={target:F5}, current={opacity:F5}, modelPosition={modelPosition}, radius={NativeFadePass.FadeRadius:F2}, localEnvelope={localEnvelope}, bodyBounds={body}, {LethalCompanyFearViewCamera.DescribeViewer()}");
            }
        }
        if (ModLog.IsDebugEnabled && Time.unscaledTime - _lastDiagnostic >= 5f)
        {
            _lastDiagnostic = Time.unscaledTime;
            ModLog.Debug($"Fade chain: model={key}, enabled={enabled}, ready={ready}, backend=whole-model, target={ClientId}/{SlotId}, body={found}, distance={distance:F3}, desired={target:F5}, current={opacity:F5}, camera={LethalCompanyFearViewCamera.ActiveView?.name}");
        }
        return opacity;
    }
}
