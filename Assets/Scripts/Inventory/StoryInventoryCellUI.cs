using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public sealed class StoryInventoryCellUI : MonoBehaviour
{
    private static readonly Color CellBackgroundColor = new Color(0.12f, 0.015f, 0.025f, 0.88f);
    private static readonly Color CellNameColor = new Color(0.12f, 1f, 0.45f, 1f);
    private static readonly Color SelectedColor = new Color(0f, 0.95f, 0.35f, 0.22f);
    private static readonly Color HitAreaColor = new Color(1f, 1f, 1f, 0.001f);

    [SerializeField] private Text nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject selectedBackground;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image hitAreaImage;

    private Button button;
    private int itemId;

    public int ItemId => itemId;

    private void Awake()
    {
        ResolveReferences();
        ApplyVisualStyle();
    }

    public void Refresh(int id, StoryInventoryItemData data, StoryInventoryUIController _, bool selected)
    {
        ResolveReferences();
        ApplyVisualStyle();
        itemId = id;

        if (nameText != null)
        {
            nameText.text = data != null ? data.itemName : "Item " + id;
        }

        if (iconImage != null)
        {
            Sprite sprite = StoryInventorySpriteLoader.Load(data != null ? data.imagePath : null);
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedBackground != null)
        {
            selectedBackground.SetActive(selected);
        }
    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
        {
            Transform background = transform.Find("Background");
            backgroundImage = background != null ? background.GetComponent<Image>() : null;
        }

        if (hitAreaImage == null)
        {
            hitAreaImage = GetComponent<Image>();
            if (hitAreaImage == null)
            {
                hitAreaImage = gameObject.AddComponent<Image>();
            }
        }

        if (nameText == null)
        {
            Transform text = transform.Find("Background/Image/Text (Legacy)")
                ?? transform.Find("Background/Text (Legacy)")
                ?? transform.Find("Text")
                ?? transform.Find("Text (Legacy)");
            nameText = text != null ? text.GetComponent<Text>() : null;
        }

        if (iconImage == null)
        {
            Transform icon = transform.Find("Background/Thing");
            iconImage = icon != null ? icon.GetComponent<Image>() : null;
        }

        if (selectedBackground == null)
        {
            Transform selected = transform.Find("SelectedBackground");
            selectedBackground = selected != null ? selected.gameObject : null;
        }
    }

    private void ApplyVisualStyle()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (hitAreaImage == null)
        {
            hitAreaImage = GetComponent<Image>();
        }

        RectTransform cellRect = transform as RectTransform;
        if (cellRect != null)
        {
            cellRect.sizeDelta = new Vector2(120f, 160f);
        }

        if (hitAreaImage != null)
        {
            hitAreaImage.color = HitAreaColor;
            hitAreaImage.raycastTarget = true;
        }

        if (button != null)
        {
            button.interactable = true;
            button.targetGraphic = hitAreaImage;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = CellBackgroundColor;
            backgroundImage.raycastTarget = false;
            RectTransform backgroundRect = backgroundImage.transform as RectTransform;
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
                backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
                backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                backgroundRect.anchoredPosition = new Vector2(0f, 28f);
                backgroundRect.sizeDelta = new Vector2(90f, 100f);
            }
        }

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            RectTransform iconRect = iconImage.transform as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.08f, 0.08f);
                iconRect.anchorMax = new Vector2(0.92f, 0.92f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = Vector2.zero;
            }
        }

        if (nameText != null)
        {
            nameText.transform.SetParent(transform, false);
            nameText.color = CellNameColor;
            nameText.fontSize = 16;
            nameText.alignment = TextAnchor.UpperCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.raycastTarget = false;

            RectTransform textRect = nameText.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 1f);
                textRect.anchoredPosition = new Vector2(0f, -52f);
                textRect.sizeDelta = new Vector2(140f, 46f);
            }

            nameText.transform.SetAsLastSibling();
        }

        if (selectedBackground != null)
        {
            Image selectedImage = selectedBackground.GetComponent<Image>();
            if (selectedImage != null)
            {
                selectedImage.color = SelectedColor;
                selectedImage.raycastTarget = false;
            }
        }
    }
}
