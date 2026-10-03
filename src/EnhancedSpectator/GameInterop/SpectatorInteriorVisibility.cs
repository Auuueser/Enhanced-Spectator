using DunGen;
using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Repairs the entrance event missed when switching to a player already inside.</summary>
internal static class SpectatorInteriorVisibility
{
    private static AdjacentRoomCullingModified? _culler;
    private static Tile? _subjectRoom;
    private static Tile? _cameraRoom;
    private static PlayerControllerB? _target;
    private static float _nextRepair, _nextReport;
    private static string _selection="none";
    private static readonly System.Collections.Generic.List<Tile> Added=new System.Collections.Generic.List<Tile>();
    private static readonly System.Collections.Generic.List<Tile> Required=new System.Collections.Generic.List<Tile>();

    internal static Tile? SpecialEntrance(Dungeon? dungeon,Vector3 position)
    {
        var special=RoundManager.Instance!=null ? RoundManager.Instance.startRoomSpecialBounds : null;
        if(dungeon==null || special==null || !special.bounds.Contains(position)) return null;
        foreach(var tile in dungeon.MainPathTiles)
            if(tile!=null && tile.HasValidBounds && tile.gameObject.activeInHierarchy) return tile;
        return null;
    }

    // Runs AFTER vanilla culling. Never writes currentTile, TargetOverride or the culling
    // transform: those are shared with vanilla, the radar camera and other rendering mods.
    internal static void Refresh(AdjacentRoomCullingModified culler)
    {
        var round=StartOfRound.Instance;
        if(round!=null && round.occlusionCuller!=culler) return;
        var local=round!=null ? round.localPlayerController : null;
        var target=local!=null && local.isPlayerDead ? local.spectatedPlayerScript : null;
        Vector3 cameraPosition=default;
        bool spatialCamera=SpectatorFreecamController.Current?.TryGetTravellingRenderPosition(out cameraPosition)==true;
        bool livingSubject=target!=null && !target.isPlayerDead && target.isInsideFactory;
        bool holdingDeath=spatialCamera && (target==null || target.isPlayerDead) && _culler==culler && _subjectRoom!=null;
        bool eligible=local!=null && local.isPlayerDead && (livingSubject || holdingDeath)
            && round!.occlusionCuller==culler && !round.overrideSpectateCamera;
        if(!eligible || !culler.isActiveAndEnabled || !culler.Ready)
        { Release(); return; }
        if(livingSubject && _target!=target) { Release(); _target=target; _nextRepair=0; _nextReport=0; }
        Tile? selected=holdingDeath ? _subjectRoom
            : ResolveRoom(culler,target!.transform.position+Vector3.up*.5f,_subjectRoom,out _selection);
        if(holdingDeath) _selection="death-hold";
        if(selected==null) { _selection="no-registered-room"; return; }
        Tile? cameraRoom=spatialCamera ? ResolveRoom(culler,cameraPosition,_cameraRoom,out _) : null;
        string selection=_selection;
        if(_culler!=culler || _subjectRoom!=selected || _cameraRoom!=cameraRoom) Release();
        _culler=culler; _subjectRoom=selected; _cameraRoom=cameraRoom; _target=target; _selection=selection;
        Required.Clear(); Required.Add(selected);
        if(cameraRoom!=null && cameraRoom!=selected) Required.Add(cameraRoom);
        int first=0;
        for(int depth=0;depth<culler.AdjacentTileDepth;depth++)
        {
            int end=Required.Count;
            for(int i=first;i<end;i++) foreach(var doorway in Required[i].UsedDoorways)
            {
                var next=doorway!=null && doorway.ConnectedDoorway!=null ? doorway.ConnectedDoorway.Tile : null;
                if(next==null || Required.Contains(next) || !culler.allTiles.Contains(next)) continue;
                if(culler.CullBehindClosedDoors && doorway!.DoorComponent!=null && doorway.DoorComponent.ShouldCullBehind) continue;
                Required.Add(next);
            }
            first=end;
        }
        bool changed=false;
        bool audit=Time.unscaledTime>=_nextRepair;
        if(audit) _nextRepair=Time.unscaledTime+.2f;
        foreach(var tile in Required)
        {
            if(!culler.visibleTiles.Contains(tile))
            {
                // Register additions with vanilla's own visibility list. Its next refresh can
                // remove them normally; no untracked renderer enables survive a target change.
                culler.visibleTiles.Add(tile); Added.Add(tile); changed=true;
                culler.SetTileVisibility(tile,true);
            }
            else if(!culler.IsTileVisible(tile)) { culler.SetTileVisibility(tile,true); changed=true; }
            else if(audit && NeedsRendererRepair(culler,tile)) { culler.SetTileVisibility(tile,true); changed=true; }
        }
        if(changed) culler.RefreshDoorVisibilities();
    }

    private static Tile? ResolveRoom(AdjacentRoomCullingModified culler,Vector3 sample,Tile? previous,out string selection)
    {
        var special=RoundManager.Instance!=null ? RoundManager.Instance.startRoomSpecialBounds : null;
        if(special!=null && special.bounds.Contains(sample))
        {
            var entrance=culler.GetStartTile();
            if(entrance!=null) { selection="special-entrance"; return entrance; }
        }
        selection="bounds";
        if(_culler==culler && previous!=null && previous.Bounds.Contains(sample)) return previous;
        Tile? selected=null;
        float smallest=float.PositiveInfinity;
        foreach(var tile in culler.allTiles)
        {
            if(tile==null || !tile.HasValidBounds || !tile.Bounds.Contains(sample)) continue;
            Vector3 size=tile.Bounds.size; float volume=size.x*size.y*size.z;
            if(volume<smallest) { smallest=volume; selected=tile; }
        }
        if(selected!=null) return selected;
        // Mine landings and doorway volumes can sit just beyond generated tile bounds.
        selection="nearest-registered-room";
        float nearest=float.PositiveInfinity;
        foreach(var tile in culler.allTiles)
        {
            if(tile==null || !tile.HasValidBounds) continue;
            float distance=tile.Bounds.SqrDistance(sample);
            if(distance<nearest) { nearest=distance; selected=tile; }
        }
        return selected;
    }

    internal static void TargetChanged()
    {
        _nextRepair=_nextReport=0;
        var round=StartOfRound.Instance;
        if(round!=null && round.occlusionCuller!=null) Refresh(round.occlusionCuller);
    }

    // The actual spectator render is the final synchronization point, independently of the
    // original culler's update order. No camera mask, player state or scene activation is changed.
    internal static string? BeforeCamera(Camera camera,bool diagnostics)
    {
        var round=StartOfRound.Instance;
        if(round==null || camera!=round.spectateCamera) return null;
        var culler=round.occlusionCuller;
        if(culler!=null) Refresh(culler);
        if(!diagnostics || Time.unscaledTime<_nextReport) return null;
        _nextReport=Time.unscaledTime+2;
        var local=round.localPlayerController;
        var target=local!=null ? local.spectatedPlayerScript : null;
        int total=0,enabled=0,active=0,forced=0,masked=0;
        if(culler!=null && _subjectRoom!=null && culler.tileRenderers.TryGetValue(_subjectRoom,out var renderers))
            foreach(var renderer in renderers)
            {
                if(renderer==null) continue;
                total++; if(renderer.enabled) enabled++;
                if(renderer.gameObject.activeInHierarchy) active++;
                if(renderer.forceRenderingOff) forced++;
                if((camera.cullingMask & (1<<renderer.gameObject.layer))!=0) masked++;
            }
        return $"[InteriorDiag render-sync-r2] dead={local?.isPlayerDead},inside={target?.isInsideFactory},target={target?.transform.name},position={target?.transform.position},override={round.overrideSpectateCamera},cullerActive={culler?.isActiveAndEnabled},ready={culler?.Ready},tiles={culler?.allTiles.Count},visible={culler?.visibleTiles.Count},selection={_selection},room={_subjectRoom?.transform.name},added={Added.Count},renderers={total},enabled={enabled},active={active},forceOff={forced},inMask={masked},camera={camera.transform.position},mask={camera.cullingMask:X8}";
    }

    private static bool NeedsRendererRepair(AdjacentRoomCullingModified culler,Tile tile)
    {
        if(!culler.tileRenderers.TryGetValue(tile,out var renderers)) return false;
        foreach(var renderer in renderers)
            if(renderer!=null && !renderer.enabled
                && (!culler.OverrideRendererVisibilities.TryGetValue(renderer,out bool visible) || visible)) return true;
        return false;
    }

    internal static void AfterVanillaRefresh(AdjacentRoomCullingModified culler)
    {
        // Vanilla has recomputed its own set and consumed the previous additions. Anything
        // surviving that refresh belongs to vanilla, even if we originally made it visible.
        if(_culler==culler) Added.Clear();
    }

    private static void Release()
    {
        if(Added.Count>0 && _culler!=null && _culler.isActiveAndEnabled && _culler.Ready)
        {
            foreach(var tile in Added)
            {
                if(tile==null) continue;
                _culler.visibleTiles.Remove(tile);
                if(!tile.Tags.Tags.Contains(_culler.DisableCullingTag)) _culler.SetTileVisibility(tile,false);
            }
            _culler.RefreshDoorVisibilities();
        }
        _culler=null; _subjectRoom=_cameraRoom=null; _target=null; _selection="inactive"; Added.Clear(); Required.Clear();
    }
}
