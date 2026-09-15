using UnityEngine;

[DisallowMultipleComponent]
public sealed class CorpseInteraction : MonoBehaviour
{
    [SerializeField] private GameObject promptObject;
    [SerializeField] private DeathTalkController deathTalk;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private bool requireWorldInteractionAllowed = true;
    [SerializeField] private Vector2 interactionOffset = new Vector2(0f, 0.2f);
    [SerializeField] private Vector2 interactionSize = new Vector2(1.5f, 2.2f);
    [SerializeField] private float fallbackInteractionDistance = 1.8f;
    [SerializeField] private bool logDiagnostics;

    private int playerLayer = -1;
    private bool playerInside;
    private bool interactionAllowed = true;
    private Collider2D triggerCollider;
    private Rigidbody2D triggerRigidbody;
    private bool promptOpen;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        bool createdCollider = false;
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider2D>();
            createdCollider = true;
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            if (createdCollider)
            {
                ConfigureTriggerCollider();
            }
        }

        EnsureTriggerRigidbody();
        playerLayer = LayerMask.NameToLayer(playerLayerName);
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

        RefreshInteractionAllowed();
        RefreshPlayerInsideFromOverlap();
        SyncPromptOpenState();

        if (playerInside && interactionAllowed && Input.GetKeyDown(interactKey) && !promptOpen)
        {
            if (logDiagnostics)
            {
                Debug.Log($"[CorpseInteraction] F accepted: object={name}", this);
            }

            OpenPrompt();
        }
        else if (Input.GetKeyDown(interactKey) && logDiagnostics)
        {
            Debug.Log($"[CorpseInteraction] F blocked: playerInside={playerInside}, interactionAllowed={interactionAllowed}, object={name}", this);
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

    private void OnDisable()
    {
        ClosePromptIfOpen();
    }

    public void SetWorldInteractionAllowed(bool allowed)
    {
        interactionAllowed = allowed;
        if (!interactionAllowed)
        {
            playerInside = false;
            SetPromptVisible(false);
            ClosePromptIfOpen();
            return;
        }

        if (playerInside)
        {
            SetPromptVisible(true);
        }
    }

    private void OpenPrompt()
    {
        promptOpen = GameUIController.ShowDeathTalk();
        ResolveDeathTalk();
    }

    private void SyncPromptOpenState()
    {
        if (!promptOpen)
        {
            return;
        }

        ResolveDeathTalk();
        if (deathTalk == null || !deathTalk.IsOpen)
        {
            promptOpen = false;
        }
    }

    private void ClosePromptIfOpen()
    {
        if (!promptOpen)
        {
            return;
        }

        promptOpen = false;
        if (deathTalk != null)
        {
            deathTalk.Hide();
        }
    }

    private void RefreshInteractionAllowed()
    {
        if (!requireWorldInteractionAllowed)
        {
            interactionAllowed = true;
            return;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        SetWorldInteractionAllowed(manager == null || manager.IsInteractionAllowedForPlayer(transform));
    }

    private void RefreshPlayerInsideFromOverlap()
    {
        if (!interactionAllowed || triggerCollider == null || !triggerCollider.enabled)
        {
            return;
        }

        Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
        Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
        bool overlappingPlayer = playerCollider != null && playerCollider.bounds.Intersects(triggerCollider.bounds);
        if (!overlappingPlayer && playerCollider != null)
        {
            Bounds corpseBounds = triggerCollider.bounds;
            Vector2 playerCenter = playerCollider.bounds.center;
            Vector2 closestPoint = corpseBounds.ClosestPoint(playerCenter);
            float distance = Vector2.Distance(playerCenter, closestPoint);
            overlappingPlayer = distance <= Mathf.Max(0.2f, fallbackInteractionDistance);
        }

        if (!overlappingPlayer && playerLayer >= 0)
        {
            Bounds bounds = triggerCollider.bounds;
            Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, 1 << playerLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit != null && hit != triggerCollider && IsPlayer(hit))
                {
                    overlappingPlayer = true;
                    break;
                }
            }
        }

        if (!overlappingPlayer && player != null)
        {
            float distance = Vector2.Distance(transform.position, player.transform.position);
            overlappingPlayer = distance <= Mathf.Max(0.2f, fallbackInteractionDistance);
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

        if (triggerCollider != null && other.transform.IsChildOf(transform))
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

        ResolveDeathTalk();
    }

    private void ResolveDeathTalk()
    {
        if (deathTalk != null)
        {
            return;
        }

        deathTalk = DeathTalkController.FindOrCreate();
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

    private void ConfigureTriggerCollider()
    {
        BoxCollider2D boxCollider = triggerCollider as BoxCollider2D;
        if (boxCollider == null)
        {
            return;
        }

        boxCollider.offset = interactionOffset;
        boxCollider.size = new Vector2(
            Mathf.Max(0.2f, interactionSize.x),
            Mathf.Max(0.2f, interactionSize.y));
    }

    private void EnsureTriggerRigidbody()
    {
        triggerRigidbody = GetComponent<Rigidbody2D>();
        if (triggerRigidbody == null)
        {
            triggerRigidbody = gameObject.AddComponent<Rigidbody2D>();
        }

        triggerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        triggerRigidbody.simulated = true;
        triggerRigidbody.gravityScale = 0f;
        triggerRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
    }
}
