using UnityEngine;

/// <summary>
/// 摄像头/视界模式的输入控制层。
/// 它只负责把“鼠标位置、长按时间、是否处于摄像机模式”转换成 Preview / Confirm 调用；
/// 目标判定、SAN、世界切换都交给 WorldSwapManager2D，避免输入脚本变得过重。
/// </summary>
public sealed class WorldSwapHoldController2D : MonoBehaviour
{
    // targetCamera 用来把屏幕坐标换算成 2D 世界坐标；默认使用 Main Camera。
    [SerializeField] private Camera targetCamera;

    // worldSwapManager 是实际执行预览、确认和世界状态更新的核心管理器。
    [SerializeField] private WorldSwapManager2D worldSwapManager;

    // 玩家需要按住鼠标多久才算真正拍照，防止误触。
    [SerializeField] private float holdDuration = 1.2f;
    [SerializeField] private int mouseButton = 0;

    private float holdTimer;
    private bool isHolding;

    private void Awake()
    {
        CameraFocusModeController.EnsureExists();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (worldSwapManager == null)
        {
            worldSwapManager = GetComponent<WorldSwapManager2D>();
        }

        if (worldSwapManager == null)
        {
            worldSwapManager = WorldSwapManager2D.Instance;
        }
    }

    private void Update()
    {
        if (targetCamera == null || worldSwapManager == null)
        {
            return;
        }

        if (!CameraFocusModeController.IsCameraModeActive)
        {
            // 退出摄像机模式后必须清掉预览区域，否则 SpriteMask 会继续影响世界显示。
            CancelHold();
            worldSwapManager.ClearPreview();
            return;
        }

        // 瞄准阶段：光圈跟随鼠标，光圈内实时预览 SpecialWorld（不扣 SAN）。
        worldSwapManager.PreviewArea(GetMouseWorldPosition());

        if (Input.GetMouseButtonDown(mouseButton))
        {
            isHolding = true;
            holdTimer = 0f;
            CameraFocusModeController.Instance.BeginFocusHold();
        }

        if (Input.GetMouseButtonUp(mouseButton))
        {
            CancelHold();
        }

        if (!isHolding)
        {
            return;
        }

        holdTimer += Time.deltaTime;
        if (holdTimer < holdDuration)
        {
            return;
        }

        // 长按达标后才进入 ConfirmArea：此时才会检查覆盖率、触发成功揭示或失败扣 SAN。
        worldSwapManager.ConfirmArea(GetMouseWorldPosition());
        CompleteHold();
        if (worldSwapManager.HasFoundVisionTarget && !worldSwapManager.HasActiveArea)
        {
            // 拍照成功：收起摄像机，目标物已揭示其 SpecialWorld 版本。
            CameraFocusModeController.Instance.ExitCameraMode();
        }
    }

    private void CancelHold()
    {
        if (isHolding || holdTimer > 0f)
        {
            CameraFocusModeController.Instance?.EndFocusHold();
        }

        isHolding = false;
        holdTimer = 0f;
    }

    private void CompleteHold()
    {
        CameraFocusModeController.Instance?.EndFocusHold();
        isHolding = false;
        holdTimer = 0f;
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = -targetCamera.transform.position.z;
        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(mousePosition);
        return new Vector2(worldPosition.x, worldPosition.y);
    }
}
