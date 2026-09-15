using UnityEngine;

/// <summary>
/// SpecialWorld 交互目标兼容标记。
/// 新流程优先使用 WorldInteractionTarget2D 的 SpecialRecoveryTarget 配置；
/// 这个空组件保留给已有场景和 LevelDataWindow 校验逻辑做兼容标记。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialWorldInteractionTarget2D : MonoBehaviour
{
}
