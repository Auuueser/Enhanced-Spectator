using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Pure visibility and paging rules for the retained fear quick menu.</summary>
public static class FearQuickMenuRules
{
    /// <summary>Maximum model cards shown on one page.</summary>
    public const int PageSize = 9;

    /// <summary>Gets whether the ESC entry icon should be visible.</summary>
    public static bool ShouldShowEntry(
        bool configured,
        bool quickMenuOpen,
        bool isHost,
        bool sessionEnabled)
    {
        return configured && quickMenuOpen;
    }

    /// <summary>Gets whether the local player may select a model.</summary>
    public static bool CanSelectModel(bool sessionEnabled, bool localPlayerDead)
    {
        return sessionEnabled && localPlayerDead;
    }

    /// <summary>Gets whether local fear-sound actions are available.</summary>
    public static bool CanUseSound(
        bool sessionEnabled,
        bool localRenderingEnabled,
        bool localPlayerDead)
    {
        return sessionEnabled && localRenderingEnabled && localPlayerDead;
    }

    /// <summary>Gets the page count for a catalog.</summary>
    public static int ResolvePageCount(int itemCount, int pageSize = PageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        return Math.Max(1, (Math.Max(0, itemCount) + pageSize - 1) / pageSize);
    }

    /// <summary>Clamps a requested page into the current catalog.</summary>
    public static int ClampPage(int requestedPage, int itemCount, int pageSize = PageSize)
    {
        return Math.Max(0, Math.Min(requestedPage, ResolvePageCount(itemCount, pageSize) - 1));
    }

    /// <summary>Gets the page that contains the specified item index.</summary>
    public static int ResolvePageForItem(int itemIndex, int itemCount, int pageSize = PageSize)
    {
        if (itemIndex < 0 || itemIndex >= Math.Max(0, itemCount))
        {
            return ClampPage(0, itemCount, pageSize);
        }

        return ClampPage(itemIndex / pageSize, itemCount, pageSize);
    }

    /// <summary>Moves one page while remaining within the current catalog.</summary>
    public static int ResolveAdjacentPage(
        int currentPage,
        int direction,
        int itemCount,
        int pageSize = PageSize)
    {
        return ClampPage(currentPage + Math.Sign(direction), itemCount, pageSize);
    }
}

/// <summary>Pure persistent-state styling rules for fear quick-menu controls.</summary>
public static class FearQuickMenuButtonRules
{
    /// <summary>Highlights the ghost-voice control only while routed voice is enabled.</summary>
    public static bool ShouldHighlightGhostVoiceEnabled(bool ghostVoiceMuted)
    {
        return !ghostVoiceMuted;
    }
}

/// <summary>Pure HDR preview compositing rules shared by runtime capture and tests.</summary>
public static class FearThumbnailColorRules
{
    /// <summary>Reconstructs foreground alpha from black- and white-background renders.</summary>
    public static float ResolveAlpha(Color blackBackground, Color whiteBackground)
    {
        float backgroundContribution = Clamp01(Math.Max(
            whiteBackground.r - blackBackground.r,
            Math.Max(
                whiteBackground.g - blackBackground.g,
                whiteBackground.b - blackBackground.b)));
        return 1f - backgroundContribution;
    }

    /// <summary>Compresses a non-negative linear HDR component into display gamma space.</summary>
    public static float ToneMapToGamma(float linearValue)
    {
        double mapped = 1d - Math.Exp(-Math.Max(0d, linearValue) * 0.65d);
        double clamped = Math.Max(0d, Math.Min(1d, mapped));
        double gamma = clamped <= 0.0031308d
            ? clamped * 12.92d
            : (1.055d * Math.Pow(clamped, 1d / 2.4d)) - 0.055d;
        return (float)Math.Max(0d, Math.Min(1d, gamma));
    }

    private static float Clamp01(float value)
    {
        return Math.Max(0f, Math.Min(1f, value));
    }
}

/// <summary>Pure camera placement rules for renderer-only fear model cards.</summary>
public static class FearThumbnailPoseRules
{
    /// <summary>Gets the camera offset direction in front of the corrected model forward axis.</summary>
    public static Vector3 ResolveFrontCameraDirection()
    {
        return new Vector3(0f, 0.12f, 1f).normalized;
    }

    /// <summary>Applies card-only bind-pose corrections without changing world fear visuals.</summary>
    public static Quaternion ResolveCardRotation(Quaternion worldRotation, string? modelKey)
    {
        if (modelKey != null && modelKey.StartsWith("item:", StringComparison.Ordinal)) return FearItemPoseRules.CardRotation(worldRotation, modelKey);
        return worldRotation * Quaternion.Euler(ResolveCardCorrectionEuler(modelKey));
    }

    /// <summary>Gets the card-only Euler correction for a stable model key.</summary>
    public static Vector3 ResolveCardCorrectionEuler(string? modelKey)
    {
        if (EqualsKey(modelKey, FearModeRules.DefaultModelKey))
        {
            return new Vector3(-90f, 0f, 0f);
        }

        if (EqualsKey(modelKey, "Manticoil")
            || EqualsKey(modelKey, "Doublewing")
            || EqualsKey(modelKey, "曼提线鸟"))
        {
            return new Vector3(0f, 180f, 0f);
        }

        if (modelKey == FearModelIdentityRules.BushWolf) return new Vector3(0f, 25f, 0f);

        if (EqualsKey(modelKey, "Centipede") || EqualsKey(modelKey, "抱脸虫"))
        {
            return new Vector3(0f, 45f, 0f);
        }

        return Vector3.zero;
    }

    /// <summary>Gets a card-only orthographic-size multiplier; smaller values enlarge the model.</summary>
    public static float ResolveOrthographicScale(string? modelKey)
    {
        if (modelKey == FearModelIdentityRules.BushWolf) return 0.72f;
        if (EqualsKey(modelKey, "MouthDog")
            || EqualsKey(modelKey, "无眼犬")
            || EqualsKey(modelKey, "无眼狗"))
        {
            return 0.62f;
        }

        if (EqualsKey(modelKey, "Puma")
            || EqualsKey(modelKey, "Black Panther")
            || EqualsKey(modelKey, "黑豹"))
        {
            return 0.58f;
        }

        return 1f;
    }

    private static bool EqualsKey(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Presentation metadata for one stable fear model key.</summary>
public sealed class FearQuickMenuModelEntry
{
    /// <summary>Creates a model-card descriptor.</summary>
    public FearQuickMenuModelEntry(
        string modelKey,
        string displayName,
        bool selected)
    {
        ModelKey = modelKey ?? throw new ArgumentNullException(nameof(modelKey));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Selected = selected;
    }

    /// <summary>Gets the stable model/network key.</summary>
    public string ModelKey { get; }

    /// <summary>Gets the localized card label.</summary>
    public string DisplayName { get; }

    /// <summary>Gets whether this entry is selected locally.</summary>
    public bool Selected { get; }
}

/// <summary>Stable model-key to localized-name and mod-authored icon mapping.</summary>
public static class FearModelUiPresentationRules
{
    /// <summary>Creates presentation entries in the catalog's authoritative order.</summary>
    public static void CopyEntriesTo(
        IReadOnlyList<string> modelKeys,
        string selectedModelKey,
        bool useChineseText,
        List<FearQuickMenuModelEntry> destination)
    {
        if (modelKeys == null)
        {
            throw new ArgumentNullException(nameof(modelKeys));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        for (int index = 0; index < modelKeys.Count; index++)
        {
            string modelKey = modelKeys[index];
            destination.Add(new FearQuickMenuModelEntry(
                modelKey,
                ResolveDisplayName(modelKey, useChineseText),
                string.Equals(modelKey, selectedModelKey, StringComparison.Ordinal)));
        }
    }

    /// <summary>Gets a localized presentation name while preserving stable network keys.</summary>
    public static string ResolveDisplayName(string? modelKey, bool useChineseText)
    {
        string value = string.IsNullOrWhiteSpace(modelKey) ? FearModeRules.DefaultModelKey : modelKey.Trim();
        if (value == FearModelIdentityRules.Ship) return useChineseText ? "迷你飞船" : "Miniature ship";
        if (value == FearModelIdentityRules.Dropship) return useChineseText ? "补给火箭" : "Delivery rocket";
        if (value == FearModelIdentityRules.BushWolf) return useChineseText ? "绑架狐狸" : "Bush wolf";
        if (value.StartsWith("item:", StringComparison.Ordinal) && OriginalItemCatalog.Items.TryGetValue(value.Substring(5), out var original))
            return OriginalItemDisplayNames.Resolve(value.Substring(5), original.Display, useChineseText);
        if (!useChineseText)
        {
            return value;
        }

        if (EqualsKey(value, FearModeRules.DefaultModelKey)) return "默认鬼魂";
        if (EqualsKey(value, "Earth Leviathan")) return "大地利维坦";
        if (EqualsKey(value, "Feiopar")) return "黑豹";
        if (EqualsKey(value, "GiantKiwi")) return "巨型几维鸟";
        if (EqualsKey(value, "Lasso")) return "套索人";
        if (EqualsKey(value, "Stingray")) return "魟鱼样本";
        if (EqualsKey(value, "Manticoil")) return "曼提线鸟";
        if (EqualsKey(value, "Doublewing")) return "曼提线鸟";
        if (EqualsKey(value, "Docile Locust Bees")) return "温顺蝗蜂";
        if (EqualsKey(value, "Red Locust Bees")) return "赤色蝗蜂群";
        if (EqualsKey(value, "Butler Bees")) return "管家蜂群";
        if (EqualsKey(value, "Centipede")) return "抱脸虫";
        if (EqualsKey(value, "Bunker Spider")) return "地堡蜘蛛";
        if (EqualsKey(value, "Hoarding bug")) return "囤积虫";
        if (EqualsKey(value, "Flowerman")) return "布拉肯";
        if (EqualsKey(value, "Crawler")) return "半身鱼";
        if (EqualsKey(value, "Blob")) return "史莱姆";
        if (EqualsKey(value, "DressGirl")
            || EqualsKey(value, "Girl")
            || EqualsKey(value, "Ghost Girl")
            || EqualsKey(value, "女孩")) return "幽灵女孩";
        if (EqualsKey(value, "Puffer")) return "孢子蜥蜴";
        if (EqualsKey(value, "Spring")) return "弹簧头";
        if (EqualsKey(value, "Jester")) return "八音盒";
        if (EqualsKey(value, "Nutcracker")) return "胡桃夹子";
        if (EqualsKey(value, "Masked")) return "面具人";
        if (EqualsKey(value, "Butler")) return "管家";
        if (EqualsKey(value, "MouthDog")) return "无眼犬";
        if (EqualsKey(value, "ForestGiant")) return "森林守卫";
        if (EqualsKey(value, "SandWorm")) return "大地利维坦";
        if (EqualsKey(value, "Baboon hawk")) return "狒狒鹰";
        if (EqualsKey(value, "RadMech")) return "旧鸟机体";
        if (EqualsKey(value, "Tulip Snake")) return "郁金香飞蛇";
        if (EqualsKey(value, "Clay Surgeon")) return "剪发师";
        if (EqualsKey(value, "Puma") || EqualsKey(value, "Black Panther")) return "黑豹";
        if (EqualsKey(value, FearModelPresentationRules.ManeaterBabyModelKey)) return "食人兽（幼体）";
        if (EqualsKey(value, FearModelPresentationRules.ManeaterAdultModelKey)) return "食人兽（成体）";
        if (EqualsKey(value, FearModelPresentationRules.CadaverBloomModelKey)) return "尸生体";
        return value;
    }

    private static bool EqualsKey(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Snapshot rendered by the retained fear quick-menu view.</summary>
public sealed class FearQuickMenuViewState
{
    /// <summary>Expanded catalog permission negotiated with the host.</summary>
    public bool SupportsExpandedModels { get; set; } = true;
    /// <summary>Whether the original delivery rocket has loaded at least once this process.</summary>
    public bool DropshipAvailable { get; set; } = true;
    /// <summary>Current category tab.</summary>
    public FearModelCategory Category { get; set; }
    /// <summary>Localized explanation when fear actions are unavailable.</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>Creates an immutable retained-view snapshot.</summary>
    public FearQuickMenuViewState(
        IReadOnlyList<FearQuickMenuModelEntry> entries,
        int pageIndex,
        bool useChineseText,
        bool isHost,
        bool sessionEnabled,
        bool localRenderingEnabled,
        bool canSelectModels,
        bool canUseSound,
        bool soundPlaying,
        bool soundButtonHighlighted,
        bool nextSoundButtonHighlighted,
        bool ghostVoiceMuted,
        string selectedDisplayName)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        PageIndex = pageIndex;
        UseChineseText = useChineseText;
        IsHost = isHost;
        SessionEnabled = sessionEnabled;
        LocalRenderingEnabled = localRenderingEnabled;
        CanSelectModels = canSelectModels;
        CanUseSound = canUseSound;
        SoundPlaying = soundPlaying;
        SoundButtonHighlighted = soundButtonHighlighted;
        NextSoundButtonHighlighted = nextSoundButtonHighlighted;
        GhostVoiceMuted = ghostVoiceMuted;
        SelectedDisplayName = selectedDisplayName ?? string.Empty;
    }

    /// <summary>Gets the model-card entries in catalog order.</summary>
    public IReadOnlyList<FearQuickMenuModelEntry> Entries { get; }

    /// <summary>Gets the zero-based active page.</summary>
    public int PageIndex { get; }

    /// <summary>Gets the number of pages required by the catalog.</summary>
    public int PageCount => FearQuickMenuRules.ResolvePageCount(Entries.Count);

    /// <summary>Gets whether Chinese presentation text is active.</summary>
    public bool UseChineseText { get; }

    /// <summary>Gets whether the local player is the host.</summary>
    public bool IsHost { get; }

    /// <summary>Gets whether the host-enabled fear session is active.</summary>
    public bool SessionEnabled { get; }

    /// <summary>Gets whether local fear rendering is enabled.</summary>
    public bool LocalRenderingEnabled { get; }

    /// <summary>Gets whether model cards may change the local selection.</summary>
    public bool CanSelectModels { get; }

    /// <summary>Gets whether sound actions are currently allowed.</summary>
    public bool CanUseSound { get; }

    /// <summary>Gets whether a local fear sound is currently playing.</summary>
    public bool SoundPlaying { get; }

    /// <summary>Gets whether the play/stop control should use its active highlight.</summary>
    public bool SoundButtonHighlighted { get; }

    /// <summary>Gets whether the next-sound control should show click feedback.</summary>
    public bool NextSoundButtonHighlighted { get; }

    /// <summary>Gets whether Enhanced Spectator routed ghost voice is locally muted.</summary>
    public bool GhostVoiceMuted { get; }

    /// <summary>Gets the localized selected-model label.</summary>
    public string SelectedDisplayName { get; }
}

/// <summary>Actions wired once into the retained quick-menu view.</summary>
public sealed class FearQuickMenuCallbacks
{
    /// <summary>Changes the retained catalog category.</summary>
    public Action<FearModelCategory>? ChangeCategory { get; set; }

    /// <summary>Creates the action set wired into the retained quick-menu view.</summary>
    public FearQuickMenuCallbacks(
        Action togglePanel,
        Action closePanel,
        Action toggleHostSession,
        Action toggleLocalRendering,
        Action toggleSound,
        Action nextSound,
        Action toggleGhostVoiceMute,
        Action<int> changePage,
        Action<string> selectModel)
    {
        TogglePanel = togglePanel ?? throw new ArgumentNullException(nameof(togglePanel));
        ClosePanel = closePanel ?? throw new ArgumentNullException(nameof(closePanel));
        ToggleHostSession = toggleHostSession ?? throw new ArgumentNullException(nameof(toggleHostSession));
        ToggleLocalRendering = toggleLocalRendering ?? throw new ArgumentNullException(nameof(toggleLocalRendering));
        ToggleSound = toggleSound ?? throw new ArgumentNullException(nameof(toggleSound));
        NextSound = nextSound ?? throw new ArgumentNullException(nameof(nextSound));
        ToggleGhostVoiceMute = toggleGhostVoiceMute ?? throw new ArgumentNullException(nameof(toggleGhostVoiceMute));
        ChangePage = changePage ?? throw new ArgumentNullException(nameof(changePage));
        SelectModel = selectModel ?? throw new ArgumentNullException(nameof(selectModel));
    }

    /// <summary>Gets the entry-icon panel toggle.</summary>
    public Action TogglePanel { get; }

    /// <summary>Gets the panel close action.</summary>
    public Action ClosePanel { get; }

    /// <summary>Gets the host-session toggle.</summary>
    public Action ToggleHostSession { get; }

    /// <summary>Gets the local rendering toggle.</summary>
    public Action ToggleLocalRendering { get; }

    /// <summary>Gets the current-sound play or stop action.</summary>
    public Action ToggleSound { get; }

    /// <summary>Gets the play-next-sound action.</summary>
    public Action NextSound { get; }

    /// <summary>Gets the local routed-ghost voice mute toggle.</summary>
    public Action ToggleGhostVoiceMute { get; }

    /// <summary>Gets the relative page-change action.</summary>
    public Action<int> ChangePage { get; }

    /// <summary>Gets the stable-key model selection action.</summary>
    public Action<string> SelectModel { get; }
}

/// <summary>Shared local sound actions used by both keyboard and retained UI controls.</summary>
public interface IFearSoundActions
{
    /// <summary>Gets whether a local fear sound is currently playing.</summary>
    bool IsLocalSoundPlaying { get; }

    /// <summary>Starts the current sound or stops the active sound.</summary>
    bool TryToggleLocalSound();

    /// <summary>Selects and immediately plays the next sound.</summary>
    bool TryPlayNextLocalSound();
}

/// <summary>Provides cached, renderer-only thumbnails sourced from loaded game model data.</summary>
public interface IFearModelThumbnailProvider : IDisposable
{
    /// <summary>Changes whenever a requested thumbnail becomes available.</summary>
    int Revision { get; }

    /// <summary>Queues a model thumbnail without duplicating work.</summary>
    void Request(string modelKey);

    /// <summary>Gets a rendered model thumbnail or the safe Default fallback.</summary>
    bool TryGet(string modelKey, out UnityEngine.Sprite? sprite);

    /// <summary>Gets the fixed pixel icon used only by the quick-menu entry button.</summary>
    bool TryGetEntryIcon(out UnityEngine.Sprite? sprite);

    /// <summary>Processes at most one queued thumbnail.</summary>
    void Tick();
}
