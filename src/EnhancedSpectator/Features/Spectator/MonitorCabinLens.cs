using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>A temporary cabin lens, independent of saved player/first-person FOV preferences.</summary>
internal sealed class MonitorCabinLens
{
    private float _value,_velocity;
    internal bool Active { get; private set; }
    internal void Clear() { Active=false; _velocity=0; }
    internal float Update(bool cabin,float current,float original,float dt,bool covered=false)
    {
        if(!Active)
        {
            if(!cabin) return original;
            _value=current; _velocity=0; Active=true;
        }
        float goal=cabin ? Mathf.Max(90f,original) : original;
        if(cabin && covered) { _value=goal; _velocity=0; return _value; }
        if(dt<=0) return _value;
        _value=Mathf.SmoothDamp(_value,goal,ref _velocity,.45f,40f,Mathf.Clamp(dt,0,.1f));
        if(!cabin && Mathf.Abs(_value-original)<.01f) { Clear(); _value=original; }
        return _value;
    }
}
