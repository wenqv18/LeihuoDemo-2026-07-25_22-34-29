using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 三结局统一入口：触发、判定与状态记录。
/// 状态存储方式与 StoryInventoryManager 一致（PlayerPrefs + JsonUtility JSON）。
/// 预留 <see cref="EndingTriggered"/> 事件，后续 CG / 转场 / 音效从这里接入。
/// </summary>
public static class EndingService
{
    private const string SaveKey = "Leihuo.EndingState";

    /// <summary>结局触发事件（预留：CG 播放、转场、音效等接入点）。</summary>
    public static event Action<EndingKind> EndingTriggered;

    /// <summary>结局三所需道具 ID（预设 4 个）。</summary>
    public static readonly int[] FuseItemIds = { 1, 2, 3, 4 };

    public static EndingKind GetLastEnding()
    {
        return (EndingKind)LoadState().endingKind;
    }

    public static int GetLastDeathFloor()
    {
        return LoadState().diedFloor;
    }

    public static bool HasUnlockedEnding(EndingKind kind)
    {
        if (kind == EndingKind.None)
        {
            return false;
        }

        EndingStateData data = LoadState();
        if (data.unlockedEndingKinds != null && data.unlockedEndingKinds.Contains((int)kind))
        {
            return true;
        }

        return data.endingKind == (int)kind;
    }

    public static void TriggerDeathEnding(int diedFloor)
    {
        RecordEnding(EndingKind.Death, diedFloor);
    }

    public static void TriggerClearEnding()
    {
        RecordEnding(EndingKind.Clear);
    }

    /// <summary>结局三：玩家在尸体对话中选择加入时，需要背包内集齐 4 个道具。</summary>
    public static bool TryTriggerFuseEnding()
    {
        if (!CanTriggerFuseEnding())
        {
            return false;
        }

        RunSaveService.ClearCorpseRecords();
        StoryInventoryManager.GetOrCreateInstance().ClearOwnedItems();
        RecordEnding(EndingKind.Fuse);
        return true;
    }

    private static void RecordEnding(EndingKind kind, int diedFloor = 0)
    {
        if (kind == EndingKind.None)
        {
            return;
        }

        EndingStateData data = LoadState();
        data.endingKind = (int)kind;
        data.diedFloor = diedFloor;
        data.triggeredAtUtcTicks = DateTime.UtcNow.Ticks;
        data.unlockedEndingKinds ??= new List<int>();
        if (!data.unlockedEndingKinds.Contains((int)kind))
        {
            data.unlockedEndingKinds.Add((int)kind);
        }

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log($"[EndingService] Ending recorded: {kind}, diedFloor={diedFloor}");
        EndingTriggered?.Invoke(kind);
    }

    /// <summary>当前是否存在玩家尸体（run-save 中）。</summary>
    public static bool HasLatestCorpse()
    {
        return RunSaveService.GetLatestCorpse() != null;
    }

    /// <summary>4 个预设道具是否已集齐。</summary>
    public static bool HasAllFuseItems()
    {
        StoryInventoryManager inventory = StoryInventoryManager.GetOrCreateInstance();
        foreach (int itemId in FuseItemIds)
        {
            if (!inventory.HasItem(itemId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>结局三是否可触发（道具齐；尸体交互由 DeathTalk 入口保证）。</summary>
    public static bool CanTriggerFuseEnding()
    {
        return HasAllFuseItems();
    }

    private static EndingStateData LoadState()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return new EndingStateData();
        }

        string json = PlayerPrefs.GetString(SaveKey);
        EndingStateData data = JsonUtility.FromJson<EndingStateData>(json);
        if (data != null && data.endingKind > (int)EndingKind.None)
        {
            data.unlockedEndingKinds ??= new List<int>();
            if (!data.unlockedEndingKinds.Contains(data.endingKind))
            {
                data.unlockedEndingKinds.Add(data.endingKind);
            }
        }

        return data ?? new EndingStateData();
    }
}

[Serializable]
public sealed class EndingStateData
{
    public int endingKind;
    public int diedFloor;
    public long triggeredAtUtcTicks;
    public List<int> unlockedEndingKinds = new List<int>();
}
