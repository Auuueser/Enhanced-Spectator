using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>V81 identities verified from local original Item assets; no game assets are embedded.</summary>
public static class OriginalItemCatalog
{
    /// <summary>Known original Item asset identities, independent of localized display names and runtime list indices.</summary>
    public static readonly IReadOnlyDictionary<string, OriginalItemIdentity> Items =
        new Dictionary<string, OriginalItemIdentity>(StringComparer.Ordinal)
        {
            ["BeltBag"] = new OriginalItemIdentity(20, "BeltBagItem", "Belt bag", false),
            ["Binoculars"] = new OriginalItemIdentity(11, "Binoculars", "Binoculars", false),
            ["Boombox"] = new OriginalItemIdentity(9, "Boombox", "Boombox", false),
            ["Clipboard"] = new OriginalItemIdentity(2, null, "clipboard", false),
            ["DiyFlashbang"] = new OriginalItemIdentity(0, "DiyFlashbang", "Homemade flashbang", true),
            ["ExtensionLadder"] = new OriginalItemIdentity(17, "ExtensionLadderItem", "Extension ladder", false),
            ["Flashlight"] = new OriginalItemIdentity(6, "BBFlashlight", "Flashlight", false),
            ["GunAmmo"] = new OriginalItemIdentity(17, "ShotgunShell", "Ammo", false),
            ["Jetpack"] = new OriginalItemIdentity(13, "JetpackItem", "Jetpack", false),
            ["Key"] = new OriginalItemIdentity(14, "Key", "Key", false),
            ["LockPicker"] = new OriginalItemIdentity(8, "LockPickerItem", "Lockpicker", false),
            ["LungApparatus"] = new OriginalItemIdentity(3, "LungApparatusTurnedOff", "Apparatus", true),
            ["MapDevice"] = new OriginalItemIdentity(4, "MappingDevice", "Mapper", false),
            ["ProFlashlight"] = new OriginalItemIdentity(1, "FlashlightItem", "Pro-flashlight", false),
            ["RadarBooster"] = new OriginalItemIdentity(16, "RadarBoosterDevice", "Radar-booster", false),
            ["7Ball"] = new OriginalItemIdentity(0, "Magic7Ball", "Magic 7 ball", true),
            ["Airhorn"] = new OriginalItemIdentity(0, "Airhorn", "Airhorn", true),
            ["BabyKiwiEgg"] = new OriginalItemIdentity(0, "KiwiBabyItem", "Egg", true),
            ["Bell"] = new OriginalItemIdentity(0, "HandBell", "Bell", true),
            ["BigBolt"] = new OriginalItemIdentity(0, "BigBolt", "Big bolt", true),
            ["BottleBin"] = new OriginalItemIdentity(0, "BinFullOfBottles", "Bottles", true),
            ["Brush"] = new OriginalItemIdentity(0, "Hairbrush", "Brush", true),
            ["Candy"] = new OriginalItemIdentity(0, "Candy", "Candy", true),
            ["CashRegister"] = new OriginalItemIdentity(0, "CashRegisterItem", "Cash register", true),
            ["ChemicalJug"] = new OriginalItemIdentity(0, "ChemicalJug", "Chemical jug", true),
            ["Clock"] = new OriginalItemIdentity(0, "Clock", "Clock", true),
            ["ClownHorn"] = new OriginalItemIdentity(0, "Clownhorn", "Clown horn", true),
            ["Cog1"] = new OriginalItemIdentity(0, "Cog", "Large axle", true),
            ["ComedyMask"] = new OriginalItemIdentity(0, "ComedyMask", "Comedy", true),
            ["ControlPad"] = new OriginalItemIdentity(0, "ControlPad", "Control pad", true),
            ["Dentures"] = new OriginalItemIdentity(0, "Dentures", "Teeth", true),
            ["DustPan"] = new OriginalItemIdentity(0, "Dustpan", "Dust pan", true),
            ["EasterEgg"] = new OriginalItemIdentity(0, "EasterEgg", "Easter egg", true),
            ["EggBeater"] = new OriginalItemIdentity(0, "EggBeater", "Egg beater", true),
            ["EnginePart1"] = new OriginalItemIdentity(0, "EnginePart", "V-type engine", true),
            ["FancyCup"] = new OriginalItemIdentity(0, "FancyGlass", "Golden cup", true),
            ["FancyLamp"] = new OriginalItemIdentity(0, "FancyLamp", "Fancy lamp", true),
            ["FancyPainting"] = new OriginalItemIdentity(0, "Painting", "Painting", true),
            ["FishTestProp"] = new OriginalItemIdentity(0, "FishTestProp", "Plastic fish", true),
            ["FlashLaserPointer"] = new OriginalItemIdentity(1, "LaserPointer", "Laser pointer", true),
            ["Flask"] = new OriginalItemIdentity(0, "Flask", "Flask", true),
            ["GarbageLid"] = new OriginalItemIdentity(0, "GarbageLid", "Garbage lid", true),
            ["GiftBox"] = new OriginalItemIdentity(152767, "GiftBox", "Gift", true),
            ["GoldBar"] = new OriginalItemIdentity(0, "GoldBar", "Gold bar", true),
            ["Hairdryer"] = new OriginalItemIdentity(0, "Hairdryer", "Hairdryer", true),
            ["Knife"] = new OriginalItemIdentity(0, "KnifeItem", "Kitchen knife", true),
            ["MagnifyingGlass"] = new OriginalItemIdentity(0, "MagnifyingGlass", "Magnifying glass", true),
            ["MetalSheet"] = new OriginalItemIdentity(0, "MetalSheet", "Metal sheet", true),
            ["MoldPan"] = new OriginalItemIdentity(0, "CookieMoldPan", "Cookie pan", true),
            ["Mug"] = new OriginalItemIdentity(0, "Mug", "Mug", true),
            ["PerfumeBottle"] = new OriginalItemIdentity(0, "PerfumeBottle", "Perfume bottle", true),
            ["Phone"] = new OriginalItemIdentity(0, "OldPhone", "Old phone", true),
            ["PickleJar"] = new OriginalItemIdentity(0, "PickleJar", "Jar of pickles", true),
            ["PillBottle"] = new OriginalItemIdentity(0, "PillBottle", "Pill bottle", true),
            ["PlasticCup"] = new OriginalItemIdentity(0, "PlasticCup", "Plastic cup", true),
            ["RedLocustHive"] = new OriginalItemIdentity(1531, "RedLocustHive", "Hive", true),
            ["Remote"] = new OriginalItemIdentity(0, "Remote", "Remote", true),
            ["Ring"] = new OriginalItemIdentity(0, "FancyRing", "Ring", true),
            ["RobotToy"] = new OriginalItemIdentity(0, "RobotToy", "Toy robot", true),
            ["RubberDuck"] = new OriginalItemIdentity(0, "RubberDucky", "Rubber Ducky", true),
            ["SeveredBone"] = new OriginalItemIdentity(0, "Bone", "Bone", true),
            ["SeveredBoneRib"] = new OriginalItemIdentity(0, "RibcageBone", "Ribcage", true),
            ["SeveredEar"] = new OriginalItemIdentity(0, "Ear", "Ear", true),
            ["SeveredFoot"] = new OriginalItemIdentity(0, "SeveredFootLOD0", "Foot", true),
            ["SeveredHand"] = new OriginalItemIdentity(0, "SeveredHandLOD0", "Hand", true),
            ["SeveredHeart"] = new OriginalItemIdentity(0, "HeartContainer", "Heart", true),
            ["SeveredThigh"] = new OriginalItemIdentity(0, "SeveredThighLOD0", "Knee", true),
            ["SeveredTongue"] = new OriginalItemIdentity(0, "Tongue", "Tongue", true),
            ["SoccerBall"] = new OriginalItemIdentity(0, "SoccerBall", "Soccer ball", true),
            ["SodaCanRed"] = new OriginalItemIdentity(0, "RedSodaCan", "Red soda", true),
            ["SteeringWheel"] = new OriginalItemIdentity(0, "SteeringWheel", "Steering wheel", true),
            ["StopSign"] = new OriginalItemIdentity(0, "StopSign", "Stop sign", true),
            ["TeaKettle"] = new OriginalItemIdentity(0, "TeaKettle", "Tea kettle", true),
            ["ToiletPaperRolls"] = new OriginalItemIdentity(0, "ToiletPaperRolls", "Toilet paper", true),
            ["Toothpaste"] = new OriginalItemIdentity(0, "Toothpaste", "Toothpaste", true),
            ["ToyCube"] = new OriginalItemIdentity(0, "ToyCube", "Toy cube", true),
            ["ToyTrain"] = new OriginalItemIdentity(0, "ToyTrain", "Toy train", true),
            ["TragedyMask"] = new OriginalItemIdentity(0, "TragedyMask", "Tragedy", true),
            ["WhoopieCushion"] = new OriginalItemIdentity(0, "WhoopieCushion", "Whoopie cushion", true),
            ["YieldSign"] = new OriginalItemIdentity(0, "YieldSign", "Yield sign", true),
            ["Zeddog"] = new OriginalItemIdentity(0, "ZeddogPlushie", "Zed Dog", true),
            ["Shotgun"] = new OriginalItemIdentity(17, "ShotgunItem", "Shotgun", true),
            ["Shovel"] = new OriginalItemIdentity(7, "ShovelItem", "Shovel", false),
            ["SprayPaint"] = new OriginalItemIdentity(18, "SprayPaintItem", "Spray paint", false),
            ["StickyNote"] = new OriginalItemIdentity(10, null, "Sticky note", false),
            ["StunGrenade"] = new OriginalItemIdentity(12, "StunGrenade", "Stun grenade", false),
            ["TZPInhalant"] = new OriginalItemIdentity(15, "TZPChemical", "TZP-Inhalant", false),
            ["WalkieTalkie"] = new OriginalItemIdentity(7, "WalkieTalkie", "Walkie-talkie", false),
            ["WeedKillerBottle"] = new OriginalItemIdentity(19, "WeedKillerItem", "Weed killer", false),
            ["ZapGun"] = new OriginalItemIdentity(5, "PatcherGunItem", "Zap gun", false),
            ["CardboardBox"] = new OriginalItemIdentity(0, null, "box", false),
        };
}

/// <summary>Original asset metadata used to validate a loaded item before exposing its visual.</summary>
public sealed class OriginalItemIdentity
{
    /// <summary>Creates immutable original identity metadata.</summary>
    public OriginalItemIdentity(int id, string? prefab, string display, bool scrap)
    { Id = id; Prefab = prefab; Display = display; Scrap = scrap; }
    /// <summary>Original serialized item id (not a list position).</summary>
    public int Id { get; }
    /// <summary>Original prefab name, or null for scene-only objects.</summary>
    public string? Prefab { get; }
    /// <summary>English fallback display label.</summary>
    public string Display { get; }
    /// <summary>Original scrap classification.</summary>
    public bool Scrap { get; }
    /// <summary>Checks the original serialized identity, without inspecting display text.</summary>
    public bool Matches(int id, string? prefab) => Id == id && string.Equals(Prefab, prefab, StringComparison.Ordinal);
}
