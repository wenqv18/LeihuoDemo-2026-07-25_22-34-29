using System;
using System.Collections.Generic;

/// <summary>
/// 单个关卡 JSON 模板的数据结构。
/// 这里保存的是“这层内容怎么配置”，不是玩家当前进度；运行时会由
/// RunSaveService 选出模板，再由 LevelRuntimeConfigurator 注入到场景对象。
/// </summary>
[Serializable]
public sealed class LevelTemplateData
{
    // version 方便以后扩展 JSON 字段时做兼容处理。
    public int version = 1;

    // difficulty 决定模板属于 1-3、4-6、7-9 哪个难度段。
    public int difficulty = 1;

    // templateId 是存档引用模板的稳定 ID，避免直接依赖文件名。
    public string templateId;

    // sceneName 指向实际复用的 Unity 场景，例如 Level_01 / Level_02 / Level_03。
    public string sceneName;

    // 旧版目标列表：只配置 NormalWorld 目标时使用。
    public List<string> targetPairIds = new List<string>();

    // 旧版特殊目标列表：未配置 targetRoutes 时作为 SpecialWorld 目标池。
    public List<string> specialTargetPairIds = new List<string>();

    // 新版路线表：明确指定 NormalWorld 异常目标与 SpecialWorld 恢复目标的对应关系。
    public List<LevelTargetRouteData> targetRoutes = new List<LevelTargetRouteData>();

    // 家具文本配置按 pairId 绑定，让同一个场景能在不同模板下替换文本。
    public List<FurnitureDialogueData> furniture = new List<FurnitureDialogueData>();

    /// <summary>
    /// 按 pairId 查找家具文本。忽略大小写是为了降低 JSON 手写时的大小写错误影响。
    /// </summary>
    public FurnitureDialogueData FindFurniture(string pairId)
    {
        if (string.IsNullOrWhiteSpace(pairId) || furniture == null)
        {
            return null;
        }

        for (int i = 0; i < furniture.Count; i++)
        {
            FurnitureDialogueData entry = furniture[i];
            if (entry != null && string.Equals(entry.pairId, pairId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }
}

/// <summary>
/// 目标路线配置：玩家在 NormalWorld 找到哪个异常物，就激活 SpecialWorld 的哪个恢复目标。
/// </summary>
[Serializable]
public sealed class LevelTargetRouteData
{
    public string normalTargetPairId;
    public string specialTargetPairId;
}

/// <summary>
/// 同一件家具在不同世界、不同目标状态下的文本。
/// 普通家具和目标家具分开写，是为了避免目标物被发现前后还讲普通台词。
/// </summary>
[Serializable]
public sealed class FurnitureDialogueData
{
    public string pairId;
    public List<string> normalLines = new List<string>();
    public List<string> specialLines = new List<string>();
    public List<string> normalTargetLines = new List<string>();
    public List<string> specialTargetLines = new List<string>();

    /// <summary>
    /// 根据当前世界和是否为本层目标，选择实际播放的文本列表。
    /// 目标文本没有配置时回退到普通文本，保证 JSON 少字段时也能正常运行。
    /// </summary>
    public IReadOnlyList<string> GetLines(WorldKind2D world, bool isTarget)
    {
        if (world == WorldKind2D.Special)
        {
            return isTarget && specialTargetLines != null && specialTargetLines.Count > 0
                ? specialTargetLines
                : specialLines;
        }

        return isTarget && normalTargetLines != null && normalTargetLines.Count > 0
            ? normalTargetLines
            : normalLines;
    }
}
