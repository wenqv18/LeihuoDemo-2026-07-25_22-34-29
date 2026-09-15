using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CanvasScalerRuntimeNormalizer
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        NormalizeLoadedCanvases();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NormalizeLoadedCanvases();
    }

    private static void NormalizeLoadedCanvases()
    {
        CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include);

        for (int i = 0; i < scalers.Length; i++)
        {
            Normalize(scalers[i]);
        }
    }

    private static void Normalize(CanvasScaler scaler)
    {
        if (scaler == null)
        {
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }
}
