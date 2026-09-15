using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SignalInterferenceOverlay : MonoBehaviour
{
    [SerializeField] private RawImage noiseImage;
    [SerializeField] private RawImage scanlineImage;
    [SerializeField] private RawImage waveImage;
    [SerializeField] private float noiseAlpha = 0.04f;
    [SerializeField] private float scanlineAlpha = 0.05f;
    [SerializeField] private float waveAlpha = 0.03f;
    [SerializeField] private float jitterPixels = 1f;
    [SerializeField] private float flickerAmount = 0.15f;

    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;
    private float noiseSeed;
    private float flickerSeed;

    private void Awake()
    {
        ResolveReferences();
        EnsureVisibleTransform();
        CacheBasePosition();
        noiseSeed = Random.value * 100f;
        flickerSeed = Random.value * 100f;
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureVisibleTransform();
        CacheBasePosition();
        ApplyAlphas();
    }

    private void Update()
    {
        float time = Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        AnimateNoise(time);
        AnimateWave(time);
        ApplyAlphas(time);
    }

    public void Configure(float newNoiseAlpha, float newScanlineAlpha, float newWaveAlpha, float newJitterPixels, float newFlickerAmount)
    {
        noiseAlpha = Mathf.Max(0f, newNoiseAlpha);
        scanlineAlpha = Mathf.Max(0f, newScanlineAlpha);
        waveAlpha = Mathf.Max(0f, newWaveAlpha);
        jitterPixels = Mathf.Max(0f, newJitterPixels);
        flickerAmount = Mathf.Clamp01(newFlickerAmount);
        ApplyAlphas();
    }

    public void ConfigureEndingSignal(EndingKind ending)
    {
        switch (ending)
        {
            case EndingKind.Death:
                Configure(noiseAlpha * 1.18f, scanlineAlpha * 0.95f, waveAlpha * 1.15f, Mathf.Max(jitterPixels, 0.75f), Mathf.Max(flickerAmount, 0.18f));
                break;
            case EndingKind.Fuse:
                Configure(noiseAlpha * 1.28f, scanlineAlpha * 1.05f, waveAlpha * 1.24f, Mathf.Max(jitterPixels, 0.95f), Mathf.Max(flickerAmount, 0.22f));
                break;
            case EndingKind.Clear:
                Configure(noiseAlpha * 0.72f, scanlineAlpha * 0.82f, waveAlpha * 0.72f, jitterPixels * 0.5f, flickerAmount * 0.7f);
                break;
        }
    }

    private void ResolveReferences()
    {
        rectTransform = transform as RectTransform;
        if (noiseImage == null)
        {
            noiseImage = FindRawImage("Noise");
        }

        if (scanlineImage == null)
        {
            scanlineImage = FindRawImage("Scanlines");
        }

        if (waveImage == null)
        {
            waveImage = FindRawImage("WaveBands");
        }

        DisableRaycast(noiseImage);
        DisableRaycast(scanlineImage);
        DisableRaycast(waveImage);
    }

    private void CacheBasePosition()
    {
        if (rectTransform != null)
        {
            baseAnchoredPosition = rectTransform.anchoredPosition;
        }
    }

    private void EnsureVisibleTransform()
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector3 scale = rectTransform.localScale;
        if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f)
        {
            rectTransform.localScale = Vector3.one;
        }
    }

    private RawImage FindRawImage(string objectName)
    {
        Transform child = transform.Find(objectName);
        return child != null ? child.GetComponent<RawImage>() : null;
    }

    private static void DisableRaycast(RawImage image)
    {
        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    private void AnimateNoise(float time)
    {
        if (noiseImage != null)
        {
            float x = Mathf.Repeat(time * 9.7f + noiseSeed, 1f);
            float y = Mathf.Repeat(time * 13.1f + noiseSeed * 0.37f, 1f);
            noiseImage.uvRect = new Rect(x, y, 4.5f, 3.2f);
        }

        if (scanlineImage != null)
        {
            float y = Mathf.Repeat(time * 0.42f, 1f);
            scanlineImage.uvRect = new Rect(0f, y, 1f, 42f);
        }
    }

    private void AnimateWave(float time)
    {
        if (waveImage != null)
        {
            float x = Mathf.Sin(time * 0.77f) * 0.03f;
            float y = Mathf.Repeat(time * 0.11f, 1f);
            waveImage.uvRect = new Rect(x, y, 1.4f, 1.15f);
        }

        if (rectTransform == null)
        {
            return;
        }

        float jitter = Mathf.Sin(time * 18.3f) + Mathf.Sin(time * 41.7f + 0.8f);
        rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(jitter * jitterPixels * 0.5f, 0f);
    }

    private void ApplyAlphas(float time = 0f)
    {
        float flicker = 1f;
        if (flickerAmount > 0f)
        {
            flicker += Mathf.PerlinNoise(flickerSeed, time * 12f) * flickerAmount - flickerAmount * 0.5f;
        }

        SetAlpha(noiseImage, noiseAlpha * flicker);
        SetAlpha(scanlineImage, scanlineAlpha);
        SetAlpha(waveImage, waveAlpha * flicker);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = Mathf.Clamp01(alpha);
        graphic.color = color;
        graphic.enabled = color.a > 0f;
    }
}
