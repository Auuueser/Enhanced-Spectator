using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using EnhancedSpectator.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace EnhancedSpectator.GameInterop;

internal sealed class OriginalDropshipSoundSource : IDisposable
{
    private Task<V81DropshipAudioData>? _read;
    private V81DropshipAudioData? _data;
    private UnityWebRequest[]? _requests;
    private readonly List<AudioClip> _clips = new List<AudioClip>();
    private bool _finished;

    internal IReadOnlyList<AudioClip> Poll()
    {
        if (_finished) return _clips;
        try
        {
            if (_read == null && _data == null)
            {
                string source = Application.dataPath;
                string cache = Path.Combine(Paths.CachePath, "EnhancedSpectator", "OriginalAudio");
                _read = Task.Run(() => V81DropshipAudioData.Prepare(source, cache));
                return _clips;
            }
            if (_read != null)
            {
                if (!_read.IsCompleted) return _clips;
                _data = _read.GetAwaiter().GetResult(); _read = null;
                _requests = new UnityWebRequest[_data.clips.Length];
                for (int i = 0; i < _requests.Length; i++)
                {
                    var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(_data.clips[i].CachePath).AbsoluteUri, AudioType.OGGVORBIS);
                    _requests[i] = request; request.timeout = 10; request.SendWebRequest();
                }
                return _clips;
            }
            if (_requests == null) return _clips;
            foreach (var request in _requests) if (!request.isDone) return _clips;
            for (int i = 0; i < _requests.Length; i++)
            {
                var request = _requests[i];
                if (request.result != UnityWebRequest.Result.Success) throw new InvalidDataException("Original rocket audio decode failed: " + request.error);
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || clip.samples <= 0) throw new InvalidDataException("Empty original rocket audio clip");
                clip.name = _data!.clips[i].name;
                _clips.Add(clip);
            }
            foreach (var request in _requests) request.Dispose();
            _requests = null; _finished = true;
            ModLog.Info("Original delivery-rocket audio available before landing: 4 verified clips.");
        }
        catch (Exception ex)
        {
            Dispose();
            ModLog.Warning("Pre-landing rocket audio unavailable; keeping confirmed scene audio fallback. " + ex.Message);
        }
        return _clips;
    }

    public void Dispose()
    {
        _finished = true;
        if (_read != null) _ = _read.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _read = null;
        if (_requests != null) foreach (var request in _requests) request?.Dispose();
        _requests = null;
        foreach (var clip in _clips) if (clip != null) UnityEngine.Object.Destroy(clip);
        _clips.Clear();
    }
}
