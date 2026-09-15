using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SpecialWorld 生存规则控制器。
/// 负责进入 SpecialWorld 后的 20 秒倒计时、恢复目标长按确认、SAN 耗尽后的死亡/保留规则。
/// 它通过监听 WorldSwapManager2D 的事件接入世界切换，而不是直接写在世界管理器里。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialWorldSurvivalController2D : MonoBehaviour
{
    // UI 和其他系统通过这些事件显示倒计时、恢复反馈和死亡反馈。
    public static event Action SpecialWorldTimerStarted;
    public static event Action SpecialWorldTimerStopped;
    public static event Action<float, float> SpecialWorldTimerChanged;
    public static event Action SpecialWorldRecoveryUsed;
    public static event Action SpecialWorldTimeoutDeath;
    public static event Action SpecialWorldRecoveredFailureDeath;

    /// <summary>是否正在长按恢复（用于锁定玩家移动）。</summary>
    public static bool IsRecoveryHolding { get; private set; }

    [SerializeField] private float targetInteractionTimeLimit = 20f;
    [SerializeField] private int recoverySanValue = 33;
    [SerializeField] private bool returnToNormalWorldAfterRecovery = true;
    [SerializeField] private bool logDiagnostics;

    [Header("Recovery Hold")]
    [SerializeField] private float recoveryHoldDuration = 1f;
    [SerializeField] private int recoveryHoldMouseButton = 0;

    private float remainingTime;
    private bool timerRunning;
    private bool deathTriggered;
    private float holdTimer;
    private bool isHoldingRecovery;

    public float RemainingTime => timerRunning ? Mathf.Max(0f, remainingTime) : 0f;
    public float TimeLimit => targetInteractionTimeLimit;

    private void OnEnable()
    {
        // 世界切换和 SAN 耗尽由 WorldSwapManager2D 统一发出，这里只订阅并处理 SpecialWorld 特有规则。
        WorldSwapManager2D.PrimaryWorldChanged += HandlePrimaryWorldChanged;
        WorldSwapManager2D.SanExhausted += HandleSanExhausted;

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        if (manager != null && manager.GetPlayerWorld() == WorldKind2D.Special)
        {
            StartSpecialWorldTimer();
        }
    }

    private void OnDisable()
    {
        WorldSwapManager2D.PrimaryWorldChanged -= HandlePrimaryWorldChanged;
        WorldSwapManager2D.SanExhausted -= HandleSanExhausted;
        CancelRecoveryHold();
    }

    private void OnValidate()
    {
        targetInteractionTimeLimit = Mathf.Max(1f, targetInteractionTimeLimit);
        recoverySanValue = Mathf.Clamp(recoverySanValue, 0, 100);
    }

    private void Update()
    {
        UpdateRecoveryHold();

        if (!timerRunning || deathTriggered)
        {
            return;
        }

        remainingTime -= Time.deltaTime;
        SpecialWorldTimerChanged?.Invoke(Mathf.Max(0f, remainingTime), targetInteractionTimeLimit);
        if (remainingTime > 0f)
        {
            return;
        }

        // 倒计时结束代表玩家没来得及完成特殊目标交互，直接进入死亡流程。
        timerRunning = false;
        SpecialWorldTimeoutDeath?.Invoke();
        TriggerDeath("[SpecialWorldSurvivalController2D] SpecialWorld target was not interacted within the time limit.");
    }

    private void HandlePrimaryWorldChanged(WorldKind2D world)
    {
        if (world == WorldKind2D.Special)
        {
            StartSpecialWorldTimer();
        }
        else
        {
            StopSpecialWorldTimer();
        }
    }

    private void HandleSanExhausted(WorldSanExhaustedEventArgs args)
    {
        if (args == null || deathTriggered)
        {
            return;
        }

        bool recoveryUsed = IsTutorialScene()
            ? false
            : RunSaveService.IsSpecialWorldRecoveryUsedOnCurrentFloor();
        if (recoveryUsed)
        {
            // 本层已经恢复过时，SAN 再次耗尽不再翻回 SpecialWorld，而是接管默认流程并死亡。
            args.CancelDefaultResolution = true;
            SpecialWorldRecoveredFailureDeath?.Invoke();
            TriggerDeath("[SpecialWorldSurvivalController2D] San exhausted after the SpecialWorld recovery was used.");
            return;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        bool playerAlreadyInSpecialWorld = args.World == WorldKind2D.Special ||
            (manager != null && manager.GetPlayerWorld() == WorldKind2D.Special);
        if (playerAlreadyInSpecialWorld)
        {
            // 玩家已经在 SpecialWorld 且本层恢复未使用时，不重复触发世界翻转，只确保计时规则存在。
            args.CancelDefaultResolution = true;
            if (!timerRunning)
            {
                StartSpecialWorldTimer();
            }

            Log("Ignored repeated San exhaustion while the unused SpecialWorld recovery is available.");
        }
    }

    /// <summary>
    /// 长按确认恢复：玩家在 SpecialWorld 中按住恢复目标
    /// <see cref="recoveryHoldDuration"/> 秒后生效。
    /// </summary>
    private void UpdateRecoveryHold()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        bool valid = manager != null &&
            manager.GetPlayerWorld() == WorldKind2D.Special &&
            !manager.IsTransitioning &&
            !deathTriggered &&
            !CameraFocusModeController.IsCameraModeActive;

        WorldInteractionTarget2D target = FindRecoveryTargetUnderPointer();
        bool holding = Input.GetMouseButton(recoveryHoldMouseButton);
        bool recoveryAvailable = IsTutorialScene() ||
            !RunSaveService.IsSpecialWorldRecoveryUsedOnCurrentFloor();

        if (!valid || target == null || !holding || !recoveryAvailable)
        {
            CancelRecoveryHold();
            return;
        }

        if (!isHoldingRecovery)
        {
            isHoldingRecovery = true;
            IsRecoveryHolding = true;
            holdTimer = 0f;
        }

        holdTimer += Time.deltaTime;
        if (holdTimer >= recoveryHoldDuration)
        {
            CancelRecoveryHold();
            ConfirmRecovery();
        }
    }

    /// <summary>执行恢复：回 SAN、标记使用、停止计时并返回 NormalWorld（走逐渐恢复过渡）。</summary>
    public void ConfirmRecovery()
    {
        if (deathTriggered)
        {
            return;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        if (manager == null || manager.GetPlayerWorld() != WorldKind2D.Special)
        {
            return;
        }

        if (!IsTutorialScene() && RunSaveService.IsSpecialWorldRecoveryUsedOnCurrentFloor())
        {
            return;
        }

        if (!IsTutorialScene())
        {
            RunSaveService.MarkSpecialWorldRecoveryUsedOnCurrentFloor();
        }

        manager.RestoreSanTo(recoverySanValue);
        SpecialWorldRecoveryUsed?.Invoke();
        StopSpecialWorldTimer();
        TalkPanelButtonHandler.CloseAllOpenPanels();

        if (returnToNormalWorldAfterRecovery)
        {
            manager.ReturnToNormalWorld();
        }

        Log("SpecialWorld recovery used (hold confirmed).");
    }

    private WorldInteractionTarget2D FindRecoveryTargetUnderPointer()
    {
        Vector2 mouseWorldPosition;
        if (!TryGetMouseWorldPosition(out mouseWorldPosition))
        {
            return null;
        }

        WorldInteractionTarget2D[] targets =
            FindObjectsByType<WorldInteractionTarget2D>(FindObjectsInactive.Include);
        for (int i = 0; i < targets.Length; i++)
        {
            // 恢复目标必须是当前被 LevelRuntimeConfigurator 激活的 SpecialWorld 目标。
            if (targets[i] != null &&
                targets[i].IsSpecialRecoveryTarget &&
                targets[i].ContainsWorldPoint(mouseWorldPosition))
            {
                return targets[i];
            }
        }

        return null;
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

    private void CancelRecoveryHold()
    {
        if (!isHoldingRecovery && !IsRecoveryHolding)
        {
            return;
        }

        isHoldingRecovery = false;
        IsRecoveryHolding = false;
        holdTimer = 0f;
    }

    private void StartSpecialWorldTimer()
    {
        if (deathTriggered)
        {
            return;
        }

        // 每次进入 SpecialWorld 都重置 20 秒倒计时，回到 NormalWorld 时由 StopSpecialWorldTimer 停止。
        remainingTime = targetInteractionTimeLimit;
        timerRunning = true;
        SpecialWorldTimerStarted?.Invoke();
        SpecialWorldTimerChanged?.Invoke(remainingTime, targetInteractionTimeLimit);
        Log("SpecialWorld timer started.");
    }

    private void StopSpecialWorldTimer()
    {
        if (!timerRunning)
        {
            return;
        }

        timerRunning = false;
        SpecialWorldTimerStopped?.Invoke();
        Log("SpecialWorld timer stopped.");
    }

    private void TriggerDeath(string message)
    {
        if (deathTriggered)
        {
            return;
        }

        deathTriggered = true;
        StopSpecialWorldTimer();
        Log(message);
        GameUIController.ShowDeath();
    }

    private void Log(string message)
    {
        if (logDiagnostics)
        {
            Debug.Log(message, this);
        }
    }

    private static bool IsTutorialScene()
    {
        // 教程场景不走正式 run 存档，避免新手教学被正式楼层规则卡住。
        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && activeScene.name == SceneNames.TutorialLevel;
    }
}
