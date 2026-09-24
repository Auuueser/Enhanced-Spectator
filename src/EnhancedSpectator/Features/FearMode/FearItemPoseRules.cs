using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Original item posture confirmed by the local full-catalog ten-view capture.</summary>
public static class FearItemPoseRules
{
    private static readonly Dictionary<string, Vector3> World = new Dictionary<string, Vector3>(StringComparer.Ordinal)
    {
        ["item:7Ball"] = new Vector3(-90f, 180f, 0f),
        ["item:Airhorn"] = new Vector3(-90f, 0f, 0f),
        ["item:Bell"] = new Vector3(-90f, 0f, 0f),
        ["item:BigBolt"] = new Vector3(-90f, 0f, 0f),
        ["item:Boombox"] = new Vector3(0f, 0f, 90f),
        ["item:BottleBin"] = new Vector3(-90f, 0f, 0f),
        ["item:Candy"] = new Vector3(0f, 0f, 0f),
        ["item:CashRegister"] = new Vector3(0f, 0f, 90f),
        ["item:ChemicalJug"] = new Vector3(-90f, 180f, 0f),
        ["item:Clock"] = new Vector3(0f, 90f, 0f),
        ["item:ControlPad"] = new Vector3(0f, 0f, 90f),
        ["item:Dentures"] = new Vector3(-90f, 0f, 0f),
        ["item:DiyFlashbang"] = new Vector3(-90f, 0f, 0f),
        ["item:DustPan"] = new Vector3(0f, 0f, 90f),
        ["item:EasterEgg"] = new Vector3(-90f, 0f, 0f),
        ["item:EggBeater"] = new Vector3(0f, 0f, 0f),
        ["item:FancyCup"] = new Vector3(-90f, 0f, 0f),
        ["item:FancyLamp"] = new Vector3(-90f, 0f, 0f),
        ["item:FancyPainting"] = new Vector3(0f, 90f, 0f),
        ["item:FishTestProp"] = new Vector3(0f, 90f, 0f),
        ["item:FlashLaserPointer"] = new Vector3(-90f, 0f, 0f),
        ["item:Flask"] = new Vector3(-90f, 0f, 0f),
        ["item:GiftBox"] = new Vector3(-90f, 0f, 0f),
        ["item:GoldBar"] = new Vector3(0f, 0f, -90f),
        ["item:GunAmmo"] = new Vector3(-90f, 0f, 0f),
        ["item:Hairdryer"] = new Vector3(0f, 90f, 0f),
        ["item:Key"] = new Vector3(0f, 90f, 0f),
        ["item:Knife"] = new Vector3(0f, 90f, 0f),
        ["item:LungApparatus"] = new Vector3(-90f, 0f, 0f),
        ["item:MagnifyingGlass"] = new Vector3(0f, 90f, 0f),
        ["item:PerfumeBottle"] = new Vector3(-90f, 0f, 0f),
        ["item:PickleJar"] = new Vector3(-90f, 0f, 0f),
        ["item:PillBottle"] = new Vector3(-90f, 75f, 0f),
        ["item:PlasticCup"] = new Vector3(-90f, 0f, 0f),
        ["item:Remote"] = new Vector3(0f, 0f, 90f),
        ["item:RobotToy"] = new Vector3(0f, 90f, 0f),
        ["item:RubberDuck"] = new Vector3(-90f, 0f, 0f),
        ["item:SeveredBoneRib"] = new Vector3(-90f, 0f, 0f),
        ["item:SeveredFoot"] = new Vector3(-90f, 0f, 0f),
        ["item:SeveredHand"] = new Vector3(0f, 0f, -90f),
        ["item:SeveredThigh"] = new Vector3(-90f, 0f, 0f),
        ["item:SeveredTongue"] = new Vector3(-90f, 0f, 0f),
        ["item:Shotgun"] = new Vector3(0f, 0f, 90f), // Muzzle +Z; grip/trigger extend toward -X, which must point down.
        ["item:Shovel"] = new Vector3(0f, 90f, 0f),
        ["item:SodaCanRed"] = new Vector3(-90f, 0f, 0f),
        ["item:SprayPaint"] = new Vector3(-90f, 0f, 0f),
        ["item:StopSign"] = new Vector3(0f, 90f, 0f),
        ["item:StunGrenade"] = new Vector3(-90f, 0f, 0f),
        ["item:TZPInhalant"] = new Vector3(-90f, 0f, 0f),
        ["item:TeaKettle"] = new Vector3(-90f, 0f, 0f),
        ["item:ToiletPaperRolls"] = new Vector3(-90f, 90f, 0f),
        ["item:Toothpaste"] = new Vector3(0f, 0f, -90f),
        ["item:WalkieTalkie"] = new Vector3(-90f, 0f, 0f),
        ["item:WeedKillerBottle"] = new Vector3(0f, 90f, 0f),
        ["item:WhoopieCushion"] = new Vector3(0f, 0f, 90f),
        ["item:YieldSign"] = new Vector3(0f, 90f, 0f),
        ["item:ZapGun"] = new Vector3(0f, 0f, 90f),
        ["item:Zeddog"] = new Vector3(0f, 180f, 0f),
        ["item:Clipboard"] = new Vector3(-90f, 0f, 0f),
        ["item:Mug"] = new Vector3(-90f, 90f, 0f),
        ["item:Ring"] = new Vector3(0f, 90f, 0f),
        ["item:ToyTrain"] = new Vector3(-20f, 0f, 0f),
        ["item:Cog1"] = new Vector3(-65f, 0f, 0f),
        ["item:LockPicker"] = new Vector3(-70f, 0f, 0f),
    };

    /// <summary>Natural upright model pose; independent from card camera composition.</summary>
    public static Vector3 WorldEuler(string? key) => key != null && World.TryGetValue(key, out var angles) ? angles : Vector3.zero;

    /// <summary>Combines a verified base view with a roll in its visible plane before spectator yaw.</summary>
    public static Quaternion WorldRotation(float yaw, string key)
    {
        float roll = key switch
        {
            "item:Key" or "item:MagnifyingGlass" or "item:RobotToy" or "item:StopSign" or "item:YieldSign"
                or "item:Clock" or "item:Candy" or "item:EggBeater" or "item:FishTestProp" or "item:WalkieTalkie" or "item:Ring" => 90f,
            "item:Knife" or "item:Shovel" => -90f,
            "item:Clipboard" => 90f,
            _ => 0f
        };
        return Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll) * Quaternion.Euler(WorldEuler(key));
    }

    /// <summary>Card-only yaw exposes depth without turning the readable face away.</summary>
    public static Quaternion CardRotation(Quaternion world, string key)
    {
        // Card-only composition, selected from the captured ten-angle originals.
        switch (key)
        {
            case "item:Clock": return world;
            case "item:Airhorn": return Quaternion.Euler(8f, -55f, 0f) * world;
            case "item:Brush": return Quaternion.Euler(12f, 155f, 0f);
            case "item:GoldBar": return Quaternion.Euler(24f, -30f, 0f) * world;
            case "item:MoldPan": return Quaternion.Euler(12f, -20f, 90f);
            case "item:LungApparatus": return Quaternion.Euler(10f, -32f, 0f) * world;
            case "item:SeveredEar": return Quaternion.Euler(0f, 90f, -25f);
            case "item:RubberDuck": return Quaternion.Euler(0f, -48f, 0f) * world;
            case "item:SeveredTongue": return Quaternion.Euler(0f, -25f, 180f) * world;
            case "item:Shotgun": return Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(90f, 0f, 0f); // pose2 rolled horizontal, muzzle left.
        }
        if (key == "item:StickyNote" || key == "item:StopSign" || key == "item:YieldSign" || key == "item:Clipboard" || key == "item:FancyPainting") return world;
        return Quaternion.Euler(0f, -18f, 0f) * world;
    }

    /// <summary>These cards need lit surface detail to distinguish grooves, teeth and rounded forms.</summary>
    public static bool UseStudioLighting(string key) => key is "item:Clock" or "item:Airhorn" or "item:Brush"
        or "item:GoldBar" or "item:MoldPan" or "item:LungApparatus" or "item:SeveredEar"
        or "item:RubberDuck" or "item:SeveredTongue" or "item:Shotgun";
}
