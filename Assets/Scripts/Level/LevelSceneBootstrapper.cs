using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class LevelSceneBootstrapper
{
    public static void EnsureRuntimeForScene(Scene scene)
    {
        EnsureLevelRuntime(scene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureLevelRuntime(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureLevelRuntime(scene);
    }

    private static void EnsureLevelRuntime(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !ShouldBootstrap(scene))
        {
            return;
        }

        EnsureEventSystem();
        EnsureUIControllerLoader();
        EnsureWorldSwapRuntime();
        EnsureWorldLampLightRuntime();
        EnsureSpecialWorldSurvivalRuntime();
        EnsureCameraRuntime();
        EnsureGlobalLight2D();
        if (scene.name != SceneNames.TutorialLevel)
        {
            // 新手教学关独立于 JSON 关卡配置与 run-save，不注入楼层配置。
            LevelRuntimeConfigurator.Apply(scene);
        }
    }

    private static bool ShouldBootstrap(Scene scene)
    {
        if (SceneNames.IsRuntimeBootstrapExcluded(scene))
        {
            return false;
        }

        bool hasNormalWorld = false;
        bool hasSpecialWorld = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            hasNormalWorld |= roots[i].name == "NormalWorld";
            hasSpecialWorld |= roots[i].name == "SpecialWorld";
        }

        return hasNormalWorld && hasSpecialWorld;
    }

    private static void EnsureEventSystem()
    {
        EventSystem[] activeSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude);
        EventSystem keeper = null;
        for (int i = 0; i < activeSystems.Length; i++)
        {
            EventSystem system = activeSystems[i];
            if (system != null && system.gameObject.scene.name != SceneNames.UIScene)
            {
                keeper = system;
                break;
            }
        }

        if (keeper == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            keeper = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        for (int i = 0; i < activeSystems.Length; i++)
        {
            EventSystem system = activeSystems[i];
            if (system != null && system != keeper)
            {
                system.gameObject.SetActive(false);
            }
        }
    }

    private static void EnsureUIControllerLoader()
    {
        if (UnityEngine.Object.FindAnyObjectByType<UIControllerSceneLoader>() != null)
        {
            return;
        }

        GameObject loaderObject = new GameObject("UIControllerSceneLoader");
        loaderObject.AddComponent<UIControllerSceneLoader>();
    }

    private static void EnsureWorldSwapRuntime()
    {
        WorldSwapManager2D manager = UnityEngine.Object.FindAnyObjectByType<WorldSwapManager2D>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("WorldSwapManager");
            manager = managerObject.AddComponent<WorldSwapManager2D>();
        }

        WorldSwapHoldController2D holdController = manager.GetComponent<WorldSwapHoldController2D>();
        if (holdController == null)
        {
            holdController = manager.gameObject.AddComponent<WorldSwapHoldController2D>();
        }

        manager.RefreshTrackedObjects();
    }

    private static void EnsureWorldLampLightRuntime()
    {
        if (UnityEngine.Object.FindAnyObjectByType<WorldLampLightController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("WorldLampLightController");
        controllerObject.AddComponent<WorldLampLightController>();
    }

    private static void EnsureSpecialWorldSurvivalRuntime()
    {
        if (UnityEngine.Object.FindAnyObjectByType<SpecialWorldSurvivalController2D>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("SpecialWorldSurvivalController");
        controllerObject.AddComponent<SpecialWorldSurvivalController2D>();
    }

    private static void EnsureCameraRuntime()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        }

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        else if (!mainCamera.CompareTag("MainCamera"))
        {
            mainCamera.tag = "MainCamera";
        }

        mainCamera.orthographic = true;

        CameraFollow2D follow = mainCamera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = mainCamera.gameObject.AddComponent<CameraFollow2D>();
        }

        Player2DMovementController player = UnityEngine.Object.FindAnyObjectByType<Player2DMovementController>();
        follow.Configure(player != null ? player.transform : null, true, false, true);
    }

    private static void EnsureGlobalLight2D()
    {
        Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
        if (lightType == null)
        {
            return;
        }

        UnityEngine.Object[] existingLights = UnityEngine.Object.FindObjectsByType(
            lightType,
            FindObjectsInactive.Exclude);
        if (existingLights.Length > 0)
        {
            return;
        }

        GameObject lightObject = new GameObject("Global Light 2D");
        Component light = lightObject.AddComponent(lightType);
        SetLight2DProperty(light, "lightType", "Global");
        SetLight2DProperty(light, "intensity", 1f);
    }

    private static void SetLight2DProperty(Component light, string propertyName, object value)
    {
        PropertyInfo property = light.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType.IsEnum && value is string enumName)
        {
            property.SetValue(light, Enum.Parse(property.PropertyType, enumName), null);
            return;
        }

        property.SetValue(light, value, null);
    }
}
