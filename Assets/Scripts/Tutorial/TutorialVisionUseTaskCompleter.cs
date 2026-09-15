using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialVisionUseTaskCompleter
{
    private const string TutorialSceneName = "TutorialLevel";
    private const string VisionUseTaskId = "TakePhoto";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        WorldSwapManager2D.VisionUseConfirmed -= HandleVisionUseConfirmed;
        WorldSwapManager2D.VisionUseConfirmed += HandleVisionUseConfirmed;
    }

    private static void HandleVisionUseConfirmed(Vector2 center)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != TutorialSceneName)
        {
            return;
        }

        TutorialTaskRegistry.SetCompleted(VisionUseTaskId, true);
    }
}
