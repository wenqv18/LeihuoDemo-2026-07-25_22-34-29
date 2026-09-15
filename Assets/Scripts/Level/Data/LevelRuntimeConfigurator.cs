using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡运行时配置器。
/// 它的职责是把 RunSaveService 选出的 FloorSaveData / LevelTemplateData
/// 应用到当前 Unity 场景：设置本层目标、特殊世界恢复目标和家具对话。
///
/// 可以把这个类理解成“存档和 JSON 数据”到“当前场景对象”的翻译层：
/// 1. RunSaveService 决定现在是第几层、使用哪份关卡模板、本层目标是谁。
/// 2. LevelTemplateData 提供目标候选、目标路线、家具对话等配置。
/// 3. 当前 Unity 场景只负责摆放 NormalWorld / SpecialWorld 里的物体。
/// 4. 本类在场景加载后，把数据写回具体物体组件，避免每层都复制一张场景。
///
/// 注意：这里不负责选择楼层，也不负责双世界切换本身。
/// 选择楼层在 RunSaveService；双世界玩法在 WorldSwapManager2D。
/// </summary>
public static class LevelRuntimeConfigurator
{
    /// <summary>
    /// 场景加载后调用。正式 run 中读取当前楼层；没有正式存档时创建 preview floor，
    /// 方便在 Unity 里直接打开 Level_01 / Level_02 / Level_03 做单场景调试。
    ///
    /// 整体流程：
    /// 1. 先向 RunSaveService 要当前楼层 floor 和对应模板 template。
    /// 2. 如果没有正式 run，就为当前场景临时创建一个 preview floor，方便编辑器直接播放测试。
    /// 3. 校验“当前打开的场景名”和“floor 记录的场景名”是否一致，防止数据注入到错的场景。
    /// 4. 找到 NormalWorld 和 SpecialWorld 根节点，分别应用普通世界和特殊世界的数据。
    /// 5. 正式 run 下恢复背包、生成尸体；preview 模式不做这些正式流程。
    /// 6. 最后通知 WorldSwapManager2D 重新扫描目标，因为本类可能刚刚添加/启用了目标组件。
    /// </summary>
    public static FloorSaveData Apply(Scene scene)
    {
        // template 是 JSON 解析出来的关卡模板；floor 是当前 run 里这一层的运行时状态。
        // 例如：这一层用哪个 templateId、普通世界目标 pairId 是什么、特殊世界恢复目标 pairId 是什么。
        LevelTemplateData template;
        FloorSaveData floor = RunSaveService.EnsureCurrentFloor(out template);

        // previewMode 用来区分“正式流程进入关卡”和“直接在 Unity 里打开某张 Level 场景测试”。
        // 直接打开场景时可能没有 run-save，此时就创建一份临时 floor，只做场景注入，不恢复正式存档内容。
        bool previewMode = false;
        if (floor == null)
        {
            floor = RunSaveService.CreatePreviewFloor(scene.name, out template);
            previewMode = floor != null;
        }

        if (floor == null)
        {
            // 找不到 floor 通常说明：当前场景不是关卡场景，或者 Resources/LevelData 里没有对应模板。
            // 返回 null，让调用方知道这次没有成功配置关卡。
            return null;
        }

        if (!string.Equals(scene.name, floor.levelSceneName, StringComparison.OrdinalIgnoreCase))
        {
            // 防止存档当前楼层和实际打开的 Unity 场景不一致，避免把 Level_02 的数据注入 Level_01。
            Debug.LogError($"[LevelRuntimeConfigurator] Floor {floor.floorNumber} expects scene {floor.levelSceneName}, not {scene.name}.");
            return null;
        }

        // 场景约定：普通世界物体放在 NormalWorld 根节点下，特殊世界物体放在 SpecialWorld 根节点下。
        // 如果某个根节点不存在，ApplyWorld / ApplyDrops 会自行 return，不会让整个流程崩掉。
        Transform normalRoot = FindRoot(scene, "NormalWorld");
        Transform specialRoot = FindRoot(scene, "SpecialWorld");

        // 同一套 ApplyWorld 同时服务普通世界和特殊世界。
        // 差异由 WorldKind2D 参数决定：Normal 处理视界目标，Special 处理恢复目标。
        ApplyWorld(normalRoot, WorldKind2D.Normal, floor, template);
        ApplyWorld(specialRoot, WorldKind2D.Special, floor, template);

        // 掉落物目前放在特殊世界里，并且会根据楼层号配置不同掉落内容。
        ApplyDrops(specialRoot, floor.floorNumber);
        if (!previewMode)
        {
            // 正式 run 才恢复跨楼层背包、生成前一次死亡留下的尸体等长期状态。
            // preview 模式跳过这些，避免单场景测试时被旧存档干扰。
            RunSaveService.RestoreInventory();
            CorpseRuntimeSpawner.Spawn(scene, floor, normalRoot, specialRoot);
        }

        // 目标组件可能是在 ApplyWorld 中动态 AddComponent 或 enable/disable 的。
        // 所以最后让 WorldSwapManager2D 重新扫描一次，保证视界判定拿到的是最新目标列表。
        WorldSwapManager2D.Instance?.RefreshTrackedObjects();
        return floor;
    }

    /// <summary>
    /// 掉落物有楼层差异，这里统一把楼层号传给 SpecialWorld 下的掉落交互组件。
    /// </summary>
    private static void ApplyDrops(Transform specialRoot, int floorNumber)
    {
        if (specialRoot == null)
        {
            // 没有特殊世界根节点时，不配置掉落物。
            // 这里选择安静返回，是为了方便某些测试场景只摆普通世界。
            return;
        }

        // includeInactive = true：即使掉落物默认隐藏，也要能提前配置它属于哪一层。
        DropPickupInteraction2D[] drops = specialRoot.GetComponentsInChildren<DropPickupInteraction2D>(true);
        for (int i = 0; i < drops.Length; i++)
        {
            if (drops[i] != null)
            {
                // ConfigureForFloor 内部根据楼层决定这个掉落物是否可用、掉什么内容。
                drops[i].ConfigureForFloor(floorNumber);
            }
        }
    }

    /// <summary>
    /// 把某一个世界根节点下的对象配置成“本层应该有的状态”。
    ///
    /// worldRoot：
    /// NormalWorld 或 SpecialWorld 的根节点。
    ///
    /// world：
    /// 当前正在配置普通世界还是特殊世界。
    ///
    /// floor：
    /// 当前楼层的运行时结果，里面记录了本层真正选中的目标 pairId。
    ///
    /// template：
    /// JSON 模板，里面记录了哪些 pairId 有资格成为目标，以及不同世界下的家具对话。
    /// </summary>
    private static void ApplyWorld(
        Transform worldRoot,
        WorldKind2D world,
        FloorSaveData floor,
        LevelTemplateData template)
    {
        if (worldRoot == null)
        {
            // 某些调试场景可能只存在一个世界根节点。
            // 根节点不存在就跳过该世界，不影响另一个世界继续配置。
            return;
        }

        // targetCandidates：这一张场景里“可能成为目标”的 pairId 集合。
        // 注意它不等于本层真正目标，只是候选池。
        // 例如 JSON 里可能配置了 5 个普通世界候选目标，但当前 floor 只抽中了其中 1 个。
        HashSet<string> targetCandidates = new HashSet<string>(
            GetTargetCandidatePairIds(template, world),
            StringComparer.OrdinalIgnoreCase);

        // activeTargetPairId：当前楼层真正被选中的目标。
        // 普通世界用 floor.targetPairId；特殊世界用 floor.specialTargetPairId。
        // 这样可以表达“普通世界找到 A 后，特殊世界需要恢复 B”的路线。
        string activeTargetPairId = world == WorldKind2D.Special
            ? floor.specialTargetPairId
            : floor.targetPairId;

        // WorldPairId 是普通世界/特殊世界配对和 JSON 数据注入的基础。
        // 本类不按物体名字找目标，因为名字可能改；按 PairId 更稳定。
        WorldPairId[] pairIds = worldRoot.GetComponentsInChildren<WorldPairId>(true);
        for (int i = 0; i < pairIds.Length; i++)
        {
            WorldPairId pair = pairIds[i];

            // 家具交互组件可能挂在 pair 对象本身，也可能挂在子节点，所以用 GetComponentInChildren。
            FurnitureInteraction2D interaction = pair.GetComponentInChildren<FurnitureInteraction2D>(true);

            // isCandidate 表示“可能成为目标的物体”，isTarget 表示“本层真正被选中的物体”。
            // 这样同一张场景可以存在多个候选目标，但每一层只激活 JSON / seed 选中的那个。
            bool isTarget = string.Equals(pair.PairId, activeTargetPairId, StringComparison.OrdinalIgnoreCase);
            FurnitureDialogueData dialogue = template.FindFurniture(pair.PairId);
            if (interaction != null && dialogue != null)
            {
                // 家具对话也由 JSON 控制。
                // 同一个 pairId 可以根据 Normal/Special、是否为目标，显示不同台词。
                interaction.SetDialogueLines(dialogue.GetLines(world, isTarget));
            }

            bool isCandidate = targetCandidates.Contains(pair.PairId);
            if (world == WorldKind2D.Normal)
            {
                // 新流程使用 WorldInteractionTarget2D 做统一目标组件。
                // NormalWorldVisionTarget2D 作为兼容组件保留，主要为了已有视界/教程逻辑。
                ConfigureUnifiedTarget(pair, WorldKind2D.Normal, isCandidate, isTarget);
                NormalWorldVisionTarget2D target = pair.GetComponent<NormalWorldVisionTarget2D>();
                if (target == null && isCandidate)
                {
                    // 只给候选目标补兼容组件；非候选物体没有必要新增目标组件。
                    target = pair.gameObject.AddComponent<NormalWorldVisionTarget2D>();
                }
                if (target != null)
                {
                    // 兼容组件只在“候选 + 当前真正目标”时启用，避免玩家拍到非目标也触发成功。
                    target.enabled = isCandidate && isTarget;
                }
            }
            else
            {
                // SpecialWorld 的目标不是“被视界发现”，而是“玩家进入特殊世界后可恢复/交互”。
                // 这里同样先配置统一组件，再兼容 SpecialWorld 的空标记组件。
                ConfigureUnifiedTarget(pair, WorldKind2D.Special, isCandidate, isTarget);
                SpecialWorldInteractionTarget2D target = pair.GetComponent<SpecialWorldInteractionTarget2D>();
                if (target == null && isCandidate)
                {
                    // SpecialWorldInteractionTarget2D 是空标记组件，用于兼容已有校验/交互流程。
                    target = pair.gameObject.AddComponent<SpecialWorldInteractionTarget2D>();
                }
                if (target != null)
                {
                    // 只有本层真正的特殊世界恢复目标才启用。
                    target.enabled = isCandidate && isTarget;
                }
            }
        }
    }

    /// <summary>
    /// 配置新流程的统一目标组件 WorldInteractionTarget2D。
    ///
    /// 为什么有这个方法：
    /// 早期项目里普通世界目标和特殊世界目标分别有不同脚本。
    /// 后来为了让 WorldSwapManager2D 统一扫描目标，增加了 WorldInteractionTarget2D。
    /// 这里集中处理“是否添加组件、是否启用组件、目标属于哪个世界”。
    /// </summary>
    private static void ConfigureUnifiedTarget(
        WorldPairId pair,
        WorldKind2D world,
        bool isTargetCandidate,
        bool isActiveTarget)
    {
        // 先尝试拿已有组件。已有组件可能是编辑器里手动挂的，也可能是之前运行时加过的。
        WorldInteractionTarget2D target = pair.GetComponent<WorldInteractionTarget2D>();
        if (!isTargetCandidate)
        {
            // 非候选目标保留组件也没有关系，但要禁用，避免被视界/恢复逻辑误判。
            if (target != null)
            {
                target.enabled = false;
            }

            return;
        }

        if (target == null)
        {
            // 只有候选目标才补统一目标组件，避免场景里所有家具都被当成玩法目标扫描。
            target = pair.gameObject.AddComponent<WorldInteractionTarget2D>();
        }

        // Configure 会根据 world 和 isActiveTarget 设置：
        // - 普通世界是否是视界目标
        // - 特殊世界是否是恢复目标
        // - 当前组件是否启用
        target.Configure(world, isActiveTarget);
    }

    /// <summary>
    /// 在当前场景根对象里查找指定名字的根节点。
    /// 这里不使用 GameObject.Find，是为了限定只在传入的 scene 内查找，避免多场景加载时拿错对象。
    /// </summary>
    private static Transform FindRoot(Scene scene, string rootName)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(gameObject => gameObject.name == rootName);
        return root != null ? root.transform : null;
    }

    /// <summary>
    /// 从 JSON 模板中取出“某个世界的候选目标 pairId”。
    ///
    /// 读取优先级：
    /// 1. 优先读 targetRoutes，因为它能明确表达：
    ///    普通世界目标 normalTargetPairId -> 特殊世界恢复目标 specialTargetPairId。
    /// 2. 如果旧模板没有 targetRoutes，再退回 targetPairIds / specialTargetPairIds。
    /// 3. 如果特殊世界没有 specialTargetPairIds，则沿用 targetPairIds，兼容更早的数据格式。
    /// </summary>
    private static IEnumerable<string> GetTargetCandidatePairIds(LevelTemplateData template, WorldKind2D world)
    {
        if (template?.targetRoutes != null && template.targetRoutes.Count > 0)
        {
            // 优先使用 targetRoutes，因为它能表达“普通世界目标 -> 特殊世界恢复目标”的明确路线。
            foreach (LevelTargetRouteData route in template.targetRoutes)
            {
                // 同一条路线在两个世界读不同字段：
                // NormalWorld 读 normalTargetPairId；SpecialWorld 读 specialTargetPairId。
                string pairId = world == WorldKind2D.Special
                    ? route?.specialTargetPairId
                    : route?.normalTargetPairId;
                if (!string.IsNullOrWhiteSpace(pairId))
                {
                    yield return pairId;
                }
            }

            yield break;
        }

        // 兼容旧模板：没有 targetRoutes 时，普通世界读 targetPairIds，特殊世界读 specialTargetPairIds。
        List<string> configuredTargetPairIds = world == WorldKind2D.Special
            ? template?.specialTargetPairIds
            : template?.targetPairIds;

        if ((configuredTargetPairIds == null || configuredTargetPairIds.Count == 0) &&
            world == WorldKind2D.Special)
        {
            // 兼容旧模板：没有 specialTargetPairIds 时，SpecialWorld 也沿用 targetPairIds。
            configuredTargetPairIds = template?.targetPairIds;
        }

        if (configuredTargetPairIds == null)
        {
            // 没有任何候选配置，直接结束迭代。
            yield break;
        }

        foreach (string pairId in configuredTargetPairIds)
        {
            if (!string.IsNullOrWhiteSpace(pairId))
            {
                // 过滤空字符串，避免 HashSet 里出现无效 pairId。
                yield return pairId;
            }
        }
    }
}
