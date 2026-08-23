using EnhancedSpectator.Features.FearMode;

namespace EnhancedSpectator.GameInterop;

/// <summary>Owns the retained UI attached to confirmed V81 quick-menu objects.</summary>
public interface IGameFearQuickMenuAdapter
{
    /// <summary>Gets whether the current local quick menu is open.</summary>
    bool IsQuickMenuOpen { get; }

    /// <summary>Creates or reuses one view for the current quick-menu instance.</summary>
    bool TryEnsureView(FearQuickMenuCallbacks callbacks, out string reason);

    /// <summary>Shows or hides the upper-right entry icon.</summary>
    void SetEntryVisible(bool visible);

    /// <summary>Shows or hides the retained catalog panel.</summary>
    void SetPanelVisible(bool visible);

    /// <summary>Renders the latest state without recreating the UI hierarchy.</summary>
    void Render(FearQuickMenuViewState state);

    /// <summary>Destroys the mod-owned view and releases its retained UI objects.</summary>
    void Dispose();
}
