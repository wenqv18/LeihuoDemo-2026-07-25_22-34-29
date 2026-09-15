using System;
using System.Collections.Generic;

/// <summary>
/// 一次完整 run 的存档根对象。
/// 它保存九层楼层计划、当前楼层、背包道具，以及“当前这条命”的临时状态。
/// </summary>
[Serializable]
public sealed class RunSaveData
{
    public int version = 2;

    // runId / runSeed 标识整次 run。死亡重开 attempt 时，一般不重抽已生成的九层计划。
    public string runId;
    public int runSeed;

    // attemptNumber 表示第几次尝试；玩家死亡后会增加。
    public int attemptNumber = 1;

    public int currentFloor = 1;
    public int maxFloors = 9;

    // 当前生命的临时状态，例如本生命内哪些楼层已经用过 SpecialWorld 恢复。
    public RunAttemptStateData attemptState = new RunAttemptStateData();

    // 九层楼层计划：每层使用哪个模板、目标 pairId、特殊恢复 pairId 等。
    public List<FloorSaveData> floors = new List<FloorSaveData>();

    public List<int> inventoryItemIds = new List<int>();
}

/// <summary>
/// “单条生命”范围内的状态。
/// 注意它和 floors 分开：死亡重开时可以清空这些状态，但保留原来的楼层模板计划。
/// </summary>
[Serializable]
public sealed class RunAttemptStateData
{
    public string lifeId;
    public List<int> specialWorldRecoveryUsedFloors = new List<int>();
}

/// <summary>
/// 单层楼在本次 run 中的实际结果。
/// JSON 模板只提供候选内容；这里记录经过 seed 选择后的具体目标和特殊世界目标。
/// </summary>
[Serializable]
public sealed class FloorSaveData
{
    public int floorNumber;
    public string levelSceneName;

    // templateId 指向 LevelTemplateData，避免直接把整份 JSON 展开进存档。
    public string templateId;

    // floorSeed 由 runSeed + floorNumber 生成，让每层选择稳定且可复现。
    public int floorSeed;

    public string targetPairId;
    public string specialTargetPairId;
    public string layoutVariantId = "base";

    // frozen 可用于保留某些楼层，不参与重新生成。
    public bool frozen;

    // 旧字段保留给兼容；当前主要使用 RunAttemptStateData.specialWorldRecoveryUsedFloors。
    public bool specialWorldRecoveryUsed;

    public List<LevelObjectStateData> objectStates = new List<LevelObjectStateData>();
    public List<CorpseSaveData> corpses = new List<CorpseSaveData>();
}

/// <summary>
/// 预留的单个关卡对象状态记录，用 pairId 关联场景物体。
/// </summary>
[Serializable]
public sealed class LevelObjectStateData
{
    public string pairId;
    public string stateId;
    public bool active = true;
}

/// <summary>
/// 玩家死亡后留下的尸体记录。
/// 当前规则会保留最新尸体，并可带到第 9 层服务结局分支。
/// </summary>
[Serializable]
public sealed class CorpseSaveData
{
    public string corpseId;
    public string world;
    public float x;
    public float y;
    public float z;
    public bool facingLeft;
    public long createdUtcTicks;
    public int diedFloor;
    public List<int> inventoryItemIds = new List<int>();
}
