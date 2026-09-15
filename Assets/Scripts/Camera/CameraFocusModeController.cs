using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CameraFocusModeController : MonoBehaviour
{
    public static CameraFocusModeController Instance { get; private set; }

    public static bool IsCameraModeActive => Instance != null && Instance.isCameraModeActive;

    [SerializeField] private KeyCode toggleKey = KeyCode.Alpha1;
    [SerializeField] private Vector2 reticleSize = new Vector2(150f, 95f);
    [SerializeField] private float cornerLength = 28f;
    [SerializeField] private float lineThickness = 3f;
    [SerializeField] private Color reticleColor = Color.white;
    [SerializeField] private Color focusDimColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private float focusDimAlpha = 0.35f;
    [SerializeField] private float focusInDuration = 0.3f;
    [SerializeField] private float focusOutDuration = 0.3f;
    [SerializeField] private float focusScale = 1.08f;

    private bool isCameraModeActive;
    private bool isFocusHolding;
    private bool previousCursorVisible = true;
    private Canvas overlayCanvas;
    private Image dimOverlay;
    private RectTransform reticleRoot;
    private VisionCameraReticleView reticleView;
    private Sprite solidSprite;
    private Tween focusTween;

    public static CameraFocusModeController EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        CameraFocusModeController existing = FindAnyObjectByType<CameraFocusModeController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            existing.BuildOverlayIfNeeded();
            return existing;
        }

        GameObject controllerObject = new GameObject(nameof(CameraFocusModeController));
        CameraFocusModeController controller = controllerObject.AddComponent<CameraFocusModeController>();
        return controller;
    }

    public void BeginFocusHold()
    {
        if (!isCameraModeActive)
        {
            return;
        }

        isFocusHolding = true;
        BuildOverlayIfNeeded();
        KillFocusTween(false);
        reticleView?.SetHoldActive(true);

        focusTween = DOTween.Sequence()
            .Join(dimOverlay.DOFade(focusDimAlpha, focusInDuration).SetEase(Ease.OutQuad))
            .Join(reticleRoot.DOScale(Vector3.one * focusScale, focusInDuration).SetEase(Ease.OutQuad))
            .SetUpdate(true)
            .OnKill(() => focusTween = null);
    }

    public void EndFocusHold()
    {
        if (!isFocusHolding && focusTween == null)
        {
            return;
        }

        isFocusHolding = false;
        BuildOverlayIfNeeded();
        KillFocusTween(false);
        reticleView?.SetHoldActive(false);

        focusTween = DOTween.Sequence()
            .Join(dimOverlay.DOFade(0f, focusOutDuration).SetEase(Ease.InOutQuad))
            .Join(reticleRoot.DOScale(Vector3.one, focusOutDuration).SetEase(Ease.OutBack))
            .SetUpdate(true)
            .OnKill(() => focusTween = null);
    }

    public void CancelFocusHold()
    {
        isFocusHolding = false;
        KillFocusTween(false);
        ResetFocusVisuals();
    }

    public void ExitCameraMode()
    {
        SetCameraMode(false);
    }

    public bool TryGetReticleScreenSize(out Vector2 screenSize)
    {
        BuildOverlayIfNeeded();
        if (reticleRoot == null || overlayCanvas == null)
        {
            screenSize = reticleSize;
            return false;
        }

        Rect rect = reticleRoot.rect;
        float scaleFactor = Mathf.Max(0.001f, overlayCanvas.scaleFactor);
        screenSize = new Vector2(
            Mathf.Max(1f, rect.width * scaleFactor),
            Mathf.Max(1f, rect.height * scaleFactor));
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlayIfNeeded();
        SetCameraMode(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        KillFocusTween(false);
    }

    private void Update()
    {
        if (!IsGameplaySceneLoaded())
        {
            if (isCameraModeActive)
            {
                SetCameraMode(false);
            }

            return;
        }

        if (IsInSpecialWorld())
        {
            // SpecialWorld 中禁止使用视界领域（只能鼠标长按恢复）。
            if (isCameraModeActive)
            {
                SetCameraMode(false);
            }

            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            SetCameraMode(!isCameraModeActive);
        }

        if (isCameraModeActive)
        {
            FollowMouse();
        }
    }

    private static bool IsInSpecialWorld()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        return manager != null && manager.GetPrimaryWorld() == WorldKind2D.Special;
    }

    private static bool IsGameplaySceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (SceneNames.IsGameplayScene(scene))
            {
                return true;
            }
        }

        return false;
    }

    private void SetCameraMode(bool active)
    {
        BuildOverlayIfNeeded();

        if (active == isCameraModeActive)
        {
            overlayCanvas.gameObject.SetActive(active);
            return;
        }

        isCameraModeActive = active;
        if (active)
        {
            previousCursorVisible = Cursor.visible;
            Cursor.visible = false;
            overlayCanvas.gameObject.SetActive(true);
            ResetFocusVisuals();
            FollowMouse();
            return;
        }

        Cursor.visible = previousCursorVisible;
        CancelFocusHold();
        overlayCanvas.gameObject.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayCanvas != null && dimOverlay != null && reticleRoot != null)
        {
            if (reticleView == null)
            {
                reticleView = reticleRoot.GetComponent<VisionCameraReticleView>();
                if (reticleView == null)
                {
                    reticleView = reticleRoot.gameObject.AddComponent<VisionCameraReticleView>();
                }
            }

            if (reticleView.NeedsConfigure)
            {
                reticleView.Configure(reticleSize, cornerLength, lineThickness, reticleColor, GetSolidSprite());
            }

            return;
        }

        GameObject canvasObject = new GameObject("CameraFocusOverlay");
        canvasObject.transform.SetParent(transform, false);
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dimObject = new GameObject("FocusDim");
        dimObject.transform.SetParent(canvasObject.transform, false);
        dimOverlay = dimObject.AddComponent<Image>();
        dimOverlay.raycastTarget = false;
        dimOverlay.color = focusDimColor;
        dimOverlay.sprite = GetSolidSprite();
        RectTransform dimRect = dimOverlay.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        GameObject reticleObject = new GameObject("CameraReticle");
        reticleObject.transform.SetParent(canvasObject.transform, false);
        reticleRoot = reticleObject.AddComponent<RectTransform>();
        reticleRoot.sizeDelta = reticleSize;
        reticleRoot.anchorMin = Vector2.zero;
        reticleRoot.anchorMax = Vector2.zero;
        reticleRoot.pivot = new Vector2(0.5f, 0.5f);

        reticleView = reticleObject.AddComponent<VisionCameraReticleView>();
        reticleView.Configure(reticleSize, cornerLength, lineThickness, reticleColor, GetSolidSprite());
        overlayCanvas.gameObject.SetActive(false);
    }

    private Sprite GetSolidSprite()
    {
        if (solidSprite != null)
        {
            return solidSprite;
        }

        Texture2D texture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        solidSprite.hideFlags = HideFlags.HideAndDontSave;
        return solidSprite;
    }

    private void FollowMouse()
    {
        if (reticleRoot != null)
        {
            reticleRoot.position = Input.mousePosition;
        }
    }

    private void ResetFocusVisuals()
    {
        KillFocusTween(false);
        if (dimOverlay != null)
        {
            Color color = focusDimColor;
            color.a = 0f;
            dimOverlay.color = color;
        }

        if (reticleRoot != null)
        {
            reticleRoot.localScale = Vector3.one;
        }

        reticleView?.SetHoldActive(false);
    }

    private void KillFocusTween(bool complete)
    {
        if (focusTween == null)
        {
            return;
        }

        focusTween.Kill(complete);
        focusTween = null;
    }
}
