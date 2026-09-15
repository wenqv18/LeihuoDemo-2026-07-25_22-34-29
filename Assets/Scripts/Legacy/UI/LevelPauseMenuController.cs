using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelPauseMenuController : MonoBehaviour
{
    private const string ControllerObjectName = "LevelPauseMenuController";
    private const string SettingsObjectName = "Settings";
    private const string ContinueButtonName = "Continue";
    private const string LeaveButtonName = "Leave";

    private static LevelPauseMenuController instance;

    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private bool pauseGameWhenOpen = true;

    private bool isOpen;
    private float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // GameUIController owns the runtime UI scene and input now.
        // This legacy bridge only keeps old inspector button hooks usable.
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        LevelPauseMenuController existing = UnityEngine.Object.FindAnyObjectByType<LevelPauseMenuController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        instance = controllerObject.AddComponent<LevelPauseMenuController>();
        DontDestroyOnLoad(controllerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        ResolveMenuReferences();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        UnbindButtons();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        // ESC is handled by GameUIController so first-open additive loading cannot be closed by this legacy script.
    }

    public void ContinueGame()
    {
        GameUIController.ContinueGame();
        isOpen = false;
    }

    public void ExitToMainScene()
    {
        GameUIController.LeaveGame();
        isOpen = false;
    }

    private void ToggleMenu()
    {
        isOpen = !GameUIController.IsSettingsOpen;
        GameUIController.ToggleGameplaySettings();
    }

    private void OpenMenu()
    {
        ResolveMenuReferences();
        if (settingsRoot == null)
        {
            return;
        }

        if (!isOpen)
        {
            previousTimeScale = Time.timeScale;
        }

        settingsRoot.SetActive(true);
        settingsRoot.transform.SetAsLastSibling();
        Canvas settingsCanvas = settingsRoot.GetComponent<Canvas>();
        if (settingsCanvas != null)
        {
            settingsCanvas.overrideSorting = true;
            settingsCanvas.sortingOrder = 10000;
        }

        isOpen = true;

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
        }

        if (EventSystem.current != null && continueButton != null)
        {
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }
    }

    private void CloseMenu(bool restoreTimeScale)
    {
        if (settingsRoot == null)
        {
            ResolveMenuReferences();
        }

        if (settingsRoot != null)
        {
            settingsRoot.SetActive(false);
        }

        isOpen = false;
        if (restoreTimeScale && pauseGameWhenOpen)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }
    }

    private void ResolveMenuReferences()
    {
        if (settingsRoot == null)
        {
            settingsRoot = FindSceneGameObject(SettingsObjectName);
        }

        if (settingsRoot == null)
        {
            return;
        }

        if (continueButton == null)
        {
            continueButton = FindButton(settingsRoot.transform, ContinueButtonName);
        }

        if (leaveButton == null)
        {
            leaveButton = FindButton(settingsRoot.transform, LeaveButtonName);
        }

        BindButtons();
    }

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(ExitToMainScene);
            leaveButton.onClick.AddListener(ExitToMainScene);
        }
    }

    private void UnbindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(ExitToMainScene);
        }
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (!IsAnyGameplaySceneLoaded())
        {
            CloseMenu(true);
        }
    }

    private static Button FindButton(Transform root, string buttonName)
    {
        if (root == null)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject sceneObject = allObjects[i];
            if (sceneObject == null || sceneObject.name != objectName)
            {
                continue;
            }

            Scene scene = sceneObject.scene;
            if (scene.IsValid() && scene.isLoaded && scene.name == SceneNames.UIScene)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private static bool IsGameplaySceneActive()
    {
        return IsAnyGameplaySceneLoaded();
    }

    private static bool IsAnyGameplaySceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (IsGameplayScene(SceneManager.GetSceneAt(i)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGameplayScene(Scene scene)
    {
        return SceneNames.IsGameplayScene(scene);
    }
}
