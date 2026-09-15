using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 本次 run 的存档与楼层计划服务。
/// 它负责生成九层 floor plan、选择每层模板与目标、保存死亡尸体和当前尝试状态。
/// 注意：它不直接操作场景物体，场景注入由 LevelRuntimeConfigurator 完成。
/// </summary>
public static class RunSaveService
{
    public const int SaveVersion = 2;
    public const int MaxFloorCount = 9;

    private const string SaveFileName = "run-save.json";
    private const string LastCorpsePrefsKey = "Leihuo.LastCorpse";
    private const int PreviewRunSeed = 20260823;
    private static RunSaveData current;
    private static bool loadAttempted;
    private static bool formalRunSessionActive;

#if UNITY_EDITOR
    // Editor 测试工具可挂接这个委托，把“下一层”临时改成指定楼层，用于快速测第 9 层和结局。
    public delegate bool FloorAdvanceOverrideHandler(int currentFloor, out int targetFloor);
    public static FloorAdvanceOverrideHandler FloorAdvanceOverride { get; set; }
#endif

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    public static bool HasSave => Load() != null;
    public static bool HasActiveRunSession => formalRunSessionActive && Load() != null;
    public static RunSaveData Current => formalRunSessionActive ? Load() : null;

    /// <summary>
    /// 开始一条新的生命尝试。
    /// preserveFrozenFloors 为 true 时可以保留被锁定的楼层槽位，但会清理单生命状态。
    /// </summary>
    public static RunSaveData StartNewAttempt(bool preserveFrozenFloors = false)
    {
        RunSaveData previous = Load();
        CorpseSaveData previousCorpse = CloneCorpse(LoadLatestCorpseRecord());
        List<FloorSaveData> frozenFloors = preserveFrozenFloors && previous != null
            ? previous.floors.Where(floor => floor != null && floor.frozen).ToList()
            : new List<FloorSaveData>();
        ResetAttemptScopedFloorState(frozenFloors);
        KeepOnlyLatestCorpse(frozenFloors);

        current = new RunSaveData
        {
            version = SaveVersion,
            runId = Guid.NewGuid().ToString("N"),
            runSeed = CreateSeed(),
            attemptNumber = previous != null ? previous.attemptNumber + 1 : 1,
            currentFloor = 1,
            maxFloors = MaxFloorCount,
            attemptState = CreateAttemptState(),
            floors = frozenFloors,
            inventoryItemIds = new List<int>()
        };
        loadAttempted = true;
        formalRunSessionActive = true;
        GenerateFloorPlan(current);
        if (previousCorpse != null)
        {
            // 上次的尸体带到新周目的第 9 层（供结局三使用）。
            FloorSaveData floorNine = current.floors.FirstOrDefault(floor =>
                floor != null && floor.floorNumber == MaxFloorCount);
            if (floorNine != null)
            {
                floorNine.corpses ??= new List<CorpseSaveData>();
                floorNine.corpses.Clear();
                floorNine.corpses.Add(previousCorpse);
            }
        }

        SaveNow();
        return current;
    }

    /// <summary>
    /// 全新 run：删除旧存档后重新生成九层计划。
    /// </summary>
    public static RunSaveData StartNewRun()
    {
        DeleteSave();
        return StartNewAttempt(false);
    }

    /// <summary>
    /// 从已有存档恢复正式 run。没有存档时保持非正式会话状态，避免单场景预览误写存档。
    /// </summary>
    public static bool TryBeginExistingRun()
    {
        if (Load() == null)
        {
            formalRunSessionActive = false;
            return false;
        }

        formalRunSessionActive = true;
        return true;
    }

    /// <summary>
    /// 死亡后从第 1 层重新开始当前 run。
    /// 这里会刷新 attemptState，但保留/修复 floor plan，避免每次死亡都完全重抽九层。
    /// </summary>
    public static RunSaveData RestartCurrentRun()
    {
        RunSaveData save = Load();
        if (save == null)
        {
            return StartNewAttempt(false);
        }

        save.floors ??= new List<FloorSaveData>();
        save.attemptNumber = Mathf.Max(1, save.attemptNumber + 1);
        save.currentFloor = 1;
        save.attemptState = CreateAttemptState();

        ResetAttemptScopedFloorState(save.floors);
        KeepOnlyLatestCorpse(save.floors);
        GenerateFloorPlan(save);

        current = save;
        loadAttempted = true;
        formalRunSessionActive = true;
        SaveNow();
        return current;
    }

    /// <summary>
    /// 读取当前楼层的运行结果和对应模板。
    /// 如果当前没有正式 run，会返回 null，让场景加载方转去 CreatePreviewFloor。
    /// </summary>
    public static FloorSaveData EnsureCurrentFloor(out LevelTemplateData template)
    {
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            template = null;
            return null;
        }

        return EnsureFloor(save.currentFloor, out template);
    }

    /// <summary>
    /// 确保指定楼层存在，并返回这层已经选好的模板、普通目标和特殊目标。
    /// 这个函数是“存档里的楼层计划”和“场景运行时配置”之间的主要桥梁。
    /// </summary>
    public static FloorSaveData EnsureFloor(int floorNumber, out LevelTemplateData template)
    {
        template = null;
        if (floorNumber < 1 || floorNumber > MaxFloorCount)
        {
            return null;
        }

        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            return null;
        }

        FloorSaveData floor = save.floors.FirstOrDefault(entry =>
            entry != null && entry.floorNumber == floorNumber);
        if (floor == null)
        {
            // 如果存档里缺了某层，就按当前 runSeed 补一个稳定的楼层槽位。
            IReadOnlyList<LevelTemplateData> pool = GetTemplatePoolForFloor(floorNumber);
            floor = CreateFloorSlot(
                save,
                floorNumber,
                pool,
                GetUsedTemplateIdsForDifficulty(save, floorNumber));
            if (floor == null)
            {
                return null;
            }

            save.floors.Add(floor);
            save.floors.Sort((left, right) => left.floorNumber.CompareTo(right.floorNumber));
        }

        if (!LevelTemplateRepository.TryLoadById(floor.templateId, out template))
        {
            // JSON 模板被删除或改名时，尽量按同难度模板池自动修复，避免旧存档直接坏掉。
            if (!TryRepairMissingTemplate(save, floor, floorNumber, out template))
            {
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(floor.targetPairId))
        {
            // 兼容旧存档：如果没有保存目标，就根据模板和 floorSeed 补选。
            floor.targetPairId = SelectTargetPairId(template, floorSeed: floor.floorSeed);
        }

        if (string.IsNullOrWhiteSpace(floor.specialTargetPairId))
        {
            floor.specialTargetPairId = SelectSpecialTargetPairId(template, floor.floorSeed, floor.targetPairId);
        }

        save.currentFloor = floorNumber;
        SaveNow();
        return floor;
    }

    /// <summary>
    /// 创建只用于编辑器/单场景运行的预览楼层。
    /// 预览模式不保存 run-save，用固定 seed 选一个稳定模板，便于反复调试同一场景。
    /// </summary>
    public static FloorSaveData CreatePreviewFloor(string sceneName, out LevelTemplateData template)
    {
        template = null;
        int floorNumber = GetPreviewFloorForScene(sceneName);
        if (floorNumber <= 0)
        {
            return null;
        }

        IReadOnlyList<LevelTemplateData> pool = LevelTemplateRepository.LoadCompatibleWithScene(sceneName);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogError($"[RunSaveService] No preview level JSON configurations are compatible with {sceneName}.");
            return null;
        }

        int difficulty = GetDifficultyForFloor(floorNumber);
        template = pool
            .Where(entry => entry != null)
            .Where(entry => GetTemplateDifficulty(entry) == difficulty)
            .OrderBy(entry => entry.templateId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ??
            pool
                .Where(entry => entry != null)
                .OrderBy(entry => entry.templateId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        if (template == null)
        {
            return null;
        }

        int floorSeed = CombineSeed(PreviewRunSeed, floorNumber);
        LevelTargetRouteData targetRoute = SelectTargetRoute(template, floorSeed);
        // 预览楼层只返回内存对象，不写入正式 run-save；方便单独打开场景调试 JSON 注入。
        return new FloorSaveData
        {
            floorNumber = floorNumber,
            levelSceneName = sceneName,
            templateId = template.templateId,
            floorSeed = floorSeed,
            targetPairId = targetRoute != null
                ? targetRoute.normalTargetPairId
                : SelectTargetPairId(template, floorSeed),
            specialTargetPairId = targetRoute != null
                ? targetRoute.specialTargetPairId
                : SelectSpecialTargetPairId(template, floorSeed, null),
            layoutVariantId = "preview"
        };
    }

    /// <summary>
    /// 推进到下一层，并返回下一层实际要加载的场景名。
    /// Editor 下可以被 FloorAdvanceOverride 改写，用来跳层测试。
    /// </summary>
    public static bool TryAdvanceToNextFloor(out string sceneName)
    {
        sceneName = SceneNames.FirstLevel;
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null || save.currentFloor >= MaxFloorCount)
        {
            return false;
        }

        CaptureInventory(save);
        int nextFloor = save.currentFloor + 1;
#if UNITY_EDITOR
        FloorAdvanceOverrideHandler overrideHandler = FloorAdvanceOverride;
        if (overrideHandler != null && overrideHandler(save.currentFloor, out int overrideFloor))
        {
            nextFloor = Mathf.Clamp(overrideFloor, 1, MaxFloorCount);
        }
#endif

        LevelTemplateData template;
        FloorSaveData floor = EnsureFloor(nextFloor, out template);
        if (floor == null || template == null)
        {
            return false;
        }

        sceneName = string.IsNullOrWhiteSpace(floor.levelSceneName)
            ? SceneNames.FirstLevel
            : floor.levelSceneName;
        SaveNow();
        return true;
    }

    /// <summary>
    /// 根据当前存档楼层返回继续游戏时要加载的场景。
    /// </summary>
    public static string GetContinueSceneName()
    {
        RunSaveData save = Load();
        if (save == null)
        {
            return SceneNames.FirstLevel;
        }

        FloorSaveData floor = save.floors.FirstOrDefault(entry =>
            entry != null && entry.floorNumber == save.currentFloor);
        return floor != null && !string.IsNullOrWhiteSpace(floor.levelSceneName)
            ? floor.levelSceneName
            : SceneNames.FirstLevel;
    }

    /// <summary>
    /// 记录玩家死亡时的尸体信息。
    /// 该信息会用于后续结局分支或尸体相关表现，但不会改变当前楼层推进逻辑。
    /// </summary>
    public static void RecordCurrentPlayerDeath()
    {
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            return;
        }

        int diedFloor = save.currentFloor;
        Player2DMovementController player = UnityEngine.Object.FindAnyObjectByType<Player2DMovementController>();
        if (player == null)
        {
            EndingService.TriggerDeathEnding(diedFloor);
            return;
        }

        SpriteRenderer renderer = player.GetComponentInChildren<SpriteRenderer>();
        WorldSwapManager2D worldManager = WorldSwapManager2D.Instance;
        Vector3 position = player.transform.position;
        // 死亡记录只保存复现尸体所需的最小信息：位置、朝向、所在世界、死亡楼层和道具。
        CorpseSaveData corpse = new CorpseSaveData
        {
            corpseId = Guid.NewGuid().ToString("N"),
            world = worldManager != null ? worldManager.GetPlayerWorld().ToString() : WorldKind2D.Normal.ToString(),
            x = position.x,
            y = position.y,
            z = position.z,
            facingLeft = renderer != null && renderer.flipX,
            createdUtcTicks = DateTime.UtcNow.Ticks,
            diedFloor = diedFloor,
            inventoryItemIds = StoryInventoryManager.GetOrCreateInstance().CopyOwnedItemIds()
        };
        SaveLatestCorpseRecord(corpse);

        // 尸体默认存放在第 9 层（结局三 / 尸体 AI 所在层）。
        // 直接查找/创建第 9 层槽位，不改动 currentFloor，也不中途保存。
        FloorSaveData floor = save.floors.FirstOrDefault(entry =>
            entry != null && entry.floorNumber == MaxFloorCount);
        if (floor == null)
        {
            IReadOnlyList<LevelTemplateData> pool = GetTemplatePoolForFloor(MaxFloorCount);
            floor = CreateFloorSlot(
                save,
                MaxFloorCount,
                pool,
                GetUsedTemplateIdsForDifficulty(save, MaxFloorCount));
            if (floor != null)
            {
                save.floors.Add(floor);
                save.floors.Sort((left, right) => left.floorNumber.CompareTo(right.floorNumber));
            }
        }

        if (floor == null)
        {
            EndingService.TriggerDeathEnding(diedFloor);
            return;
        }

        ClearAllCorpses(save);
        floor.corpses ??= new List<CorpseSaveData>();
        floor.corpses.Add(corpse);
        CaptureInventory(save);
        SaveNow();
        EndingService.TriggerDeathEnding(diedFloor);
    }

    /// <summary>
    /// 主动捕获当前 run 的轻量状态并保存。
    /// 当前背包由 StoryInventoryManager 持有，所以这里只做同步入口。
    /// </summary>
    public static void CaptureCurrentState()
    {
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            return;
        }

        CaptureInventory(save);
        SaveNow();
    }

    /// <summary>
    /// 返回当前楼层的存档槽位。没有正式 run 时返回 null。
    /// </summary>
    public static FloorSaveData GetCurrentFloor()
    {
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            return null;
        }

        return save.floors.FirstOrDefault(entry =>
            entry != null && entry.floorNumber == save.currentFloor);
    }

    /// <summary>返回 run-save 中最新的一具尸体（跨楼层比较 createdUtcTicks）。</summary>
    public static CorpseSaveData GetLatestCorpse()
    {
        RunSaveData save = Load();
        return FindLatestCorpse(save?.floors) ?? LoadLatestCorpseRecord();
    }

    /// <summary>
    /// 清理 run-save 和 PlayerPrefs 中的尸体记录。
    /// </summary>
    public static void ClearCorpseRecords()
    {
        RunSaveData save = Load();
        if (save != null)
        {
            ClearAllCorpses(save);
            SaveNow();
        }

        if (PlayerPrefs.HasKey(LastCorpsePrefsKey))
        {
            PlayerPrefs.DeleteKey(LastCorpsePrefsKey);
            PlayerPrefs.Save();
        }
    }

    private static CorpseSaveData FindLatestCorpse(IEnumerable<FloorSaveData> floors)
    {
        CorpseSaveData latest = null;
        long latestTicks = long.MinValue;
        if (floors == null)
        {
            return null;
        }

        foreach (FloorSaveData floor in floors)
        {
            if (floor?.corpses == null)
            {
                continue;
            }

            for (int corpseIndex = 0; corpseIndex < floor.corpses.Count; corpseIndex++)
            {
                CorpseSaveData corpse = floor.corpses[corpseIndex];
                if (corpse == null)
                {
                    continue;
                }

                if (latest == null || corpse.createdUtcTicks > latestTicks)
                {
                    latest = corpse;
                    latestTicks = corpse.createdUtcTicks;
                }
            }
        }

        return latest;
    }

    private static CorpseSaveData CloneCorpse(CorpseSaveData corpse)
    {
        if (corpse == null)
        {
            return null;
        }

        return new CorpseSaveData
        {
            corpseId = corpse.corpseId,
            world = corpse.world,
            x = corpse.x,
            y = corpse.y,
            z = corpse.z,
            facingLeft = corpse.facingLeft,
            createdUtcTicks = corpse.createdUtcTicks,
            diedFloor = corpse.diedFloor,
            inventoryItemIds = corpse.inventoryItemIds != null
                ? new List<int>(corpse.inventoryItemIds)
                : new List<int>()
        };
    }

    /// <summary>返回最新尸体所在的楼层号；无尸体返回 0。</summary>
    public static int GetLatestCorpseFloor()
    {
        RunSaveData save = Load();
        CorpseSaveData latest = GetLatestCorpse();
        if (latest == null)
        {
            return 0;
        }

        if (save?.floors == null)
        {
            return latest.diedFloor;
        }

        for (int floorIndex = 0; floorIndex < save.floors.Count; floorIndex++)
        {
            FloorSaveData floor = save.floors[floorIndex];
            if (floor?.corpses == null)
            {
                continue;
            }

            for (int corpseIndex = 0; corpseIndex < floor.corpses.Count; corpseIndex++)
            {
                CorpseSaveData corpse = floor.corpses[corpseIndex];
                if (corpse != null &&
                    string.Equals(corpse.corpseId, latest.corpseId, StringComparison.OrdinalIgnoreCase))
                {
                    return floor.floorNumber;
                }
            }
        }

        return latest.diedFloor;
    }

    /// <summary>
    /// 判断当前生命里，本层 SpecialWorld 恢复是否已经用过。
    /// 这里刻意读取 attemptState，让死亡重开后该记录能被清空。
    /// </summary>
    public static bool IsSpecialWorldRecoveryUsedOnCurrentFloor()
    {
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            return false;
        }

        EnsureAttemptState(save);
        // 恢复使用状态属于“当前这条命”，所以读取 attemptState，而不是 FloorSaveData 的旧字段。
        return save.attemptState.specialWorldRecoveryUsedFloors.Contains(save.currentFloor);
    }

    /// <summary>
    /// 标记当前生命里，本层 SpecialWorld 恢复已经使用。
    /// </summary>
    public static void MarkSpecialWorldRecoveryUsedOnCurrentFloor()
    {
        RunSaveData save = formalRunSessionActive ? Load() : null;
        if (save == null)
        {
            return;
        }

        EnsureAttemptState(save);
        if (!save.attemptState.specialWorldRecoveryUsedFloors.Contains(save.currentFloor))
        {
            // 同一生命同一楼层只记录一次，避免重复长按导致列表重复。
            save.attemptState.specialWorldRecoveryUsedFloors.Add(save.currentFloor);
        }

        SaveNow();
    }

    /// <summary>
    /// 恢复背包的预留入口。
    /// 当前故事道具独立保存在 PlayerPrefs，run-save 不覆盖它，避免加载楼层时误清背包。
    /// </summary>
    public static void RestoreInventory()
    {
        // Story inventory is stored independently in PlayerPrefs. Run-save must not
        // overwrite it when a level loads or continues.
    }

    /// <summary>
    /// 结束当前 run。通关或主动退出完整 run 时调用。
    /// </summary>
    public static void EndCurrentRun()
    {
        if (!formalRunSessionActive)
        {
            return;
        }

        DeleteSave();
    }

    /// <summary>
    /// 删除当前 run 存档和备份，并重置内存状态。
    /// </summary>
    public static void DeleteSave()
    {
        current = null;
        loadAttempted = true;
        formalRunSessionActive = false;
        DeleteIfExists(SavePath);
        DeleteIfExists(SavePath + ".bak");
    }

    /// <summary>
    /// 将 current 写入磁盘。采用临时文件 + 备份的方式，降低写入中断导致存档损坏的风险。
    /// </summary>
    public static void SaveNow()
    {
        if (current == null)
        {
            return;
        }

        string directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = SavePath + ".tmp";
        string backupPath = SavePath + ".bak";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(current, true));
        if (File.Exists(SavePath))
        {
            // 先写临时文件再 Replace，减少写存档中途失败导致主存档损坏的风险。
            File.Replace(temporaryPath, SavePath, backupPath);
        }
        else
        {
            File.Move(temporaryPath, SavePath);
        }
    }

    private static RunSaveData Load()
    {
        if (loadAttempted)
        {
            return current;
        }

        loadAttempted = true;
        current = TryRead(SavePath) ?? TryRead(SavePath + ".bak");
        if (current != null && current.version != SaveVersion)
        {
            Debug.LogWarning($"[RunSaveService] Unsupported save version {current.version}.");
            current = null;
        }

        if (current != null)
        {
            EnsureAttemptState(current);
            RepairDuplicateFloorTemplates(current);
            SaveNow();
        }

        return current;
    }

    private static RunSaveData TryRead(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<RunSaveData>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RunSaveService] Failed to read {path}: {exception.Message}");
            return null;
        }
    }

    private static void GenerateFloorPlan(RunSaveData save)
    {
        EnsureAttemptState(save);
        save.floors ??= new List<FloorSaveData>();
        RepairDuplicateFloorTemplates(save);

        // 生成九层 floor plan：1-3 / 4-6 / 7-9 分别映射到三张复用场景和三个难度段。
        for (int floorNumber = 1; floorNumber <= MaxFloorCount; floorNumber++)
        {
            if (save.floors.Any(floor => floor != null && floor.floorNumber == floorNumber))
            {
                continue;
            }

            IReadOnlyList<LevelTemplateData> pool = GetTemplatePoolForFloor(floorNumber);
            FloorSaveData floor = CreateFloorSlot(
                save,
                floorNumber,
                pool,
                GetUsedTemplateIdsForDifficulty(save, floorNumber));
            if (floor != null)
            {
                save.floors.Add(floor);
            }
        }

        save.floors.Sort((left, right) => left.floorNumber.CompareTo(right.floorNumber));
    }

    private static FloorSaveData CreateFloorSlot(
        RunSaveData save,
        int floorNumber,
        IReadOnlyList<LevelTemplateData> pool,
        ISet<string> excludedTemplateIds = null)
    {
        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        int floorSeed = CombineSeed(save.runSeed, floorNumber);
        LevelTemplateData template = SelectTemplateForFloor(pool, floorSeed, floorNumber, excludedTemplateIds);
        if (template == null)
        {
            return null;
        }

        LevelTargetRouteData targetRoute = SelectTargetRoute(template, floorSeed);
        // FloorSaveData 记录的是“本层已确定的结果”，场景加载时直接按这些结果注入。
        return new FloorSaveData
        {
            floorNumber = floorNumber,
            levelSceneName = GetSceneNameForFloor(floorNumber),
            templateId = template.templateId,
            floorSeed = floorSeed,
            targetPairId = targetRoute != null
                ? targetRoute.normalTargetPairId
                : SelectTargetPairId(template, floorSeed),
            specialTargetPairId = targetRoute != null
                ? targetRoute.specialTargetPairId
                : SelectSpecialTargetPairId(template, floorSeed, null),
            layoutVariantId = "base"
        };
    }

    private static bool TryRepairMissingTemplate(
        RunSaveData save,
        FloorSaveData floor,
        int floorNumber,
        out LevelTemplateData template)
    {
        template = null;
        if (save == null || floor == null)
        {
            return false;
        }

        string missingTemplateId = floor.templateId;
        IReadOnlyList<LevelTemplateData> pool = GetTemplatePoolForFloor(floorNumber);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogError(
                $"[RunSaveService] Saved level template no longer exists and no replacement pool is available: {missingTemplateId}");
            return false;
        }

        int floorSeed = floor.floorSeed != 0
            ? floor.floorSeed
            : CombineSeed(save.runSeed, floorNumber);
        // 修复时仍然沿用原 floorSeed，尽量保持同一层的随机选择稳定。
        template = SelectTemplateForFloor(
            pool,
            floorSeed,
            floorNumber,
            GetUsedTemplateIdsForDifficulty(save, floorNumber, floor.floorNumber));
        if (template == null)
        {
            Debug.LogError(
                $"[RunSaveService] Saved level template no longer exists and no replacement could be selected: {missingTemplateId}");
            return false;
        }

        LevelTargetRouteData targetRoute = SelectTargetRoute(template, floorSeed);
        floor.floorSeed = floorSeed;
        floor.levelSceneName = GetSceneNameForFloor(floorNumber);
        floor.templateId = template.templateId;
        floor.targetPairId = targetRoute != null
            ? targetRoute.normalTargetPairId
            : SelectTargetPairId(template, floorSeed);
        floor.specialTargetPairId = targetRoute != null
            ? targetRoute.specialTargetPairId
            : SelectSpecialTargetPairId(template, floorSeed, floor.targetPairId);
        if (string.IsNullOrWhiteSpace(floor.layoutVariantId))
        {
            floor.layoutVariantId = "base";
        }

        Debug.LogWarning(
            $"[RunSaveService] Saved level template no longer exists: {missingTemplateId}. Reassigned floor {floorNumber} to {template.templateId}.");
        return true;
    }

    private static void RepairDuplicateFloorTemplates(RunSaveData save)
    {
        if (save?.floors == null)
        {
            return;
        }

        // 同一难度段内尽量不重复使用同一个模板，让九层体验更有变化。
        HashSet<string> seenTemplateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FloorSaveData floor in save.floors
                     .Where(floor => floor != null)
                     .OrderBy(floor => floor.floorNumber))
        {
            if (string.IsNullOrWhiteSpace(floor.templateId) ||
                !LevelTemplateRepository.TryLoadById(floor.templateId, out _))
            {
                continue;
            }

            string groupKey = $"{GetDifficultyForFloor(floor.floorNumber)}:{floor.templateId}";
            if (seenTemplateIds.Add(groupKey))
            {
                continue;
            }

            string duplicateTemplateId = floor.templateId;
            if (TryReassignFloorTemplate(
                    save,
                    floor,
                    GetUsedTemplateIdsForDifficulty(save, floor.floorNumber, floor.floorNumber),
                    out LevelTemplateData replacement))
            {
                Debug.LogWarning(
                    $"[RunSaveService] Duplicate level template {duplicateTemplateId} on floor {floor.floorNumber}; reassigned to {replacement.templateId}.");
                seenTemplateIds.Add($"{GetDifficultyForFloor(floor.floorNumber)}:{replacement.templateId}");
            }
        }
    }

    private static bool TryReassignFloorTemplate(
        RunSaveData save,
        FloorSaveData floor,
        ISet<string> excludedTemplateIds,
        out LevelTemplateData template)
    {
        template = null;
        if (save == null || floor == null)
        {
            return false;
        }

        IReadOnlyList<LevelTemplateData> pool = GetTemplatePoolForFloor(floor.floorNumber);
        if (pool == null || pool.Count == 0)
        {
            return false;
        }

        int floorSeed = floor.floorSeed != 0
            ? floor.floorSeed
            : CombineSeed(save.runSeed, floor.floorNumber);
        template = SelectTemplateForFloor(pool, floorSeed, floor.floorNumber, excludedTemplateIds);
        if (template == null)
        {
            return false;
        }

        LevelTargetRouteData targetRoute = SelectTargetRoute(template, floorSeed);
        floor.floorSeed = floorSeed;
        floor.levelSceneName = GetSceneNameForFloor(floor.floorNumber);
        floor.templateId = template.templateId;
        floor.targetPairId = targetRoute != null
            ? targetRoute.normalTargetPairId
            : SelectTargetPairId(template, floorSeed);
        floor.specialTargetPairId = targetRoute != null
            ? targetRoute.specialTargetPairId
            : SelectSpecialTargetPairId(template, floorSeed, floor.targetPairId);
        if (string.IsNullOrWhiteSpace(floor.layoutVariantId))
        {
            floor.layoutVariantId = "base";
        }

        return true;
    }

    private static HashSet<string> GetUsedTemplateIdsForDifficulty(
        RunSaveData save,
        int floorNumber,
        int excludedFloorNumber = 0)
    {
        int floorDifficulty = GetDifficultyForFloor(floorNumber);
        return save?.floors == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : save.floors
                .Where(floor => floor != null &&
                                floor.floorNumber != excludedFloorNumber &&
                                GetDifficultyForFloor(floor.floorNumber) == floorDifficulty &&
                                !string.IsNullOrWhiteSpace(floor.templateId))
                .Select(floor => floor.templateId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<LevelTemplateData> GetTemplatePool()
    {
        return GetTemplatePoolForFloor(1);
    }

    private static IReadOnlyList<LevelTemplateData> GetTemplatePoolForFloor(int floorNumber)
    {
        string sceneName = GetSceneNameForFloor(floorNumber);
        // 模板池先按实际复用场景过滤，再在 SelectTemplateForFloor 中按 difficulty 细分。
        IReadOnlyList<LevelTemplateData> pool = LevelTemplateRepository.LoadCompatibleWithScene(sceneName);
        if (pool.Count == 0)
        {
            Debug.LogError($"[RunSaveService] No level JSON configurations are compatible with {sceneName}.");
        }

        return pool;
    }

    private static string GetSceneNameForFloor(int floorNumber)
    {
        int floorDifficulty = GetDifficultyForFloor(floorNumber);
        if (floorDifficulty == 2)
        {
            return SceneNames.SecondLevel;
        }

        if (floorDifficulty == 3)
        {
            return SceneNames.ThirdLevel;
        }

        return SceneNames.FirstLevel;
    }

    private static int GetPreviewFloorForScene(string sceneName)
    {
        if (string.Equals(sceneName, SceneNames.FirstLevel, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(sceneName, SceneNames.SecondLevel, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (string.Equals(sceneName, SceneNames.ThirdLevel, StringComparison.OrdinalIgnoreCase))
        {
            return 7;
        }

        return 0;
    }

    private static LevelTemplateData SelectTemplateForFloor(
        IReadOnlyList<LevelTemplateData> pool,
        int floorSeed,
        int floorNumber,
        ISet<string> excludedTemplateIds = null)
    {
        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        int floorDifficulty = GetDifficultyForFloor(floorNumber);
        List<LevelTemplateData> difficultyPool = pool
            .Where(template => GetTemplateDifficulty(template) == floorDifficulty)
            .ToList();
        if (difficultyPool.Count == 0)
        {
            Debug.LogWarning($"[RunSaveService] No difficulty {floorDifficulty} templates found for floor {floorNumber}; using all compatible templates.");
            difficultyPool = pool.ToList();
        }

        List<LevelTemplateData> availablePool = excludedTemplateIds != null && excludedTemplateIds.Count > 0
            ? difficultyPool
                .Where(template => !excludedTemplateIds.Contains(template.templateId))
                .ToList()
            : difficultyPool;
        if (availablePool.Count == 0)
        {
            // 模板数量不足时允许重复，优先保证流程能继续跑完。
            availablePool = difficultyPool;
        }

        // 同一个 runSeed + floorNumber 会得到稳定结果，方便复现和调试。
        int index = PositiveModulo(MixSeed(floorSeed), availablePool.Count);
        return availablePool[index];
    }

    private static int GetDifficultyForFloor(int floorNumber)
    {
        if (floorNumber <= 3)
        {
            return 1;
        }

        if (floorNumber <= 6)
        {
            return 2;
        }

        return 3;
    }

    private static int GetTemplateDifficulty(LevelTemplateData template)
    {
        return Mathf.Clamp(template?.difficulty ?? 1, 1, 3);
    }

    private static void CaptureInventory(RunSaveData save)
    {
        EnsureAttemptState(save);
        // Back-pack / story items are owned by StoryInventoryManager PlayerPrefs data,
        // not by the current run-save.
    }

    private static void ResetAttemptScopedFloorState(IEnumerable<FloorSaveData> floors)
    {
        if (floors == null)
        {
            return;
        }

        foreach (FloorSaveData floor in floors)
        {
            if (floor != null)
            {
                floor.specialWorldRecoveryUsed = false;
            }
        }
    }

    private static RunAttemptStateData CreateAttemptState()
    {
        // lifeId 用来区分每条生命；死亡重开后恢复使用记录应当重新开始。
        return new RunAttemptStateData
        {
            lifeId = Guid.NewGuid().ToString("N"),
            specialWorldRecoveryUsedFloors = new List<int>()
        };
    }

    private static void EnsureAttemptState(RunSaveData save)
    {
        if (save == null)
        {
            return;
        }

        if (save.attemptState == null)
        {
            save.attemptState = CreateAttemptState();
            return;
        }

        if (string.IsNullOrWhiteSpace(save.attemptState.lifeId))
        {
            save.attemptState.lifeId = Guid.NewGuid().ToString("N");
        }

        save.attemptState.specialWorldRecoveryUsedFloors ??= new List<int>();
    }

    private static void ClearAllCorpses(RunSaveData save)
    {
        if (save?.floors == null)
        {
            return;
        }

        for (int i = 0; i < save.floors.Count; i++)
        {
            if (save.floors[i]?.corpses != null)
            {
                save.floors[i].corpses.Clear();
            }
        }
    }

    private static void KeepOnlyLatestCorpse(IEnumerable<FloorSaveData> floors)
    {
        if (floors == null)
        {
            return;
        }

        FloorSaveData latestFloor = null;
        CorpseSaveData latestCorpse = null;
        foreach (FloorSaveData floor in floors)
        {
            if (floor?.corpses == null)
            {
                continue;
            }

            for (int i = 0; i < floor.corpses.Count; i++)
            {
                CorpseSaveData corpse = floor.corpses[i];
                if (corpse == null)
                {
                    continue;
                }

                if (latestCorpse == null || corpse.createdUtcTicks > latestCorpse.createdUtcTicks)
                {
                    latestCorpse = corpse;
                    latestFloor = floor;
                }
            }
        }

        foreach (FloorSaveData floor in floors)
        {
            if (floor?.corpses != null)
            {
                floor.corpses.Clear();
            }
        }

        if (latestFloor != null && latestCorpse != null)
        {
            latestFloor.corpses ??= new List<CorpseSaveData>();
            latestFloor.corpses.Add(latestCorpse);
        }
    }

    private static string SelectTargetPairId(LevelTemplateData template, int floorSeed)
    {
        if (template.targetPairIds == null || template.targetPairIds.Count == 0)
        {
            return string.Empty;
        }

        int index = PositiveModulo(MixSeed(floorSeed), template.targetPairIds.Count);
        return template.targetPairIds[index];
    }

    private static string SelectSpecialTargetPairId(LevelTemplateData template, int floorSeed, string normalTargetPairId)
    {
        LevelTargetRouteData matchingRoute = FindTargetRouteByNormalPairId(template, normalTargetPairId);
        if (matchingRoute != null && !string.IsNullOrWhiteSpace(matchingRoute.specialTargetPairId))
        {
            // 如果 JSON 显式写了 normal -> special 路线，优先保证两个世界目标按路线对应。
            return matchingRoute.specialTargetPairId;
        }

        LevelTargetRouteData route = SelectTargetRoute(template, floorSeed);
        if (route != null && !string.IsNullOrWhiteSpace(route.specialTargetPairId))
        {
            return route.specialTargetPairId;
        }

        List<string> candidates = template.specialTargetPairIds != null && template.specialTargetPairIds.Count > 0
            ? template.specialTargetPairIds
            : template.targetPairIds;
        if (candidates == null || candidates.Count == 0)
        {
            return string.Empty;
        }

        int index = PositiveModulo(MixSeed(floorSeed), candidates.Count);
        return candidates[index];
    }

    private static LevelTargetRouteData SelectTargetRoute(LevelTemplateData template, int floorSeed)
    {
        if (template?.targetRoutes == null || template.targetRoutes.Count == 0)
        {
            return null;
        }

        List<LevelTargetRouteData> validRoutes = template.targetRoutes
            .Where(route => route != null &&
                            !string.IsNullOrWhiteSpace(route.normalTargetPairId) &&
                            !string.IsNullOrWhiteSpace(route.specialTargetPairId))
            .ToList();
        if (validRoutes.Count == 0)
        {
            return null;
        }

        // 路线选择也用 floorSeed，确保普通目标和特殊目标的配对结果可复现。
        int index = PositiveModulo(MixSeed(floorSeed), validRoutes.Count);
        return validRoutes[index];
    }

    private static LevelTargetRouteData FindTargetRouteByNormalPairId(
        LevelTemplateData template,
        string normalTargetPairId)
    {
        if (template?.targetRoutes == null || string.IsNullOrWhiteSpace(normalTargetPairId))
        {
            return null;
        }

        return template.targetRoutes.FirstOrDefault(route =>
            route != null &&
            string.Equals(
                route.normalTargetPairId,
                normalTargetPairId,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(route.specialTargetPairId));
    }

    private static int CreateSeed()
    {
        byte[] bytes = Guid.NewGuid().ToByteArray();
        unchecked
        {
            int seed = Environment.TickCount ^ DateTime.UtcNow.Ticks.GetHashCode();
            for (int i = 0; i + 3 < bytes.Length; i += 4)
            {
                seed ^= BitConverter.ToInt32(bytes, i);
                seed = (seed * 397) ^ (seed >> 13);
            }

            return seed;
        }
    }

    private static int CombineSeed(int runSeed, int floorNumber)
    {
        // 把整局种子和楼层号混合，避免所有楼层都选到同一个模板序列位置。
        unchecked
        {
            return (runSeed * 397) ^ (floorNumber * 7919);
        }
    }

    private static int MixSeed(int seed)
    {
        unchecked
        {
            uint value = (uint)seed;
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return (int)(value & 0x7fffffff);
        }
    }

    private static int PositiveModulo(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return value % count;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void SaveLatestCorpseRecord(CorpseSaveData corpse)
    {
        if (corpse == null)
        {
            return;
        }

        PlayerPrefs.SetString(LastCorpsePrefsKey, JsonUtility.ToJson(corpse));
        PlayerPrefs.Save();
    }

    private static CorpseSaveData LoadLatestCorpseRecord()
    {
        if (!PlayerPrefs.HasKey(LastCorpsePrefsKey))
        {
            return null;
        }

        string json = PlayerPrefs.GetString(LastCorpsePrefsKey);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonUtility.FromJson<CorpseSaveData>(json);
    }
}
