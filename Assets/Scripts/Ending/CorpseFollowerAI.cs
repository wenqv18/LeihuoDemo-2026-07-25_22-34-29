using UnityEngine;

/// <summary>
/// 第 9 层尸体的简易运动 AI：在玩家附近缓慢横向徘徊，尽量保持可交互。
/// </summary>
[DisallowMultipleComponent]
public sealed class CorpseFollowerAI : MonoBehaviour
{
    [SerializeField] private bool movementEnabled;
    [SerializeField] private float moveSpeed = 0.99f;
    [SerializeField] private float minHorizontalOffset = 0.6f;
    [SerializeField] private float maxHorizontalOffset = 1.8f;
    [SerializeField] private float maxDistanceFromPlayer = 2.4f;
    [SerializeField] private float playerHeadHeightOffset = 1.6f;
    [SerializeField] private float maxVerticalOffset = 0.18f;
    [SerializeField] private float minIdleDuration = 2.2f;
    [SerializeField] private float maxIdleDuration = 4.5f;
    [SerializeField] private float flipDeadZone = 0.35f;
    [SerializeField] private bool flipToDirection = true;
    [SerializeField] private Transform nameTransform;

    private Vector2 targetPoint;
    private float idleTimer;
    private bool idling = true;

    private void OnEnable()
    {
        ResolveNameTransform();
        KeepNameUpright();
        idling = true;
        idleTimer = Random.Range(minIdleDuration, maxIdleDuration);
    }

    private void LateUpdate()
    {
        KeepNameUpright();
    }

    private void Update()
    {
        if (!movementEnabled)
        {
            return;
        }

        Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
        if (player == null)
        {
            return;
        }

        Vector2 playerPosition = player.transform.position;
        Vector2 headPosition = playerPosition + Vector2.up * playerHeadHeightOffset;
        Vector2 current = transform.position;
        if ((current - headPosition).sqrMagnitude > maxDistanceFromPlayer * maxDistanceFromPlayer)
        {
            idling = false;
            targetPoint = headPosition + new Vector2(
                Mathf.Sign(current.x - headPosition.x) * Mathf.Min(maxHorizontalOffset, maxDistanceFromPlayer * 0.5f),
                Random.Range(-maxVerticalOffset, maxVerticalOffset));
        }

        if (idling)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                idling = false;
                PickTargetNear(headPosition);
            }

            return;
        }

        Vector2 delta = targetPoint - current;
        if (delta.sqrMagnitude <= 0.01f)
        {
            idling = true;
            idleTimer = Random.Range(minIdleDuration, maxIdleDuration);
            return;
        }

        transform.position = Vector2.MoveTowards(current, targetPoint, moveSpeed * Time.deltaTime);
        if (flipToDirection && Mathf.Abs(delta.x) > flipDeadZone)
        {
            SetFlip(delta.x > 0f);
        }
    }

    private void PickTargetNear(Vector2 center)
    {
        float horizontal = Random.Range(minHorizontalOffset, maxHorizontalOffset);
        if (Random.value < 0.5f)
        {
            horizontal = -horizontal;
        }

        float vertical = Random.Range(-maxVerticalOffset, maxVerticalOffset);
        targetPoint = center + new Vector2(horizontal, vertical);
    }

    private void SetFlip(bool faceLeft)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                if (nameTransform != null && renderers[i].transform.IsChildOf(nameTransform))
                {
                    continue;
                }

                renderers[i].flipX = faceLeft;
            }
        }
    }

    private void ResolveNameTransform()
    {
        if (nameTransform != null)
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate != transform && candidate.name == "Name")
            {
                nameTransform = candidate;
                return;
            }
        }
    }

    private void KeepNameUpright()
    {
        ResolveNameTransform();
        if (nameTransform != null)
        {
            nameTransform.rotation = Quaternion.identity;
        }
    }
}
