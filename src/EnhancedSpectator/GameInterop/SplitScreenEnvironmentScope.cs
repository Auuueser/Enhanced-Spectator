using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

// The original spectator effects are global, so secondary views borrow only their
// visual lighting/fog state. They never apply reverb, HUD, weather actions or player effects.
internal sealed class SplitScreenEnvironmentScope
{
    private Light? _direct, _indirect, _cachedIndirect;
    private HDAdditionalLightData? _indirectData, _cachedIndirectData;
    private LocalVolumetricFog? _fog;
    private bool _directEnabled, _indirectEnabled;
    private float _dimmer, _meanFreePath;

    internal void Begin(PlayerControllerB target)
    {
        Restore();
        var time = TimeOfDay.Instance;
        if (time != null)
        {
            _direct = time.sunDirect; _indirect = time.sunIndirect;
            if (_direct != null) { _directEnabled = _direct.enabled; _direct.enabled = !target.isInsideFactory; }
            if (_indirect != null)
            {
                _indirectEnabled = _indirect.enabled; _indirect.enabled = !target.isInsideFactory;
                if (_cachedIndirect != _indirect) { _cachedIndirect = _indirect; _cachedIndirectData = _indirect.GetComponent<HDAdditionalLightData>(); }
                _indirectData = _cachedIndirectData;
                if (_indirectData != null)
                { _dimmer = _indirectData.lightDimmer; _indirectData.lightDimmer = target.isInsideFactory || target.isInHangarShipRoom ? 0 : 1; }
            }
        }
        var trigger = target.currentAudioTrigger;
        if (trigger != null && trigger.localFog != null)
        {
            _fog = trigger.localFog; _meanFreePath = _fog.parameters.meanFreePath;
            _fog.parameters.meanFreePath = trigger.toggleLocalFog ? trigger.fogEnabledAmount : 200;
        }
    }

    internal void Restore()
    {
        if (_direct != null) _direct.enabled = _directEnabled;
        if (_indirect != null) _indirect.enabled = _indirectEnabled;
        if (_indirectData != null) _indirectData.lightDimmer = _dimmer;
        if (_fog != null) _fog.parameters.meanFreePath = _meanFreePath;
        _direct = _indirect = null; _indirectData = null; _fog = null;
    }
}
