using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DeathPanelButtonBinder : MonoBehaviour
{
    private const string InputBlockerName = "InputBlocker";

    [SerializeField] private Button leaveButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private int deathSortingOrder = 20000;

    private bool leaveRuntimeListenerAdded;
    private bool restartRuntimeListenerAdded;

    private void Awake()
    {
        ResolveButtons();
    }

    private void OnEnable()
    {
        EnsurePanelReceivesInput();
        ResolveButtons();
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    public void OnLeaveClicked()
    {
        GameUIController.LoadMainScene();
    }

    public void OnRestartClicked()
    {
        GameUIController.RestartGame();
    }

    private void ResolveButtons()
    {
        leaveButton = ResolveButton(leaveButton, "Leave") ?? ResolveButton(leaveButton, "Back");
        restartButton = ResolveButton(restartButton, "Restart");
    }

    private Button ResolveButton(Button current, string buttonName)
    {
        if (current != null && current.transform.IsChildOf(transform))
        {
            return current;
        }

        Button fallback = null;
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !MatchesButtonName(button.transform, buttonName))
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

    private bool MatchesButtonName(Transform buttonTransform, string buttonName)
    {
        Transform current = buttonTransform;
        while (current != null && current != transform.parent)
        {
            if (current.name == buttonName || current.name.Contains(buttonName))
            {
                return true;
            }

            if (current == transform)
            {
                break;
            }

            current = current.parent;
        }

        Text text = buttonTransform.GetComponentInChildren<Text>(true);
        return text != null && text.text.Contains(buttonName);
    }

    private void BindButtons()
    {
        leaveRuntimeListenerAdded = BindFallbackListener(
            leaveButton,
            OnLeaveClicked,
            nameof(OnLeaveClicked));
        restartRuntimeListenerAdded = BindFallbackListener(
            restartButton,
            OnRestartClicked,
            nameof(OnRestartClicked));
    }

    private void UnbindButtons()
    {
        if (leaveRuntimeListenerAdded)
        {
            UnbindButton(leaveButton, OnLeaveClicked);
            leaveRuntimeListenerAdded = false;
        }

        if (restartRuntimeListenerAdded)
        {
            UnbindButton(restartButton, OnRestartClicked);
            restartRuntimeListenerAdded = false;
        }
    }

    private bool BindFallbackListener(
        Button button,
        UnityEngine.Events.UnityAction action,
        string methodName)
    {
        if (button == null)
        {
            return false;
        }

        button.enabled = true;
        button.interactable = true;
        EnsureRaycastTarget(button);

        if (HasPersistentListener(button, methodName))
        {
            return false;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        return true;
    }

    private bool HasPersistentListener(Button button, string methodName)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    private static void EnsureRaycastTarget(Button button)
    {
        if (button.targetGraphic != null)
        {
            button.targetGraphic.raycastTarget = true;
            return;
        }

        Graphic graphic = button.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
        {
            graphic.raycastTarget = true;
            button.targetGraphic = graphic;
        }
    }

    private void EnsurePanelReceivesInput()
    {
        transform.SetAsLastSibling();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = deathSortingOrder;
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        EnsureInputBlocker();
        DisableDecorativeRaycastTargets();
    }

    private void EnsureInputBlocker()
    {
        Transform blockerTransform = transform.Find(InputBlockerName);
        GameObject blockerObject;
        if (blockerTransform == null)
        {
            blockerObject = new GameObject(InputBlockerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blockerTransform = blockerObject.transform;
            blockerTransform.SetParent(transform, false);
        }
        else
        {
            blockerObject = blockerTransform.gameObject;
        }

        blockerObject.SetActive(true);
        blockerTransform.SetAsFirstSibling();

        RectTransform rectTransform = blockerObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        Image image = blockerObject.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        if (leaveButton != null)
        {
            leaveButton.transform.SetAsLastSibling();
        }

        if (restartButton != null)
        {
            restartButton.transform.SetAsLastSibling();
        }
    }

    private void DisableDecorativeRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            if (graphic.gameObject.name == InputBlockerName)
            {
                graphic.raycastTarget = true;
                continue;
            }

            if (graphic.GetComponentInParent<Button>(true) == null)
            {
                graphic.raycastTarget = false;
            }
        }
    }
}
