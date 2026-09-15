using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 楼层门交互与场景切换控制。
/// 门不是单纯加载下一张场景，它还会检查是否完成本层视界目标、处理第 9 层通关、
/// 以及配合 RunSaveService 推进当前楼层。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class DoorLevelTransition2D : MonoBehaviour
{
    [SerializeField] private GameObject promptObject;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private bool requireVisionTargetFound = true;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private bool logDiagnostics = true;
    [SerializeField, TextArea] private string tutorialBlockMessage = "教程关不能离开，可以回去找NPC了";

    [Header("Door Sound")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenSound;

    private int playerLayer = -1;
    private bool playerInside;
    private bool interactionAllowed = true;
    private Collider2D triggerCollider;
    private bool transitionLoading;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        ResolveReferences();
        SetPromptVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (CameraFocusModeController.IsCameraModeActive)
        {
            // 摄像头/视界模式中隐藏门提示，避免玩家一边瞄准一边触发换层。
            SetPromptVisible(false);
            return;
        }

        RefreshInteractionAllowed();
        RefreshPlayerInsideFromOverlap();

        if (playerInside && interactionAllowed && Input.GetKeyDown(interactKey))
        {
            TryEnterNextLevel();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;
        RefreshInteractionAllowed();
        SetPromptVisible(interactionAllowed);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;
        SetPromptVisible(interactionAllowed);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = false;
        SetPromptVisible(false);
    }

    private void TryEnterNextLevel()
    {
        if (gameObject.scene.name == SceneNames.TutorialLevel)
        {
            ShowTutorialBlockHint();
            return;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        if (requireVisionTargetFound && (manager == null || !manager.HasFoundVisionTarget))
        {
            // 正式楼层需要先找到异常目标再允许进入下一层，保证玩法流程不会被跳过。
            if (logDiagnostics)
            {
                Debug.Log($"[DoorLevelTransition2D] \u8FD8\u6CA1\u6709\u627E\u5230SpecialWorld\u76EE\u6807\uFF0C\u65E0\u6CD5\u8FDB\u5165\u4E0B\u4E00\u5173: {name}", this);
            }

            return;
        }

        string sceneName;
        if (!RunSaveService.TryAdvanceToNextFloor(out sceneName))
        {
            if (RunSaveService.Current != null &&
                RunSaveService.Current.currentFloor >= RunSaveService.MaxFloorCount)
            {
                // 第 9 层：触发结局二（通关）。记录 + 占位提示，先留在关卡。
                EndingService.TriggerClearEnding();
                RunSaveService.EndCurrentRun();
                GameUIController.ShowWin();
                return;
            }

            Debug.LogWarning("[DoorLevelTransition2D] No next floor is available.", this);
            return;
        }

        if (transitionLoading)
        {
            return;
        }

        Time.timeScale = 1f;
        PlayDoorOpenSound();
        StartCoroutine(LoadSceneAfterDelay(sceneName, 0.45f));
    }

    private System.Collections.IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        transitionLoading = true;
        yield return new WaitForSecondsRealtime(delay);
        transitionLoading = false;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void PlayDoorOpenSound()
    {
        if (doorAudioSource == null)
        {
            doorAudioSource = GetComponent<AudioSource>();
        }

        if (doorOpenSound == null)
        {
            doorOpenSound = Resources.Load<AudioClip>("UI/sound/开门声1");
        }

        if (doorOpenSound != null && doorAudioSource != null)
        {
            doorAudioSource.PlayOneShot(doorOpenSound, GameAudioManager.SfxScaleForClip(doorOpenSound));
        }
    }

    private void ShowTutorialBlockHint()
    {
        TalkPanelButtonHandler panel = TalkPanelButtonHandler.FindPreferredPanel();
        if (panel == null)
        {
            return;
        }

        string message = string.IsNullOrWhiteSpace(tutorialBlockMessage)
            ? "教程关不能离开，可以回去找NPC了"
            : tutorialBlockMessage;
        panel.PlayAutoClosingLine(message, 0.75f, 2f);
    }

    private void RefreshInteractionAllowed()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;

        // 门也遵守双世界交互限制：玩家只能和当前主世界里可交互的门发生交互。
        interactionAllowed = manager == null || manager.IsInteractionAllowedForPlayer(transform);

        if (!interactionAllowed)
        {
            playerInside = false;
            SetPromptVisible(false);
        }
    }

    private void RefreshPlayerInsideFromOverlap()
    {
        if (!interactionAllowed)
        {
            return;
        }

        bool overlappingPlayer = false;
        Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
        if (triggerCollider != null && triggerCollider.enabled && playerLayer >= 0)
        {
            // 优先用触发器 bounds 做 OverlapBox，修正某些情况下 OnTriggerExit2D 没及时触发导致的提示残留。
            Bounds bounds = triggerCollider.bounds;
            Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, 1 << playerLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsPlayer(hits[i]))
                {
                    overlappingPlayer = true;
                    break;
                }
            }
        }

        if (!overlappingPlayer && triggerCollider != null && triggerCollider.enabled)
        {
            // 如果玩家不在指定 Layer，退一步用两个 Collider 的 bounds 判断。
            Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
            overlappingPlayer = playerCollider != null && playerCollider.bounds.Intersects(triggerCollider.bounds);
        }

        if (!overlappingPlayer && player != null)
        {
            // 最后的兜底是距离判断，避免门触发器配置轻微偏差导致玩家明明贴近却不能交互。
            Vector2 playerPosition = player.transform.position;
            Vector2 doorPosition = transform.position;
            overlappingPlayer = Vector2.Distance(playerPosition, doorPosition) <= interactionDistance;
        }

        playerInside = overlappingPlayer;
        SetPromptVisible(playerInside && interactionAllowed);
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
        if (promptObject == null)
        {
            Transform prompt = transform.Find("Button") ?? transform.Find("Name");
            if (prompt != null)
            {
                promptObject = prompt.gameObject;
            }
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (CameraFocusModeController.IsCameraModeActive)
        {
            visible = false;
        }

        if (promptObject != null && promptObject.activeSelf != visible)
        {
            promptObject.SetActive(visible);
        }
    }
}
