using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string PackageButtonName = "Package";
    private const string PackageButtonLabel = "\u4FB5\u8680\u6863\u6848";

    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button packageButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Transform mainMenuCenter;
    [SerializeField] private string firstLevelSceneName = SceneNames.FirstLevel;
    [SerializeField] private float hoverBackgroundAlpha = 0.22f;
    [SerializeField] private float hoverFadeDuration = 0.12f;

    private bool settingsOpen;

    private void Awake()
    {
        ResolveReferences();
        EnsureSettingsButtonVisible();
        BindButtons();
        SetupHoverBackgrounds();
        SetSettingsPanelVisible(false);
    }

    private void LateUpdate()
    {
        EnsureSettingsButtonVisible();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        RunSaveService.StartNewRun();
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void ContinueGame()
    {
        Time.timeScale = 1f;
        if (!RunSaveService.TryBeginExistingRun())
        {
            Debug.LogWarning("[MainMenuController] Continue requested, but no active run-save exists.");
            return;
        }

        SceneManager.LoadScene(RunSaveService.GetContinueSceneName());
    }

    public void OpenPackage()
    {
        GameUIController.OpenPackageFromMainMenu();
    }

    public void OpenSettings()
    {
        Debug.Log("\u0053\u0065\u0074\u0074\u0069\u006e\u0067\u0073\u88ab\u70b9\u51fb\u4e86");
    }

    private void BindButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (packageButton != null)
        {
            EnsureButtonReceivesInput(packageButton);
            packageButton.onClick.RemoveListener(OpenPackage);
            packageButton.onClick.AddListener(OpenPackage);
        }

        if (settingsButton != null)
        {
            settingsButton.interactable = true;
        }
    }

    private void UnbindButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
        }

        if (packageButton != null)
        {
            packageButton.onClick.RemoveListener(OpenPackage);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
        }
    }

    private void ResolveReferences()
    {
        if (restartButton == null)
        {
            restartButton = FindMainMenuButton("Restart", mainMenuCenter);
        }

        if (continueButton == null)
        {
            continueButton = FindMainMenuButton("Continue", mainMenuCenter);
        }

        if (packageButton == null)
        {
            packageButton = FindMainMenuButton(PackageButtonName, mainMenuCenter)
                ?? FindMainMenuButtonByLabel(PackageButtonLabel, mainMenuCenter);
        }

        if (settingsButton == null)
        {
            settingsButton = FindMainMenuButton("Settings", mainMenuCenter);
        }

        if (settingsPanel == null)
        {
            settingsPanel = FindSettingsPanel();
        }
    }

    private static Button FindMainMenuButton(string objectName, Transform centerOverride)
    {
        Transform center = centerOverride != null ? centerOverride : FindMainMenuCenter();
        if (center == null)
        {
            return null;
        }

        Transform child = center.Find(objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Button FindMainMenuButtonByLabel(string label, Transform centerOverride)
    {
        Transform center = centerOverride != null ? centerOverride : FindMainMenuCenter();
        if (center == null)
        {
            return null;
        }

        Text[] texts = center.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
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

    private static void EnsureButtonReceivesInput(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.enabled = true;
        button.interactable = true;

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

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null && graphic != targetGraphic)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private static Transform FindMainMenuCenter()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "Canvas")
            {
                continue;
            }

            Transform center = root.transform.Find("Center");
            if (center != null)
            {
                return center;
            }
        }

        return null;
    }

    private void SetupHoverBackgrounds()
    {
        SetupHoverBackground(restartButton);
        SetupHoverBackground(continueButton);
        SetupHoverBackground(packageButton);
        SetupHoverBackground(settingsButton);
    }

    private void SetupHoverBackground(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic background = FindHoverBackground(button.transform);
        if (background == null)
        {
            return;
        }

        ButtonHoverFadeBackground hover = button.GetComponent<ButtonHoverFadeBackground>();
        if (hover == null)
        {
            hover = button.gameObject.AddComponent<ButtonHoverFadeBackground>();
        }

        hover.Configure(background, hoverBackgroundAlpha, hoverFadeDuration);
    }

    private void EnsureSettingsButtonVisible()
    {
        if (settingsButton == null)
        {
            settingsButton = FindMainMenuButton("Settings", mainMenuCenter);
            if (settingsButton == null)
            {
                return;
            }
        }

        if (!settingsButton.gameObject.activeSelf)
        {
            Debug.LogWarning("[MainMenuController] Settings button was hidden and has been restored.");
            settingsButton.gameObject.SetActive(true);
        }

        Graphic[] graphics = settingsButton.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.gameObject.SetActive(true);
            graphic.enabled = true;

            if (graphic.gameObject == settingsButton.gameObject || graphic.gameObject.name.StartsWith("GameObject"))
            {
                continue;
            }

            Color color = graphic.color;
            if (color.a <= 0f)
            {
                color.a = graphic.gameObject.name.StartsWith("GameObject") ? hoverBackgroundAlpha : 1f;
                graphic.color = color;
            }
        }

        Button button = settingsButton.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
        }

        Image image = settingsButton.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

    }

    private void SetSettingsPanelVisible(bool visible)
    {
        settingsOpen = visible;
        if (settingsPanel == null)
        {
            return;
        }

        settingsPanel.SetActive(visible);
        if (visible)
        {
            settingsPanel.transform.SetAsLastSibling();
        }
    }

    private static GameObject FindSettingsPanel()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "Canvas")
            {
                continue;
            }

            for (int childIndex = 0; childIndex < root.transform.childCount; childIndex++)
            {
                Transform child = root.transform.GetChild(childIndex);
                if (child == null || child.name == "Center")
                {
                    continue;
                }

                GameObject candidate = child.gameObject;
                if (candidate.GetComponent<Button>() != null || candidate.GetComponent<Canvas>() == null)
                {
                    continue;
                }

                if (candidate.name.Contains("Settings"))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static Graphic FindHoverBackground(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return null;
        }

        for (int i = 0; i < buttonTransform.childCount; i++)
        {
            Transform child = buttonTransform.GetChild(i);
            if (child == null || !child.name.StartsWith("GameObject"))
            {
                continue;
            }

            Graphic graphic = child.GetComponent<Graphic>();
            if (graphic != null)
            {
                return graphic;
            }
        }

        return null;
    }
}
