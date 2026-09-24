using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.FearMode;

public sealed partial class FearModelThumbnailService
{
    private readonly List<AudioClip> _auditSounds = new List<AudioClip>();
    private readonly List<string> _auditKeys = new List<string>();
    private int _auditIndex;
    private bool _auditPoses;
    private int _auditPoseIndex;
    private Vector3? _auditPoseOverride;
    private static readonly Vector3[] AuditAngles = { Vector3.zero, new Vector3(-90,0,0), new Vector3(90,0,0),
        new Vector3(0,90,0), new Vector3(0,-90,0), new Vector3(0,180,0), new Vector3(0,0,90),
        new Vector3(0,0,-90), new Vector3(-90,180,0), new Vector3(90,180,0) };
    private int _auditPollFrame;
    private bool _auditRequested;
    private int _auditPage;
    private int _auditPageFrame;
    private bool _auditPagesDone = true;
    /// <summary>Local developer-only capture directory; never included in packages.</summary>
    public string? AuditDirectory { get; private set; }

    /// <summary>Consumes an explicit local request and exports the actual card textures, one per frame.</summary>
    public bool TickDeveloperAudit()
    {
        if (_disposed) return false;
        try
        {
            if (Time.frameCount >= _auditPollFrame)
            {
                _auditPollFrame = Time.frameCount + 120;
                string request = Path.Combine(Paths.ConfigPath, "EnhancedSpectator.catalog-audit.request");
                if (File.Exists(request))
                {
                    _auditPoses = File.ReadAllText(request).Trim() == "poses";
                    _auditPoseIndex = 0;
                    File.Delete(request);
                    _auditRequested = true;
                    _auditKeys.Clear();
                    _auditIndex = 0;
                    _auditPagesDone = true;
                    AuditDirectory = Path.Combine(Paths.CachePath, "EnhancedSpectator", "Catalog-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    Directory.CreateDirectory(AuditDirectory);
                    ModLog.Info("Catalog audit requested: " + AuditDirectory);
                }
            }
            if (!_auditRequested || !_gameAdapter.TryGetActiveCameraCullingMask(out _)) return false;
            if (_auditKeys.Count == 0)
            {
                _catalog.Refresh();
                if (_catalog.ModelKeys.Count <= 1) return false;
                foreach (string model in _catalog.ModelKeys)
                    if (!_auditPoses || model.StartsWith("item:", StringComparison.Ordinal) || model == FearModelIdentityRules.BushWolf || model == FearModelIdentityRules.Dropship) _auditKeys.Add(model);
                File.WriteAllText(Path.Combine(AuditDirectory!, "catalog.tsv"), "index\tkey\tcategory\tname\timage\tstatus\n", Encoding.UTF8);
            }
            string key = _auditKeys[_auditIndex];
            _auditPoseOverride = _auditPoses ? AuditAngles[_auditPoseIndex] : (Vector3?)null;
            TryRender(key);
            _auditPoseOverride = null;
            string file = _auditIndex.ToString("D3") + (_auditPoses ? "-pose" + _auditPoseIndex : "") + ".png";
            bool rendered = _sprites.TryGetValue(key, out var sprite) && sprite != null;
            if (rendered) File.WriteAllBytes(Path.Combine(AuditDirectory!, file), sprite!.texture.EncodeToPNG());
            File.AppendAllText(Path.Combine(AuditDirectory!, "catalog.tsv"),
                $"{_auditIndex}\t{key}\t{FearModelIdentityRules.Category(key)}\t{FearModelUiPresentationRules.ResolveDisplayName(key, true)}\t{file}\t{(rendered ? "ok" : "unavailable")}\n", Encoding.UTF8);
            if (_auditPoses && ++_auditPoseIndex < AuditAngles.Length) return true;
            _auditPoseIndex = 0;
            _gameAdapter.CopyFearSoundClipsTo(key, _auditSounds);
            File.AppendAllText(Path.Combine(AuditDirectory!, "sounds.tsv"), key + "\t" + _auditSounds.Count + "\t"
                + string.Join(" | ", _auditSounds.ConvertAll(clip => clip.name + " (" + clip.samples + ")")) + "\n", Encoding.UTF8);
            if (++_auditIndex >= _auditKeys.Count)
            {
                _auditRequested = false;
                _auditPagesDone = _auditPoses;
                _auditPage = 0;
                _auditPageFrame = -1;
                ModLog.Info("Catalog textures exported; open ESC to capture UI pages: " + AuditDirectory);
            }
            return true;
        }
        catch (Exception ex)
        {
            _auditRequested = false;
            _auditPagesDone = true;
            ModLog.Warning("Catalog audit stopped: " + ex.Message);
            return false;
        }
    }

    /// <summary>Only an explicit developer capture request temporarily pages the already-open menu.</summary>
    public bool TryGetAuditPage(out int page)
    {
        page = _auditPage;
        if (_auditPagesDone) return false;
        if (_auditPageFrame < 0) _auditPageFrame = Time.frameCount + 45;
        return true;
    }

    /// <summary>Captures the real Unity UI after its cards have had time to render.</summary>
    public void CaptureAuditPage(int pageCount)
    {
        if (_auditPagesDone || Time.frameCount < _auditPageFrame) return;
        try
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(AuditDirectory!, "ui-page-" + _auditPage.ToString("D2") + ".png"));
            _auditPageFrame = Time.frameCount + 45;
            if (++_auditPage >= pageCount)
            {
                _auditPagesDone = true;
                ModLog.Info("Catalog UI capture complete: " + AuditDirectory);
            }
        }
        catch (Exception ex) { _auditPagesDone = true; ModLog.Warning("Catalog UI capture stopped: " + ex.Message); }
    }
}
