using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

    [RequireComponent(typeof(Button))]
public sealed class TalkPanelButtonHandler : MonoBehaviour
{
    /// <summary>对话框关闭时触发（教程最终阶段回主界面等场景使用）。</summary>
    public static event System.Action<TalkPanelButtonHandler> Closed;
    /// <summary>仅点框模式（BoxClickOnly）下，玩家点击对话框时触发。</summary>
    public event System.Action BoxConfirmed;

    [SerializeField] private Text dialogueText;
    [SerializeField] private Text nameText;
    [SerializeField] private float lineTypewriterDuration = 0.75f;
    [SerializeField] private float singleLineAutoCloseDelay = 2f;

    private Button button;
    private Canvas parentCanvas;
    private Component owner;
    private string[] lines;
    private int lineIndex;
    private Coroutine closeRoutine;
    private Tween textTween;
    private string currentFullText;
    private bool isAutoClosingLine;

    public bool IsOpen => gameObject.activeInHierarchy;

    /// <summary>
    /// 仅点击对话框才能推进/确认的模式（尸体回应等特殊对话）。
    /// 开启后点击场景任意位置都不会推进。
    /// </summary>
    public bool BoxClickOnly { get; set; }

    public static bool IsScreenPointInsideAnyOpenPanel(Vector2 screenPosition)
    {
        TalkPanelButtonHandler[] panels = Resources.FindObjectsOfTypeAll<TalkPanelButtonHandler>();
        for (int i = 0; i < panels.Length; i++)
        {
            TalkPanelButtonHandler panel = panels[i];
            if (panel == null || !panel.gameObject.scene.IsValid() || !panel.IsOpen)
            {
                continue;
            }

            RectTransform rectTransform = panel.transform as RectTransform;
            if (rectTransform == null)
            {
                continue;
            }

            panel.ResolveParentCanvas();
            Camera eventCamera = panel.parentCanvas != null ? panel.parentCanvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    public static void CloseAllOpenPanels()
    {
        TalkPanelButtonHandler[] panels = Resources.FindObjectsOfTypeAll<TalkPanelButtonHandler>();
        for (int i = 0; i < panels.Length; i++)
        {
            TalkPanelButtonHandler panel = panels[i];
            if (panel != null && panel.gameObject.scene.IsValid() && panel.IsOpen)
            {
                panel.Close();
            }
        }
    }

    public static TalkPanelButtonHandler FindPreferredPanel()
    {
        TalkPanelButtonHandler[] panels = Resources.FindObjectsOfTypeAll<TalkPanelButtonHandler>();
        TalkPanelButtonHandler fallback = null;
        TalkPanelButtonHandler uiSceneFallback = null;
        WorldKind2D desiredWorld = GetCurrentPlayerWorld();

        for (int i = 0; i < panels.Length; i++)
        {
            TalkPanelButtonHandler panel = panels[i];
            if (panel == null || !panel.gameObject.scene.IsValid())
            {
                continue;
            }

            if (IsDeathTalkPanel(panel))
            {
                continue;
            }

            if (panel.gameObject.scene.name == SceneNames.UIScene)
            {
                if (PanelMatchesWorld(panel, desiredWorld))
                {
                    // 优先返回当前层级已激活的面板（SpecialTalk1 取代已禁用的旧 SpecialTalk）
                    if (panel.gameObject.activeInHierarchy)
                    {
                        return panel;
                    }

                    if (uiSceneFallback == null || panel.name == "Talk")
                    {
                        uiSceneFallback = panel;
                    }
                }
                continue;
            }

            if (fallback == null || panel.name == "Talk")
            {
                fallback = panel;
            }
        }

        return uiSceneFallback != null ? uiSceneFallback : fallback;
    }

    private static WorldKind2D GetCurrentPlayerWorld()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        return manager != null ? manager.GetPlayerWorld() : WorldKind2D.Normal;
    }

    private static bool PanelMatchesWorld(TalkPanelButtonHandler panel, WorldKind2D world)
    {
        if (IsDeathTalkPanel(panel))
        {
            return false;
        }

        bool specialPanel = IsUnderRootNamed(panel.transform, "SpecialTalk") ||
                            IsUnderRootNamed(panel.transform, "SpecialTalk1");
        return world == WorldKind2D.Special ? specialPanel : !specialPanel;
    }

    private static bool IsDeathTalkPanel(TalkPanelButtonHandler panel)
    {
        return panel != null && IsUnderRootNamed(panel.transform, "DeathTalk");
    }

    private static bool IsUnderRootNamed(Transform transform, string rootName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == rootName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void Awake()
    {
        ResolveParentCanvas();
        button = GetComponent<Button>();
        ResolveDialogueText();

        // 对话推进改为“点击任意处”，对话框自身不再拦截场景点击；
        // 点击推进由 Update 中的全局点击处理统一负责（交互家具优先）。
        DisablePanelRaycastTargets();
    }

    private void OnDestroy()
    {
        StopAutoPlayback();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        StopTextTween();
        ShowNextLine();
    }

    public void Play(string[] dialogueLines)
    {
        Play(null, null, dialogueLines);
    }

    public void Play(Component dialogueOwner, string[] dialogueLines)
    {
        Play(dialogueOwner, null, dialogueLines);
    }

    public void Play(Component dialogueOwner, string objectName, string[] dialogueLines)
    {
        StopAutoPlayback();
        ResolveParentCanvas();
        EnsureParentsActive();
        if (parentCanvas != null)
        {
            parentCanvas.gameObject.SetActive(true);
        }

        owner = dialogueOwner;
        lines = dialogueLines;
        lineIndex = 0;
        isAutoClosingLine = false;
        gameObject.SetActive(true);

        ResolveNameText();
        if (nameText != null)
        {
            nameText.text = objectName ?? string.Empty;
            nameText.gameObject.SetActive(!string.IsNullOrEmpty(objectName));
        }

        ShowCurrentLine();
    }

    public void PlayAutoClosingLine(string line, float typewriterDuration, float closeDelay)
    {
        StopAutoPlayback();
        ResolveParentCanvas();
        EnsureParentsActive();
        if (parentCanvas != null)
        {
            parentCanvas.gameObject.SetActive(true);
        }

        owner = null;
        lines = null;
        lineIndex = 0;
        isAutoClosingLine = true;
        gameObject.SetActive(true);
        ResolveDialogueText();

        if (dialogueText != null)
        {
            Color textColor = dialogueText.color;
            if (textColor.a <= 0f)
            {
                textColor.a = 1f;
                dialogueText.color = textColor;
            }

            string targetText = line ?? string.Empty;
            float duration = Mathf.Max(0f, typewriterDuration);
            dialogueText.text = string.Empty;

            if (duration <= 0f || targetText.Length == 0)
            {
                dialogueText.text = targetText;
                closeRoutine = StartCoroutine(CloseAfterDelay(closeDelay));
                return;
            }

            textTween = dialogueText
                .DOText(targetText, duration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    textTween = null;
                    closeRoutine = StartCoroutine(CloseAfterDelay(closeDelay));
                });

            return;
        }

        closeRoutine = StartCoroutine(CloseAfterDelay(closeDelay));
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (BoxClickOnly)
        {
            // 仅点框模式：只有点击对话框本身才推进，并触发确认回调。
            if (Input.GetMouseButtonDown(0) && IsScreenPointInsideAnyOpenPanel(Input.mousePosition))
            {
                ShowNextLine();
                BoxConfirmed?.Invoke();
            }

            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Vector2 mouseWorldPosition;
        if (TryGetMouseWorldPosition(out mouseWorldPosition) &&
            FurnitureInteraction2D.IsMouseOverAnyInteraction(mouseWorldPosition))
        {
            // 交互家具优先级更高：点击家具时由 FurnitureInteraction2D 处理
            // （别的家具 -> 切换文本；当前家具 -> 重置文本）。
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // 点击其他 UI（设置、背包、死亡面板等）不推进对话。
            return;
        }

        ShowNextLine();
    }

    private static bool TryGetMouseWorldPosition(out Vector2 mouseWorldPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mouseWorldPosition = default;
            return false;
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPosition = new Vector2(mouseWorld.x, mouseWorld.y);
        return true;
    }

    private void DisablePanelRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }

        if (button != null && button.targetGraphic != null)
        {
            button.targetGraphic.raycastTarget = false;
        }
    }

    public void Close()
    {
        StopAutoPlayback();
        owner = null;
        lines = null;
        lineIndex = 0;
        currentFullText = null;
        isAutoClosingLine = false;

        ResolveNameText();
        if (nameText != null)
        {
            nameText.text = string.Empty;
            nameText.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
        Closed?.Invoke(this);
    }

    public void Close(Component dialogueOwner)
    {
        if (isAutoClosingLine)
        {
            return;
        }

        if (owner == null)
        {
            return;
        }

        if (owner != null && owner != dialogueOwner)
        {
            return;
        }

        Close();
    }

    private void ShowNextLine()
    {
        if (lines == null || lines.Length == 0)
        {
            Close();
            return;
        }

        lineIndex++;
        if (lineIndex >= lines.Length)
        {
            Close();
            return;
        }

        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        ResolveDialogueText();
        if (dialogueText != null)
        {
            PlayLineText(lines != null && lineIndex < lines.Length ? lines[lineIndex] : string.Empty);
        }
    }

    private void PlayLineText(string text)
    {
        StopTextTween();

        currentFullText = text ?? string.Empty;
        dialogueText.text = string.Empty;

        if (lineTypewriterDuration <= 0f || currentFullText.Length == 0)
        {
            dialogueText.text = currentFullText;
            StartSingleLineAutoCloseIfNeeded();
            return;
        }

        textTween = dialogueText
            .DOText(currentFullText, lineTypewriterDuration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                textTween = null;
                StartSingleLineAutoCloseIfNeeded();
            });
    }

    private bool CompleteCurrentLineTween()
    {
        if (textTween == null)
        {
            return false;
        }

        StopTextTween();
        ResolveDialogueText();
        if (dialogueText != null)
        {
            dialogueText.text = currentFullText ?? string.Empty;
        }

        StartSingleLineAutoCloseIfNeeded();
        return true;
    }

    private void StartSingleLineAutoCloseIfNeeded()
    {
        if (BoxClickOnly)
        {
            return;
        }

        if (lines == null || lines.Length != 1)
        {
            return;
        }

        StopCloseRoutine();
        closeRoutine = StartCoroutine(CloseAfterDelay(singleLineAutoCloseDelay));
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
        closeRoutine = null;
        Close();
    }

    private void StopCloseRoutine()
    {
        if (closeRoutine == null)
        {
            return;
        }

        StopCoroutine(closeRoutine);
        closeRoutine = null;
    }

    private void StopTextTween()
    {
        if (textTween == null)
        {
            return;
        }

        textTween.Kill();
        textTween = null;
    }

    private void StopAutoPlayback()
    {
        StopCloseRoutine();
        StopTextTween();
    }

    private void ResolveDialogueText()
    {
        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<Text>(true);
        }
    }

    private void ResolveNameText()
    {
        if (nameText == null)
        {
            Transform nameTextTransform = transform.Find("name/Text");
            if (nameTextTransform != null)
            {
                nameText = nameTextTransform.GetComponent<Text>();
            }
        }
    }

    private void ResolveParentCanvas()
    {
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>(true);
        }
    }

    private void EnsureParentsActive()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current = current.parent;
        }
    }
}
