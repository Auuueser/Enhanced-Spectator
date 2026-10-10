using System;
using System.Collections.Generic;
using EnhancedSpectator.Networking;
using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// The watch-together parties this frame (see <see cref="SpectatorPartyRules"/>). A party watching a player is drawn
/// as one: the leader's model at its camera, each follower in a formation behind it (two abreast, left then right,
/// a row further back for every pair), facing the leader's way. A follower is only seen while it speaks (see
/// LethalCompanyModelVisibility.InFormation), never more opaque than its leader, so a formation never stands unfaded
/// behind a faded leader. When a member leaves, the others close up in client order. Members of the local player's
/// party are not drawn at all.
/// </summary>
public sealed class SpectatorParties
{
    /// <summary>The formation's spacing: between the two of a row, and from one row to the next.</summary>
    internal const float Spacing = .6f;

    private readonly Dictionary<ulong, ulong?> _following = new();
    private readonly Dictionary<ulong, SpectatorPoseState> _poses = new();
    private readonly Dictionary<ulong, (SpectatorPoseState Leader, int Slot)> _formation = new();
    private readonly Dictionary<ulong, int> _rows = new();
    private readonly List<ulong> _members = new();
    private readonly Func<ulong, ulong?> _followingOf;
    private ulong? _local;

    /// <summary>Creates an empty set of parties.</summary>
    public SpectatorParties() => _followingOf = id => _following.TryGetValue(id, out var next) ? next : null;

    /// <summary>Rebuilds the parties from the local player's follow and every remote spectator's published pose.</summary>
    internal void Update(ulong local, ulong? localFollowing, Func<ulong, SpectatorPoseState?> poseOf, IReadOnlyList<SpectatorTargetState> remotes)
    {
        Clear();
        _local = local;
        _following[local] = localFollowing;
        foreach (var remote in remotes)
            if (remote.LocalClientId != local && poseOf(remote.LocalClientId) is { } pose)
            {
                _poses[remote.LocalClientId] = pose; _following[remote.LocalClientId] = pose.FollowingClientId;
                _members.Add(remote.LocalClientId);
            }
        // Slots in client order, so every outsider lines a party up alike.
        _members.Sort();
        foreach (ulong member in _members)
        {
            ulong leader = SpectatorPartyRules.Leader(member, _followingOf);
            if (leader == member || leader == local || !_poses.TryGetValue(leader, out var leaderPose)
                || leaderPose.SplitView != SpectatorSplitView.Watching || _poses[member].SplitView != SpectatorSplitView.Watching) continue;
            _rows.TryGetValue(leader, out int slot);
            _rows[leader] = slot + 1;
            _formation[member] = (leaderPose, slot);
        }
    }

    internal void Clear()
    {
        _local = null; _following.Clear(); _poses.Clear(); _formation.Clear(); _rows.Clear(); _members.Clear();
    }

    /// <summary><paramref name="member"/> watches together with the local player (as leader, follower or fellow follower).</summary>
    internal bool InLocalParty(ulong member) => _local is { } local && member != local
        && SpectatorPartyRules.Leader(member, _followingOf) == SpectatorPartyRules.Leader(local, _followingOf);

    /// <summary><paramref name="member"/> stands in its leader's formation: the leader's pose and its place in it.</summary>
    internal bool TryGetFormation(ulong member, out SpectatorPoseState leader, out int slot)
    {
        bool found = _formation.TryGetValue(member, out var place);
        leader = place.Leader; slot = place.Slot;
        return found;
    }

    /// <summary>The leader whose formation <paramref name="member"/> stands in, if any.</summary>
    internal ulong? FormationLeader(ulong member) => _formation.TryGetValue(member, out var place) ? place.Leader.LocalClientId : null;

    /// <summary>
    /// Moves a pose from the leader's to formation place <paramref name="slot"/> (0 first): left-back and right-back
    /// in turn, by the leader's heading only, keeping the leader's facing.
    /// </summary>
    internal static Vector3 Place(int slot, Vector3 leaderPosition, Quaternion leaderRotation)
    {
        float side = slot % 2 == 0 ? -1 : 1, row = slot / 2 + 1;
        Vector3 forward = leaderRotation * Vector3.forward;
        forward.y = 0;
        // Looking straight up or down, the heading is the top of the view.
        if (forward.sqrMagnitude < .0001f) { forward = leaderRotation * Vector3.up; forward.y = 0; }
        forward.Normalize();
        var right = new Vector3(forward.z, 0, -forward.x);
        return leaderPosition + right * (side * Spacing * .5f) - forward * (Spacing * row);
    }
}
