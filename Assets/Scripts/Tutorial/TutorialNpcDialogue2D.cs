using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class TutorialDialogueStage
{
    [SerializeField] private string stageName;
    [SerializeField] private string requiredTaskId;
    [TextArea]
    [SerializeField] private string[] startDialogue;
    [TextArea]
    [SerializeField] private string[] incompleteDialogue;
    [TextArea]
    [SerializeField] private string[] completeDialogue;

    public string StageName => stageName;
    public string RequiredTaskId => requiredTaskId;
    public string[] StartDialogue => startDialogue;
    public string[] IncompleteDialogue => incompleteDialogue;
    public string[] CompleteDialogue => completeDialogue;

    public TutorialDialogueStage()
    {
    }

    public TutorialDialogueStage(
        string stageName,
        string requiredTaskId,
        string[] startDialogue,
        string[] incompleteDialogue,
        string[] completeDialogue)
    {
        this.stageName = stageName;
        this.requiredTaskId = requiredTaskId;
        this.startDialogue = startDialogue;
        this.incompleteDialogue = incompleteDialogue;
        this.completeDialogue = completeDialogue;
    }
}

[DisallowMultipleComponent]
public sealed class TutorialNpcDialogue2D : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Transform interactionTarget;
    [SerializeField] private Collider2D interactionCollider;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private TalkPanelButtonHandler talkPanel;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string tutorialSceneName = "TutorialLevel";
    [SerializeField] private bool requireWorldInteractionAllowed = true;
    [SerializeField] private bool disableRegularNpcInteraction = true;

    [Header("Tutorial Flow")]
    [SerializeField] private TutorialDialogueStage[] stages =
    {
        new TutorialDialogueStage(
            "任务简报",
            "ClickTable",
            new[]
            {
                "你是被研究所雇来的外勤探险家。",
                "这栋办公楼看起来正常，但一次秘密维度实验后，异常都藏进了空间底层。",
                "你的任务不是战斗，是记录、判断、确认异常源。",
                "先从普通调查开始。点击带提示的家具，读取现场信息。"
            },
            new[] { "先检查附近的桌子。正式调查里，家具描述会留下判断目标的线索。" },
            new[] { "很好。普通观察只能得到表层信息，真正的异常通常不会直接暴露。" }),
        new TutorialDialogueStage(
            "异常侦测摄像机",
            "TableFound",
            new[]
            {
                "研究所给你的核心装备，是这台异常侦测摄像机。",
                "按数字键 1 打开或收起摄像机。取景框会跟随鼠标移动。",
                "它能短暂看穿正常空间，看到被侵蚀物体留下的另一层痕迹。",
                "这次目标是桌子。对准它，长按鼠标左键完成拍摄。",
                "正式关里，目标不会总是被点名，你要先靠文本推理，再用摄像机确认。"
            },
            new[] { "按 1 打开摄像机，对准桌子，长按鼠标左键拍摄。" },
            new[]
            {
                "记录成功。摄像机确认的是异常源，不是普通可疑物。",
                "正式调查中，找到本层异常源后，右侧出口才会放行。"
            }),
        new TutorialDialogueStage(
            "侵蚀世界",
            "EnteredSpecialWorld",
            new[]
            {
                "如果判断错误，精神稳定度会下降。连续失误会把你拖入侵蚀世界。",
                "那里不是另一间办公室，而是实验泄露后的底层空间。",
                "侵蚀世界里不能使用摄像机，停留时间也很短。",
                "你要寻找不受侵蚀影响的锚点。它往往干净、稳定，甚至显得过于正常。",
                "现在故意在桌子外拍错几次，进入侵蚀世界后靠近吊灯，长按鼠标左键返回。"
            },
            new[] { "在桌子外拍错几次。进入侵蚀世界后，靠近吊灯并长按鼠标左键。" },
            new[]
            {
                "你回来了。记住这种差别：正常世界找被侵蚀的源头，侵蚀世界找仍然正常的锚点。",
                "锚点不是答案本身，它是把你拉回现实的东西。"
            }),
        new TutorialDialogueStage(
            "正式勘探",
            string.Empty,
            new[]
            {
                "接下来的楼层会复用同一套调查流程。",
                "先观察家具，找出异常影响扩散的方向。",
                "再用摄像机确认正常世界里的异常源。",
                "若坠入侵蚀世界，寻找干净、稳定、不被污染的锚点。",
                "确认后，从右侧出口离开。左侧门只是本层入口。",
                "教程结束。回到主界面后，可以开始新的轮回。"
            },
            new[] { "确认线索后，往右走，靠近出口并按 F。" },
            Array.Empty<string>())
    };
    [SerializeField] private bool logDiagnostics;

    private int playerLayer = -1;
    private int currentStageIndex;
    private bool currentStageStarted;
    private bool tutorialFinished;
    private bool awaitingFinalStageFinish;
    private bool returnToMenuPending;
    private float returnToMenuTimer = -1f;
    private bool playerInside;
    private bool interactionAllowed = true;

    private void Awake()
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        ResolveReferences();
        DisableRegularNpcInteractionIfNeeded();
        playerLayer = LayerMask.NameToLayer(playerLayerName);
        SetPromptVisible(false);
        CloseTalk();
    }

    private void OnEnable()
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        TalkPanelButtonHandler.Closed += HandleTalkPanelClosed;
        ResolveReferences();
        DisableRegularNpcInteractionIfNeeded();
        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        TalkPanelButtonHandler.Closed -= HandleTalkPanelClosed;
        SetPromptVisible(false);
        CloseTalk();
    }

    private void Update()
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        if (returnToMenuTimer >= 0f)
        {
            returnToMenuTimer -= Time.unscaledDeltaTime;
            if (returnToMenuTimer <= 0f)
            {
                returnToMenuTimer = -1f;
                ReturnToMainMenu();
                return;
            }
        }

        RefreshInteractionAllowed();
        RefreshPlayerInsideFromOverlap();

        if (playerInside && interactionAllowed && Input.GetKeyDown(interactKey))
        {
            TryPlayCurrentStage();
        }
    }

    public void ConfigureDefaultReferences(Transform target)
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        if (interactionTarget == null)
        {
            interactionTarget = target;
        }

        ResolveReferences();
        DisableRegularNpcInteractionIfNeeded();
    }

    private void TryPlayCurrentStage()
    {
        ResolveTalkPanel();
        if (talkPanel == null || talkPanel.IsOpen || tutorialFinished)
        {
            return;
        }

        TutorialDialogueStage stage = GetCurrentStage();
        if (stage == null)
        {
            tutorialFinished = true;
            return;
        }

        if (!currentStageStarted)
        {
            currentStageStarted = true;
            awaitingFinalStageFinish = currentStageIndex == stages.Length - 1;
            PlayDialogue(stage.StartDialogue);
            LogStage("Start", stage);
            return;
        }

        if (!TutorialTaskRegistry.IsCompleted(stage.RequiredTaskId))
        {
            PlayDialogue(stage.IncompleteDialogue);
            LogStage("Incomplete", stage);
            return;
        }

        PlayStageCompletionAndAdvance(stage);
    }

    private void HandleTalkPanelClosed(TalkPanelButtonHandler panel)
    {
        if (!IsSceneAllowed() || !awaitingFinalStageFinish || returnToMenuPending)
        {
            return;
        }

        returnToMenuPending = true;
        returnToMenuTimer = 0.5f;
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.StartScene, LoadSceneMode.Single);
    }

    private void PlayStageCompletionAndAdvance(TutorialDialogueStage completedStage)
    {
        List<string> lines = new List<string>();
        AddLines(lines, completedStage.CompleteDialogue);

        currentStageIndex++;
        TutorialDialogueStage nextStage = GetCurrentStage();
        if (nextStage == null)
        {
            tutorialFinished = true;
            currentStageStarted = false;
        }
        else
        {
            currentStageStarted = true;
            awaitingFinalStageFinish = currentStageIndex == stages.Length - 1;
            AddLines(lines, nextStage.StartDialogue);
        }

        PlayDialogue(lines.ToArray());
        LogStage("Complete", completedStage);
    }

    private TutorialDialogueStage GetCurrentStage()
    {
        if (stages == null || currentStageIndex < 0 || currentStageIndex >= stages.Length)
        {
            return null;
        }

        return stages[currentStageIndex];
    }

    private void PlayDialogue(string[] lines)
    {
        if (talkPanel == null)
        {
            return;
        }

        string[] playableLines = HasAnyLine(lines) ? lines : new[] { string.Empty };
        talkPanel.Play(this, playableLines);
    }

    private static void AddLines(List<string> target, string[] source)
    {
        if (target == null || source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            target.Add(source[i] ?? string.Empty);
        }
    }

    private static bool HasAnyLine(string[] lines)
    {
        return lines != null && lines.Length > 0;
    }

    private void RefreshInteractionAllowed()
    {
        if (!requireWorldInteractionAllowed)
        {
            interactionAllowed = true;
            return;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        Transform target = interactionTarget != null ? interactionTarget : transform;
        interactionAllowed = manager == null || manager.IsInteractionAllowedForPlayer(target);

        if (!interactionAllowed)
        {
            playerInside = false;
            SetPromptVisible(false);
            CloseTalk();
        }
    }

    private void RefreshPlayerInsideFromOverlap()
    {
        if (!interactionAllowed || interactionCollider == null || !interactionCollider.enabled)
        {
            return;
        }

        bool overlappingPlayer = false;
        if (playerLayer >= 0)
        {
            Bounds bounds = interactionCollider.bounds;
            Collider2D hit = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, 1 << playerLayer);
            overlappingPlayer = hit != null && IsPlayer(hit);
        }

        if (!overlappingPlayer)
        {
            Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
            Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
            overlappingPlayer = playerCollider != null && playerCollider.bounds.Intersects(interactionCollider.bounds);
        }

        playerInside = overlappingPlayer;
        SetPromptVisible(playerInside && interactionAllowed && !tutorialFinished);
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (playerLayer >= 0 && other.gameObject.layer == playerLayer)
        {
            return true;
        }

        return other.GetComponentInParent<Player2DMovementController>() != null;
    }

    private void ResolveReferences()
    {
        if (interactionTarget == null)
        {
            interactionTarget = transform;
        }

        if (interactionCollider == null && interactionTarget != null)
        {
            interactionCollider = interactionTarget.GetComponent<Collider2D>();
        }

        if (promptObject == null && interactionTarget != null)
        {
            Transform prompt = interactionTarget.Find("Button") ?? interactionTarget.Find("Name");
            if (prompt == null && interactionTarget.parent != null)
            {
                prompt = interactionTarget.parent.Find("Button") ?? interactionTarget.parent.Find("Name");
            }

            if (prompt != null)
            {
                promptObject = prompt.gameObject;
            }
        }

        ResolveTalkPanel();
    }

    private void ResolveTalkPanel()
    {
        TalkPanelButtonHandler preferredPanel = TalkPanelButtonHandler.FindPreferredPanel();
        if (preferredPanel != null)
        {
            talkPanel = preferredPanel;
        }
    }

    private void DisableRegularNpcInteractionIfNeeded()
    {
        if (!disableRegularNpcInteraction || interactionTarget == null || !IsSceneAllowed())
        {
            return;
        }

        NpcInteraction2D regularNpc = interactionTarget.GetComponent<NpcInteraction2D>();
        if (regularNpc != null)
        {
            regularNpc.enabled = false;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptObject != null && promptObject.activeSelf != visible)
        {
            promptObject.SetActive(visible);
        }
    }

    private void CloseTalk()
    {
        if (talkPanel != null)
        {
            talkPanel.Close(this);
        }
    }

    private void LogStage(string action, TutorialDialogueStage stage)
    {
        if (!logDiagnostics || stage == null)
        {
            return;
        }

        Debug.Log($"[TutorialNpcDialogue2D] {action}: stage={stage.StageName}, task={stage.RequiredTaskId}", this);
    }

    private bool IsSceneAllowed()
    {
        return string.IsNullOrWhiteSpace(tutorialSceneName) || gameObject.scene.name == tutorialSceneName;
    }
}
