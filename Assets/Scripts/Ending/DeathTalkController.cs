using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DeathTalkController : MonoBehaviour
{
    private const int SortingOrder = 21000;

    [SerializeField] private Button joinButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private GameObject talkRoot;

    public bool IsOpen => gameObject.activeInHierarchy;

    private void Awake()
    {
        BindButtons();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    public static DeathTalkController FindOrCreate()
    {
        DeathTalkController controller = FindAnyObjectByType<DeathTalkController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.BindButtons();
            return controller;
        }

        GameObject root = FindSceneObject("DeathTalk");
        if (root == null)
        {
            return null;
        }

        controller = root.GetComponent<DeathTalkController>();
        if (controller == null)
        {
            controller = root.AddComponent<DeathTalkController>();
        }

        controller.BindButtons();
        return controller;
    }

    public void Show()
    {
        TalkPanelButtonHandler.CloseAllOpenPanels();
        gameObject.SetActive(true);
        EnsurePanelReceivesInput();
        BindButtons();
        ResolveTalkRoot();
        if (talkRoot != null)
        {
            talkRoot.SetActive(true);
            EnsureActionButtonsOnTop();
        }
    }

    public void Hide()
    {
        ResolveTalkRoot();
        if (talkRoot != null)
        {
            talkRoot.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    private void BindButtons()
    {
        ResolveTalkRoot();
        DisableNonActionRaycasts();
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null)
            {
                continue;
            }

            if (joinButton == null && IsButton(btn, "Join", "加入"))
            {
                joinButton = btn;
            }

            if (leaveButton == null && IsButton(btn, "Leave", "离开"))
            {
                leaveButton = btn;
            }
        }

        if (joinButton != null)
        {
            EnableButtonRaycast(joinButton);
            joinButton.onClick.RemoveListener(OnJoinClicked);
            joinButton.onClick.AddListener(OnJoinClicked);
        }

        if (leaveButton != null)
        {
            EnableButtonRaycast(leaveButton);
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        DisableContainerButtons();
        EnsureActionButtonsOnTop();
    }

    private void EnsurePanelReceivesInput()
    {
        transform.SetAsLastSibling();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void EnsureActionButtonsOnTop()
    {
        if (joinButton != null)
        {
            joinButton.transform.SetAsLastSibling();
        }

        if (leaveButton != null)
        {
            leaveButton.transform.SetAsLastSibling();
        }
    }

    private void DisableNonActionRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    private void ResolveTalkRoot()
    {
        if (talkRoot != null)
        {
            return;
        }

        Transform talk = transform.Find("Talk");
        if (talk == null)
        {
            talk = FindChildNamed(transform, "Talk");
        }

        if (talk != null)
        {
            talkRoot = talk.gameObject;
        }
    }

    private void OnJoinClicked()
    {
        Debug.Log(
            $"[DeathTalkController] Join clicked. hasItems={EndingService.HasAllFuseItems()}, hasCorpse={EndingService.HasLatestCorpse()}",
            this);
        Hide();
        GameUIController.ContinueGame();
        if (EndingService.TryTriggerFuseEnding())
        {
            GameUIController.ShowFuseEnding();
            return;
        }

        Debug.LogWarning("[DeathTalkController] Join failed: fuse ending conditions are not met.", this);
        GameUIController.ShowDeath();
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[DeathTalkController] Leave clicked.", this);
        Hide();
        GameUIController.ContinueGame();
    }

    private void DisableContainerButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null || btn == joinButton || btn == leaveButton)
            {
                continue;
            }

            if (btn.name == "Talk" || (talkRoot != null && btn.gameObject == talkRoot))
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = false;
                btn.enabled = false;
                DisableButtonRaycast(btn);
            }
        }
    }

    private static void EnableButtonRaycast(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = true;
        button.enabled = true;
        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = true;
            }
        }
    }

    private static void DisableButtonRaycast(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic[] graphics = button.GetComponents<Graphic>();
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    private static bool IsButton(Button button, string objectName, string label)
    {
        if (button == null)
        {
            return false;
        }

        if (button.name == objectName || button.name.Contains(objectName))
        {
            return true;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        return text != null && text.text.Contains(label);
    }

    private static GameObject FindSceneObject(string objectName)
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
            if (scene.IsValid() && scene.isLoaded)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private static Transform FindChildNamed(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildNamed(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
