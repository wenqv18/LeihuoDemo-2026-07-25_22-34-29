using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VisionFailureFeedbackController : MonoBehaviour
{
    private enum Edge
    {
        Top,
        Bottom,
        Left,
        Right
    }

    private enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private sealed class EdgeLayer
    {
        public Edge Edge;
        public RawImage Image;
        public RectTransform Rect;
    }

    private sealed class CornerLayer
    {
        public Corner Corner;
        public RawImage Image;
        public RectTransform Rect;
    }

    private sealed class Particle
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float StartSize;
    }

    private sealed class Tendril
    {
        public RectTransform Rect;
        public Image Image;
        public Edge Edge;
        public Vector2 BasePosition;
        public float Age;
        public float Delay;
        public float GrowDuration;
        public float Lifetime;
        public float MaxLength;
        public float Width;
        public float WobbleSeed;
    }

    private const int OverlaySortOrder = short.MaxValue - 4;
    private static readonly Color ProjectTealGreen = new Color(0.12f, 1f, 0.45f, 1f);
    private static readonly Color DarkRed = new Color(0.42f, 0.02f, 0.05f, 1f);

    [SerializeField] private float baseDuration = 0.86f;
    [SerializeField] private float exhaustedDuration = 1.32f;
    [SerializeField] private float minEdgeDepth = 76f;
    [SerializeField] private float maxEdgeDepth = 172f;
    [SerializeField] private float cornerPatchSize = 230f;
    [SerializeField] private int minParticleCount = 34;
    [SerializeField] private int maxParticleCount = 96;
    [SerializeField] private int minTendrilCount = 12;
    [SerializeField] private int maxTendrilCount = 30;
    [SerializeField] private float scanlinePeakAlpha = 0.22f;

    private readonly List<EdgeLayer> edgeLayers = new List<EdgeLayer>();
    private readonly List<CornerLayer> cornerLayers = new List<CornerLayer>();
    private readonly List<Particle> particles = new List<Particle>();
    private readonly List<Tendril> tendrils = new List<Tendril>();

    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private Image scanlineLayer;
    private Sprite particleSprite;
    private Sprite tendrilSprite;
    private Texture2D horizontalErosionTexture;
    private Texture2D verticalErosionTexture;
    private Texture2D cornerErosionTexture;
    private Coroutine activeRoutine;

    public static VisionFailureFeedbackController EnsureExists()
    {
        VisionFailureFeedbackController existing =
            FindAnyObjectByType<VisionFailureFeedbackController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.BuildOverlayIfNeeded();
            return existing;
        }

        GameObject feedbackObject = new GameObject(nameof(VisionFailureFeedbackController));
        VisionFailureFeedbackController controller = feedbackObject.AddComponent<VisionFailureFeedbackController>();
        DontDestroyOnLoad(feedbackObject);
        return controller;
    }

    public void PlayFailure(float intensity, bool exhausted)
    {
        BuildOverlayIfNeeded();

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        ResetVisuals();
        activeRoutine = StartCoroutine(PlayFailureRoutine(Mathf.Clamp01(intensity), exhausted));
    }

    private void Awake()
    {
        BuildOverlayIfNeeded();
        ResetVisuals();
    }

    private void OnDestroy()
    {
        if (horizontalErosionTexture != null)
        {
            Destroy(horizontalErosionTexture);
        }

        if (verticalErosionTexture != null)
        {
            Destroy(verticalErosionTexture);
        }

        if (cornerErosionTexture != null)
        {
            Destroy(cornerErosionTexture);
        }

        if (particleSprite != null)
        {
            Destroy(particleSprite.texture);
            Destroy(particleSprite);
        }

        if (tendrilSprite != null)
        {
            Destroy(tendrilSprite.texture);
            Destroy(tendrilSprite);
        }
    }

    private IEnumerator PlayFailureRoutine(float intensity, bool exhausted)
    {
        float duration = exhausted ? exhaustedDuration : Mathf.Lerp(baseDuration, baseDuration + 0.24f, intensity);
        float edgeDepth = Mathf.Lerp(minEdgeDepth, maxEdgeDepth, intensity);
        float peakAlpha = Mathf.Lerp(0.38f, 0.78f, intensity);
        int particleCount = Mathf.RoundToInt(Mathf.Lerp(minParticleCount, maxParticleCount, intensity));
        int tendrilCount = Mathf.RoundToInt(Mathf.Lerp(minTendrilCount, maxTendrilCount, intensity));

        overlayCanvas.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        SpawnParticles(particleCount, intensity);
        SpawnTendrils(tendrilCount, intensity);

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.58f));
            float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.62f) / 0.38f));
            float pulse = attack * release;

            ApplyEdgeDepth(edgeDepth * pulse, peakAlpha * pulse * 0.72f, time, intensity);
            ApplyScanline(pulse, time, intensity);
            UpdateParticles(Time.unscaledDeltaTime, intensity);
            UpdateTendrils(Time.unscaledDeltaTime, intensity);

            yield return null;
        }

        ResetVisuals();
        activeRoutine = null;
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("VisionFailureFeedbackOverlay", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = OverlaySortOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        horizontalErosionTexture = CreateErosionTexture(256, 128, true);
        verticalErosionTexture = CreateErosionTexture(128, 256, false);
        cornerErosionTexture = CreateCornerErosionTexture(192);
        particleSprite = CreateParticleSprite();
        tendrilSprite = CreateTendrilSprite();

        CreateEdgeLayer(Edge.Top, "TopErosion", horizontalErosionTexture);
        CreateEdgeLayer(Edge.Bottom, "BottomErosion", horizontalErosionTexture);
        CreateEdgeLayer(Edge.Left, "LeftErosion", verticalErosionTexture);
        CreateEdgeLayer(Edge.Right, "RightErosion", verticalErosionTexture);
        CreateCornerLayer(Corner.TopLeft, "TopLeftErosion", cornerErosionTexture);
        CreateCornerLayer(Corner.TopRight, "TopRightErosion", cornerErosionTexture);
        CreateCornerLayer(Corner.BottomLeft, "BottomLeftErosion", cornerErosionTexture);
        CreateCornerLayer(Corner.BottomRight, "BottomRightErosion", cornerErosionTexture);
        scanlineLayer = CreateScanlineLayer(canvasObject.transform);

        overlayCanvas.gameObject.SetActive(false);
    }

    private void CreateEdgeLayer(Edge edge, string objectName, Texture texture)
    {
        GameObject layerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        layerObject.transform.SetParent(overlayCanvas.transform, false);

        RawImage image = layerObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.clear;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        switch (edge)
        {
            case Edge.Top:
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                image.uvRect = new Rect(0f, 1f, 1f, -1f);
                break;
            case Edge.Bottom:
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                break;
            case Edge.Left:
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                break;
            case Edge.Right:
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                image.uvRect = new Rect(1f, 0f, -1f, 1f);
                break;
        }

        edgeLayers.Add(new EdgeLayer
        {
            Edge = edge,
            Image = image,
            Rect = rect
        });
    }

    private void CreateCornerLayer(Corner corner, string objectName, Texture texture)
    {
        GameObject layerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        layerObject.transform.SetParent(overlayCanvas.transform, false);

        RawImage image = layerObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.clear;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        switch (corner)
        {
            case Corner.TopLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                image.uvRect = new Rect(0f, 1f, 1f, -1f);
                break;
            case Corner.TopRight:
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                image.uvRect = new Rect(1f, 1f, -1f, -1f);
                break;
            case Corner.BottomLeft:
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                break;
            case Corner.BottomRight:
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                image.uvRect = new Rect(1f, 0f, -1f, 1f);
                break;
        }

        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        cornerLayers.Add(new CornerLayer
        {
            Corner = corner,
            Image = image,
            Rect = rect
        });
    }

    private static Image CreateScanlineLayer(Transform parent)
    {
        GameObject layerObject = new GameObject("FailureScanline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        layerObject.transform.SetParent(parent, false);

        Image image = layerObject.GetComponent<Image>();
        image.sprite = CreateSolidSprite();
        image.color = Color.clear;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 4f);
        return image;
    }

    private void ApplyEdgeDepth(float depth, float alpha, float time, float intensity)
    {
        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        Rect canvasBounds = canvasRect != null
            ? canvasRect.rect
            : new Rect(-Screen.width * 0.5f, -Screen.height * 0.5f, Screen.width, Screen.height);
        float cornerInset = Mathf.Min(cornerPatchSize * 0.72f, Mathf.Min(canvasBounds.width, canvasBounds.height) * 0.22f);

        for (int i = 0; i < edgeLayers.Count; i++)
        {
            EdgeLayer layer = edgeLayers[i];
            float jitter = Mathf.Sin((time * 36f) + i * 1.7f) * Mathf.Lerp(2f, 8f, intensity);
            float layerDepth = Mathf.Max(0f, depth + jitter);
            layer.Image.color = new Color(1f, 1f, 1f, alpha);

            switch (layer.Edge)
            {
                case Edge.Top:
                case Edge.Bottom:
                    layer.Rect.sizeDelta = new Vector2(Mathf.Max(0f, canvasBounds.width - cornerInset * 2f), layerDepth);
                    break;
                case Edge.Left:
                case Edge.Right:
                    layer.Rect.sizeDelta = new Vector2(layerDepth, Mathf.Max(0f, canvasBounds.height - cornerInset * 2f));
                    break;
            }
        }

        ApplyCornerDepth(depth, alpha, time, intensity);
    }

    private void ApplyCornerDepth(float depth, float alpha, float time, float intensity)
    {
        float size = Mathf.Max(0f, Mathf.Lerp(cornerPatchSize * 0.58f, cornerPatchSize, Mathf.Clamp01(depth / Mathf.Max(1f, maxEdgeDepth))));
        for (int i = 0; i < cornerLayers.Count; i++)
        {
            CornerLayer layer = cornerLayers[i];
            float jitter = Mathf.Sin(time * 24f + i * 2.3f) * Mathf.Lerp(0.04f, 0.14f, intensity);
            float cornerAlpha = Mathf.Clamp01(alpha * Mathf.Lerp(0.72f, 0.95f, intensity) * (1f + jitter));
            layer.Rect.sizeDelta = Vector2.one * size;
            layer.Image.color = new Color(1f, 1f, 1f, cornerAlpha);
        }
    }

    private void ApplyScanline(float pulse, float time, float intensity)
    {
        if (scanlineLayer == null)
        {
            return;
        }

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        float height = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float y = Mathf.Lerp(0f, -height, Mathf.Repeat(time * Mathf.Lerp(1.6f, 2.6f, intensity), 1f));
        scanlineLayer.rectTransform.anchoredPosition = new Vector2(0f, y);
        scanlineLayer.color = new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, scanlinePeakAlpha * pulse);
    }

    private void SpawnParticles(int count, float intensity)
    {
        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Rect rect = canvasRect.rect;
        for (int i = 0; i < count; i++)
        {
            Particle particle = GetParticle(i);
            Edge edge = (Edge)Random.Range(0, 4);
            Vector2 position = GetEdgePosition(rect, edge);
            Vector2 inward = GetInwardDirection(edge);
            Vector2 tangent = new Vector2(-inward.y, inward.x);
            float speed = Random.Range(55f, 170f) * Mathf.Lerp(0.75f, 1.25f, intensity);
            float drift = Random.Range(-58f, 58f);

            particle.Rect.anchoredPosition = position;
            particle.Velocity = inward * speed + tangent * drift;
            particle.Age = 0f;
            particle.Lifetime = Random.Range(0.34f, 0.82f) * Mathf.Lerp(0.9f, 1.2f, intensity);
            particle.StartSize = Random.Range(4f, 14f) * Mathf.Lerp(0.85f, 1.35f, intensity);
            particle.Rect.sizeDelta = Vector2.one * particle.StartSize;
            particle.Image.color = PickParticleColor();
            particle.Image.enabled = true;
        }

        for (int i = count; i < particles.Count; i++)
        {
            particles[i].Image.enabled = false;
        }
    }

    private void SpawnTendrils(int count, float intensity)
    {
        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Rect rect = canvasRect.rect;
        for (int i = 0; i < count; i++)
        {
            Tendril tendril = GetTendril(i);
            Edge edge = (Edge)Random.Range(0, 4);
            tendril.Edge = edge;
            tendril.BasePosition = GetEdgePosition(rect, edge);
            tendril.Age = 0f;
            tendril.Delay = Random.Range(0f, Mathf.Lerp(0.2f, 0.34f, intensity));
            tendril.GrowDuration = Random.Range(0.36f, 0.72f) * Mathf.Lerp(1.15f, 0.95f, intensity);
            tendril.Lifetime = tendril.Delay + tendril.GrowDuration + Random.Range(0.22f, 0.44f);
            tendril.MaxLength = Random.Range(62f, 156f) * Mathf.Lerp(0.82f, 1.28f, intensity);
            tendril.Width = Random.Range(8f, 22f) * Mathf.Lerp(0.9f, 1.22f, intensity);
            tendril.WobbleSeed = Random.Range(0f, 1000f);
            ConfigureTendrilTransform(tendril, 0f);
            tendril.Image.color = PickTendrilColor();
            tendril.Image.enabled = false;
        }

        for (int i = count; i < tendrils.Count; i++)
        {
            tendrils[i].Image.enabled = false;
        }
    }

    private void UpdateTendrils(float deltaTime, float intensity)
    {
        for (int i = 0; i < tendrils.Count; i++)
        {
            Tendril tendril = tendrils[i];
            if (tendril.Age >= tendril.Lifetime)
            {
                tendril.Image.enabled = false;
                continue;
            }

            tendril.Age += deltaTime;
            float localAge = tendril.Age - tendril.Delay;
            if (localAge <= 0f)
            {
                tendril.Image.enabled = false;
                continue;
            }

            tendril.Image.enabled = true;
            float growT = Mathf.Clamp01(localAge / Mathf.Max(0.01f, tendril.GrowDuration));
            float fadeT = Mathf.Clamp01((tendril.Age - tendril.Lifetime + 0.24f) / 0.24f);
            float grow = Mathf.SmoothStep(0f, 1f, growT);
            float length = Mathf.Max(1f, tendril.MaxLength * grow);
            ConfigureTendrilTransform(tendril, length);

            Vector2 inward = GetInwardDirection(tendril.Edge);
            Vector2 tangent = new Vector2(-inward.y, inward.x);
            float wobble = Mathf.Sin((Time.unscaledTime + tendril.WobbleSeed) * 5.2f) * Mathf.Lerp(3f, 12f, intensity);
            tendril.Rect.anchoredPosition = tendril.BasePosition + tangent * wobble;

            Color color = tendril.Image.color;
            float peakAlpha = tendril.Edge == Edge.Top || tendril.Edge == Edge.Bottom ? 0.58f : 0.5f;
            color.a = Mathf.Lerp(0f, peakAlpha, grow) * (1f - Mathf.SmoothStep(0f, 1f, fadeT));
            tendril.Image.color = color;
        }
    }

    private Tendril GetTendril(int index)
    {
        while (tendrils.Count <= index)
        {
            GameObject tendrilObject = new GameObject("ErosionTendril", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tendrilObject.transform.SetParent(overlayCanvas.transform, false);
            Image image = tendrilObject.GetComponent<Image>();
            image.sprite = tendrilSprite;
            image.raycastTarget = false;
            image.enabled = false;

            tendrils.Add(new Tendril
            {
                Rect = image.rectTransform,
                Image = image
            });
        }

        return tendrils[index];
    }

    private static void ConfigureTendrilTransform(Tendril tendril, float length)
    {
        if (tendril == null || tendril.Rect == null)
        {
            return;
        }

        tendril.Rect.anchoredPosition = tendril.BasePosition;
        tendril.Rect.localRotation = Quaternion.identity;
        switch (tendril.Edge)
        {
            case Edge.Top:
                tendril.Rect.pivot = new Vector2(0.5f, 1f);
                tendril.Rect.sizeDelta = new Vector2(tendril.Width, length);
                break;
            case Edge.Bottom:
                tendril.Rect.pivot = new Vector2(0.5f, 0f);
                tendril.Rect.sizeDelta = new Vector2(tendril.Width, length);
                break;
            case Edge.Left:
                tendril.Rect.pivot = new Vector2(0f, 0.5f);
                tendril.Rect.sizeDelta = new Vector2(length, tendril.Width);
                break;
            case Edge.Right:
                tendril.Rect.pivot = new Vector2(1f, 0.5f);
                tendril.Rect.sizeDelta = new Vector2(length, tendril.Width);
                break;
        }
    }

    private void UpdateParticles(float deltaTime, float intensity)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            Particle particle = particles[i];
            if (!particle.Image.enabled)
            {
                continue;
            }

            particle.Age += deltaTime;
            float t = Mathf.Clamp01(particle.Age / Mathf.Max(0.01f, particle.Lifetime));
            if (t >= 1f)
            {
                particle.Image.enabled = false;
                continue;
            }

            Vector2 floatDrift = new Vector2(
                Mathf.Sin((Time.unscaledTime + i) * 6.1f),
                Mathf.Cos((Time.unscaledTime + i * 0.37f) * 4.8f)) * Mathf.Lerp(5f, 18f, intensity);
            particle.Rect.anchoredPosition += (particle.Velocity + floatDrift) * deltaTime;
            particle.Velocity *= Mathf.Pow(0.08f, deltaTime);

            Color color = particle.Image.color;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, t) * 0.18f;
            color.a = Mathf.Lerp(color.a, 0f, Mathf.SmoothStep(0.42f, 1f, t));
            particle.Image.color = color;
            particle.Rect.sizeDelta = Vector2.one * Mathf.Lerp(particle.StartSize, particle.StartSize * 0.35f, t);
        }
    }

    private Particle GetParticle(int index)
    {
        while (particles.Count <= index)
        {
            GameObject particleObject = new GameObject("ErosionParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            particleObject.transform.SetParent(overlayCanvas.transform, false);
            Image image = particleObject.GetComponent<Image>();
            image.sprite = particleSprite;
            image.raycastTarget = false;
            image.enabled = false;

            particles.Add(new Particle
            {
                Rect = image.rectTransform,
                Image = image
            });
        }

        return particles[index];
    }

    private static Vector2 GetEdgePosition(Rect rect, Edge edge)
    {
        switch (edge)
        {
            case Edge.Top:
                return new Vector2(Random.Range(rect.xMin, rect.xMax), rect.yMax + Random.Range(-12f, 8f));
            case Edge.Bottom:
                return new Vector2(Random.Range(rect.xMin, rect.xMax), rect.yMin + Random.Range(-8f, 12f));
            case Edge.Left:
                return new Vector2(rect.xMin + Random.Range(-8f, 12f), Random.Range(rect.yMin, rect.yMax));
            default:
                return new Vector2(rect.xMax + Random.Range(-12f, 8f), Random.Range(rect.yMin, rect.yMax));
        }
    }

    private static Vector2 GetInwardDirection(Edge edge)
    {
        switch (edge)
        {
            case Edge.Top:
                return Vector2.down;
            case Edge.Bottom:
                return Vector2.up;
            case Edge.Left:
                return Vector2.right;
            default:
                return Vector2.left;
        }
    }

    private static Color PickParticleColor()
    {
        float roll = Random.value;
        if (roll < 0.72f)
        {
            float value = Random.Range(0.01f, 0.08f);
            return new Color(value, value, value, Random.Range(0.35f, 0.75f));
        }

        if (roll < 0.88f)
        {
            return new Color(DarkRed.r, DarkRed.g, DarkRed.b, Random.Range(0.35f, 0.68f));
        }

        return new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, Random.Range(0.35f, 0.72f));
    }

    private static Color PickTendrilColor()
    {
        float roll = Random.value;
        if (roll < 0.84f)
        {
            float value = Random.Range(0.005f, 0.045f);
            return new Color(value, value, value, 0f);
        }

        if (roll < 0.94f)
        {
            return new Color(DarkRed.r, DarkRed.g, DarkRed.b, 0f);
        }

        return new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, 0f);
    }

    private void ResetVisuals()
    {
        if (edgeLayers.Count > 0)
        {
            ApplyEdgeDepth(0f, 0f, 0f, 0f);
        }

        if (scanlineLayer != null)
        {
            scanlineLayer.color = Color.clear;
        }

        for (int i = 0; i < particles.Count; i++)
        {
            particles[i].Image.enabled = false;
        }

        for (int i = 0; i < tendrils.Count; i++)
        {
            tendrils[i].Image.enabled = false;
        }

        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(false);
        }
    }

    private static Texture2D CreateErosionTexture(int width, int height, bool horizontal)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        float seedA = Random.Range(0f, 1000f);
        float seedB = Random.Range(0f, 1000f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float tangent = horizontal
                    ? x / Mathf.Max(1f, width - 1f)
                    : y / Mathf.Max(1f, height - 1f);
                float inward = horizontal
                    ? 1f - y / Mathf.Max(1f, height - 1f)
                    : 1f - x / Mathf.Max(1f, width - 1f);
                float distanceFromOuter = 1f - inward;
                float reachNoise = Mathf.PerlinNoise(tangent * 3.2f + seedA, seedB);
                float crackNoise = Mathf.PerlinNoise(tangent * 17.5f + seedB, seedA);
                float reach = Mathf.Lerp(0.28f, 1f, Mathf.SmoothStep(0.18f, 0.92f, reachNoise));
                if (crackNoise > 0.76f)
                {
                    reach = Mathf.Min(1f, reach + Mathf.InverseLerp(0.76f, 1f, crackNoise) * 0.42f);
                }

                float rough = Mathf.PerlinNoise(x * 0.045f + seedA, y * 0.08f + seedB);
                float grain = Random.value > 0.82f ? Random.Range(0.18f, 0.52f) : 0f;
                float edge = 1f - Mathf.SmoothStep(reach * 0.62f, reach, distanceFromOuter);
                float fissure = Mathf.PerlinNoise(tangent * 31f + seedA * 0.37f, distanceFromOuter * 9f + seedB);
                float fissureBoost = fissure > 0.68f ? Mathf.InverseLerp(0.68f, 1f, fissure) * 0.28f : 0f;
                float alpha = Mathf.Clamp01((edge + fissureBoost) * Mathf.Pow(inward, 0.72f) * (0.45f + rough * 0.75f) + grain * inward);

                Color color = Color.black;
                float accentRoll = Random.value;
                if (accentRoll > 0.995f)
                {
                    color = ProjectTealGreen;
                    alpha *= 0.9f;
                }
                else if (accentRoll > 0.988f)
                {
                    color = DarkRed;
                    alpha *= 0.75f;
                }

                color.a = alpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateCornerErosionTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float seedA = Random.Range(0f, 1000f);
        float seedB = Random.Range(0f, 1000f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / Mathf.Max(1f, size - 1f);
                float ny = y / Mathf.Max(1f, size - 1f);
                float radial = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx) / Mathf.PI;
                float reachNoise = Mathf.PerlinNoise(angle * 8f + seedA, seedB);
                float reach = Mathf.Lerp(0.36f, 1.12f, Mathf.SmoothStep(0.12f, 0.88f, reachNoise));
                float edge = 1f - Mathf.SmoothStep(reach * 0.58f, reach, radial);
                float crack = Mathf.PerlinNoise(nx * 12f + seedB, ny * 12f + seedA);
                float grain = Random.value > 0.84f ? Random.Range(0.12f, 0.46f) : 0f;
                float alpha = Mathf.Clamp01((edge * (0.55f + crack * 0.65f) + grain) * (1f - Mathf.SmoothStep(0.86f, 1.2f, radial)));

                Color color = Color.black;
                float accentRoll = Random.value;
                if (accentRoll > 0.995f)
                {
                    color = ProjectTealGreen;
                    alpha *= 0.85f;
                }
                else if (accentRoll > 0.988f)
                {
                    color = DarkRed;
                    alpha *= 0.72f;
                }

                color.a = alpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Sprite CreateParticleSprite()
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(15.5f, 15.5f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 15.5f;
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.15f, 1f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateTendrilSprite()
    {
        Texture2D texture = new Texture2D(32, 96, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float seedA = Random.Range(0f, 1000f);
        float seedB = Random.Range(0f, 1000f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = Mathf.Abs((x / Mathf.Max(1f, texture.width - 1f)) - 0.5f) * 2f;
                float ny = y / Mathf.Max(1f, texture.height - 1f);
                float centerWander = Mathf.PerlinNoise(ny * 3.5f + seedA, seedB) * 0.46f + 0.27f;
                float sideNoise = Mathf.PerlinNoise(x * 0.22f + seedB, y * 0.055f + seedA);
                float vein = 1f - Mathf.SmoothStep(centerWander * 0.42f, centerWander, nx);
                float broken = sideNoise > 0.34f ? 1f : 0.35f;
                float tipFade = Mathf.SmoothStep(0f, 0.16f, ny) * (1f - Mathf.SmoothStep(0.86f, 1f, ny));
                float alpha = Mathf.Clamp01(vein * broken * tipFade);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
