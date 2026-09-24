using System;
using System.Collections.Generic;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace EnhancedSpectator.GameInterop;

/// <summary>Reuses only the original tongue's three damped bone constraints, without item behaviour.</summary>
public sealed class OriginalTongueVisualMotion : IDisposable
{
    private readonly RigBuilder _builder;
    private readonly Transform _root;
    private readonly List<(Transform bone, Vector3 position, Quaternion rotation)> _rest = new List<(Transform, Vector3, Quaternion)>();
    private bool _built;
    private bool _failed;
    private int _lastFrame = -1;
    private Vector3 _lastPosition;
    private Vector3 _lastScale;

    private OriginalTongueVisualMotion(RigBuilder builder, Transform root) { _builder = builder; _root = root; }

    /// <summary>Maps confirmed Rig/DampedTransform references exclusively onto clean cloned bones.</summary>
    public static OriginalTongueVisualMotion? Create(Transform source, Dictionary<Transform, Transform> map)
    {
        var original = source.GetComponentInChildren<RigBuilder>(true);
        if (original == null || !map.TryGetValue(original.transform, out var clone)) return null;
        var animator = clone.gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // No controller, animation events, item scripts or network behaviours are copied.
        var builder = clone.gameObject.AddComponent<RigBuilder>();
        builder.enabled = false;
        var motion = new OriginalTongueVisualMotion(builder, clone);
        foreach (var layer in original.layers)
        {
            if (layer.rig == null || !map.TryGetValue(layer.rig.transform, out var rigTransform)) continue;
            var rig = rigTransform.gameObject.AddComponent<Rig>();
            rig.weight = layer.rig.weight;
            foreach (var originalConstraint in layer.rig.GetComponentsInChildren<DampedTransform>(true))
            {
                var data = originalConstraint.data;
                if (data.constrainedObject == null || data.sourceObject == null
                    || !map.TryGetValue(originalConstraint.transform, out var owner)
                    || !map.TryGetValue(data.constrainedObject, out var bone)
                    || !map.TryGetValue(data.sourceObject, out var target)) continue;
                data.constrainedObject = bone;
                data.sourceObject = target;
                var constraint = owner.gameObject.AddComponent<DampedTransform>();
                constraint.data = data;
                constraint.weight = originalConstraint.weight;
                motion._rest.Add((bone, bone.localPosition, bone.localRotation));
            }
            builder.layers.Add(new RigLayer(rig, layer.active));
        }
        return motion;
    }

    /// <summary>Evaluates the original rig after the ghost pose; resets inertia on teleports or resizing.</summary>
    public void Tick()
    {
        if (_failed || _builder == null || _lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;
        try
        {
            if (!_built || Vector3.Distance(_root.position, _lastPosition) > 8f || _root.lossyScale != _lastScale)
            {
                _builder.Clear();
                foreach (var pose in _rest) { pose.bone.localPosition = pose.position; pose.bone.localRotation = pose.rotation; }
                _built = _builder.Build();
                if (!_built) { _failed = true; ModLog.Warning("Original tongue visual rig could not build; retaining static visual."); return; }
                _builder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _builder.Evaluate(0f);
            }
            _builder.Evaluate(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            _lastPosition = _root.position;
            _lastScale = _root.lossyScale;
        }
        catch (Exception ex) { _failed = true; _builder.Clear(); ModLog.Warning("Tongue visual rig stopped: " + ex.GetType().Name); }
    }

    /// <inheritdoc />
    public void Dispose() { if (_builder != null) _builder.Clear(); }

    internal void ResetForReuse()
    {
        if (_builder != null) _builder.Clear();
        foreach (var pose in _rest)
            if (pose.bone != null) { pose.bone.localPosition = pose.position; pose.bone.localRotation = pose.rotation; }
        _built = false;
        _lastFrame = -1;
    }
}
