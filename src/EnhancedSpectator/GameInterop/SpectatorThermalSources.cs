using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedSpectator.GameInterop;

// Only actual game actors enter this registry. Fear replicas and corpses never do.
internal sealed class SpectatorThermalSources
{
    internal sealed class Actor
    {
        internal PlayerControllerB? Player;
        internal EnemyAI? Enemy;
        internal float NextRefresh;
        internal Renderer[] Renderers=System.Array.Empty<Renderer>();
        internal bool Alive => Player!=null ? Player.isPlayerControlled && !Player.isPlayerDead : Enemy!=null && !Enemy.isEnemyDead;
    }
    private readonly Dictionary<int,Actor> _actors=new Dictionary<int,Actor>();
    private readonly HashSet<int> _seen=new HashSet<int>();
    private readonly List<int> _stale=new List<int>();
    private float _next;
    private int _cursor;
    internal IEnumerable<Actor> Actors => _actors.Values;
    internal void Clear() { _actors.Clear(); _seen.Clear(); _stale.Clear(); _next=0; _cursor=0; }
    internal void Refresh()
    {
        if(Time.unscaledTime<_next) return;
        _next=Time.unscaledTime+.1f;
        var players=StartOfRound.Instance?.allPlayerScripts;
        var enemies=RoundManager.Instance?.SpawnedEnemies;
        int playerCount=players?.Length??0, total=playerCount+(enemies?.Count??0);
        if(total==0) { Clear(); return; }
        // Skip inactive slots cheaply; rebuild at most two body lists per tick.
        int refreshed=0;
        for(int n=0;n<8 && refreshed<2;n++)
        {
            if(_cursor>=total)
            {
                _stale.Clear(); foreach(var pair in _actors) if(!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
                foreach(int id in _stale) _actors.Remove(id);
                _seen.Clear(); _cursor=0;
            }
            PlayerControllerB? player=_cursor<playerCount?players![_cursor]:null;
            EnemyAI? enemy=_cursor>=playerCount?enemies![_cursor-playerCount]:null;
            _cursor++;
            Component? component=player!=null?(Component)player:enemy;
            if(component==null || (player!=null && (!player.isPlayerControlled || player.isPlayerDead))
                || (enemy!=null && enemy.isEnemyDead)) continue;
            int key=component.GetInstanceID(); _seen.Add(key);
            if(!_actors.TryGetValue(key,out var actor)) { actor=new Actor(); _actors.Add(key,actor); }
            actor.Player=player; actor.Enemy=enemy;
            if(Time.unscaledTime<actor.NextRefresh) continue;
            actor.NextRefresh=Time.unscaledTime+2; refreshed++;
            if(player!=null)
                actor.Renderers=new Renderer[]{player.thisPlayerModel,player.thisPlayerModelLOD1,player.thisPlayerModelLOD2,player.thisPlayerModelArms};
            else if(enemy!=null)
            {
                // Confirmed EnemyAI body renderer fields; do not collect nested props or fear replicas.
                int skins=enemy.skinnedMeshRenderers?.Length??0,meshes=enemy.meshRenderers?.Length??0;
                actor.Renderers=new Renderer[skins+meshes];
                if(skins>0) System.Array.Copy(enemy.skinnedMeshRenderers!,0,actor.Renderers,0,skins);
                if(meshes>0) System.Array.Copy(enemy.meshRenderers!,0,actor.Renderers,skins,meshes);
            }
        }
    }
    internal static bool Visible(Renderer r,Camera camera) => r!=null && r.enabled && r.isVisible && !r.forceRenderingOff
        && r.gameObject.activeInHierarchy && r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly
        && (camera.cullingMask & (1<<r.gameObject.layer))!=0 && !(r is ParticleSystemRenderer)
        && !(r is TrailRenderer) && !(r is LineRenderer);
}
