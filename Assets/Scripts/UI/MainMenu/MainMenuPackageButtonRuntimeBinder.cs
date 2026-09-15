using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuPackageButtonRuntimeBinder
{
    private const string PackageObjectName = "Package";
    private const string PackageLabel = "\u4FB5\u8680\u6863\u6848";

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitializeEditor()
    {
        UnityEditor.EditorApplication.update -= HandleEditorUpdate;
        UnityEditor.EditorApplication.update += HandleEditorUpdate;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        BindIfStartScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindIfStartScene(scene);
    }

    private static void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        BindIfStartScene(next);
    }

#if UNITY_EDITOR
    private static void HandleEditorUpdate()
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.name != SceneNames.StartScene)
        {
            return;
        }

        if (UnityEditor.EditorApplication.isPaused)
        {
            UnityEditor.EditorApplication.isPaused = false;
        }

        BindIfStartScene(activeScene);
    }
#endif

    private static void BindIfStartScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != SceneNames.StartScene)
        {
            return;
        }

        Button packageButton = FindPackageButton(scene);
        if (packageButton == null)
        {
            Debug.LogWarning("[MainMenuPackageButtonRuntimeBinder] Package button was not found in Start Scene.");
            return;
        }

        packageButton.enabled = true;
        packageButton.interactable = true;
        packageButton.onClick.RemoveListener(OpenPackage);
        packageButton.onClick.AddListener(OpenPackage);
        EnsureButtonRaycast(packageButton);
        EnsureParentCanvasGroupsAllowInput(packageButton);
    }

    private static void OpenPackage()
    {
        GameUIController.OpenPackageFromMainMenu();
    }

    private static Button FindPackageButton(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Button byName = FindButtonByName(roots[i].transform, PackageObjectName);
            if (byName != null)
            {
                return byName;
            }
        }

        for (int i = 0; i < roots.Length; i++)
        {
            Button byLabel = FindButtonByLabel(roots[i].transform, PackageLabel);
            if (byLabel != null)
            {
                return byLabel;
            }
        }

        return null;
    }

    private static Button FindButtonByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static Button FindButtonByLabel(Transform root, string label)
    {
        if (root == null)
        {
            return null;
        }

        Text[] labels = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text text = labels[i];
            if (text == null || !text.text.Contains(label))
            {
                continue;
            }

            Button button = text.GetComponentInParent<Button>(true);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static void EnsureButtonRaycast(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic targetGraphic = button.targetGraphic;
        if (targetGraphic == null)
        {
            targetGraphic = button.GetComponent<Graphic>();
            button.targetGraphic = targetGraphic;
        }

        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
        }

        Graphic[] childGraphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < childGraphics.Length; i++)
        {
            Graphic graphic = childGraphics[i];
            if (graphic != null && graphic != targetGraphic)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private static void EnsureParentCanvasGroupsAllowInput(Button button)
    {
        CanvasGroup[] groups = button.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup group = groups[i];
            if (group == null)
            {
                continue;
            }

            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }
}
