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
        internal LungProp? Apparatus;
        internal float NextRefresh;
        // Body renderers outside any level-of-detail group, drawn whenever visible.
        internal Renderer[] Renderers=System.Array.Empty<Renderer>();
        // The body's LOD groups, each its levels most detailed first: thermal draws only the first level any camera
        // shows. Each level is "visible" when some camera (a shadow map, a split-screen view at another distance) drew
        // it, so several were often drawn on top of each other and fought, which looked like the model shaking.
        internal Renderer[][][] Lods=System.Array.Empty<Renderer[][]>();
        internal bool Alive => Player!=null ? Player.isPlayerControlled && !Player.isPlayerDead
            : Enemy!=null ? !Enemy.isEnemyDead : Apparatus!=null && !Apparatus.deactivated;
    }

    // Hot things that are not bodies: the apparatus (an item, listed at its Start) and gun muzzle flashes (a particle
    // effect, which thermal cannot redraw: shown as a short hot burst at the muzzle).
    internal static readonly List<LungProp> ApparatusList=new List<LungProp>();
    internal const float FlashSeconds=.12f;
    internal static readonly List<(Transform Muzzle,Transform Aim,float At)> Flashes=new List<(Transform,Transform,float)>();
    private static readonly System.Predicate<LungProp> GoneApparatus=lung => lung==null;
    private static readonly System.Predicate<(Transform Muzzle,Transform Aim,float At)> Spent=flash => flash.Muzzle==null || flash.Aim==null || Time.unscaledTime-flash.At>FlashSeconds;
    internal static void ApparatusStarted(LungProp lung) { ApparatusList.RemoveAll(GoneApparatus); if(!ApparatusList.Contains(lung)) ApparatusList.Add(lung); }
    internal static void GunFired(Transform muzzle,Transform aim) { Flashes.RemoveAll(Spent); Flashes.Add((muzzle,aim,Time.unscaledTime)); }
    internal static void ExpireFlashes() => Flashes.RemoveAll(Spent);
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
        var unlisted=SpectatorEnemyVisibility.Unlisted; unlisted.RemoveAll(SpectatorEnemyVisibility.Gone);
        ApparatusList.RemoveAll(GoneApparatus);
        int playerCount=players?.Length??0, enemyCount=enemies?.Count??0, unlistedCount=unlisted.Count;
        int total=playerCount+enemyCount+unlistedCount+ApparatusList.Count;
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
            int at=_cursor++;
            Component? component=at<playerCount ? players![at]
                : at<playerCount+enemyCount ? enemies![at-playerCount]
                : at<playerCount+enemyCount+unlistedCount ? unlisted[at-playerCount-enemyCount]
                : ApparatusList[at-playerCount-enemyCount-unlistedCount];
            var player=component as PlayerControllerB; var enemy=component as EnemyAI; var apparatus=component as LungProp;
            if(component==null || (player!=null && (!player.isPlayerControlled || player.isPlayerDead))
                || (enemy!=null && enemy.isEnemyDead) || (apparatus!=null && apparatus.deactivated)) continue;
            int key=component.GetInstanceID(); _seen.Add(key);
            if(!_actors.TryGetValue(key,out var actor)) { actor=new Actor(); _actors.Add(key,actor); }
            actor.Player=player; actor.Enemy=enemy; actor.Apparatus=apparatus;
            if(Time.unscaledTime<actor.NextRefresh) continue;
            actor.NextRefresh=Time.unscaledTime+2; refreshed++;
            Renderer[] body;
            if(player!=null) body=new Renderer[]{player.thisPlayerModel,player.thisPlayerModelLOD1,player.thisPlayerModelLOD2,player.thisPlayerModelArms};
            else if(apparatus!=null) body=apparatus.mainObjectRenderer==apparatus.lungDeviceMesh ? new Renderer[]{apparatus.lungDeviceMesh}
                : new Renderer[]{apparatus.lungDeviceMesh,apparatus.mainObjectRenderer};
            else
            {
                // Confirmed EnemyAI body renderer fields; do not collect nested props or fear replicas.
                int skins=enemy!.skinnedMeshRenderers?.Length??0,meshes=enemy.meshRenderers?.Length??0;
                _late.Clear(); LateBodyParts(enemy);
                body=new Renderer[skins+meshes+_late.Count];
                if(skins>0) System.Array.Copy(enemy.skinnedMeshRenderers!,0,body,0,skins);
                if(meshes>0) System.Array.Copy(enemy.meshRenderers!,0,body,skins,meshes);
                _late.CopyTo(body,skins+meshes);
            }
            Split(component,body,actor);
        }
    }

    // The game lists an enemy's renderers once, at its Start, from the parts active then. Parts it switches on later
    // are missing from those lists, and so from thermal: the maneater's adult form, the masked's other mask, the
    // kidnapper fox's tongue. Collected here inactive too; they draw once the game shows them. The lists also leave
    // out parts under a carryable item: the maneater's baby form sits in its carryable prop.
    private readonly List<Renderer> _late=new List<Renderer>();
    private void LateBodyParts(EnemyAI enemy)
    {
        switch(enemy)
        {
            case CaveDwellerAI maneater:
                AddParts(maneater.babyContainer!=null?maneater.babyContainer.transform:null,enemy);
                AddParts(maneater.adultContainer!=null?maneater.adultContainer.transform:null,enemy);
                break;
            case MaskedPlayerEnemy masked: if(masked.maskTypes!=null) foreach(var mask in masked.maskTypes) AddParts(mask!=null?mask.transform:null,enemy); break;
            case BushWolfEnemy fox: AddParts(fox.tongue,enemy); break;
        }
    }
    private void AddParts(Transform? part,EnemyAI enemy)
    {
        if(part==null) return;
        foreach(var renderer in part.GetComponentsInChildren<Renderer>(true))
            if(renderer is SkinnedMeshRenderer skin ? enemy.skinnedMeshRenderers==null || System.Array.IndexOf(enemy.skinnedMeshRenderers,skin)<0
                : renderer is MeshRenderer mesh && (enemy.meshRenderers==null || System.Array.IndexOf(enemy.meshRenderers,mesh)<0))
                if(!_late.Contains(renderer)) _late.Add(renderer);
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
