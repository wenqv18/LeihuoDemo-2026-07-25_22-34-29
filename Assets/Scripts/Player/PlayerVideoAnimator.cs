using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player0 transparent sprite-sequence animation: idle / walk / photo / death.
/// Player1 keeps the static character Sprite.
/// </summary>
public sealed class PlayerVideoAnimator : MonoBehaviour
{
    public enum AnimState
    {
        Idle,
        Walk,
        Photo,
        Death
    }

    private enum PhotoPhase
    {
        None,
        Entering,
        Holding
    }

    private const string IdlePath = "Player/AnimationTransparent/idle";
    private const string WalkPath = "Player/AnimationTransparent/walk";
    private const string PhotoPath = "Player/AnimationTransparent/camera_raise";
    private const string DeathPath = "Player/AnimationTransparent/death";
    private const string SequenceRendererName = "Player0SequenceRenderer";

    [SerializeField, Tooltip("死亡动画相对其他动画的放大倍数（人物尺寸修正）")]
    private float deathScaleMultiplier = 1f;
    [SerializeField, Tooltip("序列画面在场景中的基础高度")]
    private float baseFrameHeight = 4.1f;
    [SerializeField, Tooltip("普通动画播放帧率")]
    private float frameRate = 24f;
    [SerializeField, Tooltip("死亡播放速度")]
    private float deathPlaybackSpeed = 1.5f;
    [SerializeField, Tooltip("拍照动画正向播放速度倍率")]
    private float photoPlaybackSpeed = 3f;
    [SerializeField, Tooltip("当当前场景的玩家动画素材默认朝向相反时启用。")]
    private bool invertHorizontalFacing;

    private readonly Dictionary<AnimState, Sprite[]> clips = new Dictionary<AnimState, Sprite[]>();

    private SpriteRenderer sequenceRenderer;
    private SpriteRenderer[] characterRenderers;
    private Rigidbody2D body;

    private AnimState currentState = AnimState.Idle;
    private bool deathPlaying;
    private Action deathFinished;
    private float stateStartTime;
    private float facing = 1f;
    private Vector3 baseRendererLocalPosition;

    private PhotoPhase photoPhase = PhotoPhase.None;
    private bool wasCameraModeActive;

    public bool IsDeathPlaying => deathPlaying;
    public bool CanPlayDeath => !deathPlaying && HasFrames(AnimState.Death);
    public static bool IsPhotoAnimating { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        characterRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        EnsureSequenceRenderer();
        LoadClips();
        RefreshAppearance();
        SetState(AnimState.Idle, true);
    }

    private void Update()
    {
        if (deathPlaying)
        {
            UpdateFrame();
            CheckDeathFinished();
            return;
        }

        RefreshAppearance();

        if (PlayerAppearanceController.CurrentIndex != PlayerAppearanceController.MaleIndex)
        {
            return;
        }

        bool cameraActive = CameraFocusModeController.IsCameraModeActive;

        if (cameraActive && !wasCameraModeActive)
        {
            EnterPhotoMode();
        }
        else if (!cameraActive && wasCameraModeActive && photoPhase != PhotoPhase.None)
        {
            FinishPhotoMode();
        }

        wasCameraModeActive = cameraActive;

        if (photoPhase != PhotoPhase.None)
        {
            UpdatePhotoFrame();
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        AnimState target;
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            target = AnimState.Walk;
            facing = horizontal < 0f ? -1f : 1f;
        }
        else
        {
            target = AnimState.Idle;
        }

        SetState(target, false);
        UpdateFrame();
    }

    public void PlayDeath(Action onFinished)
    {
        if (deathPlaying || !HasFrames(AnimState.Death))
        {
            onFinished?.Invoke();
            return;
        }

        deathPlaying = true;
        deathFinished = onFinished;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        Player2DMovementController movement = GetComponent<Player2DMovementController>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        SetState(AnimState.Death, true);
        UpdateFrame();
    }

    public void RefreshAppearance()
    {
        bool male = PlayerAppearanceController.CurrentIndex == PlayerAppearanceController.MaleIndex;
        if (sequenceRenderer != null)
        {
            sequenceRenderer.enabled = male;
        }

        if (characterRenderers == null)
        {
            return;
        }

        for (int i = 0; i < characterRenderers.Length; i++)
        {
            SpriteRenderer renderer = characterRenderers[i];
            if (renderer != null && renderer != sequenceRenderer)
            {
                renderer.enabled = !male;
            }
        }
    }

    private void EnterPhotoMode()
    {
        photoPhase = PhotoPhase.Entering;
        IsPhotoAnimating = true;
        SetState(AnimState.Photo, true);
    }

    private void FinishPhotoMode()
    {
        photoPhase = PhotoPhase.None;
        IsPhotoAnimating = false;
        SetState(AnimState.Idle, true);
    }

    private void UpdatePhotoFrame()
    {
        if (sequenceRenderer == null || !HasFrames(AnimState.Photo))
        {
            return;
        }

        Sprite[] frames = clips[AnimState.Photo];

        switch (photoPhase)
        {
            case PhotoPhase.Entering:
                int enterFrame = Mathf.FloorToInt((Time.time - stateStartTime) * frameRate * photoPlaybackSpeed);
                if (enterFrame >= frames.Length - 1)
                {
                    enterFrame = frames.Length - 1;
                    photoPhase = PhotoPhase.Holding;
                }
                sequenceRenderer.sprite = frames[enterFrame];
                break;

            case PhotoPhase.Holding:
                sequenceRenderer.sprite = frames[frames.Length - 1];
                break;
        }
    }

    private void SetState(AnimState state, bool forceRestart)
    {
        if (!forceRestart && currentState == state)
        {
            return;
        }

        currentState = state;
        stateStartTime = Time.time;
        ApplyRendererScale(state);
    }

    private void UpdateFrame()
    {
        if (sequenceRenderer == null || !HasFrames(currentState))
        {
            return;
        }

        Sprite[] frames = clips[currentState];
        float speed = currentState == AnimState.Death ? deathPlaybackSpeed : 1f;
        int frameIndex = Mathf.FloorToInt((Time.time - stateStartTime) * frameRate * speed);
        if (currentState == AnimState.Death)
        {
            frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        }
        else
        {
            frameIndex %= frames.Length;
        }

        sequenceRenderer.sprite = frames[frameIndex];
    }

    private void CheckDeathFinished()
    {
        Sprite[] frames = clips[AnimState.Death];
        float duration = frames.Length / Mathf.Max(1f, frameRate * deathPlaybackSpeed);
        if (Time.time - stateStartTime < duration)
        {
            return;
        }

        FinishDeath();
    }

    private void FinishDeath()
    {
        deathPlaying = false;

        Player2DMovementController movement = GetComponent<Player2DMovementController>();
        if (movement != null)
        {
            movement.enabled = true;
        }

        Action callback = deathFinished;
        deathFinished = null;
        callback?.Invoke();
    }

    private void EnsureSequenceRenderer()
    {
        Transform rendererTransform = transform.Find(SequenceRendererName);
        if (rendererTransform == null)
        {
            GameObject rendererObject = new GameObject(SequenceRendererName);
            rendererTransform = rendererObject.transform;
            rendererTransform.SetParent(transform, false);
        }

        sequenceRenderer = rendererTransform.GetComponent<SpriteRenderer>();
        if (sequenceRenderer == null)
        {
            sequenceRenderer = rendererTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer referenceRenderer = FindReferenceRenderer();
        if (referenceRenderer != null)
        {
            sequenceRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
            sequenceRenderer.sortingOrder = referenceRenderer.sortingOrder;
        }

        baseRendererLocalPosition = CalculateBaseLocalPosition();
        rendererTransform.localPosition = baseRendererLocalPosition;
    }

    private void LoadClips()
    {
        clips[AnimState.Idle] = LoadFrames(IdlePath);
        clips[AnimState.Walk] = LoadFrames(WalkPath);
        clips[AnimState.Photo] = LoadFrames(PhotoPath);
        clips[AnimState.Death] = LoadFrames(DeathPath);
    }

    private static Sprite[] LoadFrames(string path)
    {
        Sprite[] frames = Resources.LoadAll<Sprite>(path);
        Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (frames.Length == 0)
        {
            Debug.LogWarning($"[PlayerVideoAnimator] Missing Player0 animation frames at Resources/{path}.");
        }

        return frames;
    }

    private void ApplyRendererScale(AnimState state)
    {
        if (sequenceRenderer == null || !HasFrames(state))
        {
            return;
        }

        Sprite firstFrame = clips[state][0];
        float spriteHeight = Mathf.Max(0.001f, firstFrame.bounds.size.y);
        float sizeMultiplier = state == AnimState.Death ? deathScaleMultiplier : 1f;
        float scale = baseFrameHeight * sizeMultiplier / spriteHeight;
        float facingScale = state == AnimState.Death ? 1f : facing;
        if (state != AnimState.Death && invertHorizontalFacing)
        {
            facingScale = -facingScale;
        }

        Transform rendererTransform = sequenceRenderer.transform;
        sequenceRenderer.flipX = false;
        rendererTransform.localScale = new Vector3(scale * facingScale, scale, 1f);
        rendererTransform.localPosition = baseRendererLocalPosition;
    }

    private Vector3 CalculateBaseLocalPosition()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            Vector2 colliderBottomCenter = collider.bounds.center - new Vector3(0f, collider.bounds.extents.y, 0f);
            Vector3 localBottom = transform.InverseTransformPoint(colliderBottomCenter);
            return new Vector3(localBottom.x, localBottom.y + baseFrameHeight * 0.5f, -0.5f);
        }

        return new Vector3(0f, baseFrameHeight * 0.5f, -0.5f);
    }

    private SpriteRenderer FindReferenceRenderer()
    {
        if (characterRenderers == null)
        {
            return null;
        }

        for (int i = 0; i < characterRenderers.Length; i++)
        {
            if (characterRenderers[i] != null && characterRenderers[i] != sequenceRenderer)
            {
                return characterRenderers[i];
            }
        }

        return null;
    }

    private bool HasFrames(AnimState state)
    {
        return clips.TryGetValue(state, out Sprite[] frames) && frames != null && frames.Length > 0;
    }
}
