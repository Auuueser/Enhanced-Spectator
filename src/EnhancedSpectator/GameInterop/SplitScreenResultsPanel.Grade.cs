using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The grade's seal: a ring that draws itself round from the top, and the letter stamped into it, the box flashing and
/// jolting and a shockwave running out as it lands.
/// Drawn from sharp, mipmapped textures so the ring stays crisp at any size.
/// </summary>
internal sealed partial class SplitScreenResultsPanel
{
    private const float SealDrawSeconds = .4f, SealLead = .45f, SealSize = 176;
    private Texture2D _sealRingTexture = null!, _sealWaveTexture = null!;
    private Image _sealTrack = null!, _sealRing = null!, _sealWave = null!, _sealFlash = null!;

    private void CreateSeal()
    {
        // Thick ring, a thin ring for the shockwave.
        _sealRingTexture = SealTexture("Ring", (d, a) => Band(d, .78f, .9f));
        _sealWaveTexture = SealTexture("Wave", (d, a) => Band(d, .93f, .97f));
        // The flash lights the grade box itself: square to it and filling it, not tilted like the seal and the letter.
        _sealFlash = SealImage("Grade flash", null, SealSize);
        SplitScreenView.Stretch(_sealFlash.rectTransform); _sealFlash.rectTransform.localRotation = Quaternion.identity;
        _sealFlash.rectTransform.SetAsFirstSibling(); _sealFlash.color = new Color(1, 1, 1, 0);
        _sealWave = SealImage("Grade wave", _sealWaveTexture, SealSize);
        _sealTrack = SealImage("Grade track", _sealRingTexture, SealSize);
        _sealRing = SealImage("Grade ring", _sealRingTexture, SealSize);
        _sealRing.type = Image.Type.Filled; _sealRing.fillMethod = Image.FillMethod.Radial360;
        _sealRing.fillOrigin = (int)Image.Origin360.Top; _sealRing.fillClockwise = true;
    }

    private void OpenSeal(Color color)
    {
        _sealTrack.color = Alpha(color, 0); _sealRing.color = color; _sealWave.color = Alpha(color, 0);
        _sealRing.fillAmount = 0; _sealFlash.color = new Color(1, 1, 1, 0);
    }

    private void AnimateSeal(float t)
    {
        float draw = (t - (GradeAt - SealLead)) / SealDrawSeconds, ease = Mathf.SmoothStep(0, 1, Mathf.Clamp01(draw));
        var color = _sealRing.color;
        _sealTrack.color = Alpha(color, .16f * ease);
        _sealRing.fillAmount = ease;
        // The letter lands: the box flashes and jolts, the ring jumps, a shockwave runs out.
        float landed = t - GradeAt - StampSeconds;
        float jolt = landed >= 0 ? Wobble(landed, 3) : 0;
        _gradeBox.localRotation = Quaternion.Euler(0, 0, jolt);
        _sealFlash.color = new Color(1, 1, 1, landed >= 0 ? .3f * Mathf.Clamp01(1 - landed / .35f) : 0);
        float jump = landed >= 0 ? Mathf.Clamp01(1 - landed / .3f) : 0;
        _sealRing.rectTransform.localScale = _sealTrack.rectTransform.localScale = Vector3.one * (1 + .07f * jump * jump);
        float wave = Mathf.Clamp01(landed / RippleSeconds);
        _sealWave.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 1.75f, EaseOut(wave));
        _sealWave.color = Alpha(color, landed > 0 && wave < 1 ? .7f * (1 - wave) : 0);
    }

    private Image SealImage(string name, Texture2D? texture, float size)
    {
        var image = SplitScreenView.CreateImage(name, _gradeBox, Color.white);
        if (texture != null) image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        // Centred in the box: as far from its top edge as from its bottom one.
        rect.sizeDelta = new Vector2(size, size); rect.anchoredPosition = Vector2.zero; rect.localRotation = Quaternion.Euler(0, 0, -8);
        return image;
    }

    // A 512 px white mask from a shape of (distance from the centre 0–1, angle 0–1 from the top, clockwise), its
    // edges smoothed over about a pixel and mipmapped so it shrinks cleanly.
    private static Texture2D SealTexture(string name, System.Func<float, float, float> shape)
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "EnhancedSpectator Grade " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / (size / 2f) - 1, dy = (y + .5f) / (size / 2f) - 1;
                float angle = Mathf.Repeat(Mathf.Atan2(dx, dy) / (2 * Mathf.PI), 1);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(shape(Mathf.Sqrt(dx * dx + dy * dy), angle))));
            }
        texture.SetPixels32(pixels); texture.Apply(true, true);
        return texture;
    }
    // 1 inside [from, to], 0 outside, over a soft edge (default about a pixel at 512 px).
    private static float Band(float value, float from, float to, float soft = 1.2f / 256)
        => Mathf.Clamp01((value - from) / soft + .5f) * Mathf.Clamp01((to - value) / soft + .5f);

    private void DisposeSeal()
    {
        foreach (var image in new[] { _sealTrack, _sealRing, _sealWave }) if (image.sprite != null) UnityEngine.Object.Destroy(image.sprite);
        UnityEngine.Object.Destroy(_sealRingTexture); UnityEngine.Object.Destroy(_sealWaveTexture);
    }
}
