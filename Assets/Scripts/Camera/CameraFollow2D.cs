using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset;
    [SerializeField] private bool useInitialOffset = true;
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private float defaultShakeDuration = 0.24f;
    [SerializeField] private float defaultShakeAmplitude = 0.18f;
    [SerializeField] private float shakeFrequency = 42f;

    private Vector3 velocity;
    private Vector3 lastShakeOffset;
    private float shakeTimer;
    private float shakeDuration;
    private float shakeAmplitude;

    public void Configure(Transform followTarget, bool shouldFollowX, bool shouldFollowY, bool shouldUseInitialOffset)
    {
        target = followTarget;
        followX = shouldFollowX;
        followY = shouldFollowY;
        useInitialOffset = shouldUseInitialOffset;

        if (useInitialOffset && target != null)
        {
            offset = transform.position - target.position;
        }
    }

    public void PlayImpactShake(float duration = -1f, float amplitude = -1f)
    {
        shakeDuration = Mathf.Max(0.01f, duration > 0f ? duration : defaultShakeDuration);
        shakeAmplitude = Mathf.Max(0f, amplitude >= 0f ? amplitude : defaultShakeAmplitude);
        shakeTimer = shakeDuration;
    }

    private void Awake()
    {
        ResolveTarget();
        if (useInitialOffset && target != null)
        {
            offset = transform.position - target.position;
        }
    }

    private void LateUpdate()
    {
        ResolveTarget();
        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + offset;
        Vector3 current = transform.position - lastShakeOffset;

        if (!followX)
        {
            desired.x = current.x;
        }

        if (!followY)
        {
            desired.y = current.y;
        }

        desired.z = current.z;
        Vector3 smoothedPosition = Vector3.SmoothDamp(current, desired, ref velocity, Mathf.Max(0f, smoothTime));
        lastShakeOffset = CalculateShakeOffset();
        transform.position = smoothedPosition + lastShakeOffset;
    }

    private Vector3 CalculateShakeOffset()
    {
        if (shakeTimer <= 0f || shakeAmplitude <= 0f)
        {
            return Vector3.zero;
        }

        shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
        float elapsed = shakeDuration - shakeTimer;
        float decay = shakeTimer / Mathf.Max(0.01f, shakeDuration);
        float x = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitude * decay;
        float y = Mathf.Sin(elapsed * shakeFrequency * 1.7f) * shakeAmplitude * 0.45f * decay;
        return new Vector3(x, y, 0f);
    }

    private void ResolveTarget()
    {
        if (target != null)
        {
            return;
        }

        Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
        if (player != null)
        {
            target = player.transform;
            return;
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
        }
    }
}
