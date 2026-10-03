using EnhancedSpectator.Features.Spectator;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>Local optical transition only for obstructed monitor changes; independent of HUD suppression.</summary>
internal static class LethalCompanyCameraTransition
{
    private static Canvas? _canvas;
    private static Image? _image;
    internal static bool Owns(Canvas canvas) => canvas == _canvas;
    internal static void Tick()
    {
        var round=StartOfRound.Instance;
        var local=round!=null ? round.localPlayerController : null;
        float alpha=round!=null && local!=null && local.isPlayerDead && local.isInGameOverAnimation<=0
            && !round.overrideSpectateCamera && round.activeCamera==round.spectateCamera
            ? SpectatorFreecamController.Current?.TransitionOpacity ?? 0 : 0;
        if(alpha<=0) { if(_canvas!=null) _canvas.gameObject.SetActive(false); return; }
        if(_canvas==null)
        {
            var root=new GameObject("EnhancedSpectator Camera Transition",typeof(RectTransform),typeof(Canvas));
            root.hideFlags=HideFlags.DontSave;
            _canvas=root.GetComponent<Canvas>(); _canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder=32760;
            var cover=new GameObject("Optical transition",typeof(RectTransform),typeof(Image));
            cover.transform.SetParent(root.transform,false);
            var rect=(RectTransform)cover.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            _image=cover.GetComponent<Image>(); _image.raycastTarget=false;
        }
        _canvas.gameObject.SetActive(true); _image!.color=new Color(0,0,0,Mathf.Clamp01(alpha));
    }
    internal static void Clear() { if(_canvas!=null) Object.Destroy(_canvas.gameObject); _canvas=null; _image=null; }
}
