using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 两世界穿梭过渡特效（全屏 Overlay，自建 Canvas）：
/// - 进入 SpecialWorld：侵蚀从屏幕边缘压入，世界在冲击点切换；
/// - 返回 NormalWorld：侵蚀残留从边界松动并褪去。
/// 烟雾只贴近侵蚀边界，中央玩家视野不生成烟雾。
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldTransitionFX : MonoBehaviour
{
    private enum Edge
    {
        Top,
        Bottom,
        Left,
        Right
    }

    private sealed class EdgeLayer
    {
        public Edge Edge;
        public ErosionEdgeGraphic Graphic;
        public RectTransform Rect;
    }

    private sealed class SpeckleLayer
    {
        public Edge Edge;
        public ErosionSpeckleGraphic Graphic;
        public RectTransform Rect;
    }

    private sealed class SmokePuff
    {
        public Edge Edge;
        public Image Image;
        public RectTransform Rect;
        public float Lane;
        public float OffsetSeed;
        public float Drift;
        public float Size;
        public float Stretch;
        public float Delay;
        public float Phase;
        public float Alpha;
    }

    private sealed class ErosionParticle
    {
        public Edge Edge;
        public Image Image;
        public RectTransform Rect;
        public float Lane;
        public float OffsetSeed;
        public float Drift;
        public float Size;
        public float Delay;
        public float Phase;
        public Color Color;
    }

    private sealed class Tendril
    {
        public Edge Edge;
        public Image Image;
        public RectTransform Rect;
        public float Lane;
        public float Length;
        public float Width;
        public float Delay;
        public float Wobble;
        public float Phase;
    }

    private sealed class ErosionEdgeGraphic : MaskableGraphic
    {
        private const int SegmentCount = 156;
        private const int BandCount = 6;
        private const int LobeCount = 12;

        private Edge edge;
        private float edgeDepth;
        private float edgeAlpha;
        private float time;
        private float intensity;

        public void Configure(Edge targetEdge, float targetDepth, float targetAlpha, float targetTime, float targetIntensity)
        {
            edge = targetEdge;
            edgeDepth = Mathf.Max(0f, targetDepth);
            edgeAlpha = Mathf.Clamp01(targetAlpha);
            time = targetTime;
            intensity = Mathf.Clamp01(targetIntensity);
            SetVerticesDirty();
        }

        public void Clear()
        {
            edgeAlpha = 0f;
            edgeDepth = 0f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (edgeDepth <= 0.01f || edgeAlpha <= 0.01f)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            for (int i = 0; i <= SegmentCount; i++)
            {
                float u = i / (float)SegmentCount;
                float boundary = SmoothBoundaryDepth(u);
                for (int band = 0; band <= BandCount; band++)
                {
                    float v = band / (float)BandCount;
                    vh.AddVert(PointAtDistance(rect, u, boundary * v), BandColor(u, v), new Vector2(u, v));
                }
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                int row = i * (BandCount + 1);
                int nextRow = (i + 1) * (BandCount + 1);
                for (int band = 0; band < BandCount; band++)
                {
                    vh.AddTriangle(row + band, row + band + 1, nextRow + band);
                    vh.AddTriangle(nextRow + band, row + band + 1, nextRow + band + 1);
                }
            }
        }

        private float SmoothBoundaryDepth(float u)
        {
            float edgeSeed = (int)edge * 19.37f;
            float broadWave =
                Mathf.Sin((u * 2.05f + edgeSeed) * Mathf.PI * 2f + time * 0.28f) * 0.08f +
                Mathf.Sin((u * 3.7f + edgeSeed * 0.37f) * Mathf.PI * 2f - time * 0.19f) * 0.045f;
            float value = edgeDepth * (0.44f + broadWave);

            for (int i = 0; i < LobeCount; i++)
            {
                float seed = edgeSeed + i * 7.91f;
                float center = Random01(Mathf.FloorToInt(seed * 100f + 11f));
                float width = Mathf.Lerp(0.038f, 0.11f, Random01(Mathf.FloorToInt(seed * 100f + 23f)));
                float length = Mathf.Lerp(0.1f, 0.42f, Random01(Mathf.FloorToInt(seed * 100f + 37f))) * edgeDepth;
                float drift = Mathf.Sin(time * Mathf.Lerp(0.18f, 0.42f, Random01(Mathf.FloorToInt(seed * 100f + 41f))) + seed) * 0.035f;
                float distance = Mathf.Abs(Mathf.DeltaAngle((u - center - drift) * 360f, 0f)) / 360f;
                float lobe = Mathf.Exp(-(distance * distance) / (2f * width * width));
                float breathe = 0.86f + Mathf.Sin(time * 0.72f + seed) * 0.14f;
                value += lobe * length * breathe;
            }

            for (int i = 0; i < 8; i++)
            {
                float seed = edgeSeed + i * 13.43f + 100f;
                float center = Random01(Mathf.FloorToInt(seed * 100f + 5f));
                float width = Mathf.Lerp(0.028f, 0.075f, Random01(Mathf.FloorToInt(seed * 100f + 9f)));
                float depthPull = Mathf.Lerp(0.05f, 0.2f, Random01(Mathf.FloorToInt(seed * 100f + 17f))) * edgeDepth;
                float distance = Mathf.Abs(Mathf.DeltaAngle((u - center) * 360f, 0f)) / 360f;
                float recess = Mathf.Exp(-(distance * distance) / (2f * width * width));
                value -= recess * depthPull;
            }

            float crawl = SmoothValueNoise(u * 11.5f + edgeSeed, time * 0.14f + edgeSeed) * edgeDepth * 0.08f;
            float fine = SmoothValueNoise(u * 42.5f + edgeSeed * 2.7f, time * 0.22f + edgeSeed) * edgeDepth * 0.035f;
            value += (crawl + fine) * intensity;
            return Mathf.Clamp(value, edgeDepth * 0.18f, edgeDepth * 1.23f);
        }

        private Vector2 PointAtDistance(Rect rect, float u, float distance)
        {
            switch (edge)
            {
                case Edge.Top:
                    return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), rect.yMax - distance);
                case Edge.Bottom:
                    return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), rect.yMin + distance);
                case Edge.Left:
                    return new Vector2(rect.xMin + distance, Mathf.Lerp(rect.yMin, rect.yMax, u));
                default:
                    return new Vector2(rect.xMax - distance, Mathf.Lerp(rect.yMin, rect.yMax, u));
            }
        }

        private Color32 BandColor(float u, float v)
        {
            float fade = Mathf.Lerp(0.72f, 0.06f, Mathf.SmoothStep(0f, 1f, v));
            float fiber = SmoothValueNoise(u * 58f + (int)edge * 6.3f, v * 12f + time * 0.35f);
            float alpha = edgeAlpha * Mathf.Clamp01(fade + fiber * 0.08f) * 0.62f;
            Color color = new Color(0f, 0f, 0f, alpha);

            float speck = SmoothValueNoise(u * 170f + (int)edge * 23.5f, v * 31f + time * 0.55f) + 0.5f;
            if (v > 0.28f && speck > 0.965f)
            {
                color = new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, alpha * 0.46f);
            }
            else if (v > 0.22f && speck > 0.935f)
            {
                color = new Color(DarkRed.r, DarkRed.g, DarkRed.b, alpha * 0.35f);
            }

            return color;
        }

        private static float SmoothValueNoise(float x, float y)
        {
            float xi = Mathf.Floor(x);
            float yi = Mathf.Floor(y);
            float tx = Mathf.SmoothStep(0f, 1f, x - xi);
            float ty = Mathf.SmoothStep(0f, 1f, y - yi);
            float a = Random01(Mathf.FloorToInt(xi * 157f + yi * 313f));
            float b = Random01(Mathf.FloorToInt((xi + 1f) * 157f + yi * 313f));
            float c = Random01(Mathf.FloorToInt(xi * 157f + (yi + 1f) * 313f));
            float d = Random01(Mathf.FloorToInt((xi + 1f) * 157f + (yi + 1f) * 313f));
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty) - 0.5f;
        }
    }

    private sealed class ErosionSpeckleGraphic : MaskableGraphic
    {
        private const int SpeckleCount = 210;

        private Edge edge;
        private float boundaryDepth;
        private float visibility;
        private float time;
        private float intensity;

        public void Configure(Edge targetEdge, float targetBoundaryDepth, float targetVisibility, float targetTime, float targetIntensity)
        {
            edge = targetEdge;
            boundaryDepth = Mathf.Max(0f, targetBoundaryDepth);
            visibility = Mathf.Clamp01(targetVisibility);
            time = targetTime;
            intensity = Mathf.Clamp01(targetIntensity);
            SetVerticesDirty();
        }

        public void Clear()
        {
            visibility = 0f;
            boundaryDepth = 0f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (boundaryDepth <= 0.01f || visibility <= 0.01f)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            int vertexIndex = 0;
            int edgeSeed = (int)edge * 1000;
            for (int i = 0; i < SpeckleCount; i++)
            {
                float lane = Random01(edgeSeed + i * 17 + 3);
                float radialSeed = Random01(edgeSeed + i * 17 + 7);
                float edgeBand = Mathf.Pow(radialSeed, 1.85f);
                float boundaryWave = BoundaryWave(lane, edgeSeed);
                float distance = Mathf.Lerp(4f, boundaryDepth * boundaryWave, edgeBand);
                float drift = Mathf.Sin(time * Mathf.Lerp(0.4f, 1.25f, Random01(edgeSeed + i * 17 + 11)) + Random01(edgeSeed + i * 17 + 13) * 20f);
                float along = drift * Mathf.Lerp(3f, 18f, Random01(edgeSeed + i * 17 + 19)) * intensity;
                Vector2 center = PointAtDistance(rect, lane, distance, along);

                float pulse = 0.72f + Mathf.Sin(time * Mathf.Lerp(1.8f, 4.6f, Random01(edgeSeed + i * 17 + 23)) + i * 0.73f) * 0.28f;
                float size = Mathf.Lerp(1.5f, 7.5f, Mathf.Pow(Random01(edgeSeed + i * 17 + 29), 2.1f)) * Mathf.Lerp(0.72f, 1.18f, edgeBand);
                float stretch = Mathf.Lerp(1f, 3.8f, Random01(edgeSeed + i * 17 + 31));
                float alpha = Mathf.Lerp(0.22f, 0.82f, Random01(edgeSeed + i * 17 + 37)) * visibility * pulse;
                if (edgeBand > 0.72f)
                {
                    alpha *= 0.55f;
                }

                Color color = new Color(0f, 0f, 0f, alpha);
                float tint = Random01(edgeSeed + i * 17 + 41);
                if (tint > 0.965f)
                {
                    color = new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, alpha * 0.68f);
                }
                else if (tint > 0.925f)
                {
                    color = new Color(DarkRed.r, DarkRed.g, DarkRed.b, alpha * 0.5f);
                }

                float angle = Random01(edgeSeed + i * 17 + 43) * Mathf.PI * 2f + time * 0.12f;
                Vector2 tangent = Tangent(angle) * size * stretch;
                Vector2 normal = Normal(angle) * size;
                AddQuad(vh, ref vertexIndex, center, tangent, normal, color);
            }
        }

        private float BoundaryWave(float u, int edgeSeed)
        {
            float wave =
                Mathf.Sin((u * 2.1f + edgeSeed * 0.011f) * Mathf.PI * 2f + time * 0.28f) * 0.08f +
                Mathf.Sin((u * 4.3f + edgeSeed * 0.019f) * Mathf.PI * 2f - time * 0.22f) * 0.045f;
            float local = 0.62f + wave;
            for (int i = 0; i < 7; i++)
            {
                float seed = edgeSeed + i * 29.17f;
                float center = Random01(Mathf.FloorToInt(seed + 17f));
                float width = Mathf.Lerp(0.04f, 0.12f, Random01(Mathf.FloorToInt(seed + 31f)));
                float length = Mathf.Lerp(0.16f, 0.54f, Random01(Mathf.FloorToInt(seed + 47f)));
                float distance = Mathf.Abs(Mathf.DeltaAngle((u - center) * 360f, 0f)) / 360f;
                local += Mathf.Exp(-(distance * distance) / (2f * width * width)) * length;
            }

            return Mathf.Clamp(local, 0.24f, 1.18f);
        }

        private Vector2 PointAtDistance(Rect rect, float lane, float distance, float along)
        {
            switch (edge)
            {
                case Edge.Top:
                    return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, lane) + along, rect.yMax - distance);
                case Edge.Bottom:
                    return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, lane) + along, rect.yMin + distance);
                case Edge.Left:
                    return new Vector2(rect.xMin + distance, Mathf.Lerp(rect.yMin, rect.yMax, lane) + along);
                default:
                    return new Vector2(rect.xMax - distance, Mathf.Lerp(rect.yMin, rect.yMax, lane) + along);
            }
        }

        private static Vector2 Tangent(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static Vector2 Normal(float angle)
        {
            return new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
        }

        private static void AddQuad(VertexHelper vh, ref int vertexIndex, Vector2 center, Vector2 tangent, Vector2 normal, Color color)
        {
            Color32 color32 = color;
            vh.AddVert(center - tangent - normal, color32, Vector2.zero);
            vh.AddVert(center - tangent + normal, color32, Vector2.up);
            vh.AddVert(center + tangent + normal, color32, Vector2.one);
            vh.AddVert(center + tangent - normal, color32, Vector2.right);
            vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vh.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
            vertexIndex += 4;
        }
    }

    private const int OverlaySortOrder = short.MaxValue - 10;
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float EnterDuration = 1.75f;
    private const float EnterPeak = 0.38f;
    private const float ErosionIntensity = 0.78f;
    private const float SmokeIntensity = 0.5f;
    private const float ShakeIntensity = 0.64f;
    private const int SmokeCount = 30;
    private const int ParticleCount = 220;
    private const int TendrilCount = 24;

    private static readonly Color ProjectTealGreen = new Color(0.12f, 1f, 0.45f, 1f);
    private static readonly Color DarkRed = new Color(0.42f, 0.02f, 0.05f, 1f);

    private readonly List<EdgeLayer> edgeLayers = new List<EdgeLayer>();
    private readonly List<SpeckleLayer> speckleLayers = new List<SpeckleLayer>();
    private readonly List<SmokePuff> smokePuffs = new List<SmokePuff>();
    private readonly List<ErosionParticle> particles = new List<ErosionParticle>();
    private readonly List<Tendril> tendrils = new List<Tendril>();

    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform overlayRect;
    private RawImage vignetteLayer;
    private RawImage redPulseLayer;
    private RawImage scanlineLayer;
    private Texture2D vignetteTexture;
    private Texture2D redPulseTexture;
    private Texture2D scanlineTexture;
    private Sprite smokeSprite;
    private Sprite particleSprite;
    private Sprite tendrilSprite;
    private Camera targetCamera;
    private float originalOrthoSize;
    private Coroutine activeRoutine;
    private Tween activeCameraTween;
    private bool peakInvoked;

    private void Awake()
    {
        BuildOverlay();
    }

    public void PlayEnterSpecialImpact(System.Action onPeak, System.Action onComplete)
    {
        KillActiveTransition();
        BuildOverlay();
        ResetVisuals(true);
        activeRoutine = StartCoroutine(PlayTransitionRoutine(true, EnterDuration, EnterPeak, onPeak, onComplete));
    }

    public void PlayReturnNormalRecovery(float duration, System.Action onComplete)
    {
        KillActiveTransition();
        BuildOverlay();
        ResetVisuals(true);
        activeRoutine = StartCoroutine(PlayTransitionRoutine(false, Mathf.Max(0.4f, duration), 0.48f, null, onComplete));
    }

    private IEnumerator PlayTransitionRoutine(bool enteringSpecial, float duration, float peakTime, System.Action onPeak, System.Action onComplete)
    {
        overlayCanvas.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        peakInvoked = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (enteringSpecial && !peakInvoked && t >= peakTime)
            {
                peakInvoked = true;
                onPeak?.Invoke();
                PlayCameraImpactPulse();
            }

            float attack = enteringSpecial ? EaseOutCubic(t) : 1f - EaseInOutCubic(t);
            float boundary = Mathf.Lerp(22f, 138f, attack) * ErosionIntensity;
            float edgeAlpha = enteringSpecial
                ? Mathf.Lerp(0.1f, 0.54f, attack)
                : Mathf.Lerp(0.36f, 0f, t);
            float smokeAlpha = enteringSpecial
                ? Mathf.Clamp01((attack - 0.08f) / 0.58f)
                : 1f - Mathf.SmoothStep(0.15f, 1f, t);

            ApplyVignette(enteringSpecial, attack, t);
            ApplyEdgeLayers(boundary, edgeAlpha, elapsed);
            ApplySpeckles(boundary, smokeAlpha, elapsed);
            ApplySmoke(boundary, smokeAlpha, elapsed);
            ApplyParticles(boundary, smokeAlpha, elapsed);
            ApplyTendrils(boundary, attack, enteringSpecial, elapsed);
            ApplyScanline(enteringSpecial, t, elapsed);
            ApplyOverlayShake(enteringSpecial, t);

            yield return null;
        }

        ResetVisuals(false);
        activeRoutine = null;
        onComplete?.Invoke();
    }

    private void ApplyVignette(bool enteringSpecial, float attack, float t)
    {
        float alpha = enteringSpecial
            ? Mathf.Lerp(0.03f, 0.62f, attack)
            : Mathf.Lerp(0.46f, 0f, t);
        vignetteLayer.color = new Color(0f, 0f, 0f, alpha);

        float redAlpha = enteringSpecial
            ? Mathf.Exp(-Mathf.Pow((t - EnterPeak) / 0.09f, 2f)) * 0.34f
            : Mathf.Lerp(0.18f, 0f, t);
        redPulseLayer.color = new Color(DarkRed.r, DarkRed.g, DarkRed.b, redAlpha);
    }

    private void ApplyEdgeLayers(float boundary, float alpha, float time)
    {
        for (int i = 0; i < edgeLayers.Count; i++)
        {
            EdgeLayer layer = edgeLayers[i];
            layer.Graphic.Configure(layer.Edge, boundary, alpha, time, ErosionIntensity);
        }
    }

    private void ApplySpeckles(float boundary, float visibility, float time)
    {
        float alpha = Mathf.Clamp01(visibility) * 0.95f;
        for (int i = 0; i < speckleLayers.Count; i++)
        {
            SpeckleLayer layer = speckleLayers[i];
            layer.Graphic.Configure(layer.Edge, boundary, alpha, time, ErosionIntensity);
        }
    }

    private void ApplySmoke(float boundary, float visibility, float time)
    {
        Vector2 size = GetCanvasSize();
        float safeLeft = size.x * 0.25f;
        float safeRight = size.x * 0.75f;
        float safeTop = size.y * 0.23f;
        float safeBottom = size.y * 0.77f;

        for (int i = 0; i < smokePuffs.Count; i++)
        {
            SmokePuff smoke = smokePuffs[i];
            float local = Mathf.Clamp01((visibility - smoke.Delay) / 0.62f);
            if (local <= 0.01f)
            {
                smoke.Image.color = Color.clear;
                continue;
            }

            float wobble = Mathf.Sin(time * 1.55f + smoke.Phase) * smoke.Drift * SmokeIntensity;
            float normalOffset = Mathf.Sin(time * 1.2f + smoke.Phase * 0.7f) * smoke.Drift * 0.35f * SmokeIntensity;
            float edgeDistance = boundary * (0.68f + smoke.OffsetSeed * 0.22f) + normalOffset;
            float x = 0f;
            float y = 0f;

            switch (smoke.Edge)
            {
                case Edge.Top:
                    x = wobble;
                    y = -edgeDistance;
                    y = Mathf.Max(y, -safeTop + smoke.Size * 0.45f);
                    break;
                case Edge.Bottom:
                    x = wobble;
                    y = edgeDistance;
                    y = Mathf.Min(y, size.y - safeBottom - smoke.Size * 0.45f);
                    break;
                case Edge.Left:
                    x = edgeDistance;
                    x = Mathf.Min(x, safeLeft - smoke.Size * 0.45f);
                    y = wobble;
                    break;
                case Edge.Right:
                    x = -edgeDistance;
                    x = Mathf.Max(x, -(size.x - safeRight) + smoke.Size * 0.45f);
                    y = wobble;
                    break;
            }

            smoke.Rect.anchoredPosition = new Vector2(x, y);
            float radius = smoke.Size * (0.55f + SmokeIntensity * 0.34f);
            smoke.Rect.sizeDelta = new Vector2(radius * smoke.Stretch, radius);
            smoke.Rect.localRotation = Quaternion.Euler(0f, 0f, smoke.Phase * Mathf.Rad2Deg);
            float alpha = smoke.Alpha * local * (0.28f + SmokeIntensity * 0.26f);
            smoke.Image.color = new Color(0f, 0f, 0f, alpha);
        }
    }

    private void ApplyParticles(float boundary, float visibility, float time)
    {
        Vector2 size = GetCanvasSize();
        for (int i = 0; i < particles.Count; i++)
        {
            ErosionParticle particle = particles[i];
            float local = Mathf.Clamp01((visibility - particle.Delay) / 0.58f);
            if (local <= 0.01f)
            {
                particle.Image.color = Color.clear;
                continue;
            }

            float along = Mathf.Lerp(-32f, 32f, Mathf.Sin(time * 2.1f + particle.Phase) * 0.5f + 0.5f);
            float edgeDistance = boundary * (0.42f + particle.OffsetSeed * 0.68f) + Mathf.Sin(time * 1.3f + particle.Phase) * particle.Drift;
            Vector2 position = ParticlePosition(particle.Edge, particle.Lane, edgeDistance, along, size);
            particle.Rect.anchoredPosition = position;
            particle.Rect.sizeDelta = Vector2.one * particle.Size * Mathf.Lerp(0.55f, 1.2f, local);
            Color color = particle.Color;
            color.a *= local;
            particle.Image.color = color;
        }
    }

    private void ApplyTendrils(float boundary, float attack, bool enteringSpecial, float time)
    {
        Vector2 size = GetCanvasSize();
        for (int i = 0; i < tendrils.Count; i++)
        {
            Tendril tendril = tendrils[i];
            float grow = enteringSpecial
                ? Mathf.Clamp01((attack - tendril.Delay) / 0.52f)
                : Mathf.Clamp01((attack - tendril.Delay) / 0.72f);
            if (grow <= 0.01f)
            {
                tendril.Image.color = Color.clear;
                continue;
            }

            float length = Mathf.Min(tendril.Length * EaseOutCubic(grow), boundary * 0.72f);
            float wobble = Mathf.Sin(time * 1.8f + tendril.Phase) * tendril.Wobble;
            PlaceTendril(tendril, length, wobble, size);
            tendril.Rect.sizeDelta = new Vector2(tendril.Width, length);
            tendril.Image.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.12f, 0.48f, grow));
        }
    }

    private void ApplyScanline(bool enteringSpecial, float t, float time)
    {
        float alpha = enteringSpecial
            ? Mathf.Exp(-Mathf.Pow((t - EnterPeak) / 0.13f, 2f)) * 0.22f
            : Mathf.Lerp(0.08f, 0f, t);
        scanlineLayer.color = new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, alpha);
        scanlineLayer.uvRect = new Rect(0f, time * 0.45f, 1f, 1f);
    }

    private void ApplyOverlayShake(bool enteringSpecial, float t)
    {
        float impact = enteringSpecial
            ? Mathf.Exp(-Mathf.Pow((t - EnterPeak) / 0.095f, 2f))
            : Mathf.Exp(-Mathf.Pow((t - 0.34f) / 0.18f, 2f)) * 0.28f;
        float amplitude = 18f * ShakeIntensity * impact;
        overlayRect.anchoredPosition = new Vector2(
            (ValueNoise(Time.unscaledTime * 38.1f) - 0.5f) * amplitude,
            (ValueNoise(Time.unscaledTime * 41.7f + 8.2f) - 0.5f) * amplitude);
    }

    private Vector2 ParticlePosition(Edge edge, float lane, float edgeDistance, float alongOffset, Vector2 size)
    {
        switch (edge)
        {
            case Edge.Top:
                return new Vector2(alongOffset, -edgeDistance);
            case Edge.Bottom:
                return new Vector2(alongOffset, edgeDistance);
            case Edge.Left:
                return new Vector2(edgeDistance, alongOffset);
            default:
                return new Vector2(-edgeDistance, alongOffset);
        }
    }

    private void PlaceTendril(Tendril tendril, float length, float wobble, Vector2 size)
    {
        RectTransform rect = tendril.Rect;
        switch (tendril.Edge)
        {
            case Edge.Top:
                rect.anchorMin = new Vector2(tendril.Lane, 1f);
                rect.anchorMax = new Vector2(tendril.Lane, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(wobble, 0f);
                rect.localRotation = Quaternion.identity;
                break;
            case Edge.Bottom:
                rect.anchorMin = new Vector2(tendril.Lane, 0f);
                rect.anchorMax = new Vector2(tendril.Lane, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(wobble, 0f);
                rect.localRotation = Quaternion.identity;
                break;
            case Edge.Left:
                rect.anchorMin = new Vector2(0f, tendril.Lane);
                rect.anchorMax = new Vector2(0f, tendril.Lane);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, wobble);
                rect.localRotation = Quaternion.Euler(0f, 0f, -90f);
                break;
            case Edge.Right:
                rect.anchorMin = new Vector2(1f, tendril.Lane);
                rect.anchorMax = new Vector2(1f, tendril.Lane);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, wobble);
                rect.localRotation = Quaternion.Euler(0f, 0f, 90f);
                break;
        }
    }

    private void PlayCameraImpactPulse()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        }

        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        originalOrthoSize = targetCamera.orthographicSize;
        activeCameraTween?.Kill();
        activeCameraTween = DOTween.Sequence()
            .Append(DOTween.To(() => targetCamera.orthographicSize, x => targetCamera.orthographicSize = x, originalOrthoSize * 0.965f, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true))
            .Append(DOTween.To(() => targetCamera.orthographicSize, x => targetCamera.orthographicSize = x, originalOrthoSize, 0.22f).SetEase(Ease.OutQuad).SetUpdate(true))
            .OnComplete(() => activeCameraTween = null);
    }

    private void KillActiveTransition()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        activeCameraTween?.Kill();
        activeCameraTween = null;
        if (targetCamera != null && targetCamera.orthographic && Mathf.Abs(originalOrthoSize) > 0.001f)
        {
            targetCamera.orthographicSize = originalOrthoSize;
        }

        ResetVisuals(false);
    }

    private void OnDestroy()
    {
        KillActiveTransition();
        DestroyGeneratedAssets();
    }

    private void BuildOverlay()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("WorldTransitionOverlay", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = OverlaySortOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        overlayRect = canvasObject.GetComponent<RectTransform>();

        vignetteTexture = CreateVignetteTexture(512, 288);
        redPulseTexture = CreateVignetteTexture(512, 288);
        scanlineTexture = CreateScanlineTexture(8, 64);
        smokeSprite = CreateSoftCircleSprite(96, 1f);
        particleSprite = CreateSoftCircleSprite(32, 0.8f);
        tendrilSprite = CreateTendrilSprite(16, 128);

        vignetteLayer = CreateRawLayer("BoundaryVignette", canvasObject.transform, vignetteTexture, new Color(0f, 0f, 0f, 0f));
        redPulseLayer = CreateRawLayer("RedPulse", canvasObject.transform, redPulseTexture, Color.clear);
        CreateEdgeLayer(Edge.Top, "TopErosion");
        CreateEdgeLayer(Edge.Bottom, "BottomErosion");
        CreateEdgeLayer(Edge.Left, "LeftErosion");
        CreateEdgeLayer(Edge.Right, "RightErosion");
        CreateSpeckleLayer(Edge.Top, "TopErosionSpeckles");
        CreateSpeckleLayer(Edge.Bottom, "BottomErosionSpeckles");
        CreateSpeckleLayer(Edge.Left, "LeftErosionSpeckles");
        CreateSpeckleLayer(Edge.Right, "RightErosionSpeckles");
        BuildSmokePool(canvasObject.transform);
        BuildParticlePool(canvasObject.transform);
        BuildTendrilPool(canvasObject.transform);
        scanlineLayer = CreateRawLayer("WeakTealScanlines", canvasObject.transform, scanlineTexture, Color.clear);

        overlayCanvas.gameObject.SetActive(false);
    }

    private void CreateEdgeLayer(Edge edge, string objectName)
    {
        GameObject layerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(ErosionEdgeGraphic));
        layerObject.transform.SetParent(overlayCanvas.transform, false);

        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ErosionEdgeGraphic graphic = layerObject.GetComponent<ErosionEdgeGraphic>();
        graphic.raycastTarget = false;
        graphic.Configure(edge, 0f, 0f, 0f, ErosionIntensity);

        edgeLayers.Add(new EdgeLayer { Edge = edge, Graphic = graphic, Rect = rect });
    }

    private void CreateSpeckleLayer(Edge edge, string objectName)
    {
        GameObject layerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(ErosionSpeckleGraphic));
        layerObject.transform.SetParent(overlayCanvas.transform, false);

        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ErosionSpeckleGraphic graphic = layerObject.GetComponent<ErosionSpeckleGraphic>();
        graphic.raycastTarget = false;
        graphic.Configure(edge, 0f, 0f, 0f, ErosionIntensity);

        speckleLayers.Add(new SpeckleLayer { Edge = edge, Graphic = graphic, Rect = rect });
    }

    private void BuildSmokePool(Transform parent)
    {
        for (int i = 0; i < SmokeCount; i++)
        {
            Edge edge = (Edge)(i % 4);
            float lane = Random01(i + 5);
            Image image = CreateImage("BoundarySmoke", parent, smokeSprite);
            image.color = Color.clear;
            RectTransform rect = image.rectTransform;
            ConfigureBoundaryAnchors(rect, edge, lane);
            smokePuffs.Add(new SmokePuff
            {
                Edge = edge,
                Image = image,
                Rect = rect,
                Lane = lane,
                OffsetSeed = Random01(i + 9),
                Drift = Mathf.Lerp(6f, 22f, Random01(i + 13)),
                Size = Mathf.Lerp(26f, 72f, Random01(i + 17)),
                Stretch = Mathf.Lerp(1.05f, 1.8f, Random01(i + 21)),
                Delay = Random01(i + 25) * 0.22f,
                Phase = Random01(i + 29) * Mathf.PI * 2f,
                Alpha = Mathf.Lerp(0.08f, 0.16f, Random01(i + 33))
            });
        }
    }

    private void BuildParticlePool(Transform parent)
    {
        for (int i = 0; i < ParticleCount; i++)
        {
            Edge edge = (Edge)(i % 4);
            float lane = Random01(i + 117);
            Image image = CreateImage("BoundaryParticle", parent, particleSprite);
            RectTransform rect = image.rectTransform;
            ConfigureBoundaryAnchors(rect, edge, lane);

            float tint = Random01(i + 111);
            Color color = new Color(0f, 0f, 0f, Mathf.Lerp(0.42f, 0.92f, Random01(i + 113)));
            if (tint > 0.95f)
            {
                color = new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, 0.58f);
            }
            else if (tint > 0.9f)
            {
                color = new Color(DarkRed.r, DarkRed.g, DarkRed.b, 0.5f);
            }

            particles.Add(new ErosionParticle
            {
                Edge = edge,
                Image = image,
                Rect = rect,
                Lane = lane,
                OffsetSeed = Random01(i + 121),
                Drift = Mathf.Lerp(5f, 28f, Random01(i + 123)),
                Size = Mathf.Lerp(1.4f, 8f, Mathf.Pow(Random01(i + 127), 1.7f)),
                Delay = Random01(i + 131) * 0.32f,
                Phase = Random01(i + 137) * Mathf.PI * 2f,
                Color = color
            });
        }
    }

    private void BuildTendrilPool(Transform parent)
    {
        for (int i = 0; i < TendrilCount; i++)
        {
            Edge edge = (Edge)(i % 4);
            Image image = CreateImage("BoundaryTendril", parent, tendrilSprite);
            image.color = Color.clear;
            tendrils.Add(new Tendril
            {
                Edge = edge,
                Image = image,
                Rect = image.rectTransform,
                Lane = Random01(i + 151),
                Length = Mathf.Lerp(42f, 150f, Random01(i + 157)),
                Width = Mathf.Lerp(2f, 6f, Random01(i + 163)),
                Delay = Random01(i + 167) * 0.36f,
                Wobble = Mathf.Lerp(5f, 28f, Random01(i + 173)),
                Phase = Random01(i + 179) * Mathf.PI * 2f
            });
        }
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }

    private static RawImage CreateRawLayer(string name, Transform parent, Texture texture, Color color)
    {
        GameObject layerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        layerObject.transform.SetParent(parent, false);
        RawImage image = layerObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static void ConfigureBoundaryAnchors(RectTransform rect, Edge edge, float lane)
    {
        switch (edge)
        {
            case Edge.Top:
                rect.anchorMin = new Vector2(lane, 1f);
                rect.anchorMax = new Vector2(lane, 1f);
                break;
            case Edge.Bottom:
                rect.anchorMin = new Vector2(lane, 0f);
                rect.anchorMax = new Vector2(lane, 0f);
                break;
            case Edge.Left:
                rect.anchorMin = new Vector2(0f, lane);
                rect.anchorMax = new Vector2(0f, lane);
                break;
            case Edge.Right:
                rect.anchorMin = new Vector2(1f, lane);
                rect.anchorMax = new Vector2(1f, lane);
                break;
        }

        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private Vector2 GetCanvasSize()
    {
        Vector2 size = overlayRect.rect.size;
        if (size.x < 1f || size.y < 1f)
        {
            return new Vector2(ReferenceWidth, ReferenceHeight);
        }

        return size;
    }

    private void ResetVisuals(bool keepCanvasActive)
    {
        if (overlayCanvas == null)
        {
            return;
        }

        overlayRect.anchoredPosition = Vector2.zero;
        canvasGroup.alpha = 0f;
        vignetteLayer.color = Color.clear;
        redPulseLayer.color = Color.clear;
        scanlineLayer.color = Color.clear;

        for (int i = 0; i < edgeLayers.Count; i++)
        {
            edgeLayers[i].Graphic.Clear();
        }

        for (int i = 0; i < speckleLayers.Count; i++)
        {
            speckleLayers[i].Graphic.Clear();
        }

        for (int i = 0; i < smokePuffs.Count; i++)
        {
            smokePuffs[i].Image.color = Color.clear;
        }

        for (int i = 0; i < particles.Count; i++)
        {
            particles[i].Image.color = Color.clear;
        }

        for (int i = 0; i < tendrils.Count; i++)
        {
            tendrils[i].Image.color = Color.clear;
        }

        overlayCanvas.gameObject.SetActive(keepCanvasActive);
    }

    private static Texture2D CreateVignetteTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float maxDistance = Vector2.Distance(center, Vector2.zero);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.98f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateScanlineTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < height; y++)
        {
            float alpha = y % 7 == 0 ? 0.62f : 0f;
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Sprite CreateSoftCircleSprite(int size, float centerAlpha)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(distance), 1.65f) * centerAlpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateTendrilSprite(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float center = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float horizontal = Mathf.Abs(x - center) / center;
                float vertical = 1f - y / (float)(height - 1);
                float noise = FractalNoise(x * 0.6f + 9f, y * 0.08f);
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(horizontal), 1.4f) * Mathf.Pow(vertical, 0.7f) * (0.72f + noise * 0.28f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 1f), height);
    }

    private void DestroyGeneratedAssets()
    {
        DestroyTexture(vignetteTexture);
        DestroyTexture(redPulseTexture);
        DestroyTexture(scanlineTexture);
        DestroySprite(smokeSprite);
        DestroySprite(particleSprite);
        DestroySprite(tendrilSprite);
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }

    private static void DestroySprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        if (sprite.texture != null)
        {
            Destroy(sprite.texture);
        }

        Destroy(sprite);
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - value, 3f);
    }

    private static float EaseInOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return value < 0.5f ? 4f * value * value * value : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
    }

    private static float FractalNoise(float x, float y)
    {
        return ValueNoise(x, y) * 0.58f + ValueNoise(x * 2.1f + 17.2f, y * 2.1f + 3.7f) * 0.28f + ValueNoise(x * 4.2f + 8.1f, y * 4.2f + 55.4f) * 0.14f;
    }

    private static float ValueNoise(float value)
    {
        return Random01(Mathf.FloorToInt(value * 1000f));
    }

    private static float ValueNoise(float x, float y)
    {
        return Random01(Mathf.FloorToInt(x * 157.13f + y * 311.7f));
    }

    private static float Random01(int seed)
    {
        float value = Mathf.Sin(seed * 127.1f + 311.7f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }
}
