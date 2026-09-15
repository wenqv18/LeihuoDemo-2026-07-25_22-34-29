using UnityEngine;

/// <summary>
/// 双世界通用目标组件。
/// NormalWorld 中作为“视界可发现目标”，SpecialWorld 中作为“可长按恢复目标”。
/// LevelRuntimeConfigurator 会根据 JSON 结果配置它，避免手动在场景里频繁开关组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionTarget2D : MonoBehaviour
{
    // world 决定这个目标属于普通世界还是特殊世界；两个布尔值决定它在该世界承担哪种玩法职责。
    [SerializeField] private WorldKind2D world = WorldKind2D.Normal;
    [SerializeField] private bool normalVisionTarget;
    [SerializeField] private bool specialRecoveryTarget;

    // 视界框覆盖目标多少比例才算命中。默认 0.1 是为了让玩家拍到目标的一部分也能成立。
    [SerializeField, Range(0.01f, 1f)] private float coverageThreshold = 0.1f;
    [SerializeField] private bool logOnVisionFound = true;

    public WorldKind2D World => world;
    public bool IsNormalVisionTarget => enabled && gameObject.activeInHierarchy && world == WorldKind2D.Normal && normalVisionTarget;
    public bool IsSpecialRecoveryTarget => enabled && gameObject.activeInHierarchy && world == WorldKind2D.Special && specialRecoveryTarget;
    public float CoverageThreshold => Mathf.Clamp01(coverageThreshold);

    /// <summary>
    /// 由运行时配置器调用。isActiveTarget 为 false 时会禁用组件，
    /// 这样候选目标可以保留在场景里，但只有本层被选中的目标参与玩法判定。
    /// </summary>
    public void Configure(WorldKind2D targetWorld, bool isActiveTarget)
    {
        world = targetWorld;
        normalVisionTarget = targetWorld == WorldKind2D.Normal && isActiveTarget;
        specialRecoveryTarget = targetWorld == WorldKind2D.Special && isActiveTarget;
        enabled = isActiveTarget;
    }

    /// <summary>
    /// 判断视界矩形是否覆盖该目标。
    /// 使用覆盖率而不是鼠标点命中，是因为玩家看到的是一个取景框区域，
    /// 大小不同的家具目标也能用同一套阈值逻辑处理。
    /// </summary>
    public bool IsCoveredBy(Rect visionArea, out float coverageRatio)
    {
        Rect targetRect;
        if (!TryGetTargetRect(out targetRect))
        {
            Vector2 position = transform.position;
            coverageRatio = visionArea.Contains(position) ? 1f : 0f;
            return coverageRatio >= CoverageThreshold;
        }

        coverageRatio = CalculateCoverageRatio(visionArea, targetRect);
        return coverageRatio >= CoverageThreshold;
    }

    /// <summary>
    /// 判断鼠标世界坐标是否压在目标上，SpecialWorld 长按恢复会用到。
    /// 优先用 Collider2D，因为它可以由设计者精确调整；没有碰撞体时再用 Renderer bounds 兜底。
    /// </summary>
    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D targetCollider = colliders[i];
            if (targetCollider == null ||
                !targetCollider.enabled ||
                !targetCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (targetCollider.OverlapPoint(worldPoint))
            {
                return true;
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null ||
                !targetRenderer.enabled ||
                !targetRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (targetRenderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, targetRenderer.bounds.center.z)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 目标被视界发现后的通知入口，目前主要用于日志和后续教程/任务监听。
    /// </summary>
    public void NotifyVisionFound(float coverageRatio)
    {
        if (!logOnVisionFound)
        {
            return;
        }

        Debug.Log($"Vision target found: {name}, coverage={coverageRatio:P0}", this);
    }

    public static WorldInteractionTarget2D FindSpecialRecoveryTargetInParents(Component source)
    {
        if (source == null)
        {
            return null;
        }

        WorldInteractionTarget2D target = source.GetComponentInParent<WorldInteractionTarget2D>();
        return target != null && target.IsSpecialRecoveryTarget ? target : null;
    }

    private bool TryGetTargetRect(out Rect targetRect)
    {
        Bounds bounds = default;
        bool hasBounds = false;

        // 先合并所有启用的 Collider2D 作为判定范围，方便在 Inspector 中微调真实交互区域。
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
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
            // 没有 Collider2D 时，用可见 Renderer 的 bounds 兜底，避免纯美术物体完全无法被识别。
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
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
            targetRect = default;
            return false;
        }

        targetRect = new Rect(
            bounds.min.x,
            bounds.min.y,
            Mathf.Max(0.001f, bounds.size.x),
            Mathf.Max(0.001f, bounds.size.y));
        return true;
    }

    private static float CalculateCoverageRatio(Rect visionArea, Rect targetRect)
    {
        if (!visionArea.Overlaps(targetRect, true))
        {
            return 0f;
        }

        float minX = Mathf.Max(visionArea.xMin, targetRect.xMin);
        float maxX = Mathf.Min(visionArea.xMax, targetRect.xMax);
        float minY = Mathf.Max(visionArea.yMin, targetRect.yMin);
        float maxY = Mathf.Min(visionArea.yMax, targetRect.yMax);
        float overlapArea = Mathf.Max(0f, maxX - minX) * Mathf.Max(0f, maxY - minY);
        float targetArea = Mathf.Max(0.001f, targetRect.width * targetRect.height);

        // 返回“目标自身被拍到多少”，而不是“视界框被占多少”，所以分母是 targetArea。
        return Mathf.Clamp01(overlapArea / targetArea);
    }
}
