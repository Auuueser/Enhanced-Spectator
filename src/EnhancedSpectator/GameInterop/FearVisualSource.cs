using System;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Separates the hierarchy needed for complete bone mapping from the subtree whose renderers are copied.
/// </summary>
public sealed class FearVisualSource
{
    /// <summary>Creates a read-only renderer source descriptor.</summary>
    public FearVisualSource(
        Transform hierarchyRoot,
        Transform rendererRoot,
        bool forceRendererVisibility = false,
        bool useMeshBoundsForNormalization = false,
        bool skinnedMeshOnly = false)
    {
        HierarchyRoot = hierarchyRoot ?? throw new ArgumentNullException(nameof(hierarchyRoot));
        RendererRoot = rendererRoot ?? throw new ArgumentNullException(nameof(rendererRoot));
        ForceRendererVisibility = forceRendererVisibility;
        UseMeshBoundsForNormalization = useMeshBoundsForNormalization;
        SkinnedMeshOnly = skinnedMeshOnly;
    }

    /// <summary>Gets the complete transform hierarchy required by renderer bones.</summary>
    public Transform HierarchyRoot { get; }

    /// <summary>Gets the subtree whose supported renderers should be copied.</summary>
    public Transform RendererRoot { get; }

    /// <summary>Gets whether inactive/disabled renderers in the selected form should be made visible.</summary>
    public bool ForceRendererVisibility { get; }

    /// <summary>Gets whether mesh-local bounds should replace unreliable prefab renderer world bounds.</summary>
    public bool UseMeshBoundsForNormalization { get; }

    /// <summary>Gets whether helper MeshRenderers should be excluded from this visual form.</summary>
    public bool SkinnedMeshOnly { get; }
}
