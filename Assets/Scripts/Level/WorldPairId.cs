using UnityEngine;

/// <summary>
/// 双世界对象的配对 ID。
/// NormalWorld 和 SpecialWorld 中代表同一“位置/家具/目标”的对象使用同一个 pairId，
/// 运行时才能从普通世界目标找到特殊世界对应物，Editor 工具也能据此做差异检查。
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldPairId : MonoBehaviour
{
    [SerializeField] private string pairId;

    public string PairId => pairId;

#if UNITY_EDITOR
    /// <summary>
    /// 仅供编辑器工具批量生成或同步 pairId，运行时不应该主动改这个值。
    /// </summary>
    public void SetPairIdInEditor(string value)
    {
        pairId = value;
    }
#endif
}
