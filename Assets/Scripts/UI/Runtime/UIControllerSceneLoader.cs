using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class UIControllerSceneLoader : MonoBehaviour
{
    [SerializeField] private string uiSceneName = SceneNames.UIScene;

    private static bool loadRequested;
    private static string requestedSceneName = SceneNames.UIScene;
    private static AsyncOperation loadOperation;

    private void Awake()
    {
        RequestLoad(uiSceneName);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public static void RequestLoad(string sceneName)
    {
        if (IsSceneLoaded(sceneName))
        {
            loadRequested = false;
            loadOperation = null;
            return;
        }

        if (loadRequested &&
            loadOperation != null &&
            !loadOperation.isDone &&
            requestedSceneName == sceneName)
        {
            return;
        }

        if (loadRequested)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            loadRequested = false;
            loadOperation = null;
        }

        requestedSceneName = sceneName;
        loadRequested = true;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            loadRequested = false;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Debug.LogError($"[UIControllerSceneLoader] Failed to request UI scene load: {sceneName}");
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != requestedSceneName)
        {
            return;
        }

        DisableSceneCameras(scene);
        DisableSceneEventSystems(scene);
        loadRequested = false;
        loadOperation = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private static bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName && scene.isLoaded)
            {
                return true;
            }
        }

        return false;
    }

    private static void DisableSceneCameras(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
            for (int c = 0; c < cameras.Length; c++)
            {
                cameras[c].enabled = false;
            }

            AudioListener[] listeners = roots[i].GetComponentsInChildren<AudioListener>(true);
            for (int l = 0; l < listeners.Length; l++)
            {
                listeners[l].enabled = false;
            }
        }
    }

    private static void DisableSceneEventSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            EventSystem[] eventSystems = roots[i].GetComponentsInChildren<EventSystem>(true);
            for (int e = 0; e < eventSystems.Length; e++)
            {
                eventSystems[e].gameObject.SetActive(false);
            }
        }
    }
}
