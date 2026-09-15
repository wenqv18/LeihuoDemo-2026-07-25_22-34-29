using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 当前主世界类型。Normal 是常规探索世界，Special 是 SAN 耗尽或恢复规则触发后的特殊世界。
/// </summary>
public enum WorldKind2D
{
    Normal,
    Special
}

/// <summary>
/// SAN 耗尽事件参数。监听者可以把 CancelDefaultResolution 设为 true，
/// 用自己的规则接管“耗尽后默认翻转世界”的处理。
/// </summary>
public sealed class WorldSanExhaustedEventArgs : EventArgs
{
    public WorldSanExhaustedEventArgs(WorldKind2D world)
    {
        World = world;
    }

    public WorldKind2D World { get; }
    public bool CancelDefaultResolution { get; set; }
}

/// <summary>
/// 双世界显示与视界玩法的核心管理器。
/// 主要职责包括：视界区域预览、拍照确认、目标覆盖率检测、Normal/Special 显隐切换、
/// SAN 失败次数统计、世界过渡事件派发，以及 SpriteMask 状态应用。
/// </summary>
public sealed class WorldSwapManager2D : MonoBehaviour
{
    public static WorldSwapManager2D Instance { get; private set; }

    // 这些事件把世界系统和 UI、教程、SpecialWorld 生存规则解耦。
    public static event Action<Vector2> VisionUseConfirmed;
    public static event Action<WorldKind2D> PrimaryWorldChanged;
    public static event Action SpecialWorldEntered;
    public static event Action ReturnedToNormalWorld;
    public static event Action<WorldSanExhaustedEventArgs> SanExhausted;

    [Header("World Roots")]
    [SerializeField] private Transform normalWorldRoot;
    [SerializeField] private Transform specialWorldRoot;

    [Header("Swap Area")]
    [SerializeField] private Vector2 areaSize = new Vector2(4f, 2f);
    [SerializeField] private bool matchAreaToCameraReticle = true;
    [SerializeField] private int usesBeforeWorldFlip = 3;
    [SerializeField] private Color areaFillColor = new Color(0.35f, 0.75f, 1f, 0f);
    [SerializeField] private Color areaLineColor = new Color(0.35f, 0.75f, 1f, 0.85f);
    [SerializeField] private bool logWorldDiagnostics;

    [Header("Failure Feedback")]
    [SerializeField] private float failureShakeDuration = 0.24f;
    [SerializeField] private float failureShakeAmplitude = 0.18f;

    // 缓存两套世界中的渲染器和交互组件，避免每帧重复遍历场景。
    private readonly List<SpriteRenderer> normalRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> specialRenderers = new List<SpriteRenderer>();
    private readonly List<NpcInteraction2D> normalNpcInteractions = new List<NpcInteraction2D>();
    private readonly List<NpcInteraction2D> specialNpcInteractions = new List<NpcInteraction2D>();

    // 新版目标组件和旧版目标组件并存：优先使用 WorldInteractionTarget2D，旧组件用于兼容早期场景。
    private readonly List<WorldInteractionTarget2D> visionTargets = new List<WorldInteractionTarget2D>();
    private readonly List<NormalWorldVisionTarget2D> legacyVisionTargets = new List<NormalWorldVisionTarget2D>();

    // 拍照成功后的持久显隐状态：Special 目标强制显示，Normal 目标强制隐藏。
    private readonly List<SpriteRenderer> revealedRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> hiddenRenderers = new List<SpriteRenderer>();

    private SpriteMask areaMask;
    private bool normalIsPrimary = true;
    private bool hasActiveArea;
    private bool isPreviewing;
    private bool hasFoundVisionTarget;
    private Rect activeArea;
    private int usesSinceFlip;

    public bool HasFoundVisionTarget => hasFoundVisionTarget;
    public bool HasActiveArea => hasActiveArea;
    public bool IsTransitioning { get; private set; }

    public Vector2 AreaSize
    {
        get => areaSize;
        set
        {
            areaSize = new Vector2(Mathf.Max(0.1f, value.x), Mathf.Max(0.1f, value.y));
            UpdateAreaVisual();
            ApplyWorldState();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveRoots();
        BuildAreaVisual();
        RefreshTrackedObjects();
        ApplyWorldState();
        UpdateSanUI();
    }

    private void OnDisable()
    {
        ResetRendererState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ResetRendererState();
    }

    private void OnValidate()
    {
        areaSize = new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y));
        usesBeforeWorldFlip = Mathf.Max(1, usesBeforeWorldFlip);
    }

    /// <summary>
    /// 确认一次视界拍照。
    /// 成功时根据覆盖率找到目标并揭示 SpecialWorld 对应物；失败时累积 SAN 失败次数。
    /// </summary>
    public void ConfirmArea(Vector2 center)
    {
        if (GetPrimaryWorld() == WorldKind2D.Special)
        {
            // SpecialWorld 中禁用视界领域确认。
            return;
        }

        VisionUseConfirmed?.Invoke(center);

        // ConfirmArea 是“真正拍照”的入口：这里才会把鼠标中心点转换成视界矩形并做目标覆盖率判定。
        Vector2 currentAreaSize = GetCurrentAreaSize();
        Vector2 halfSize = currentAreaSize * 0.5f;
        Rect proposedArea = new Rect(center - halfSize, currentAreaSize);
        WorldInteractionTarget2D coveredTarget;
        float coverageRatio;
        if (TryFindCoveredVisionTarget(proposedArea, out coveredTarget, out coverageRatio))
        {
            // 命中新版目标后，不再保留临时视界区域，而是进入“目标已揭示”的持久状态。
            hasFoundVisionTarget = true;
            isPreviewing = false;
            hasActiveArea = false;
            coveredTarget.NotifyVisionFound(coverageRatio);
            RevealSpecialTarget(coveredTarget.transform, proposedArea, coverageRatio);
            UpdateAreaVisual();
            ApplyWorldState();
            LogWorldState("Vision target found");
            return;
        }

        NormalWorldVisionTarget2D legacyCoveredTarget;
        if (!TryFindCoveredLegacyVisionTarget(proposedArea, out legacyCoveredTarget, out coverageRatio))
        {
            // 新旧目标都没有命中时才算失败，并推动 SAN 失败计数。
            HandleVisionFailure();
            return;
        }

        hasFoundVisionTarget = true;
        isPreviewing = false;
        hasActiveArea = false;
        legacyCoveredTarget.NotifyVisionFound(coverageRatio);
        RevealSpecialTarget(legacyCoveredTarget.transform, proposedArea, coverageRatio);
        UpdateAreaVisual();
        ApplyWorldState();
        LogWorldState("Vision target found");
    }

    /// <summary>
    /// 瞄准阶段预览：把视界领域（光圈）移动到指定位置，光圈内显示 SpecialWorld，
    /// 光圈外保持 NormalWorld。不扣 SAN、不改变主世界、不判定目标。
    /// </summary>
    public void PreviewArea(Vector2 center)
    {
        if (GetPrimaryWorld() == WorldKind2D.Special)
        {
            ClearPreview();
            return;
        }

        Vector2 currentAreaSize = GetCurrentAreaSize();
        Vector2 halfSize = currentAreaSize * 0.5f;
        activeArea = new Rect(center - halfSize, currentAreaSize);

        bool wasActive = hasActiveArea;
        hasActiveArea = true;
        isPreviewing = true;
        if (!wasActive)
        {
            ApplyWorldState();
        }

        UpdateAreaVisual();
    }

    /// <summary>
    /// 清除瞄准预览区域，让双世界显示回到当前主世界。
    /// </summary>
    public void ClearPreview()
    {
        if (!isPreviewing && !hasActiveArea)
        {
            return;
        }

        isPreviewing = false;
        hasActiveArea = false;
        UpdateAreaVisual();
        ApplyWorldState();
    }

    /// <summary>
    /// 拍照成功后，Normal 目标先保留并被反向侵蚀，SpecialWorld 对应物同步显形。
    /// 动画完成后再隐藏 Normal，揭示状态一直持续到场景重载。
    /// </summary>
    private void RevealSpecialTarget(Transform normalTarget, Rect captureArea, float coverageRatio)
    {
        if (normalTarget == null)
        {
            return;
        }

        WorldPairId pair = normalTarget.GetComponentInParent<WorldPairId>();
        if (pair == null || string.IsNullOrWhiteSpace(pair.PairId))
        {
            return;
        }

        ResolveRoots();
        // 通过 pairId 找 SpecialWorld 中的对应物，这是双世界对象配对的核心依赖。
        Transform specialTarget = FindPairTransform(specialWorldRoot, pair.PairId);
        if (specialTarget == null)
        {
            return;
        }

        Rect normalTargetArea;
        if (!TryGetTransformRect(normalTarget, out normalTargetArea))
        {
            normalTargetArea = new Rect(normalTarget.position, Vector2.one * 0.1f);
        }

        SpriteRenderer[] normalRenderers = normalTarget.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer[] specialRenderers = specialTarget.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < specialRenderers.Length; i++)
        {
            if (specialRenderers[i] != null && !revealedRenderers.Contains(specialRenderers[i]))
            {
                revealedRenderers.Add(specialRenderers[i]);
            }
        }

        VisionRevealErosionEffect2D revealEffect = specialTarget.GetComponent<VisionRevealErosionEffect2D>();
        if (revealEffect == null)
        {
            revealEffect = specialTarget.gameObject.AddComponent<VisionRevealErosionEffect2D>();
        }

        // 显形动画完成后再隐藏 Normal 目标，避免玩家看到目标突然消失。
        revealEffect.Play(normalTarget, captureArea, normalTargetArea, coverageRatio, () =>
        {
            for (int i = 0; i < normalRenderers.Length; i++)
            {
                if (normalRenderers[i] != null && !hiddenRenderers.Contains(normalRenderers[i]))
                {
                    hiddenRenderers.Add(normalRenderers[i]);
                }
            }

            ApplyWorldState();
        });
    }

    private static bool TryGetTransformRect(Transform target, out Rect rect)
    {
        rect = default;
        if (target == null)
        {
            return false;
        }

        Bounds bounds = default;
        bool hasBounds = false;
        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D targetCollider = colliders[i];
            if (targetCollider == null || !targetCollider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetCollider.bounds);
            }
        }

        if (!hasBounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        rect = new Rect(bounds.min.x, bounds.min.y, Mathf.Max(0.001f, bounds.size.x), Mathf.Max(0.001f, bounds.size.y));
        return true;
    }

    private static Transform FindPairTransform(Transform worldRoot, string pairId)
    {
        if (worldRoot == null || string.IsNullOrWhiteSpace(pairId))
        {
            return null;
        }

        WorldPairId[] pairs = worldRoot.GetComponentsInChildren<WorldPairId>(true);
        for (int i = 0; i < pairs.Length; i++)
        {
            WorldPairId candidate = pairs[i];
            if (candidate != null &&
                string.Equals(candidate.PairId, pairId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.transform;
            }
        }

        return null;
    }

    /// <summary>
    /// 对外保留的交互判断入口，内部转给 IsInteractionAllowedForPlayer。
    /// </summary>
    public bool IsInteractionAllowed(Transform target)
    {
        return IsInteractionAllowedForPlayer(target);
    }

    /// <summary>
    /// 判断玩家当前是否可以和某个对象交互。
    /// 过渡中、视界区域内、非当前主世界对象都会被限制。
    /// </summary>
    public bool IsInteractionAllowedForPlayer(Transform target)
    {
        if (IsTransitioning)
        {
            return false;
        }

        if (target == null)
        {
            return true;
        }

        WorldKind2D worldKind;
        if (!TryGetWorldKind(target, out worldKind))
        {
            return true;
        }

        bool isPrimaryWorld = worldKind == GetPlayerWorld();
        bool insideSwapArea = hasActiveArea && IsTransformInsideActiveArea(target);
        if (insideSwapArea)
        {
            // 视界预览区域内只负责展示另一个世界，不允许玩家直接隔着视界交互。
            return false;
        }

        if (worldKind == WorldKind2D.Normal && IsNormalInteractionSuppressedByReveal(target))
        {
            return false;
        }

        return isPrimaryWorld;
    }

    private bool IsNormalInteractionSuppressedByReveal(Transform target)
    {
        if (target == null || GetPrimaryWorld() != WorldKind2D.Normal)
        {
            return false;
        }

        if (HasHiddenRendererInChildren(target))
        {
            return true;
        }

        WorldPairId pair = target.GetComponentInParent<WorldPairId>();
        if (pair == null || string.IsNullOrWhiteSpace(pair.PairId))
        {
            return false;
        }

        Transform specialTarget = FindPairTransform(specialWorldRoot, pair.PairId);
        return specialTarget != null && HasRevealedRendererInChildren(specialTarget);
    }

    private bool HasHiddenRendererInChildren(Transform target)
    {
        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = hiddenRenderers[i];
            if (spriteRenderer != null && spriteRenderer.transform.IsChildOf(target))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasRevealedRendererInChildren(Transform target)
    {
        for (int i = 0; i < revealedRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = revealedRenderers[i];
            if (spriteRenderer != null &&
                spriteRenderer.enabled &&
                spriteRenderer.gameObject.activeInHierarchy &&
                spriteRenderer.transform.IsChildOf(target))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsWorldPointInsideActiveArea(Vector2 worldPoint)
    {
        return hasActiveArea && activeArea.Contains(worldPoint);
    }

    /// <summary>
    /// 玩家当前所在世界。当前实现里它等同于主世界。
    /// </summary>
    public WorldKind2D GetPlayerWorld()
    {
        return GetPrimaryWorld();
    }

    /// <summary>
    /// 当前显示和交互的主世界。
    /// </summary>
    public WorldKind2D GetPrimaryWorld()
    {
        return normalIsPrimary ? WorldKind2D.Normal : WorldKind2D.Special;
    }

    /// <summary>
    /// 把 SAN 恢复到指定数值，并同步内部失败次数和 UI。
    /// </summary>
    public void RestoreSanTo(int sanValue)
    {
        int clampedSan = Mathf.Clamp(sanValue, 0, 100);
        // SAN 在本项目里用“还能失败几次”近似表达，恢复 SAN 就反推 usesSinceFlip。
        int restoredUses = Mathf.Clamp(
            Mathf.CeilToInt(usesBeforeWorldFlip * (clampedSan / 100f)),
            0,
            usesBeforeWorldFlip);
        usesSinceFlip = Mathf.Clamp(usesBeforeWorldFlip - restoredUses, 0, usesBeforeWorldFlip);
        UpdateSanUI();
        LogWorldState($"San restored to {clampedSan}");
    }

    /// <summary>
    /// 从 SpecialWorld 返回 NormalWorld。
    /// 用于恢复目标确认后的回归流程，也会清掉临时视界区域。
    /// </summary>
    public void ReturnToNormalWorld()
    {
        hasActiveArea = false;
        UpdateAreaVisual();
        UpdateSanUI();

        if (GetPrimaryWorld() == WorldKind2D.Normal)
        {
            ApplyWorldState();
            LogWorldState("Already in NormalWorld");
            return;
        }

        StartWorldTransition(WorldKind2D.Normal);
    }

    /// <summary>
    /// 带过渡的世界切换：
    /// - 进入 SpecialWorld：快速冲击（黑/红/青），峰值切换；
    /// - 返回 NormalWorld：逐渐恢复（侵蚀残留淡出 + Volume 交叉淡化）。
    /// 过渡期间 IsTransitioning 为 true，屏蔽交互。
    /// </summary>
    public void StartWorldTransition(WorldKind2D target)
    {
        if (IsTransitioning)
        {
            return;
        }

        if (target == GetPrimaryWorld())
        {
            ApplyWorldState();
            return;
        }

        IsTransitioning = true;
        hasActiveArea = false;
        UpdateAreaVisual();

        if (target == WorldKind2D.Special)
        {
            GameAudioManager.Instance?.PlayTransitionToSpecial();
            // SAN 耗尽翻转进入 SpecialWorld 时，主动退出摄像机模式，
            // 否则玩家仍处于瞄准状态，长按恢复会被 IsCameraModeActive 拦截。
            CameraFocusModeController.Instance?.ExitCameraMode();

            WorldTransitionFX fx = EnsureTransitionFX();
            fx.PlayEnterSpecialImpact(
                () =>
                {
                    SetPrimaryWorld(WorldKind2D.Special);
                    ApplyWorldState();
                    // 进入 SpecialWorld 也做后处理风格交叉淡化，和黑幕淡出同步
                    WorldVisualStyleController style = FindAnyObjectByType<WorldVisualStyleController>(FindObjectsInactive.Include);
                    if (style != null)
                    {
                        style.BeginCrossfade(WorldKind2D.Special, 0.8f);
                    }
                },
                () =>
                {
                    IsTransitioning = false;
                    LogWorldState("Transition to SpecialWorld finished");
                });
            return;
        }

        SetPrimaryWorld(WorldKind2D.Normal);
        ApplyWorldState();
        UpdateSanUI();
        GameAudioManager.Instance?.PlayTransitionToNormal();

        float duration = 1.6f;
        WorldTransitionFX transitionFx = EnsureTransitionFX();
        transitionFx.PlayReturnNormalRecovery(duration, () =>
        {
            IsTransitioning = false;
            LogWorldState("Transition to NormalWorld finished");
        });

        WorldVisualStyleController style =
            FindAnyObjectByType<WorldVisualStyleController>(FindObjectsInactive.Include);
        if (style != null)
        {
            style.BeginCrossfade(WorldKind2D.Normal, duration);
        }
    }

    private WorldTransitionFX EnsureTransitionFX()
    {
        WorldTransitionFX fx = GetComponent<WorldTransitionFX>();
        if (fx == null)
        {
            fx = gameObject.AddComponent<WorldTransitionFX>();
        }

        return fx;
    }

    /// <summary>
    /// 重新收集 NormalWorld / SpecialWorld 下的 SpriteRenderer 和交互组件。
    /// 场景加载或运行时注入组件后需要调用，确保显示状态应用到最新对象。
    /// </summary>
    public void RefreshTrackedObjects()
    {
        ResolveRoots();
        EnsureWorldRootsAreActive();
        normalRenderers.Clear();
        specialRenderers.Clear();
        normalNpcInteractions.Clear();
        specialNpcInteractions.Clear();

        CollectWorldObjects(normalWorldRoot, normalRenderers, normalNpcInteractions);
        CollectWorldObjects(specialWorldRoot, specialRenderers, specialNpcInteractions);
    }

    private void ClearArea()
    {
        hasActiveArea = false;
        UpdateAreaVisual();
        ApplyWorldState();
        UpdateSanUI();
        LogWorldState("Area cleared");
    }

    private void HandleVisionFailure()
    {
        hasActiveArea = false;
        usesSinceFlip++;
        GameAudioManager.Instance?.PlayPhotoFail();
        float failureIntensity = Mathf.Clamp01(usesSinceFlip / (float)Mathf.Max(1, usesBeforeWorldFlip));
        bool exhausted = usesSinceFlip >= usesBeforeWorldFlip;
        VisionFailureFeedbackController.EnsureExists().PlayFailure(failureIntensity, exhausted);

        if (exhausted)
        {
            // SAN 耗尽前先发事件，让 SpecialWorldSurvivalController2D 有机会接管死亡/倒计时规则。
            UpdateSanUI();
            PlayFailureShake();

            WorldSanExhaustedEventArgs exhaustedArgs = new WorldSanExhaustedEventArgs(GetPrimaryWorld());
            SanExhausted?.Invoke(exhaustedArgs);
            if (exhaustedArgs.CancelDefaultResolution)
            {
                LogWorldState("San exhausted; default world flip cancelled");
                return;
            }

            WorldKind2D nextWorld = normalIsPrimary ? WorldKind2D.Special : WorldKind2D.Normal;
            usesSinceFlip = nextWorld == WorldKind2D.Special ? usesBeforeWorldFlip : 0;
            UpdateSanUI();
            UpdateAreaVisual();
            StartWorldTransition(nextWorld);
            LogWorldState("Vision failures exhausted; world flipped");
            return;
        }

        UpdateAreaVisual();
        ApplyWorldState();
        UpdateSanUI();
        PlayFailureShake();
        LogWorldState("Vision target missed");
    }

    private Vector2 GetCurrentAreaSize()
    {
        if (!matchAreaToCameraReticle)
        {
            return areaSize;
        }

        Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            return areaSize;
        }

        CameraFocusModeController focusMode = CameraFocusModeController.Instance;
        if (focusMode == null || !focusMode.TryGetReticleScreenSize(out Vector2 reticleScreenSize))
        {
            return areaSize;
        }

        int pixelWidth = Mathf.Max(1, camera.pixelWidth);
        int pixelHeight = Mathf.Max(1, camera.pixelHeight);

        if (camera.orthographic)
        {
            float worldHeight = camera.orthographicSize * 2f;
            float worldWidth = worldHeight * camera.aspect;
            return new Vector2(
                Mathf.Max(0.1f, worldWidth * reticleScreenSize.x / pixelWidth),
                Mathf.Max(0.1f, worldHeight * reticleScreenSize.y / pixelHeight));
        }

        float distance = Mathf.Abs(camera.transform.position.z);
        Vector3 center = new Vector3(pixelWidth * 0.5f, pixelHeight * 0.5f, distance);
        Vector3 left = camera.ScreenToWorldPoint(center + new Vector3(-reticleScreenSize.x * 0.5f, 0f, 0f));
        Vector3 right = camera.ScreenToWorldPoint(center + new Vector3(reticleScreenSize.x * 0.5f, 0f, 0f));
        Vector3 bottom = camera.ScreenToWorldPoint(center + new Vector3(0f, -reticleScreenSize.y * 0.5f, 0f));
        Vector3 top = camera.ScreenToWorldPoint(center + new Vector3(0f, reticleScreenSize.y * 0.5f, 0f));
        return new Vector2(
            Mathf.Max(0.1f, Mathf.Abs(right.x - left.x)),
            Mathf.Max(0.1f, Mathf.Abs(top.y - bottom.y)));
    }

    private void PlayFailureShake()
    {
        Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            return;
        }

        CameraFollow2D cameraFollow = camera.GetComponent<CameraFollow2D>();
        if (cameraFollow != null)
        {
            cameraFollow.PlayImpactShake(failureShakeDuration, failureShakeAmplitude);
        }
    }

    private bool TryFindCoveredVisionTarget(Rect proposedArea, out WorldInteractionTarget2D coveredTarget, out float coverageRatio)
    {
        coveredTarget = null;
        coverageRatio = 0f;
        ResolveRoots();

        if (normalWorldRoot == null)
        {
            return false;
        }

        visionTargets.Clear();
        normalWorldRoot.GetComponentsInChildren(true, visionTargets);

        // 多个候选同时被视界框覆盖时，选择覆盖率最高且达到自身阈值的目标。
        for (int i = 0; i < visionTargets.Count; i++)
        {
            WorldInteractionTarget2D candidate = visionTargets[i];
            if (candidate == null || !candidate.IsNormalVisionTarget)
            {
                continue;
            }

            float candidateCoverage;
            candidate.IsCoveredBy(proposedArea, out candidateCoverage);
            if (candidateCoverage <= coverageRatio)
            {
                continue;
            }

            coverageRatio = candidateCoverage;
            coveredTarget = candidate;
        }

        return coveredTarget != null && coverageRatio >= coveredTarget.CoverageThreshold;
    }

    private bool TryFindCoveredLegacyVisionTarget(
        Rect proposedArea,
        out NormalWorldVisionTarget2D coveredTarget,
        out float coverageRatio)
    {
        coveredTarget = null;
        coverageRatio = 0f;
        ResolveRoots();

        if (normalWorldRoot == null)
        {
            return false;
        }

        legacyVisionTargets.Clear();
        normalWorldRoot.GetComponentsInChildren(true, legacyVisionTargets);

        // 兼容旧版 NormalWorldVisionTarget2D，避免早期场景没有迁移时直接失效。
        for (int i = 0; i < legacyVisionTargets.Count; i++)
        {
            NormalWorldVisionTarget2D candidate = legacyVisionTargets[i];
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            float candidateCoverage;
            candidate.IsCoveredBy(proposedArea, out candidateCoverage);
            if (candidateCoverage <= coverageRatio)
            {
                continue;
            }

            coverageRatio = candidateCoverage;
            coveredTarget = candidate;
        }

        return coveredTarget != null && coverageRatio >= coveredTarget.CoverageThreshold;
    }

    private void UpdateSanUI()
    {
        int remainingUses = Mathf.Max(0, usesBeforeWorldFlip - usesSinceFlip);
        SanBrainUIController.SetBrainState(remainingUses, usesBeforeWorldFlip);
    }

    private void LogWorldState(string source)
    {
        if (!logWorldDiagnostics)
        {
            return;
        }

        Debug.Log($"[WorldSwapManager2D] {source}: primaryWorld={GetPrimaryWorld()}, hasActiveArea={hasActiveArea}, usesSinceFlip={usesSinceFlip}");
    }

    private void SetPrimaryWorld(WorldKind2D world)
    {
        WorldKind2D previous = GetPrimaryWorld();
        normalIsPrimary = world == WorldKind2D.Normal;
        WorldKind2D current = GetPrimaryWorld();
        if (previous == current)
        {
            return;
        }

        PrimaryWorldChanged?.Invoke(current);
        if (current == WorldKind2D.Special)
        {
            SpecialWorldEntered?.Invoke();
        }
        else
        {
            ReturnedToNormalWorld?.Invoke();
        }
    }

    private void ResolveRoots()
    {
        if (normalWorldRoot == null)
        {
            normalWorldRoot = FindLoadedSceneRoot("NormalWorld");
        }

        if (specialWorldRoot == null)
        {
            specialWorldRoot = FindLoadedSceneRoot("SpecialWorld");
        }
    }

    private void EnsureWorldRootsAreActive()
    {
        if (normalWorldRoot != null && !normalWorldRoot.gameObject.activeSelf)
        {
            normalWorldRoot.gameObject.SetActive(true);
        }

        if (specialWorldRoot != null && !specialWorldRoot.gameObject.activeSelf)
        {
            specialWorldRoot.gameObject.SetActive(true);
        }
    }

    private static Transform FindLoadedSceneRoot(string rootName)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == rootName);
            if (root != null)
            {
                return root.transform;
            }
        }

        return null;
    }

    private static void CollectWorldObjects(
        Transform root,
        List<SpriteRenderer> renderers,
        List<NpcInteraction2D> npcInteractions)
    {
        if (root == null)
        {
            return;
        }

        root.GetComponentsInChildren(true, renderers);
        root.GetComponentsInChildren(true, npcInteractions);
    }

    private void ApplyWorldState()
    {
        // 统一入口：先按当前主世界/视界区域刷新两套世界，再叠加拍照成功后的强制显隐状态。
        ApplyFrontState(WorldKind2D.Normal, normalRenderers, normalNpcInteractions);
        ApplyFrontState(WorldKind2D.Special, specialRenderers, specialNpcInteractions);
        ApplyForcedRendererState();
    }

    private void ApplyForcedRendererState()
    {
        // revealedRenderers / hiddenRenderers 是拍照成功后的长期状态，不受临时视界区域影响。
        for (int i = 0; i < revealedRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = revealedRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.enabled = true;
            spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
        }

        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = hiddenRenderers[i];
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
        }
    }

    private void ResetRendererState()
    {
        ResetRendererState(normalRenderers);
        ResetRendererState(specialRenderers);
    }

    private static void ResetRendererState(List<SpriteRenderer> renderers)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            spriteRenderer.enabled = true;
        }
    }

    private void ApplyFrontState(
        WorldKind2D worldKind,
        List<SpriteRenderer> renderers,
        List<NpcInteraction2D> npcInteractions)
    {
        bool isPrimaryWorld = worldKind == GetPrimaryWorld();

        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.enabled = hasActiveArea || isPrimaryWorld;
            if (!hasActiveArea)
            {
                // 没有视界区域时，只显示主世界；SpriteMask 不参与。
                spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
            else
            {
                // 有视界区域时，主世界在 mask 外可见，副世界在 mask 内可见，形成局部双世界叠加。
                spriteRenderer.maskInteraction = isPrimaryWorld
                    ? SpriteMaskInteraction.VisibleOutsideMask
                    : SpriteMaskInteraction.VisibleInsideMask;
            }

            RefreshSpriteBinding(spriteRenderer);
        }

        for (int i = 0; i < npcInteractions.Count; i++)
        {
            NpcInteraction2D npcInteraction = npcInteractions[i];
            if (npcInteraction == null)
            {
                continue;
            }

            npcInteraction.SetWorldInteractionAllowed(IsInteractionAllowed(npcInteraction.transform));
        }
    }

    private static void RefreshSpriteBinding(SpriteRenderer spriteRenderer)
    {
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null)
        {
            return;
        }

        // Unity 6 URP 2D Renderer 在切换 SpriteMask 状态后，偶尔会保留错误的纹理绑定。
        // 这里不涉及自定义图形学实现，只是通过临时置空再赋回 sprite，强制 SpriteRenderer 刷新绑定。
        spriteRenderer.sprite = null;
        spriteRenderer.sprite = sprite;
    }

    private bool TryGetWorldKind(Transform target, out WorldKind2D worldKind)
    {
        if (normalWorldRoot != null && target.IsChildOf(normalWorldRoot))
        {
            worldKind = WorldKind2D.Normal;
            return true;
        }

        if (specialWorldRoot != null && target.IsChildOf(specialWorldRoot))
        {
            worldKind = WorldKind2D.Special;
            return true;
        }

        worldKind = WorldKind2D.Normal;
        return false;
    }

    private bool IsTransformInsideActiveArea(Transform target)
    {
        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null)
        {
            return BoundsIntersectsActiveArea(collider.bounds);
        }

        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            return BoundsIntersectsActiveArea(renderer.bounds);
        }

        Vector3 position = target.position;
        return activeArea.Contains(new Vector2(position.x, position.y));
    }

    private bool BoundsIntersectsActiveArea(Bounds bounds)
    {
        if (!hasActiveArea)
        {
            return false;
        }

        Rect boundsRect = new Rect(
            bounds.min.x,
            bounds.min.y,
            Mathf.Max(0.001f, bounds.size.x),
            Mathf.Max(0.001f, bounds.size.y));

        return activeArea.Overlaps(boundsRect, true);
    }

    private void BuildAreaVisual()
    {
        Transform existing = transform.Find("ActiveSwapArea");
        GameObject areaObject = existing != null ? existing.gameObject : new GameObject("ActiveSwapArea");
        areaObject.transform.SetParent(transform, false);

        areaMask = areaObject.GetComponent<SpriteMask>();
        if (areaMask == null)
        {
            areaMask = areaObject.AddComponent<SpriteMask>();
        }

        Sprite whiteSprite = CreateWhiteSprite();
        areaMask.sprite = whiteSprite;
        areaMask.isCustomRangeActive = true;
        areaMask.frontSortingLayerID = SortingLayer.NameToID("UI");
        areaMask.frontSortingOrder = 32767;
        areaMask.backSortingLayerID = SortingLayer.NameToID("Default");
        areaMask.backSortingOrder = -32768;

        UpdateAreaVisual();
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private void UpdateAreaVisual()
    {
        if (areaMask == null)
        {
            return;
        }

        if (!hasActiveArea)
        {
            return;
        }

        Vector2 center = activeArea.center;
        areaMask.transform.position = new Vector3(center.x, center.y, 0f);
        areaMask.transform.localScale = new Vector3(activeArea.width, activeArea.height, 1f);
    }
}
