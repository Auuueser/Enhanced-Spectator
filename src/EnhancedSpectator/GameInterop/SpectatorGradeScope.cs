using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EnhancedSpectator.GameInterop;

/// <summary>Edits only a resolved per-camera stack, never scene VolumeProfiles or materials.</summary>
internal sealed class SpectatorGradeScope
{
    private sealed class Saved<T>
    {
        private readonly VolumeParameter<T> _parameter;
        private T _before=default!, _applied=default!;
        private bool _override, _set;
        internal Saved(VolumeParameter<T> p) { _parameter=p; }
        internal void Set(T value) { _before=_parameter.value; _override=_parameter.overrideState; _applied=value; _set=true; _parameter.Override(value); }
        internal void Restore()
        {
            if (_set && EqualityComparer<T>.Default.Equals(_parameter.value,_applied) && _parameter.overrideState)
            { _parameter.value=_before; _parameter.overrideState=_override; }
            _set=false;
        }
    }
    private readonly ColorAdjustments _color;
    private readonly LiftGammaGain _lgg;
    private readonly DepthOfField _dof;
    private readonly Saved<float> _exposure, _saturation, _contrast, _nearStart, _nearEnd, _farStart, _farEnd;
    private readonly Saved<Vector4> _gamma;
    private readonly Saved<Color> _filter;
    private readonly Saved<DepthOfFieldMode> _focusMode;
    private bool _colorActive, _lggActive, _dofActive, _active;
    internal readonly VolumeStack Stack;
    internal SpectatorGradeScope(VolumeStack stack)
    {
        Stack=stack; _color=stack.GetComponent<ColorAdjustments>(); _lgg=stack.GetComponent<LiftGammaGain>(); _dof=stack.GetComponent<DepthOfField>();
        _exposure=new Saved<float>(_color.postExposure); _saturation=new Saved<float>(_color.saturation); _contrast=new Saved<float>(_color.contrast);
        _gamma=new Saved<Vector4>(_lgg.gamma); _filter=new Saved<Color>(_color.colorFilter);
        _focusMode=new Saved<DepthOfFieldMode>(_dof.focusMode);
        _nearStart=new Saved<float>(_dof.nearFocusStart); _nearEnd=new Saved<float>(_dof.nearFocusEnd);
        _farStart=new Saved<float>(_dof.farFocusStart); _farEnd=new Saved<float>(_dof.farFocusEnd);
    }
    internal void Apply(float brightness, int filter, float strength, float blur, float focus, bool wide)
    {
        Restore(); _colorActive=_color.active; _lggActive=_lgg.active; _dofActive=_dof.active; _active=true;
        brightness=Mathf.Clamp(brightness,0,2); strength=Mathf.Clamp01(strength);
        if(brightness>0)
        {
            Vector4 gamma=_lgg.gamma.value; gamma.w+=brightness*.28f;
            _gamma.Set(gamma); _exposure.Set(_color.postExposure.value+brightness*.15f);
            _color.active=_lgg.active=true;
        }
        if(filter>0 && strength>0)
        {
            _color.active=true;
            Color tint=filter==1 ? new Color(.93f,.97f,1) : filter==2 ? new Color(1,.98f,.94f) : new Color(.91f,1,.93f);
            _filter.Set(_color.colorFilter.value*Color.Lerp(Color.white,tint,strength));
            _saturation.Set(Mathf.Clamp(_color.saturation.value-(filter==3?65:filter==1?18:6)*strength,-100,100));
            _contrast.Set(Mathf.Clamp(_color.contrast.value+(filter==2?4:filter==1?2:-4)*strength,-100,100));
        }
        if(blur>0)
        {
            _dof.active=true;
            _focusMode.Set(wide ? DepthOfFieldMode.Off : DepthOfFieldMode.Manual);
            float band=Mathf.Lerp(6,1.5f,Mathf.Clamp01(blur)); focus=Mathf.Max(.3f,focus);
            _nearStart.Set(0); _nearEnd.Set(Mathf.Max(0,focus-band));
            _farStart.Set(focus+band); _farEnd.Set(focus+band+Mathf.Lerp(25,5,blur));
        }
    }
    internal void Restore()
    {
        if(!_active) return;
        _exposure.Restore(); _saturation.Restore(); _contrast.Restore(); _filter.Restore(); _gamma.Restore();
        _focusMode.Restore(); _nearStart.Restore(); _nearEnd.Restore(); _farStart.Restore(); _farEnd.Restore();
        if(_color.active) _color.active=_colorActive;
        if(_lgg.active) _lgg.active=_lggActive;
        if(_dof.active) _dof.active=_dofActive;
        _active=false;
    }
}
