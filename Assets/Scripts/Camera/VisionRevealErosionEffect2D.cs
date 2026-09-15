using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 视界命中后的显形表现层。
/// 它临时替换目标 SpriteRenderer 的材质，通过半径参数做“SpecialWorld 目标显现、NormalWorld 目标退场”的动画，
/// 播放结束后恢复原材质，再通知 WorldSwapManager2D 写入最终显隐状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class VisionRevealErosionEffect2D : MonoBehaviour
{
    /// <summary>
    /// 记录每个 Renderer 动画前的材质和半径参数，保证特效中断或对象禁用时能恢复现场。
    /// </summary>
    private sealed class RendererState
    {
        public SpriteRenderer Renderer;
        public Material OriginalMaterial;
        public Material RuntimeMaterial;
        public float InitialRadius;
        public float FinalRadius;
    }

    private static readonly int RevealCenterId = Shader.PropertyToID("_RevealCenter");
    private static readonly int RevealRadiusId = Shader.PropertyToID("_RevealRadius");
    private static readonly int RevealSoftnessId = Shader.PropertyToID("_RevealSoftness");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int RevealInvertId = Shader.PropertyToID("_RevealInvert");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int TealColorId = Shader.PropertyToID("_TealColor");
    private static readonly int RedColorId = Shader.PropertyToID("_RedColor");
    private static readonly int BlackColorId = Shader.PropertyToID("_BlackColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    // 项目里使用青绿 + 暗红 + 黑色来表达双世界异常感。
    private static readonly Color ProjectTealGreen = new Color(0.12f, 1f, 0.45f, 1f);
    private static readonly Color DarkRed = new Color(0.42f, 0.02f, 0.05f, 1f);

    [SerializeField] private float revealDuration = 2.35f;
    [SerializeField] private float smallTargetDuration = 1.55f;
    [SerializeField] private float edgeWorldWidth = 0.22f;
    [SerializeField] private float revealSoftness = 0.1f;
    [SerializeField] private float noiseScale = 18f;
    [SerializeField] private float noiseStrength = 0.28f;
    [SerializeField] private float lingerAfterReveal = 0.28f;

    private readonly List<RendererState> rendererStates = new List<RendererState>();
    private Coroutine activeRoutine;
    private ParticleSystem blackParticles;
    private ParticleSystem tealParticles;
    private ParticleSystem redParticles;
    private Action revealCompleted;

    /// <summary>
    /// 播放一次显形。
    /// captureArea 是玩家拍照的视界框，normalTargetArea 是普通世界目标范围，
    /// coverageRatio 用来判断目标是否几乎完整覆盖，从而决定动画中心和时长。
    /// </summary>
    public void Play(
        Transform normalTarget,
        Rect captureArea,
        Rect normalTargetArea,
        float coverageRatio,
        Action onRevealCompleted)
    {
        StopActiveEffect(true);
        revealCompleted = onRevealCompleted;

        SpriteRenderer[] specialRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (specialRenderers.Length == 0 || !TryGetRendererBounds(specialRenderers, out Bounds specialBounds))
        {
            Destroy(this);
            return;
        }

        // 特殊世界目标负责“显现”，普通世界目标负责“反向退场”，两个方向一起播会更自然。
        SpriteRenderer[] normalRenderers = normalTarget != null
            ? normalTarget.GetComponentsInChildren<SpriteRenderer>(true)
            : Array.Empty<SpriteRenderer>();
        Bounds normalBounds;
        if (!TryGetRendererBounds(normalRenderers, out normalBounds))
        {
            normalBounds = RectToBounds(normalTargetArea);
        }

        Vector2 specialRevealCenter = CalculateSpecialRevealCenter(captureArea, normalTargetArea, specialBounds, coverageRatio);
        Vector2 normalRevealCenter = CalculateNormalRevealCenter(captureArea, normalTargetArea, normalBounds, coverageRatio);
        float specialBoundsDiagonal = Mathf.Max(0.05f, new Vector2(specialBounds.size.x, specialBounds.size.y).magnitude);
        bool fullyCoveredOrSmall = coverageRatio >= 0.92f ||
            (captureArea.width >= normalTargetArea.width * 0.96f && captureArea.height >= normalTargetArea.height * 0.96f) ||
            specialBoundsDiagonal <= Mathf.Max(captureArea.width, captureArea.height) * 0.42f;

        // 小目标或几乎完整覆盖时，从中心快速扩散；局部覆盖时，从视界和目标的交叠位置扩散。
        float specialInitialRadius = fullyCoveredOrSmall
            ? Mathf.Max(0.012f, specialBoundsDiagonal * 0.018f)
            : CalculateInitialRadius(captureArea, normalTargetArea, specialBounds);
        float normalBoundsDiagonal = Mathf.Max(0.05f, new Vector2(normalBounds.size.x, normalBounds.size.y).magnitude);
        float normalInitialRadius = fullyCoveredOrSmall
            ? Mathf.Max(0.012f, normalBoundsDiagonal * 0.018f)
            : CalculateInitialRadius(captureArea, normalTargetArea, normalBounds);
        float specialFinalRadius = CalculateFinalRadius(specialRevealCenter, specialBounds) + edgeWorldWidth * 2.4f;
        float normalFinalRadius = CalculateFinalRadius(normalRevealCenter, normalBounds) + edgeWorldWidth * 2.4f;
        float duration = fullyCoveredOrSmall ? smallTargetDuration : revealDuration;

        rendererStates.Clear();
        PrepareRenderers(specialRenderers, specialRevealCenter, specialInitialRadius, specialFinalRadius, false);
        PrepareRenderers(normalRenderers, normalRevealCenter, normalInitialRadius, normalFinalRadius, true);
        PrepareParticles(specialRevealCenter, specialInitialRadius, specialFinalRadius);
        activeRoutine = StartCoroutine(PlayRoutine(specialInitialRadius, specialFinalRadius, duration));
    }

    private IEnumerator PlayRoutine(float particleInitialRadius, float particleFinalRadius, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            // smoothstep 缓动：开头和结尾更柔和，中段变化更明显。
            float eased = t * t * (3f - 2f * t);

            // 轻微脉冲让边缘不那么死板，结束时逐渐收掉。
            float pulse = Mathf.Sin(Time.time * 18f) * edgeWorldWidth * 0.12f * (1f - t);
            ApplyRevealProgress(eased, pulse);
            UpdateParticleRadius(Mathf.Lerp(particleInitialRadius, particleFinalRadius, eased) + pulse);
            yield return null;
        }

        ApplyRevealProgress(1f, 0f);
        UpdateParticleRadius(particleFinalRadius);
        yield return new WaitForSeconds(lingerAfterReveal);
        RestoreRenderers();
        StopParticles();
        revealCompleted?.Invoke();
        revealCompleted = null;
        activeRoutine = null;
        Destroy(this, 1.2f);
    }

    private void PrepareRenderers(
        SpriteRenderer[] renderers,
        Vector2 revealCenter,
        float initialRadius,
        float finalRadius,
        bool invertReveal)
    {
        Shader shader = Shader.Find("Leihuo/VisionRevealErosion2D");
        if (shader == null)
        {
            return;
        }

        // 每个 SpriteRenderer 单独创建运行时材质，避免修改 sharedMaterial 影响同材质的其他对象。
        float seed = UnityEngine.Random.Range(0f, 999f);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                continue;
            }

            RendererState state = new RendererState
            {
                Renderer = spriteRenderer,
                OriginalMaterial = spriteRenderer.sharedMaterial,
                InitialRadius = initialRadius,
                FinalRadius = finalRadius,
                RuntimeMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }
            };

            // 把 Sprite 当前纹理传给材质，并写入显形中心、半径、噪声、颜色等表现参数。
            state.RuntimeMaterial.SetTexture(MainTexId, spriteRenderer.sprite.texture);
            state.RuntimeMaterial.SetVector(RevealCenterId, revealCenter);
            state.RuntimeMaterial.SetFloat(RevealRadiusId, initialRadius);
            state.RuntimeMaterial.SetFloat(RevealSoftnessId, revealSoftness);
            state.RuntimeMaterial.SetFloat(EdgeWidthId, edgeWorldWidth);
            state.RuntimeMaterial.SetFloat(NoiseScaleId, noiseScale);
            state.RuntimeMaterial.SetFloat(NoiseStrengthId, noiseStrength);
            state.RuntimeMaterial.SetFloat(RevealInvertId, invertReveal ? 1f : 0f);
            state.RuntimeMaterial.SetFloat(SeedId, seed + i * 13.37f);
            state.RuntimeMaterial.SetColor(TealColorId, ProjectTealGreen);
            state.RuntimeMaterial.SetColor(RedColorId, DarkRed);
            state.RuntimeMaterial.SetColor(BlackColorId, Color.black);

            spriteRenderer.sharedMaterial = state.RuntimeMaterial;
            spriteRenderer.enabled = true;
            rendererStates.Add(state);
        }
    }

    private void ApplyRevealProgress(float eased, float pulse)
    {
        for (int i = 0; i < rendererStates.Count; i++)
        {
            RendererState state = rendererStates[i];
            Material material = state.RuntimeMaterial;
            if (material != null)
            {
                float radius = Mathf.Lerp(state.InitialRadius, state.FinalRadius, eased) + pulse;
                material.SetFloat(RevealRadiusId, radius);
            }
        }
    }

    private void RestoreRenderers()
    {
        // 特效结束或中断时恢复原材质，避免一次显形污染后续渲染状态。
        for (int i = 0; i < rendererStates.Count; i++)
        {
            RendererState state = rendererStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.sharedMaterial = state.OriginalMaterial;
                state.Renderer.enabled = true;
            }

            if (state.RuntimeMaterial != null)
            {
                Destroy(state.RuntimeMaterial);
            }
        }

        rendererStates.Clear();
    }

    private void PrepareParticles(Vector2 center, float initialRadius, float finalRadius)
    {
        // 粒子只做额外反馈，不参与实际判定；判定结果已经在 WorldSwapManager2D 中确定。
        blackParticles = CreateParticleSystem("RevealBlackErosionParticles", center, initialRadius, finalRadius, new Color(0f, 0f, 0f, 0.82f), 78f);
        tealParticles = CreateParticleSystem("RevealTealErosionParticles", center, initialRadius, finalRadius, new Color(ProjectTealGreen.r, ProjectTealGreen.g, ProjectTealGreen.b, 0.42f), 8f);
        redParticles = CreateParticleSystem("RevealRedErosionParticles", center, initialRadius, finalRadius, new Color(DarkRed.r, DarkRed.g, DarkRed.b, 0.36f), 6f);
    }

    private ParticleSystem CreateParticleSystem(string objectName, Vector2 center, float initialRadius, float finalRadius, Color color, float rate)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.SetActive(false);
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.position = new Vector3(center.x, center.y, transform.position.z - 0.05f);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = Mathf.Max(revealDuration, smallTargetDuration) + lingerAfterReveal;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.26f, 0.72f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.065f);
        main.startColor = color;
        main.maxParticles = 260;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.01f, initialRadius);
        shape.radiusThickness = 0.1f;
        shape.arc = 360f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = new ParticleSystem.MinMaxCurve(0.03f, 0.18f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = SortingLayer.NameToID("Default");
        renderer.sortingOrder = GetTopSortingOrder() + 2;
        renderer.material = CreateParticleMaterial();

        particleObject.SetActive(true);
        particles.Play(true);
        return particles;
    }

    private void UpdateParticleRadius(float radius)
    {
        UpdateParticleRadius(blackParticles, radius);
        UpdateParticleRadius(tealParticles, radius);
        UpdateParticleRadius(redParticles, radius);
    }

    private static void UpdateParticleRadius(ParticleSystem particles, float radius)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.radius = Mathf.Max(0.01f, radius);
    }

    private void StopParticles()
    {
        StopParticleSystem(blackParticles);
        StopParticleSystem(tealParticles);
        StopParticleSystem(redParticles);
        blackParticles = null;
        tealParticles = null;
        redParticles = null;
    }

    private static void StopParticleSystem(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(particles.gameObject, 0.9f);
    }

    private Material CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return material;
    }

    private int GetTopSortingOrder()
    {
        int order = 0;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                order = Mathf.Max(order, renderers[i].sortingOrder);
            }
        }

        return order;
    }

    private void StopActiveEffect(bool restore)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        StopParticles();
        revealCompleted = null;
        if (restore)
        {
            RestoreRenderers();
        }
    }

    private void OnDisable()
    {
        StopActiveEffect(true);
    }

    private void OnDestroy()
    {
        StopActiveEffect(true);
    }

    private static Vector2 CalculateSpecialRevealCenter(Rect captureArea, Rect normalTargetArea, Bounds specialBounds, float coverageRatio)
    {
        if (coverageRatio >= 0.92f || normalTargetArea.width <= 0.001f || normalTargetArea.height <= 0.001f)
        {
            return specialBounds.center;
        }

        // 把 NormalWorld 中“被拍到的位置”映射到 SpecialWorld 对应目标范围，
        // 让两个世界的显形起点看起来是同一个位置。
        Rect overlap = Intersect(captureArea, normalTargetArea);
        Vector2 normalPoint = overlap.width > 0f && overlap.height > 0f ? overlap.center : captureArea.center;
        float normalizedX = Mathf.InverseLerp(normalTargetArea.xMin, normalTargetArea.xMax, normalPoint.x);
        float normalizedY = Mathf.InverseLerp(normalTargetArea.yMin, normalTargetArea.yMax, normalPoint.y);
        return new Vector2(
            Mathf.Lerp(specialBounds.min.x, specialBounds.max.x, normalizedX),
            Mathf.Lerp(specialBounds.min.y, specialBounds.max.y, normalizedY));
    }

    private static Vector2 CalculateNormalRevealCenter(Rect captureArea, Rect normalTargetArea, Bounds normalBounds, float coverageRatio)
    {
        if (coverageRatio >= 0.92f || normalTargetArea.width <= 0.001f || normalTargetArea.height <= 0.001f)
        {
            return normalBounds.center;
        }

        Rect overlap = Intersect(captureArea, normalTargetArea);
        Vector2 normalPoint = overlap.width > 0f && overlap.height > 0f ? overlap.center : captureArea.center;

        // NormalWorld 目标只需要在自身 bounds 内退场，所以把起点限制在普通目标范围内。
        return new Vector2(
            Mathf.Clamp(normalPoint.x, normalBounds.min.x, normalBounds.max.x),
            Mathf.Clamp(normalPoint.y, normalBounds.min.y, normalBounds.max.y));
    }

    private static float CalculateInitialRadius(Rect captureArea, Rect normalTargetArea, Bounds specialBounds)
    {
        Rect overlap = Intersect(captureArea, normalTargetArea);
        if (overlap.width <= 0f || overlap.height <= 0f)
        {
            return Mathf.Max(0.012f, Mathf.Min(specialBounds.size.x, specialBounds.size.y) * 0.035f);
        }

        float normalDiagonal = Mathf.Max(0.001f, new Vector2(normalTargetArea.width, normalTargetArea.height).magnitude);
        float specialDiagonal = Mathf.Max(0.001f, new Vector2(specialBounds.size.x, specialBounds.size.y).magnitude);
        float scale = specialDiagonal / normalDiagonal;
        float overlapDiagonal = new Vector2(overlap.width, overlap.height).magnitude * scale;
        return Mathf.Clamp(overlapDiagonal * 0.055f, 0.012f, specialDiagonal * 0.09f);
    }

    private static float CalculateFinalRadius(Vector2 center, Bounds bounds)
    {
        // 最终半径要覆盖目标 bounds 的四个角，确保动画结束时整件物体都完成显形/退场。
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;
        float radius = 0f;
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(min.x, min.y)));
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(min.x, max.y)));
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(max.x, min.y)));
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(max.x, max.y)));
        return radius;
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        float minX = Mathf.Max(a.xMin, b.xMin);
        float maxX = Mathf.Min(a.xMax, b.xMax);
        float minY = Mathf.Max(a.yMin, b.yMin);
        float maxY = Mathf.Min(a.yMax, b.yMax);
        return new Rect(minX, minY, Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
    }

    private static Bounds RectToBounds(Rect rect)
    {
        Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
        Vector3 size = new Vector3(Mathf.Max(0.001f, rect.width), Mathf.Max(0.001f, rect.height), 0.001f);
        return new Bounds(center, size);
    }

    public static bool TryGetRendererRect(Transform root, out Rect rect)
    {
        rect = default;
        if (root == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (!TryGetRendererBounds(renderers, out Bounds bounds))
        {
            return false;
        }

        rect = new Rect(bounds.min.x, bounds.min.y, Mathf.Max(0.001f, bounds.size.x), Mathf.Max(0.001f, bounds.size.y));
        return true;
    }

    private static bool TryGetRendererBounds(SpriteRenderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spriteRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spriteRenderer.bounds);
            }
        }

        return hasBounds;
    }
}
