using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FurnitureProximityInteraction2D : MonoBehaviour
{
    [SerializeField] private GameObject promptObject;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private bool requireSpecialWorldTarget;
    [SerializeField] private bool logDiagnostics;

    private int playerLayer = -1;
    private bool playerInside;
    private Collider2D triggerCollider;
    private FurnitureInteraction2D furnitureInteraction;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        furnitureInteraction = GetComponent<FurnitureInteraction2D>();
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
            SetPromptVisible(false);
            return;
        }

        RefreshPlayerInsideFromOverlap();
        bool interactionAllowed = IsInteractionAllowed();
        SetPromptVisible(playerInside && interactionAllowed);

        if (playerInside && interactionAllowed && Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;
        SetPromptVisible(IsInteractionAllowed());
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;
        SetPromptVisible(IsInteractionAllowed());
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

    private void TryInteract()
    {
        if (furnitureInteraction == null)
        {
            furnitureInteraction = GetComponent<FurnitureInteraction2D>();
        }

        if (furnitureInteraction == null || !furnitureInteraction.TryPlayFromExternal())
        {
            if (logDiagnostics)
            {
                Debug.Log($"[FurnitureProximityInteraction2D] F interaction blocked: {name}", this);
            }

            return;
        }

        if (logDiagnostics)
        {
            Debug.Log($"[FurnitureProximityInteraction2D] F interaction accepted: {name}", this);
        }
    }

    private bool IsInteractionAllowed()
    {
        if (requireSpecialWorldTarget)
        {
            WorldInteractionTarget2D target = WorldInteractionTarget2D.FindSpecialRecoveryTargetInParents(this);
            if (target == null)
            {
                SpecialWorldInteractionTarget2D legacyTarget = GetComponentInParent<SpecialWorldInteractionTarget2D>();
                if (legacyTarget == null || !legacyTarget.enabled || !legacyTarget.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        return manager == null || manager.IsInteractionAllowedForPlayer(transform);
    }

    private void RefreshPlayerInsideFromOverlap()
    {
        bool overlappingPlayer = false;
        Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
        if (triggerCollider != null && triggerCollider.enabled && playerLayer >= 0)
        {
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
            Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
            overlappingPlayer = playerCollider != null && playerCollider.bounds.Intersects(triggerCollider.bounds);
        }

        if (!overlappingPlayer && player != null)
        {
            overlappingPlayer = Vector2.Distance(player.transform.position, transform.position) <= interactionDistance;
        }

        playerInside = overlappingPlayer;
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
        if (promptObject != null)
        {
            return;
        }

        Transform prompt = transform.Find("Button") ?? transform.Find("Name");
        if (prompt != null)
        {
            promptObject = prompt.gameObject;
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
