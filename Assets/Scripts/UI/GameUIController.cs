using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameUIController : MonoBehaviour
{
    private const string ControllerObjectName = "GameUIController";

    private static GameUIController instance;
    private static Action pendingOpenAction;

    [SerializeField] private GameObject gameUIRoot;
    [SerializeField] private GameObject specialGameUIRoot;
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private GameObject packageRoot;
    [SerializeField] private GameObject deathRoot;
    [SerializeField] private GameObject winRoot;
    [SerializeField] private GameObject end3Root;
    [SerializeField] private GameObject deathTalkRoot;
    [SerializeField] private KeyCode settingsToggleKey = KeyCode.Escape;
    [SerializeField] private KeyCode packageToggleKey = KeyCode.B;
    [SerializeField] private float endingFadeDuration = 0.55f;

    private Button settingsContinueButton;
    private Button settingsLeaveButton;
    private Button deathBackButton;
    private Button deathRestartButton;
    private Button winBackButton;
    private Button winRestartButton;
    private Button packageCloseButton;
    private Text deathDetailText;
    private Text winDetailText;
    private Text end3DetailText;
    private readonly Dictionary<GameObject, Tween> rootFadeTweens = new Dictionary<GameObject, Tween>();
    private bool overlayOpen;
    private PanelKind currentPanel = PanelKind.None;
    private float previousTimeScale = 1f;

    private enum PanelKind
    {
        None,
        Settings,
        Package,
        Death,
        Win,
        End3
    }

    public static bool IsSettingsOpen => instance != null && instance.currentPanel == PanelKind.Settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        EnsureInstance();

        if (IsGameplayScene(SceneManager.GetActiveScene()))
        {
            RequestUISceneLoad();
        }
    }

    public static void OpenSettingsFromMainMenu()
    {
        EnsureUISceneLoaded(() => Instance.OpenPanel(PanelKind.Settings));
    }

    public static void ToggleGameplaySettings()
    {
        EnsureUISceneLoaded(() =>
        {
            if (Instance.currentPanel == PanelKind.Settings)
            {
                Instance.CloseOverlay();
            }
            else
            {
                Instance.OpenPanel(PanelKind.Settings);
            }
        });
    }

    public static bool TryTogglePackage()
    {
        EnsureUISceneLoaded(() =>
        {
            if (Instance.currentPanel == PanelKind.Package)
            {
                Instance.CloseOverlay();
            }
            else
            {
                Instance.OpenPanel(PanelKind.Package);
            }
        });
        return true;
    }

    public static bool TryClosePackage()
    {
        if (instance == null)
        {
            return false;
        }

        if (instance.currentPanel == PanelKind.Package)
        {
            instance.CloseOverlay();
            return true;
        }

        return false;
    }

    public static void OpenPackageFromMainMenu()
    {
        EnsureMainMenuCanReceiveInput();
        EnsureUISceneLoaded(() => Instance.OpenPanel(PanelKind.Package));
    }

    public static void ShowDeath()
    {
        if (SceneManager.GetActiveScene().name == SceneNames.TutorialLevel)
        {
            // 教学关与 run-save 隔离：死亡时直接重开教程，不记录尸体/冻结楼层。
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.TutorialLevel, LoadSceneMode.Single);
            return;
        }

        // 先播放玩家死亡视频（1.5 倍速），播完再进入死亡结算。
        PlayerVideoAnimator deathAnimator = UnityEngine.Object.FindAnyObjectByType<PlayerVideoAnimator>(
            FindObjectsInactive.Include);
        if (deathAnimator != null && deathAnimator.CanPlayDeath)
        {
            deathAnimator.PlayDeath(CompleteDeathFlow);
            return;
        }

        CompleteDeathFlow();
    }

    private static void CompleteDeathFlow()
    {
        RunSaveService.RecordCurrentPlayerDeath();
        RunSaveService.EndCurrentRun();
        EnsureUISceneLoaded(() => Instance.OpenPanel(PanelKind.Death));
    }

    public static void ShowWin()
    {
        EnsureUISceneLoaded(() => Instance.OpenPanel(PanelKind.Win));
    }

    public static void ShowFuseEnding()
    {
        RunSaveService.EndCurrentRun();
        EnsureUISceneLoaded(() => Instance.OpenPanel(PanelKind.End3));
    }

    public static bool ShowDeathTalk()
    {
        EnsureUISceneLoaded(() =>
        {
            DeathTalkController controller = DeathTalkController.FindOrCreate();
            if (controller == null)
            {
                Debug.LogError("[GameUIController] DeathTalk panel was not found in UIController scene.");
                return;
            }

            controller.Show();
        });
        return true;
    }

    public static void ContinueGame()
    {
        if (instance != null)
        {
            instance.CloseOverlay();
        }
    }

    public static void LeaveGame()
    {
        RunSaveService.CaptureCurrentState();
        LoadMainScene();
    }

    public static void RestartGame()
    {
        Time.timeScale = 1f;
        LoadMainScene();
    }

    public static void LoadMainScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.StartScene, LoadSceneMode.Single);
    }

    private static GameUIController Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameUIController existing = UnityEngine.Object.FindAnyObjectByType<GameUIController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(existing.gameObject);
            }
            return;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        instance = controllerObject.AddComponent<GameUIController>();
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(controllerObject);
        }
    }

    private static void EnsureUISceneLoaded(Action openAction)
    {
        EnsureInstance();
        if (openAction != null)
        {
            pendingOpenAction = openAction;
        }

        if (IsSceneLoaded(SceneNames.UIScene))
        {
            ExecutePendingOpenAction();
            return;
        }

        RequestUISceneLoad();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        instance.ResolveReferences();
        instance.BindButtons();

        if (scene.name == SceneNames.UIScene)
        {
            instance.ApplyDefaultState();
            ExecutePendingOpenAction();
        }
        else if (mode == LoadSceneMode.Single)
        {
            instance.currentPanel = PanelKind.None;
            instance.overlayOpen = false;
            instance.previousTimeScale = 1f;

            if (IsGameplayScene(scene))
            {
                RequestUISceneLoad();
            }
        }
    }

    private static void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (instance == null)
        {
            return;
        }

        instance.ResolveReferences();
        if (!IsAnyGameplaySceneLoaded() && instance.currentPanel == PanelKind.None)
        {
            instance.ApplyDefaultState();
        }
    }

    private static void ExecutePendingOpenAction()
    {
        Action action = pendingOpenAction;
        pendingOpenAction = null;
        action?.Invoke();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtons();
        SubscribeWorldEvents();
        ApplyDefaultState();
    }

    private void Update()
    {
        if (!IsAnyGameplaySceneLoaded())
        {
            return;
        }

        if (Input.GetKeyDown(settingsToggleKey))
        {
            ToggleGameplaySettings();
        }

        if (Input.GetKeyDown(packageToggleKey))
        {
            TryTogglePackage();
        }
    }

    private void OnDisable()
    {
        UnsubscribeWorldEvents();
        UnbindButtons();
    }

    private void SubscribeWorldEvents()
    {
        WorldSwapManager2D.PrimaryWorldChanged -= HandlePrimaryWorldChanged;
        WorldSwapManager2D.PrimaryWorldChanged += HandlePrimaryWorldChanged;
    }

    private void UnsubscribeWorldEvents()
    {
        WorldSwapManager2D.PrimaryWorldChanged -= HandlePrimaryWorldChanged;
    }

    private void HandlePrimaryWorldChanged(WorldKind2D world)
    {
        TalkPanelButtonHandler.CloseAllOpenPanels();
        ApplyPanelState(currentPanel);
    }

    private void OpenPanel(PanelKind panel)
    {
        EnsureEventSystemAvailable();
        ResolveReferences();
        BindButtons();

        bool gameplay = IsAnyGameplaySceneLoaded();
        if (!overlayOpen && gameplay)
        {
            previousTimeScale = Time.timeScale;
        }

        overlayOpen = panel != PanelKind.None;
        currentPanel = panel;
        Time.timeScale = gameplay && overlayOpen ? 0f : 1f;

        ApplyPanelState(panel);
        ResolveReferences();
        BindButtons();

        if (EventSystem.current != null)
        {
            Button selectedButton = panel == PanelKind.Settings ? settingsContinueButton :
                panel == PanelKind.Package ? packageCloseButton :
                panel == PanelKind.Death ? deathRestartButton :
                panel == PanelKind.Win ? winRestartButton : null;

            if (selectedButton != null)
            {
                EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
            }
        }
    }

    private void CloseOverlay()
    {
        currentPanel = PanelKind.None;
        overlayOpen = false;
        Time.timeScale = IsAnyGameplaySceneLoaded()
            ? (previousTimeScale <= 0f ? 1f : previousTimeScale)
            : 1f;

        ApplyDefaultState();
    }

    private void ApplyDefaultState()
    {
        ApplyPanelState(PanelKind.None);
    }

    private void ApplyPanelState(PanelKind panel)
    {
        bool gameplay = IsAnyGameplaySceneLoaded();
        WorldKind2D currentWorld = GetCurrentPlayerWorld();
        SetRootActive(gameUIRoot, gameplay && panel == PanelKind.None && currentWorld == WorldKind2D.Normal);
        SetRootActive(specialGameUIRoot, gameplay && panel == PanelKind.None && currentWorld == WorldKind2D.Special);
        SetRootActive(settingsRoot, panel == PanelKind.Settings);
        SetRootActive(packageRoot, panel == PanelKind.Package);
        SetRootActive(deathRoot, panel == PanelKind.Death, panel == PanelKind.Death);
        SetRootActive(winRoot, panel == PanelKind.Win, panel == PanelKind.Win);
        SetRootActive(end3Root, panel == PanelKind.End3, panel == PanelKind.End3);
        SetRootActive(deathTalkRoot, false);

        if (panel == PanelKind.Package)
        {
            PreparePackagePanel();
            StoryInventoryUIController inventory = FindAnyObjectByType<StoryInventoryUIController>(FindObjectsInactive.Include);
            inventory?.RefreshUI();
        }
        else if (panel == PanelKind.Death || panel == PanelKind.Win || panel == PanelKind.End3)
        {
            ApplyEndingDetailText(panel);
            ApplyEndingSignalPreset(panel);
        }
        else
        {
            WorldVisualStyleController.RefreshAll();
        }
    }

    private void ResolveReferences()
    {
        gameUIRoot = ResolveSceneRoot(gameUIRoot, "GameUI");
        specialGameUIRoot = ResolveSceneRoot(specialGameUIRoot, "SpecialGameUI");
        settingsRoot = ResolveSceneRoot(settingsRoot, "Settings");
        packageRoot = ResolveSceneRoot(packageRoot, "Package");
        deathRoot = ResolveSceneRoot(deathRoot, "Death");
        winRoot = ResolveSceneRoot(winRoot, "Win");
        end3Root = ResolveSceneRoot(end3Root, "End3");
        deathTalkRoot = ResolveSceneRoot(deathTalkRoot, "DeathTalk");

        settingsContinueButton = ResolveButton(settingsRoot, settingsContinueButton, "Continue");
        settingsLeaveButton = ResolveButton(settingsRoot, settingsLeaveButton, "Leave");
        EnsureDeathPanelBinder(deathRoot);
        EnsureDeathPanelBinder(winRoot);
        EnsureDeathPanelBinder(end3Root);
        deathBackButton = ResolveButton(deathRoot, deathBackButton, "Back") ?? ResolveButton(deathRoot, deathBackButton, "Leave");
        deathRestartButton = ResolveButton(deathRoot, deathRestartButton, "Restart");
        winBackButton = ResolveButton(winRoot, winBackButton, "Back") ?? ResolveButton(winRoot, winBackButton, "Leave");
        winRestartButton = ResolveButton(winRoot, winRestartButton, "Restart");
        packageCloseButton = ResolveButton(packageRoot, packageCloseButton, "ESC")
            ?? ResolveButton(packageRoot, packageCloseButton, "Back")
            ?? ResolveButton(packageRoot, packageCloseButton, "Leave");
        deathDetailText = ResolveText(deathRoot, deathDetailText, "Detai") ?? ResolveText(deathRoot, deathDetailText, "Detail");
        winDetailText = ResolveText(winRoot, winDetailText, "Detai") ?? ResolveText(winRoot, winDetailText, "Detail");
        end3DetailText = ResolveText(end3Root, end3DetailText, "Detai") ?? ResolveText(end3Root, end3DetailText, "Detail");
    }

    private void BindButtons()
    {
        BindButton(settingsContinueButton, ContinueGame);
        BindButton(settingsLeaveButton, LeaveGame);
        BindButton(packageCloseButton, ContinueGame);
    }

    private void PreparePackagePanel()
    {
        if (packageRoot == null)
        {
            return;
        }

        packageRoot.SetActive(true);
        if (packageRoot.transform.localScale == Vector3.zero)
        {
            packageRoot.transform.localScale = Vector3.one;
        }

        SetChildPathActive(packageRoot.transform, "Center", true);
        SetChildPathActive(packageRoot.transform, "Center/Package1", true);
        SetChildPathActive(packageRoot.transform, "Center/Package1/Top", true);
        SetChildPathActive(packageRoot.transform, "Center/Package1/Top/Background", true);
        SetChildPathActive(packageRoot.transform, "Center/Package1/Center", true);

        PackageTabController tabs = packageRoot.GetComponent<PackageTabController>()
            ?? packageRoot.GetComponentInChildren<PackageTabController>(true);
        tabs?.ShowPackageTab();
    }

    private static void SetChildPathActive(Transform root, string path, bool active)
    {
        Transform target = root != null ? root.Find(path) : null;
        if (target != null && target.gameObject.activeSelf != active)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void UnbindButtons()
    {
        UnbindButton(settingsContinueButton, ContinueGame);
        UnbindButton(settingsLeaveButton, LeaveGame);
        UnbindButton(packageCloseButton, ContinueGame);
    }

    private static GameObject ResolveSceneRoot(GameObject current, string objectName)
    {
        if (current != null && current.scene.IsValid() && current.scene.isLoaded)
        {
            return current;
        }

        return FindSceneGameObject(SceneNames.UIScene, objectName);
    }

    private static Button ResolveButton(GameObject root, Button current, string buttonName)
    {
        if (current != null &&
            current.gameObject.scene.IsValid() &&
            current.gameObject.scene.isLoaded &&
            (root == null || current.transform.IsChildOf(root.transform)))
        {
            return current;
        }

        return FindButton(root != null ? root.transform : null, buttonName);
    }

    private static Text ResolveText(GameObject root, Text current, string textName)
    {
        if (current != null &&
            current.gameObject.scene.IsValid() &&
            current.gameObject.scene.isLoaded &&
            (root == null || current.transform.IsChildOf(root.transform)))
        {
            return current;
        }

        return FindText(root != null ? root.transform : null, textName);
    }

    private static Button FindButton(Transform root, string buttonName)
    {
        if (root == null)
        {
            return null;
        }

        Button fallback = null;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !MatchesButtonName(button.transform, root, buttonName))
            {
                continue;
            }

            if (button.gameObject.activeSelf)
            {
                return button;
            }

            if (fallback == null)
            {
                fallback = button;
            }
        }

        return fallback;
    }

    private static Text FindText(Transform root, string textName)
    {
        if (root == null)
        {
            return null;
        }

        Text fallback = null;
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || !MatchesTextName(text.transform, root, textName))
            {
                continue;
            }

            if (text.gameObject.activeSelf)
            {
                return text;
            }

            if (fallback == null)
            {
                fallback = text;
            }
        }

        return fallback;
    }

    private static bool MatchesTextName(Transform textTransform, Transform searchRoot, string textName)
    {
        if (textTransform == null || string.IsNullOrWhiteSpace(textName))
        {
            return false;
        }

        Transform current = textTransform;
        while (current != null && current != searchRoot.parent)
        {
            if (current.name == textName || current.name.Contains(textName))
            {
                return true;
            }

            if (current == searchRoot)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool MatchesButtonName(Transform buttonTransform, Transform searchRoot, string buttonName)
    {
        if (buttonTransform == null || string.IsNullOrWhiteSpace(buttonName))
        {
            return false;
        }

        Transform current = buttonTransform;
        while (current != null && current != searchRoot.parent)
        {
            if (current.name == buttonName || current.name.Contains(buttonName))
            {
                return true;
            }

            if (current == searchRoot)
            {
                break;
            }

            current = current.parent;
        }

        Text text = buttonTransform.GetComponentInChildren<Text>(true);
        return text != null && text.text.Contains(buttonName);
    }

    private static GameObject FindSceneGameObject(string sceneName, string objectName)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject fallback = null;
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject sceneObject = allObjects[i];
            if (sceneObject == null || sceneObject.name != objectName)
            {
                continue;
            }

            Scene scene = sceneObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || scene.name != sceneName)
            {
                continue;
            }

            // 优先返回根节点（例如 Package 根 Canvas），避免命中同名的内层容器。
            if (sceneObject.transform.parent == null)
            {
                return sceneObject;
            }

            if (fallback == null)
            {
                fallback = sceneObject;
            }
        }

        return fallback;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = true;
        button.enabled = true;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void EnsureDeathPanelBinder(GameObject root)
    {
        if (root == null || root.GetComponent<DeathPanelButtonBinder>() != null)
        {
            return;
        }

        root.AddComponent<DeathPanelButtonBinder>();
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private void SetRootActive(GameObject root, bool active, bool fadeIn = false)
    {
        if (root == null)
        {
            return;
        }

        if (!active)
        {
            KillRootFade(root);
            root.SetActive(false);
            return;
        }

        if (!fadeIn)
        {
            KillRootFade(root);
            root.SetActive(true);
            CanvasGroup instantGroup = root.GetComponent<CanvasGroup>();
            if (instantGroup != null)
            {
                instantGroup.alpha = 1f;
                instantGroup.interactable = true;
                instantGroup.blocksRaycasts = true;
            }

            return;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }

        bool wasActive = root.activeSelf;
        root.SetActive(true);
        group.interactable = true;
        group.blocksRaycasts = true;

        if (wasActive && group.alpha >= 0.99f)
        {
            return;
        }

        KillRootFade(root);
        group.alpha = 0f;
        rootFadeTweens[root] = group
            .DOFade(1f, Mathf.Max(0.01f, endingFadeDuration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => rootFadeTweens.Remove(root));
    }

    private void KillRootFade(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        if (rootFadeTweens.TryGetValue(root, out Tween tween))
        {
            tween?.Kill();
            rootFadeTweens.Remove(root);
        }
    }

    private void ApplyEndingDetailText(PanelKind panel)
    {
        if (panel == PanelKind.Death && deathDetailText != null)
        {
            deathDetailText.text = BuildDeathDetailText();
        }
        else if (panel == PanelKind.Win && winDetailText != null)
        {
            winDetailText.text = BuildWinDetailText();
        }
        else if (panel == PanelKind.End3 && end3DetailText != null)
        {
            end3DetailText.text = BuildFuseDetailText();
        }
    }

    private static void ApplyEndingSignalPreset(PanelKind panel)
    {
        if (panel == PanelKind.Death)
        {
            WorldVisualStyleController.ApplyEndingSignalPreset(EndingKind.Death);
        }
        else if (panel == PanelKind.Win)
        {
            WorldVisualStyleController.ApplyEndingSignalPreset(EndingKind.Clear);
        }
        else if (panel == PanelKind.End3)
        {
            WorldVisualStyleController.ApplyEndingSignalPreset(EndingKind.Fuse);
        }
    }

    private static string BuildDeathDetailText()
    {
        int floor = EndingService.GetLastDeathFloor();
        if (floor <= 0)
        {
            floor = RunSaveService.GetLatestCorpseFloor();
        }

        return floor > 0 ? $"你死在了第 {floor} 层" : "你死在了楼内";
    }

    private static string BuildWinDetailText()
    {
        EndingKind ending = EndingService.GetLastEnding();
        if (ending == EndingKind.Fuse)
        {
            return "你回应了第 9 层的尸体";
        }

        return "你离开了第 9 层";
    }

    private static string BuildFuseDetailText()
    {
        return "你回应了第 9 层的尸体\n楼内多了一次属于你的呼吸";
    }

    private static WorldKind2D GetCurrentPlayerWorld()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        return manager != null ? manager.GetPlayerWorld() : WorldKind2D.Normal;
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

    private static void EnsureMainMenuCanReceiveInput()
    {
        if (SceneManager.GetActiveScene().name == SceneNames.StartScene)
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
            {
                UnityEditor.EditorApplication.isPaused = false;
            }
#endif
        }

        EnsureEventSystemAvailable();
    }

    private static void EnsureEventSystemAvailable()
    {
        EventSystem current = EventSystem.current;
        if (current != null && current.isActiveAndEnabled)
        {
            return;
        }

        EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system == null || !system.gameObject.scene.IsValid() || !system.gameObject.scene.isLoaded)
            {
                continue;
            }

            system.gameObject.SetActive(true);
            system.enabled = true;
            BaseInputModule inputModule = system.GetComponent<BaseInputModule>();
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(eventSystemObject);
        }
    }

    private static void RequestUISceneLoad()
    {
        if (IsSceneLoaded(SceneNames.UIScene))
        {
            return;
        }

        UIControllerSceneLoader.RequestLoad(SceneNames.UIScene);
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
