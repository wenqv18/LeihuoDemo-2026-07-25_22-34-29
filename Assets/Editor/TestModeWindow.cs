using UnityEditor;
using UnityEngine;

public sealed class TestModeWindow : EditorWindow
{
    private const string SkipToLastFloorPrefKey = "Leihuo_TestMode_SkipToLastFloor";
    private const string GrantFuseItemsPrefKey = "Leihuo_TestMode_GrantFuseItems";

    [MenuItem("Tools/Leihuo/Test Mode")]
    private static void Open()
    {
        GetWindow<TestModeWindow>("Test Mode");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Test Mode", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        bool skipToLastFloor = EditorPrefs.GetBool(SkipToLastFloorPrefKey, false);
        bool newSkipToLastFloor = EditorGUILayout.Toggle("第1关后直接进入第9关", skipToLastFloor);
        if (newSkipToLastFloor != skipToLastFloor)
        {
            EditorPrefs.SetBool(SkipToLastFloorPrefKey, newSkipToLastFloor);
        }

        bool grantFuseItems = EditorPrefs.GetBool(GrantFuseItemsPrefKey, false);
        bool newGrantFuseItems = EditorGUILayout.Toggle("视作已集齐4个结局道具", grantFuseItems);
        if (newGrantFuseItems != grantFuseItems)
        {
            EditorPrefs.SetBool(GrantFuseItemsPrefKey, newGrantFuseItems);
            if (newGrantFuseItems && EditorApplication.isPlaying)
            {
                TestModeBootstrapper.ApplyGrantFuseItemsNow();
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "开启后，玩家通过第1关后，存档系统会直接生成并进入第9关（Level_03）。\n" +
            "之后在第9关再次通过门，会走正式的通关结局触发逻辑。\n" +
            "勾选“视作已集齐4个结局道具”后，运行时会自动把结局三所需的 1~4 号道具写入独立背包。\n" +
            "此模式只改变跳层逻辑，不会锁定或还原 run-save。此设置仅在 Editor 中生效。",
            MessageType.Info);
    }
}
