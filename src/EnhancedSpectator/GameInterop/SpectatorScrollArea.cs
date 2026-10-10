using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// A clipped, scrollable region inside a retained panel: content is laid out from the top of <see cref="Content"/>,
/// the mouse wheel (or an automatic glide) moves it smoothly, and a thin bar on the right shows where you are.
/// The bar hides when everything fits.
/// </summary>
internal sealed class SpectatorScrollArea
{
    private const float Step = 60, Smooth = .09f;
    private readonly RectTransform _viewport, _track, _thumb;
    private float _offset, _target, _contentHeight, _viewportHeight, _ui = 1;

    internal RectTransform Content { get; }
    internal float Offset => _offset;
    internal bool Overflows => _contentHeight > _viewportHeight + .5f;
    internal bool AtEnd => _target >= MaxOffset - .5f;
    private float MaxOffset => Mathf.Max(0, _contentHeight - _viewportHeight);

    internal SpectatorScrollArea(Transform parent, string name, Color thumb)
    {
        _viewport = new GameObject(name, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        _viewport.SetParent(parent, false);
        _viewport.anchorMin = _viewport.anchorMax = _viewport.pivot = new Vector2(0, 1);
        Content = new GameObject(name + " content", typeof(RectTransform)).GetComponent<RectTransform>();
        Content.SetParent(_viewport, false);
        Content.anchorMin = Content.anchorMax = Content.pivot = new Vector2(0, 1);
        _track = SplitScreenView.CreateImage(name + " track", parent, new Color(1, 1, 1, .06f)).rectTransform;
        _thumb = SplitScreenView.CreateImage(name + " thumb", _track, thumb).rectTransform;
        _track.anchorMin = _track.anchorMax = _track.pivot = new Vector2(0, 1);
        _thumb.anchorMin = new Vector2(0, 1); _thumb.anchorMax = new Vector2(1, 1); _thumb.pivot = new Vector2(.5f, 1);
    }

    /// <summary>Places the visible window (panel coordinates from its top-left) and sets the content's height.</summary>
    internal void Layout(float x, float y, float width, float height, float contentHeight, float ui)
    {
        _ui = ui; _viewportHeight = height; _contentHeight = contentHeight;
        _viewport.anchoredPosition = new Vector2(x, -y); _viewport.sizeDelta = new Vector2(width, height);
        Content.sizeDelta = new Vector2(width, contentHeight);
        float bar = Mathf.Max(2, Mathf.Round(3 * ui));
        _track.anchoredPosition = new Vector2(x + width + Mathf.Round(3 * ui), -y); _track.sizeDelta = new Vector2(bar, height);
        _target = Mathf.Clamp(_target, 0, MaxOffset); _offset = Mathf.Clamp(_offset, 0, MaxOffset);
        Apply();
    }

    internal void Reset() { _offset = _target = 0; Apply(); }

    internal bool Contains(Vector2 point, Camera? eye) => RectTransformUtility.RectangleContainsScreenPoint(_viewport, point, eye);

    /// <summary>Wheel notches: positive scrolls up (towards the start).</summary>
    internal void Scroll(float notches) => _target = Mathf.Clamp(_target - notches * Step * _ui, 0, MaxOffset);

    /// <summary>Glides down just far enough that content ending at <paramref name="bottom"/> is visible; never back up.</summary>
    internal void Reveal(float bottom)
    {
        if (bottom > _target + _viewportHeight) _target = Mathf.Min(MaxOffset, bottom - _viewportHeight);
    }

    internal void Tick(float deltaTime)
    {
        if (Mathf.Abs(_offset - _target) < .25f) { if (_offset == _target) return; _offset = _target; }
        else _offset = Mathf.Lerp(_offset, _target, 1 - Mathf.Exp(-deltaTime / Smooth));
        Apply();
    }

    private void Apply()
    {
        Content.anchoredPosition = new Vector2(0, Mathf.Round(_offset));
        bool overflow = Overflows;
        if (_track.gameObject.activeSelf != overflow) _track.gameObject.SetActive(overflow);
        if (!overflow) return;
        float visible = _viewportHeight / _contentHeight, size = Mathf.Max(Mathf.Round(18 * _ui), _viewportHeight * visible);
        float travel = _viewportHeight - size, position = MaxOffset > 0 ? _offset / MaxOffset : 0;
        _thumb.sizeDelta = new Vector2(0, size); _thumb.anchoredPosition = new Vector2(0, -travel * position);
    }
}
