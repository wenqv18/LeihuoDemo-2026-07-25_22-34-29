#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TestModeBootstrapper
{
    private const string SkipToLastFloorPrefKey = "Leihuo_TestMode_SkipToLastFloor";
    private const string GrantFuseItemsPrefKey = "Leihuo_TestMode_GrantFuseItems";

    [InitializeOnLoadMethod]
    private static void RegisterEditorHooks()
    {
        RunSaveService.FloorAdvanceOverride = TryOverrideFloorAdvance;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        RegisterEditorHooks();
    }

    public static void ApplyGrantFuseItemsNow()
    {
        if (!IsGrantFuseItemsEnabled())
        {
            return;
        }

        StoryInventoryManager manager = StoryInventoryManager.GetOrCreateInstance();
        for (int i = 0; i < EndingService.FuseItemIds.Length; i++)
        {
            manager.AddItem(EndingService.FuseItemIds[i]);
        }
    }

    private static bool TryOverrideFloorAdvance(int currentFloor, out int targetFloor)
    {
        targetFloor = 0;
        if (!IsSkipToLastFloorEnabled() || currentFloor != 1)
        {
            return false;
        }

        targetFloor = RunSaveService.MaxFloorCount;
        return true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyGrantFuseItemsNow();
    }

    private static bool IsSkipToLastFloorEnabled()
    {
        return EditorPrefs.GetBool(SkipToLastFloorPrefKey, false);
    }

    private static bool IsGrantFuseItemsEnabled()
    {
        return EditorPrefs.GetBool(GrantFuseItemsPrefKey, false);
    }
}
#endif
