using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.FearMode;
using EnhancedSpectator.Logging;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Reads confirmed V81 player and enemy visual-source state through direct publicized members.
/// </summary>
public sealed class LethalCompanyFearModeAdapter : IGameFearModeAdapter
{
    /// <inheritdoc />
    public bool TryGetLocalDeadPlayerIdentity(out ulong clientId, out ulong slotId)
    {
        StartOfRound round = StartOfRound.Instance;
        PlayerControllerB localPlayer = round != null ? round.localPlayerController : null!;
        if (localPlayer != null && localPlayer.isPlayerDead)
        {
            clientId = localPlayer.actualClientId;
            slotId = localPlayer.playerClientId;
            return true;
        }

        clientId = 0;
        slotId = 0;
        return false;
    }

    /// <inheritdoc />
    public bool IsPlayerDead(ulong clientId, ulong claimedSlotId)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.allPlayerScripts == null)
        {
            return false;
        }

        for (int index = 0; index < round.allPlayerScripts.Length; index++)
        {
            PlayerControllerB player = round.allPlayerScripts[index];
            if (player != null
                && player.actualClientId == clientId
                && player.playerClientId == claimedSlotId)
            {
                return player.isPlayerDead;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsLocalQuickMenuOpen()
    {
        StartOfRound round = StartOfRound.Instance;
        PlayerControllerB localPlayer = round != null ? round.localPlayerController : null!;
        return localPlayer != null
            && localPlayer.quickMenuManager != null
            && localPlayer.quickMenuManager.isMenuOpen;
    }

    /// <inheritdoc />
    public bool TryGetActiveCameraCullingMask(out int cullingMask)
    {
        StartOfRound round = StartOfRound.Instance;
        Camera camera = round != null ? round.activeCamera : null!;
        if (camera == null && round != null)
        {
            camera = round.spectateCamera;
        }

        if (camera == null)
        {
            cullingMask = 0;
            return false;
        }

        cullingMask = camera.cullingMask;
        return true;
    }

    /// <inheritdoc />
    public bool TryGetActiveAudioListenerPosition(out Vector3 position)
    {
        StartOfRound round = StartOfRound.Instance;
        PlayerControllerB localPlayer = round != null ? round.localPlayerController : null!;
        AudioListener listener = localPlayer != null ? localPlayer.activeAudioListener : null!;
        if (listener == null || !listener.enabled)
        {
            position = Vector3.zero;
            return false;
        }

        position = listener.transform.position;
        return true;
    }

    /// <inheritdoc />
    public void CopyAvailableModelKeysTo(List<string> destination)
    {
        destination.Clear();
        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.levels == null)
        {
            return;
        }

        for (int levelIndex = 0; levelIndex < round.levels.Length; levelIndex++)
        {
            SelectableLevel level = round.levels[levelIndex];
            if (level == null)
            {
                continue;
            }

            AddModelKeys(level.Enemies, destination);
            AddModelKeys(level.OutsideEnemies, destination);
            AddModelKeys(level.DaytimeEnemies, destination);
        }
    }

    /// <inheritdoc />
    public bool TryGetEnemyVisualSource(string modelKey, out FearVisualSource? source)
    {
        source = null;
        if (!FearModeRules.IsValidModelKey(modelKey))
        {
            return false;
        }

        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.levels == null)
        {
            return false;
        }

        for (int levelIndex = 0; levelIndex < round.levels.Length; levelIndex++)
        {
            SelectableLevel level = round.levels[levelIndex];
            if (level == null)
            {
                continue;
            }

            if (TryFindVisualSource(level.Enemies, modelKey, out source)
                || TryFindVisualSource(level.OutsideEnemies, modelKey, out source)
                || TryFindVisualSource(level.DaytimeEnemies, modelKey, out source))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public void CopyFearSoundClipsTo(string modelKey, List<AudioClip> destination)
    {
        destination.Clear();
        if (string.Equals(modelKey, FearModeRules.DefaultModelKey, StringComparison.Ordinal))
        {
            CopyDefaultPlayerDeathClipsTo(destination);
            return;
        }

        if (!TryFindEnemyType(modelKey, out EnemyType? enemyType) || enemyType == null)
        {
            return;
        }

        GameObject prefab = enemyType.enemyPrefab;
        if (prefab == null)
        {
            return;
        }

        AddKnownEnemyClips(prefab, modelKey, destination);
        if (prefab.GetComponent<NutcrackerEnemyAI>() == null)
        {
            AddClip(destination, enemyType.overrideVentSFX);
        }
        else if (enemyType.overrideVentSFX != null)
        {
            ModLog.Debug(
                $"Fear sound catalog removed long near-silent Nutcracker vent clip: clip={enemyType.overrideVentSFX.name}, duration={enemyType.overrideVentSFX.length:0.###}s.");
        }
        if (!IsManeaterFormKey(modelKey))
        {
            AddClips(destination, enemyType.audioClips);
        }
        MiscAnimation[] miscAnimations = enemyType.miscAnimations;
        if (miscAnimations != null)
        {
            for (int index = 0; index < miscAnimations.Length; index++)
            {
                MiscAnimation animation = miscAnimations[index];
                if (animation != null)
                {
                    AddClip(destination, animation.AnimVoiceclip);
                }
            }
        }

        EnemyAI enemy = prefab.GetComponent<EnemyAI>();
        if (enemy != null)
        {
            AddClip(destination, enemy.dieSFX);
            EnemyBehaviourState[] states = enemy.enemyBehaviourStates;
            if (states != null)
            {
                for (int index = 0; index < states.Length; index++)
                {
                    EnemyBehaviourState state = states[index];
                    if (state != null)
                    {
                        AddClip(destination, state.VoiceClip);
                        AddClip(destination, state.SFXClip);
                    }
                }
            }
        }

        bool useGenericBoundComponents = prefab.GetComponent<CaveDwellerAI>() == null
            && prefab.GetComponent<MaskedPlayerEnemy>() == null;
        if (useGenericBoundComponents)
        {
            PlayAudioAnimationEvent[] animationEvents =
                prefab.GetComponentsInChildren<PlayAudioAnimationEvent>(includeInactive: true);
            for (int index = 0; index < animationEvents.Length; index++)
            {
                PlayAudioAnimationEvent audioEvent = animationEvents[index];
                AddClip(destination, audioEvent.audioClip);
                AddClip(destination, audioEvent.audioClip2);
                AddClip(destination, audioEvent.audioClip3);
                AddClips(destination, audioEvent.randomClips);
                AddClips(destination, audioEvent.randomClips2);
            }

            RandomPeriodicAudioPlayer[] periodicPlayers =
                prefab.GetComponentsInChildren<RandomPeriodicAudioPlayer>(includeInactive: true);
            for (int index = 0; index < periodicPlayers.Length; index++)
            {
                AddClips(destination, periodicPlayers[index].randomClips);
            }

            if (prefab.GetComponent<BaboonBirdAI>() != null)
            {
                BaboonHawkAudioEvents[] hawkEvents =
                    prefab.GetComponentsInChildren<BaboonHawkAudioEvents>(includeInactive: true);
                for (int index = 0; index < hawkEvents.Length; index++)
                {
                    AddClips(destination, hawkEvents[index].randomClips);
                }
            }

            AnimatedObjectTrigger[] animatedTriggers =
                prefab.GetComponentsInChildren<AnimatedObjectTrigger>(includeInactive: true);
            for (int index = 0; index < animatedTriggers.Length; index++)
            {
                AnimatedObjectTrigger trigger = animatedTriggers[index];
                AddClips(destination, trigger.boolFalseAudios);
                AddClips(destination, trigger.boolTrueAudios);
                AddClips(destination, trigger.secondaryAudios);
                AddClip(destination, trigger.playWhileTrue);
            }

            AnimatedObjectFloatSetter[] floatSetters =
                prefab.GetComponentsInChildren<AnimatedObjectFloatSetter>(includeInactive: true);
            for (int index = 0; index < floatSetters.Length; index++)
            {
                AnimatedObjectFloatSetter setter = floatSetters[index];
                AddClip(destination, setter.trueAudio);
                AddClip(destination, setter.falseAudio);
                AddClip(destination, setter.completionTrueAudio);
                AddClip(destination, setter.completionFalseAudio);
            }
        }

        RemoveMonsterHurtClips(prefab, enemyType, destination);
        if (prefab.GetComponent<ForestGiantAI>() != null)
        {
            AddClip(destination, enemyType.stunSFX);
        }

        RemoveForeignMonsterClips(modelKey, destination);
    }

    private static void CopyDefaultPlayerDeathClipsTo(List<AudioClip> destination)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null)
        {
            return;
        }

        AddClip(destination, round.playerFallDeath);
        AddClip(destination, round.playerCrushDeath);
        if (round.playerRagdolls == null)
        {
            return;
        }

        for (int index = 0; index < round.playerRagdolls.Count; index++)
        {
            GameObject ragdoll = round.playerRagdolls[index];
            DeadBodyInfo deadBody = ragdoll != null ? ragdoll.GetComponent<DeadBodyInfo>() : null!;
            AddClip(
                destination,
                deadBody != null && deadBody.playAudioOnDeath != null
                    ? deadBody.playAudioOnDeath.clip
                    : null);
        }
    }

    private static void AddKnownEnemyClips(
        GameObject prefab,
        string modelKey,
        List<AudioClip> destination)
    {
        HoarderBugAI hoarderBug = prefab.GetComponent<HoarderBugAI>();
        if (hoarderBug != null)
        {
            AddClips(destination, hoarderBug.chitterSFX);
            AddClips(destination, hoarderBug.angryScreechSFX);
            AddClip(destination, hoarderBug.angryVoiceSFX);
            AddClip(destination, hoarderBug.bugFlySFX);
            AddClip(destination, hoarderBug.hitPlayerSFX);
        }

        CaveDwellerAI maneater = prefab.GetComponent<CaveDwellerAI>();
        if (maneater != null)
        {
            if (IsManeaterBabyKey(modelKey))
            {
                AddClips(destination, maneater.scaredBabyVoiceSFX);
                AddClip(destination, maneater.squirmingSFX);
                AddClip(destination, maneater.biteSFX);
                AddClip(destination, maneater.pukeSFX);
                AddClip(destination, maneater.transformationSFX);
                AddClip(destination, maneater.babyCryingAudio != null ? maneater.babyCryingAudio.clip : null);
                AddClip(destination, maneater.babyVoice != null ? maneater.babyVoice.clip : null);
            }
            else
            {
                AddClip(destination, maneater.growlSFX);
                AddClips(destination, maneater.fakeCrySFX);
                AddClip(destination, maneater.cooldownSFX);
                AddClip(destination, maneater.transformationSFX);
                AddClip(destination, maneater.walkingAudio != null ? maneater.walkingAudio.clip : null);
                AddClip(destination, maneater.clickingAudio1 != null ? maneater.clickingAudio1.clip : null);
                AddClip(destination, maneater.clickingAudio2 != null ? maneater.clickingAudio2.clip : null);
                AddClip(destination, maneater.screamAudio != null ? maneater.screamAudio.clip : null);
            }
        }

        MaskedPlayerEnemy masked = prefab.GetComponent<MaskedPlayerEnemy>();
        if (masked != null)
        {
            AddClip(destination, masked.movementAudio != null ? masked.movementAudio.clip : null);
        }

        BaboonBirdAI baboon = prefab.GetComponent<BaboonBirdAI>();
        if (baboon != null)
        {
            AddClips(destination, baboon.cawScreamSFX);
            AddClips(destination, baboon.cawLaughSFX);
        }

        BlobAI blob = prefab.GetComponent<BlobAI>();
        if (blob != null)
        {
            AddClip(destination, blob.agitatedSFX);
            AddClip(destination, blob.jiggleSFX);
            AddClip(destination, blob.killPlayerSFX);
            AddClip(destination, blob.idleSFX);
        }

        ButlerEnemyAI butler = prefab.GetComponent<ButlerEnemyAI>();
        if (butler != null)
        {
            AddClips(destination, butler.footsteps);
            AddClips(destination, butler.broomSweepSFX);
            AddClip(destination, butler.ambience1 != null ? butler.ambience1.clip : null);
            AddClip(destination, butler.buzzingAmbience != null ? butler.buzzingAmbience.clip : null);
            AddClip(destination, butler.sweepingAudio != null ? butler.sweepingAudio.clip : null);
            AddClip(destination, butler.popAudio != null ? butler.popAudio.clip : null);
            AddClip(destination, butler.popAudioFar != null ? butler.popAudioFar.clip : null);
            AddClip(destination, butler.ambience2 != null ? butler.ambience2.clip : null);
        }

        ClaySurgeonAI claySurgeon = prefab.GetComponent<ClaySurgeonAI>();
        if (claySurgeon != null)
        {
            AddClip(destination, claySurgeon.snareDrum);
            AddClips(destination, claySurgeon.paradeClips);
            AddClip(destination, claySurgeon.snipScissors);
        }

        CentipedeAI centipede = prefab.GetComponent<CentipedeAI>();
        if (centipede != null)
        {
            AddClip(destination, centipede.fallShriek);
            AddClip(destination, centipede.hitGroundSFX);
            AddClips(destination, centipede.shriekClips);
            AddClip(destination, centipede.clingToPlayer3D);
        }

        CrawlerAI crawler = prefab.GetComponent<CrawlerAI>();
        if (crawler != null)
        {
            AddClip(destination, crawler.shortRoar);
            AddClips(destination, crawler.hitWallSFX);
            AddClip(destination, crawler.bitePlayerSFX);
            AddClip(destination, crawler.eatPlayerSFX);
            AddClips(destination, crawler.longRoarSFX);
        }

        DressGirlAI girl = prefab.GetComponent<DressGirlAI>();
        if (girl != null)
        {
            AddClips(destination, girl.appearStaringSFX);
            AddClip(destination, girl.skipWalkSFX);
            AddClip(destination, girl.breathingSFX);
        }

        FlowermanAI flowerman = prefab.GetComponent<FlowermanAI>();
        if (flowerman != null)
        {
            AddClip(destination, flowerman.crackNeckSFX);
            AddClip(destination, flowerman.creatureAngerVoice != null ? flowerman.creatureAngerVoice.clip : null);
        }

        ForestGiantAI forestGiant = prefab.GetComponent<ForestGiantAI>();
        if (forestGiant != null)
        {
            AddClip(destination, forestGiant.giantFall);
            AddClip(destination, forestGiant.giantCry);
        }

        GiantKiwiAI kiwi = prefab.GetComponent<GiantKiwiAI>();
        if (kiwi != null)
        {
            AddClips(destination, kiwi.footstepSFX);
            AddClips(destination, kiwi.footstepBassSFX);
            AddClip(destination, kiwi.peckTreeSFX);
            AddClips(destination, kiwi.attackSFX);
            AddClips(destination, kiwi.screamSFX);
            AddClip(destination, kiwi.squawkSFX);
            AddClip(destination, kiwi.wakeUpSFX);
            AddClip(destination, kiwi.breakAndEnter);
            AddClip(destination, kiwi.shipAlarm);
        }

        JesterAI jester = prefab.GetComponent<JesterAI>();
        if (jester != null)
        {
            AddClip(destination, jester.popGoesTheWeaselTheme);
            AddClip(destination, jester.popUpSFX);
            AddClip(destination, jester.screamingSFX);
            AddClip(destination, jester.killPlayerSFX);
        }

        MouthDogAI dog = prefab.GetComponent<MouthDogAI>();
        if (dog != null)
        {
            AddClip(destination, dog.screamSFX);
            AddClip(destination, dog.breathingSFX);
            AddClip(destination, dog.killPlayerSFX);
        }

        NutcrackerEnemyAI nutcracker = prefab.GetComponent<NutcrackerEnemyAI>();
        if (nutcracker != null)
        {
            AddClips(destination, nutcracker.torsoFinishTurningClips);
            AddClip(destination, nutcracker.aimSFX);
            AddClip(destination, nutcracker.kickSFX);
        }

        PufferAI puffer = prefab.GetComponent<PufferAI>();
        if (puffer != null)
        {
            AddClips(destination, puffer.footstepsSFX);
            AddClips(destination, puffer.frightenSFX);
            AddClip(destination, puffer.stomp);
            AddClip(destination, puffer.angry);
            AddClip(destination, puffer.puff);
            AddClip(destination, puffer.nervousMumbling);
            AddClip(destination, puffer.rattleTail);
            AddClip(destination, puffer.bitePlayerSFX);
        }

        RadMechAI radMech = prefab.GetComponent<RadMechAI>();
        if (radMech != null)
        {
            AddClip(destination, radMech.spotlightOff);
            AddClip(destination, radMech.spotlightFlicker);
            AddClips(destination, radMech.shootGunSFX);
            AddClips(destination, radMech.largeExplosionSFX);
        }

        SandSpiderAI spider = prefab.GetComponent<SandSpiderAI>();
        if (spider != null)
        {
            AddClips(destination, spider.footstepSFX);
            AddClip(destination, spider.hitWebSFX);
            AddClip(destination, spider.attackSFX);
            AddClip(destination, spider.spoolPlayerSFX);
            AddClip(destination, spider.hangPlayerSFX);
            AddClip(destination, spider.breakWebSFX);
        }

        SpringManAI spring = prefab.GetComponent<SpringManAI>();
        if (spring != null)
        {
            AddClips(destination, spring.springNoises);
            AddClip(destination, spring.enterCooldownSFX);
        }

        StingrayAI stingray = prefab.GetComponent<StingrayAI>();
        if (stingray != null)
        {
            AddClip(destination, stingray.attackSFX);
            AddClip(destination, stingray.poisonInjectSFX);
            AddClip(destination, stingray.vocalSFX);
            AddClip(destination, stingray.floppingSFX);
            AddClip(destination, stingray.spitSFX);
        }

        BushWolfEnemy bushWolf = prefab.GetComponent<BushWolfEnemy>();
        if (bushWolf != null)
        {
            AddClip(destination, bushWolf.snarlSFX);
            AddClips(destination, bushWolf.growlSFX);
            AddClip(destination, bushWolf.shootTongueSFX);
            AddClip(destination, bushWolf.tongueShootSFX);
            AddClip(destination, bushWolf.killSFX);
            AddClips(destination, bushWolf.callsClose);
            AddClips(destination, bushWolf.callsFar);
        }

        SandWormAI sandWorm = prefab.GetComponent<SandWormAI>();
        if (sandWorm != null)
        {
            AddClips(destination, sandWorm.groundRumbleSFX);
            AddClips(destination, sandWorm.ambientRumbleSFX);
            AddClip(destination, sandWorm.hitGroundSFX);
            AddClip(destination, sandWorm.emergeFromGroundSFX);
            AddClips(destination, sandWorm.roarSFX);
        }

        DoublewingAI manticoil = prefab.GetComponent<DoublewingAI>();
        if (manticoil != null)
        {
            AddClips(destination, manticoil.birdScreechSFX);
            AddClip(destination, manticoil.birdHitGroundSFX);
        }

        PumaAI puma = prefab.GetComponent<PumaAI>();
        if (puma != null)
        {
            AddClip(destination, puma.climbTreeSFX1);
            AddClip(destination, puma.climbTreeSFX2);
            AddClip(destination, puma.pumaLeapSFX1);
            AddClip(destination, puma.pumaLeapSFX2);
            AddClips(destination, puma.pumaGrowlSFX);
            AddClip(destination, puma.pumaRunSFX);
            AddClips(destination, puma.pumaFootstepSFX);
            AddClip(destination, puma.attackScream);
            AddClip(destination, puma.dropSFX);
            AddClips(destination, puma.scratchSFX);
        }

        CadaverBloomAI bloom = prefab.GetComponent<CadaverBloomAI>();
        if (bloom != null)
        {
            AddClip(destination, bloom.burstSFX);
            AddClip(destination, bloom.plantFeetHit);
            AddClip(destination, bloom.chestBurst);
            AddClips(destination, bloom.chestPlantRoars);
            AddClip(destination, bloom.bitePlayer);
        }
    }

    private static bool TryFindEnemyType(string modelKey, out EnemyType? resolved)
    {
        resolved = null;
        if (!FearModeRules.IsValidModelKey(modelKey))
        {
            return false;
        }

        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.levels == null)
        {
            return false;
        }

        for (int levelIndex = 0; levelIndex < round.levels.Length; levelIndex++)
        {
            SelectableLevel level = round.levels[levelIndex];
            if (level == null)
            {
                continue;
            }

            if (TryFindEnemyType(level.Enemies, modelKey, out resolved)
                || TryFindEnemyType(level.OutsideEnemies, modelKey, out resolved)
                || TryFindEnemyType(level.DaytimeEnemies, modelKey, out resolved))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindEnemyType(
        List<SpawnableEnemyWithRarity>? enemies,
        string modelKey,
        out EnemyType? resolved)
    {
        resolved = null;
        if (enemies == null)
        {
            return false;
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            EnemyType? enemyType = enemies[index]?.enemyType;
            if (enemyType == null || enemyType.enemyPrefab == null)
            {
                continue;
            }

            if (string.Equals(modelKey, enemyType.enemyName, StringComparison.OrdinalIgnoreCase)
                || (IsManeaterFormKey(modelKey)
                    && string.Equals(enemyType.enemyName, "Maneater", StringComparison.OrdinalIgnoreCase)))
            {
                resolved = enemyType;
                return true;
            }

            if (!string.Equals(
                modelKey,
                FearModelPresentationRules.CadaverBloomModelKey,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CadaverGrowthAI growth = enemyType.enemyPrefab.GetComponent<CadaverGrowthAI>();
            if (growth != null && growth.bloomEnemyType != null)
            {
                resolved = growth.bloomEnemyType;
                return true;
            }
        }

        return false;
    }

    private static void AddClip(List<AudioClip> destination, AudioClip? clip)
    {
        if (clip == null || clip.samples <= 0 || clip.length <= 0.001f)
        {
            return;
        }

        for (int index = 0; index < destination.Count; index++)
        {
            if (ReferenceEquals(destination[index], clip))
            {
                return;
            }
        }

        destination.Add(clip);
    }

    private static void RemoveMonsterHurtClips(
        GameObject prefab,
        EnemyType enemyType,
        List<AudioClip> destination)
    {
        List<AudioClip> hurtClips = new List<AudioClip>();
        AddClip(hurtClips, enemyType.hitBodySFX);
        AddClip(hurtClips, enemyType.hitEnemyVoiceSFX);
        AddClip(hurtClips, enemyType.stunSFX);

        BlobAI blob = prefab.GetComponent<BlobAI>();
        AddClip(hurtClips, blob != null ? blob.hitSlimeSFX : null);
        CentipedeAI centipede = prefab.GetComponent<CentipedeAI>();
        AddClip(hurtClips, centipede != null ? centipede.hitCentipede : null);
        CrawlerAI crawler = prefab.GetComponent<CrawlerAI>();
        AddClips(hurtClips, crawler != null ? crawler.hitCrawlerSFX : null);
        SandSpiderAI spider = prefab.GetComponent<SandSpiderAI>();
        AddClip(hurtClips, spider != null ? spider.hitSpiderSFX : null);
        BushWolfEnemy bushWolf = prefab.GetComponent<BushWolfEnemy>();
        AddClip(hurtClips, bushWolf != null ? bushWolf.hitBushWolfSFX : null);

        for (int destinationIndex = destination.Count - 1; destinationIndex >= 0; destinationIndex--)
        {
            AudioClip candidate = destination[destinationIndex];
            for (int hurtIndex = 0; hurtIndex < hurtClips.Count; hurtIndex++)
            {
                if (!ReferenceEquals(candidate, hurtClips[hurtIndex]))
                {
                    continue;
                }

                ModLog.Debug($"Fear sound catalog removed monster hurt clip: clip={candidate.name}.");
                destination.RemoveAt(destinationIndex);
                break;
            }
        }
    }

    private static void AddClips(List<AudioClip> destination, AudioClip[]? clips)
    {
        if (clips == null)
        {
            return;
        }

        for (int index = 0; index < clips.Length; index++)
        {
            AddClip(destination, clips[index]);
        }
    }

    private static void RemoveForeignMonsterClips(string modelKey, List<AudioClip> destination)
    {
        for (int index = destination.Count - 1; index >= 0; index--)
        {
            AudioClip clip = destination[index];
            if (FearSoundClipOwnershipRules.IsAllowed(modelKey, clip.name))
            {
                continue;
            }

            ModLog.Debug($"Fear sound catalog rejected foreign clip: model={modelKey}, clip={clip.name}.");
            destination.RemoveAt(index);
        }
    }

    private static void AddModelKeys(
        List<SpawnableEnemyWithRarity>? enemies,
        List<string> destination)
    {
        if (enemies == null)
        {
            return;
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            EnemyType? enemyType = enemies[index]?.enemyType;
            string modelKey = enemyType != null ? enemyType.enemyName : string.Empty;
            if (enemyType == null
                || enemyType.enemyPrefab == null
                || !FearModeRules.IsValidModelKey(modelKey))
            {
                continue;
            }

            if (string.Equals(modelKey, "Maneater", StringComparison.OrdinalIgnoreCase))
            {
                if (!ContainsOrdinal(destination, FearModelPresentationRules.ManeaterBabyModelKey))
                {
                    destination.Add(FearModelPresentationRules.ManeaterBabyModelKey);
                }

                if (!ContainsOrdinal(destination, FearModelPresentationRules.ManeaterAdultModelKey))
                {
                    destination.Add(FearModelPresentationRules.ManeaterAdultModelKey);
                }
            }
            else if (!ContainsOrdinal(destination, modelKey))
            {
                destination.Add(modelKey);
            }

            CadaverGrowthAI growth = enemyType.enemyPrefab.GetComponent<CadaverGrowthAI>();
            if (growth != null
                && (growth.bloomEnemyType != null || growth.bloomEnemyPrefab != null)
                && !ContainsOrdinal(destination, FearModelPresentationRules.CadaverBloomModelKey))
            {
                destination.Add(FearModelPresentationRules.CadaverBloomModelKey);
            }
        }
    }

    private static bool TryFindVisualSource(
        List<SpawnableEnemyWithRarity>? enemies,
        string modelKey,
        out FearVisualSource? source)
    {
        source = null;
        if (enemies == null)
        {
            return false;
        }

        for (int index = 0; index < enemies.Count; index++)
        {
            EnemyType? enemyType = enemies[index]?.enemyType;
            if (enemyType != null
                && enemyType.enemyPrefab != null
                && IsManeaterFormKey(modelKey)
                && string.Equals(enemyType.enemyName, "Maneater", StringComparison.OrdinalIgnoreCase))
            {
                CaveDwellerAI maneater = enemyType.enemyPrefab.GetComponent<CaveDwellerAI>();
                GameObject formContainer = maneater == null
                    ? null!
                    : IsManeaterBabyKey(modelKey)
                        ? maneater.babyContainer
                        : maneater.adultContainer;
                if (formContainer != null)
                {
                    source = new FearVisualSource(
                        enemyType.enemyPrefab.transform,
                        formContainer.transform,
                        forceRendererVisibility: !IsManeaterBabyKey(modelKey),
                        useMeshBoundsForNormalization: !IsManeaterBabyKey(modelKey),
                        skinnedMeshOnly: !IsManeaterBabyKey(modelKey));
                    return true;
                }
            }

            if (enemyType != null
                && enemyType.enemyPrefab != null
                && string.Equals(
                    modelKey,
                    FearModelPresentationRules.CadaverBloomModelKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                CadaverGrowthAI growth = enemyType.enemyPrefab.GetComponent<CadaverGrowthAI>();
                GameObject bloomPrefab = growth != null && growth.bloomEnemyType != null
                    ? growth.bloomEnemyType.enemyPrefab
                    : growth != null
                        ? growth.bloomEnemyPrefab
                        : null!;
                if (bloomPrefab != null)
                {
                    source = new FearVisualSource(bloomPrefab.transform, bloomPrefab.transform);
                    return true;
                }
            }

            if (enemyType != null
                && enemyType.enemyPrefab != null
                && string.Equals(enemyType.enemyName, modelKey, StringComparison.Ordinal))
            {
                Transform prefabRoot = enemyType.enemyPrefab.transform;
                source = new FearVisualSource(prefabRoot, prefabRoot);
                return true;
            }
        }

        return false;
    }

    private static bool IsManeaterFormKey(string modelKey)
    {
        return IsManeaterBabyKey(modelKey)
            || string.Equals(
                modelKey,
                FearModelPresentationRules.ManeaterAdultModelKey,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManeaterBabyKey(string modelKey)
    {
        return string.Equals(
            modelKey,
            FearModelPresentationRules.ManeaterBabyModelKey,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsOrdinal(List<string> values, string value)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
