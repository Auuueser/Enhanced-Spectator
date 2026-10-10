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
        // Body renderers outside any level-of-detail group, drawn whenever visible.
        internal Renderer[] Renderers=System.Array.Empty<Renderer>();
        // The body's LOD groups, each its levels most detailed first: thermal draws only the first level any camera
        // shows. Each level is "visible" when some camera (a shadow map, a split-screen view at another distance) drew
        // it, so several were often drawn on top of each other and fought, which looked like the model shaking.
        internal Renderer[][][] Lods=System.Array.Empty<Renderer[][]>();
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
        // Skip inactive slots cheaply; rebuild at most two body lists per tick, or every body at once
        // when the registry is empty so thermal shows all bodies from its first frame.
        bool filling=_actors.Count==0;
        int refreshed=0, slots=filling?total:8, budget=filling?total:2;
        for(int n=0;n<slots && refreshed<budget;n++)
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
            Renderer[] body;
            if(player!=null) body=new Renderer[]{player.thisPlayerModel,player.thisPlayerModelLOD1,player.thisPlayerModelLOD2,player.thisPlayerModelArms};
            else
            {
                // Confirmed EnemyAI body renderer fields; do not collect nested props or fear replicas.
                int skins=enemy!.skinnedMeshRenderers?.Length??0,meshes=enemy.meshRenderers?.Length??0;
                body=new Renderer[skins+meshes];
                if(skins>0) System.Array.Copy(enemy.skinnedMeshRenderers!,0,body,0,skins);
                if(meshes>0) System.Array.Copy(enemy.meshRenderers!,0,body,skins,meshes);
            }
            Split(component,body,actor);
        }
    }

    private readonly List<Renderer[][]> _groups=new List<Renderer[][]>();
    private readonly List<Renderer[]> _levels=new List<Renderer[]>();
    private readonly List<Renderer> _level=new List<Renderer>(), _plain=new List<Renderer>();
    private readonly HashSet<Renderer> _grouped=new HashSet<Renderer>();
    // Sorts the body's renderers by the LOD groups under the actor. Only body renderers enter a level, so props
    // with their own groups stay out; a renderer may sit in two levels of one group (the jester's lower jaw).
    private void Split(Component root,Renderer[] body,Actor actor)
    {
        _groups.Clear(); _grouped.Clear(); _plain.Clear();
        foreach(var group in root.GetComponentsInChildren<LODGroup>(true))
        {
            _levels.Clear();
            foreach(var lod in group.GetLODs())
            {
                _level.Clear();
                foreach(var renderer in lod.renderers) if(renderer!=null && System.Array.IndexOf(body,renderer)>=0) _level.Add(renderer);
                if(_level.Count>0) _levels.Add(_level.ToArray());
            }
            if(_levels.Count<2) continue;
            _groups.Add(_levels.ToArray());
            foreach(var level in _levels) _grouped.UnionWith(level);
        }
        foreach(var renderer in body) if(renderer!=null && !_grouped.Contains(renderer)) _plain.Add(renderer);
        actor.Renderers=_plain.ToArray(); actor.Lods=_groups.ToArray();
    }
    internal static bool AnyVisible(Renderer[] level,Camera camera)
    {
        foreach(var renderer in level) if(Visible(renderer,camera)) return true;
        return false;
    }
    internal static bool Visible(Renderer r,Camera camera) => r!=null && r.enabled && r.isVisible && !r.forceRenderingOff
        && r.gameObject.activeInHierarchy && r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly
        && (camera.cullingMask & (1<<r.gameObject.layer))!=0 && !(r is ParticleSystemRenderer)
        && !(r is TrailRenderer) && !(r is LineRenderer);
}
