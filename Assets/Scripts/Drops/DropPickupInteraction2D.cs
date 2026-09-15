using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DropPickupInteraction2D : MonoBehaviour
{
    private const int NoFloorItem = 0;
    private const string ItemImagePathFormat = "UI/Items/RecordBook_{0:00}";
    private const string DefaultItemName = "Story Item";
    private const string DefaultItemDescription = "A story item.";

    [SerializeField] private int itemId = 1;
    [SerializeField] private string itemName = "Story Item";
    [TextArea]
    [SerializeField] private string itemDescription = "A story item.";
    [SerializeField] private string imagePath;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private bool fitColliderToDropSprite = true;
    [SerializeField] private bool logDiagnostics;

    private int playerLayer = -1;
    private bool playerInside;
    private bool interactionAllowed = true;
    private Collider2D triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
        ResolveReferences();
        RegisterInventoryDefinition();

        if (fitColliderToDropSprite)
        {
            FitBoxColliderToVisibleSprite();
        }

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        SetPromptVisible(false);
    }

    public void ConfigureForFloor(int floorNumber)
    {
        int floorItemId = GetItemIdForFloor(floorNumber);
        if (floorItemId == NoFloorItem)
        {
            HideForFloor();
            return;
        }

        itemId = floorItemId;
        imagePath = string.Format(ItemImagePathFormat, itemId);
        ApplyPickupSprite();
        RegisterInventoryDefinition();

        if (StoryInventoryManager.GetOrCreateInstance().HasItem(itemId))
        {
            HideForFloor();
            return;
        }

        gameObject.SetActive(true);
        triggerCollider = triggerCollider != null ? triggerCollider : GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        ResolveReferences();
        if (fitColliderToDropSprite)
        {
            FitBoxColliderToVisibleSprite();
        }

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        playerInside = false;
        interactionAllowed = true;
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

        if (playerInside && interactionAllowed && Input.GetKeyDown(interactKey))
        {
            Pickup();
        }
        else if (Input.GetKeyDown(interactKey) && logDiagnostics)
        {
            LogBlockedPickup();
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

    private void Pickup()
    {
        StoryInventoryManager manager = StoryInventoryManager.GetOrCreateInstance();
        bool added = manager.AddItem(itemId);

        if (added)
        {
            RunSaveService.CaptureCurrentState();
        }

        if (logDiagnostics)
        {
            Debug.Log($"[DropPickupInteraction2D] Picked item id={itemId}, addedToInventory={added}, object={name}");
        }

        Destroy(gameObject);
    }

    private void LogBlockedPickup()
    {
        Debug.Log($"[DropPickupInteraction2D] F blocked: playerInside={playerInside}, interactionAllowed={interactionAllowed}, object={name}");
    }

    private void RefreshInteractionAllowed()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        interactionAllowed = manager == null || manager.IsInteractionAllowedForPlayer(transform);
        if (!interactionAllowed)
        {
            playerInside = false;
        }

        SetPromptVisible(playerInside && interactionAllowed);
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
            Transform prompt = transform.Find("Name") ?? transform.Find("Button");
            if (prompt != null)
            {
                promptObject = prompt.gameObject;
            }
        }
    }

    private void FitBoxColliderToVisibleSprite()
    {
        BoxCollider2D boxCollider = triggerCollider as BoxCollider2D;
        if (boxCollider == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer = FindPickupSpriteRenderer();
        if (spriteRenderer == null)
        {
            return;
        }

        Bounds bounds = spriteRenderer.bounds;
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        boxCollider.offset = new Vector2(localCenter.x, localCenter.y);
        boxCollider.size = new Vector2(
            Mathf.Max(0.2f, Mathf.Abs(localSize.x)),
            Mathf.Max(0.2f, Mathf.Abs(localSize.y)));
    }

    private SpriteRenderer FindPickupSpriteRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null || spriteRenderer.gameObject == promptObject)
            {
                continue;
            }

            if (spriteRenderer.gameObject.name == "Drop")
            {
                return spriteRenderer;
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer != null && spriteRenderer.gameObject != promptObject)
            {
                return spriteRenderer;
            }
        }

        return null;
    }

    private void ApplyPickupSprite()
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(imagePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[DropPickupInteraction2D] Missing item sprite at Resources/{imagePath}.", this);
            return;
        }

        SpriteRenderer spriteRenderer = FindPickupSpriteRenderer();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
    }

    private void RegisterInventoryDefinition()
    {
        bool hasCustomName = !string.IsNullOrWhiteSpace(itemName) && itemName != DefaultItemName;
        bool hasCustomDescription = !string.IsNullOrWhiteSpace(itemDescription) &&
                                    itemDescription != DefaultItemDescription;
        if (!hasCustomName &&
            !hasCustomDescription &&
            string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        StoryInventoryItemData existing = StoryInventoryManager.GetData(itemId);
        StoryInventoryManager.GetOrCreateInstance().RegisterItemData(new StoryInventoryItemData
        {
            id = itemId,
            itemName = hasCustomName
                ? itemName
                : existing?.itemName,
            description = hasCustomDescription
                ? itemDescription
                : existing?.description,
            imagePath = !string.IsNullOrWhiteSpace(imagePath)
                ? imagePath
                : existing?.imagePath
        });
    }

    private void HideForFloor()
    {
        playerInside = false;
        SetPromptVisible(false);
        gameObject.SetActive(false);
    }

    private static int GetItemIdForFloor(int floorNumber)
    {
        switch (floorNumber)
        {
            case 2:
                return 1;
            case 4:
                return 2;
            case 6:
                return 3;
            case 8:
                return 4;
            default:
                return NoFloorItem;
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
