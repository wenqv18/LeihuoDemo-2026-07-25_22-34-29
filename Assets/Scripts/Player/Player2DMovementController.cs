using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class Player2DMovementController : MonoBehaviour
{
    private const string SequenceRendererName = "Player0SequenceRenderer";

    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool flipVisualsToDirection = true;
    [SerializeField] private AudioSource stepAudioSource;
    // 走路声单独加大 1 倍：0.45 -> 0.9。
    [SerializeField] private float stepBaseVolume = 0.9f;

    private Rigidbody2D body;
    private SpriteRenderer[] spriteRenderers;
    private float horizontalInput;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        if (stepAudioSource == null)
        {
            stepAudioSource = GetComponent<AudioSource>();
        }

        if (stepAudioSource != null)
        {
            stepBaseVolume = Mathf.Max(0f, stepAudioSource.volume);
        }

        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (SpecialWorldSurvivalController2D.IsRecoveryHolding)
        {
            // 长按恢复期间锁定移动。
            horizontalInput = 0f;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            // 走路声为长音频：移动时循环播放，不叠加。
            if (stepAudioSource != null && stepAudioSource.clip != null && !stepAudioSource.isPlaying)
            {
                stepAudioSource.volume = stepBaseVolume * GameAudioManager.SfxScaleForClip(stepAudioSource.clip);
                stepAudioSource.loop = true;
                stepAudioSource.Play();
            }
        }
        else if (stepAudioSource != null && stepAudioSource.isPlaying)
        {
            // 停下立即停止脚步声。
            stepAudioSource.Stop();
        }

        if (flipVisualsToDirection && Mathf.Abs(horizontalInput) > 0.01f)
        {
            bool faceLeft = horizontalInput < 0f;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null && spriteRenderers[i].name != SequenceRendererName)
                {
                    spriteRenderers[i].flipX = faceLeft;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = horizontalInput * moveSpeed;
        body.linearVelocity = velocity;
    }
}
