using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 教程关专用任务判定器。仅在 TutorialLevel 场景生效，完成三个教学任务：
/// - ClickTable：玩家点击教程中的 Table（interactions-table）
/// - TableFound：玩家通过视界（拍照）找到 Table 目标
/// - EnteredSpecialWorld：玩家进入过 SpecialWorld
/// </summary>
public static class TutorialTaskCompleters
{
    private const string ClickTableTaskId = "ClickTable";
    private const string TableFoundTaskId = "TableFound";
    private const string SpecialWorldEnteredTaskId = "EnteredSpecialWorld";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        FurnitureInteraction2D.InteractionPlayed -= HandleInteractionPlayed;
        FurnitureInteraction2D.InteractionPlayed += HandleInteractionPlayed;

        NormalWorldVisionTarget2D.VisionTargetFound -= HandleVisionTargetFound;
        NormalWorldVisionTarget2D.VisionTargetFound += HandleVisionTargetFound;

        WorldSwapManager2D.SpecialWorldEntered -= HandleSpecialWorldEntered;
        WorldSwapManager2D.SpecialWorldEntered += HandleSpecialWorldEntered;
    }

    private static void HandleInteractionPlayed(FurnitureInteraction2D interaction)
    {
        if (!IsTutorialScene() || interaction == null || !IsTutorialTable(interaction.transform))
        {
            return;
        }

        TutorialTaskRegistry.SetCompleted(ClickTableTaskId, true);
    }

    private static void HandleVisionTargetFound(NormalWorldVisionTarget2D target)
    {
        if (!IsTutorialScene() || target == null || !IsTutorialTable(target.transform))
        {
            return;
        }

        TutorialTaskRegistry.SetCompleted(TableFoundTaskId, true);
    }

    private static void HandleSpecialWorldEntered()
    {
        if (!IsTutorialScene())
        {
            return;
        }

        TutorialTaskRegistry.SetCompleted(SpecialWorldEnteredTaskId, true);
    }

    private static bool IsTutorialScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && activeScene.name == SceneNames.TutorialLevel;
    }

    private static bool IsTutorialTable(Transform transform)
    {
        WorldPairId pairId = transform != null ? transform.GetComponentInParent<WorldPairId>() : null;
        if (pairId != null &&
            string.Equals(pairId.PairId, "interactions-table", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Transform current = transform;
        while (current != null)
        {
            if (current.name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
