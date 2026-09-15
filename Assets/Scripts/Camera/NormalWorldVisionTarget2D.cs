using System;
using UnityEngine;

/// <summary>
/// NormalWorld 视界目标兼容组件。
/// 新流程优先使用 WorldInteractionTarget2D，但这个脚本保留给已有场景对象和教程任务兼容。
/// </summary>
[DisallowMultipleComponent]
public sealed class NormalWorldVisionTarget2D : MonoBehaviour
{
    /// <summary>当某个视界目标被成功找到时触发（教程任务判定使用）。</summary>
    public static event Action<NormalWorldVisionTarget2D> VisionTargetFound;

    [SerializeField, Range(0.01f, 1f)] private float coverageThreshold = 0.1f;
    [SerializeField] private bool logOnFound = true;

    public float CoverageThreshold => Mathf.Clamp01(coverageThreshold);

    /// <summary>
    /// 与 WorldInteractionTarget2D 使用同一套覆盖率思路：
    /// 视界框覆盖目标范围达到阈值才算找到，而不是只判断鼠标点击点。
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

    public void NotifyVisionFound(float coverageRatio)
    {
        VisionTargetFound?.Invoke(this);
        if (!logOnFound)
        {
            return;
        }

        Debug.Log($"成功找到视界目标: {name}, coverage={coverageRatio:P0}", this);
    }

    private bool TryGetTargetRect(out Rect targetRect)
    {
        Bounds bounds = default;
        bool hasBounds = false;

        // Collider2D 是优先判定范围，便于手动调整；没有 Collider 时才使用 Renderer 的可见范围。
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

        // 分母使用目标面积，含义是“目标有多少比例被视界拍到”。
        return Mathf.Clamp01(overlapArea / targetArea);
    }
}
