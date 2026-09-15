using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class NpcInteraction2D : MonoBehaviour
{
    [SerializeField] private GameObject promptObject;
    [SerializeField] private TalkPanelButtonHandler talkPanel;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerLayerName = "Player";
    [TextArea]
    [SerializeField] private string[] dialogueLines;
    [SerializeField] private bool logDiagnostics;

    private int playerLayer = -1;
    private bool playerInside;
    private bool interactionAllowed = true;
    private Collider2D triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        playerLayer = LayerMask.NameToLayer(playerLayerName);
        ResolveReferences();
        SetPromptVisible(false);
        CloseTalk();
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

        if (playerInside && interactionAllowed && Input.GetKeyDown(interactKey))
        {
            if (logDiagnostics)
            {
                Debug.Log($"[NpcInteraction2D] F accepted: playerInside={playerInside}, interactionAllowed={interactionAllowed}, object={name}");
            }

            ResolveTalkPanel();
            if (talkPanel != null)
            {
                talkPanel.Play(this, dialogueLines);
            }
        }
        else if (Input.GetKeyDown(interactKey) && logDiagnostics)
        {
            Debug.Log($"[NpcInteraction2D] F blocked: playerInside={playerInside}, interactionAllowed={interactionAllowed}, object={name}");
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
        CloseTalk();
    }

    public void SetWorldInteractionAllowed(bool allowed)
    {
        interactionAllowed = allowed;

        if (!interactionAllowed)
        {
            playerInside = false;
            SetPromptVisible(false);
            CloseTalk();
            return;
        }

        if (playerInside)
        {
            SetPromptVisible(true);
        }
    }

    private void RefreshInteractionAllowed()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        SetWorldInteractionAllowed(manager == null || manager.IsInteractionAllowedForPlayer(transform));
    }

    private void RefreshPlayerInsideFromOverlap()
    {
        if (!interactionAllowed || triggerCollider == null || !triggerCollider.enabled)
        {
            return;
        }

        bool overlappingPlayer = false;
        if (playerLayer >= 0)
        {
            Bounds bounds = triggerCollider.bounds;
            Collider2D hit = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, 1 << playerLayer);
            overlappingPlayer = hit != null && IsPlayer(hit);
        }

        if (!overlappingPlayer)
        {
            Player2DMovementController player = FindAnyObjectByType<Player2DMovementController>();
            Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
            overlappingPlayer = playerCollider != null && playerCollider.bounds.Intersects(triggerCollider.bounds);
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

        ResolveTalkPanel();
    }

    private void ResolveTalkPanel()
    {
        TalkPanelButtonHandler preferredPanel = TalkPanelButtonHandler.FindPreferredPanel();
        if (preferredPanel != null)
        {
            talkPanel = preferredPanel;
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

    private void CloseTalk()
    {
        if (talkPanel != null)
        {
            talkPanel.Close(this);
        }
    }
}
