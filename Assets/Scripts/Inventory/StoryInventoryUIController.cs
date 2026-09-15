using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class StoryInventoryUIController : MonoBehaviour
{
    private static readonly Color DetailNameColor = new Color(0.12f, 1f, 0.45f, 1f);
    private static readonly Color DetailTextColor = new Color(1f, 0.2f, 0.18f, 1f);

    [SerializeField] private GameObject inventoryRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Image detailIcon;
    [SerializeField] private Text nameText;
    [SerializeField] private Text detailText;
    [SerializeField] private Button closeButton;

    private readonly List<StoryInventoryCellUI> cells = new List<StoryInventoryCellUI>();
    private int selectedItemId = -1;
    private Button boundCloseButton;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        SetInventoryVisible(false);
    }

    private void OnEnable()
    {
        StoryInventoryManager.Changed += RefreshUI;
        RefreshUI();
    }

    private void OnDisable()
    {
        StoryInventoryManager.Changed -= RefreshUI;
        UnbindCloseButton();
    }

    public void ToggleInventory()
    {
        if (GameUIController.TryTogglePackage())
        {
            return;
        }

        ToggleInventoryDirect();
    }

    public void CloseInventory()
    {
        if (GameUIController.TryClosePackage())
        {
            return;
        }

        SetInventoryVisible(false);
    }

    public void ToggleInventoryDirect()
    {
        ResolveReferences();
        BindButtons();
        SetInventoryVisible(inventoryRoot == null || !inventoryRoot.activeSelf);
    }

    public void SelectItem(int itemId)
    {
        selectedItemId = itemId;
        RefreshSelection();
        RefreshDetail();
    }

    public void RefreshUI()
    {
        ResolveReferences();
        BindButtons();
        ApplyPanelStyle();
        RebuildCells();
        EnsureSelection();
        RefreshSelection();
        RefreshDetail();
    }

    private void SetInventoryVisible(bool visible)
    {
        if (inventoryRoot != null)
        {
            inventoryRoot.SetActive(visible);
        }

        if (visible)
        {
            ResolveReferences();
            BindButtons();
            RefreshUI();
        }
    }

    private void RebuildCells()
    {
        cells.Clear();
        if (contentRoot == null || cellPrefab == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        StoryInventoryManager manager = StoryInventoryManager.GetOrCreateInstance();
        IReadOnlyList<int> ids = manager.OwnedItemIds;
        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            GameObject cellObject = Instantiate(cellPrefab, contentRoot, false);
            cellObject.name = "PackageCell_" + id;

            StoryInventoryCellUI cell = cellObject.GetComponent<StoryInventoryCellUI>();
            if (cell == null)
            {
                cell = cellObject.AddComponent<StoryInventoryCellUI>();
            }

            cell.Refresh(id, manager.GetItemData(id), this, id == selectedItemId);
            BindCellButton(cellObject, id);
            cells.Add(cell);
        }
    }

    private void EnsureSelection()
    {
        StoryInventoryManager manager = StoryInventoryManager.GetOrCreateInstance();
        IReadOnlyList<int> ids = manager.OwnedItemIds;
        if (selectedItemId >= 0 && manager.HasItem(selectedItemId))
        {
            return;
        }

        selectedItemId = ids.Count > 0 ? ids[0] : -1;
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].SetSelected(cells[i].ItemId == selectedItemId);
        }
    }

    private void RefreshDetail()
    {
        StoryInventoryItemData data = selectedItemId >= 0 ? StoryInventoryManager.GetData(selectedItemId) : null;
        ApplyPanelStyle();

        if (detailText != null)
        {
            detailText.text = data != null ? data.description : string.Empty;
        }

        if (nameText != null)
        {
            nameText.text = data != null ? data.itemName : string.Empty;
        }

        if (detailIcon != null)
        {
            Sprite sprite = data != null ? StoryInventorySpriteLoader.Load(data.imagePath) : null;
            detailIcon.sprite = sprite;
            detailIcon.enabled = sprite != null;
            detailIcon.preserveAspect = true;
        }
    }

    private void BindButtons()
    {
        if (closeButton == null)
        {
            return;
        }

        if (boundCloseButton != null && boundCloseButton != closeButton)
        {
            boundCloseButton.onClick.RemoveListener(CloseInventory);
        }

        closeButton.interactable = true;
        closeButton.onClick.RemoveListener(CloseInventory);
        closeButton.onClick.AddListener(CloseInventory);
        boundCloseButton = closeButton;
    }

    private void BindCellButton(GameObject cellObject, int itemId)
    {
        if (cellObject == null)
        {
            return;
        }

        Button cellButton = cellObject.GetComponent<Button>();
        if (cellButton == null)
        {
            cellButton = cellObject.AddComponent<Button>();
        }

        cellButton.interactable = true;

        Graphic targetGraphic = cellButton.targetGraphic;
        if (targetGraphic == null)
        {
            targetGraphic = cellObject.GetComponent<Graphic>();
            cellButton.targetGraphic = targetGraphic;
        }

        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
        }

        Graphic[] graphics = cellObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null && graphic != targetGraphic)
            {
                graphic.raycastTarget = false;
            }
        }

        int capturedItemId = itemId;
        cellButton.onClick.RemoveAllListeners();
        cellButton.onClick.AddListener(() => SelectItem(capturedItemId));
    }

    private void UnbindCloseButton()
    {
        if (boundCloseButton == null)
        {
            return;
        }

        boundCloseButton.onClick.RemoveListener(CloseInventory);
        boundCloseButton = null;
    }

    private void ResolveReferences()
    {
        if (inventoryRoot == null)
        {
            GameObject package = FindGameObjectInLoadedScenes("Package");
            inventoryRoot = package;
        }

        Transform root = inventoryRoot != null ? inventoryRoot.transform : null;

        if (contentRoot == null && root != null)
        {
            Transform content = root.Find("Center/Package1/Center/Package/CenterLeft/Background/Content")
                ?? root.Find("Package1/Center/Package/CenterLeft/Background/Content")
                ?? root.Find("Center/Package1/Center/CenterLeft/Background/Content")
                ?? root.Find("Package1/Center/CenterLeft/Background/Content")
                ?? root.Find("Center/Package1/Center/CenterLeft/Scroll View/Viewport/Content")
                ?? root.Find("Package1/Center/CenterLeft/Scroll View/Viewport/Content")
                ?? FindChild(root, "Content");
            contentRoot = content != null ? content.GetComponent<RectTransform>() : null;
        }

        if (detailIcon == null && root != null)
        {
            Transform icon = root.Find("Center/Package1/Center/Package/CenterRight/Top/Thing")
                ?? root.Find("Package1/Center/Package/CenterRight/Top/Thing")
                ?? root.Find("Center/Package1/Center/CenterRight/Top/Thing")
                ?? root.Find("Package1/Center/CenterRight/Top/Thing");
            detailIcon = icon != null ? icon.GetComponent<Image>() : null;
        }

        if (nameText == null && root != null)
        {
            Transform name = root.Find("Center/Package1/Center/Package/CenterRight/Top/Name")
                ?? root.Find("Package1/Center/Package/CenterRight/Top/Name")
                ?? root.Find("Center/Package1/Center/CenterRight/Top/Name")
                ?? root.Find("Package1/Center/CenterRight/Top/Name");
            nameText = name != null ? name.GetComponent<Text>() : null;
        }

        if (detailText == null && root != null)
        {
            Transform text = root.Find("Center/Package1/Center/Package/CenterRight/Bottom/Detail")
                ?? root.Find("Package1/Center/Package/CenterRight/Bottom/Detail")
                ?? root.Find("Center/Package1/Center/CenterRight/Bottom/Detail")
                ?? root.Find("Package1/Center/CenterRight/Bottom/Detail")
                ?? root.Find("Center/Package1/Center/CenterRight/Bottom/Text")
                ?? root.Find("Package1/Center/CenterRight/Bottom/Text")
                ?? root.Find("Center/Package1/Center/CenterRight/Bottom/Name")
                ?? root.Find("Package1/Center/CenterRight/Bottom/Name");
            detailText = text != null ? text.GetComponent<Text>() : null;
        }

        if (closeButton == null && root != null)
        {
            Transform close = root.Find("Center/Package1/Top/Background/ESC")
                ?? root.Find("Package1/Top/Background/ESC");
            if (close != null)
            {
                closeButton = close.GetComponent<Button>();
                if (closeButton == null)
                {
                    closeButton = close.gameObject.AddComponent<Button>();
                }
            }
        }

        BindButtons();
        ApplyPanelStyle();
    }

    private void ApplyPanelStyle()
    {
        if (detailIcon != null)
        {
            detailIcon.preserveAspect = true;
        }

        if (nameText != null)
        {
            nameText.color = DetailNameColor;

            RectTransform nameRect = nameText.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.anchoredPosition = new Vector2(-130f, nameRect.anchoredPosition.y);
            }
        }

        if (detailText != null)
        {
            detailText.color = DetailTextColor;
        }
    }

    private static GameObject FindGameObjectInLoadedScenes(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject fallback = null;
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate.name != objectName ||
                !candidate.scene.IsValid() || !candidate.scene.isLoaded ||
                candidate.scene.name != SceneNames.UIScene)
            {
                continue;
            }

            // 优先根节点，避免命中同名内层容器。
            if (candidate.transform.parent == null)
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
