using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SanSliderImageToggle : MonoBehaviour
{
    private const string SliderObjectName = "Slider";
    private const string SanNumberObjectName = "SanNumber";
    private const string StatusTextObjectName = "Text";
    private const string SanityObjectName = "Sanity";

    [SerializeField] private Slider sanSlider;
    [SerializeField] private Text sanNumberText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text sanityText;
    [SerializeField, Range(0f, 1f)] private float image1Threshold = 0.34f;
    [SerializeField, Range(0f, 1f)] private float image2Threshold = 0.67f;
    [SerializeField] private bool invert;
    [SerializeField] private Color dangerColor = new Color(1f, 0.36f, 0.36f, 1f);
    [SerializeField] private Image sliderTrackImage;
    [SerializeField] private Image sliderFillImage;
    [SerializeField] private Image sliderHandleImage;
    [SerializeField] private Sprite sliderTrackNormalSprite;
    [SerializeField] private Sprite sliderTrackDangerSprite;
    [SerializeField] private Sprite sliderFillNormalSprite;
    [SerializeField] private Sprite sliderFillDangerSprite;
    [SerializeField] private Sprite sliderHandleNormalSprite;
    [SerializeField] private Sprite sliderHandleDangerSprite;
    [SerializeField] private Image packageCellImage;
    [SerializeField] private Image packagePanelImage1;
    [SerializeField] private Image packagePanelImage2;
    [SerializeField] private Sprite packageCellNormalSprite;
    [SerializeField] private Sprite packageCellDangerSprite;
    [SerializeField] private Sprite packagePanelNormalSprite;
    [SerializeField] private Sprite packagePanelDangerSprite;
    [SerializeField] private Color packageCellDangerColor = new Color(1f, 0.36f, 0.36f, 1f);

    private Color defaultStatusColor;
    private bool defaultStatusColorCached;
    private Color defaultSanNumberColor;
    private bool defaultSanNumberColorCached;
    private Color defaultSanityColor;
    private bool defaultSanityColorCached;
    private Color defaultPackageCellColor;
    private bool defaultPackageCellColorCached;
    private Color defaultPackagePanel1Color;
    private bool defaultPackagePanel1ColorCached;
    private Color defaultPackagePanel2Color;
    private bool defaultPackagePanel2ColorCached;

    private void Awake()
    {
        ResolveReferences();
        if (sanSlider != null)
        {
            sanSlider.onValueChanged.AddListener(ApplyState);
            ApplyState(sanSlider.value);
        }
    }

    private void OnDestroy()
    {
        if (sanSlider != null)
        {
            sanSlider.onValueChanged.RemoveListener(ApplyState);
        }
    }

    private void ApplyState(float value)
    {
        bool showImage1 = invert ? value <= image1Threshold : value >= image1Threshold;
        bool showImage2 = invert ? value <= image2Threshold : value >= image2Threshold;

        if (sanNumberText != null)
        {
            sanNumberText.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100).ToString();
            sanNumberText.color = showImage1 ? defaultSanNumberColor : dangerColor;
        }

        if (sanityText != null)
        {
            sanityText.color = showImage1 ? defaultSanityColor : dangerColor;
        }

        if (statusText != null)
        {
            statusText.text = showImage2
                ? "稳定 未发现异常"
                : showImage1 ? "不稳定 有异常现象" : "危险 极度危险";
            statusText.color = showImage1 ? defaultStatusColor : dangerColor;
        }

        bool danger = !showImage1;
        if (sliderTrackImage != null)
        {
            Sprite target = danger ? sliderTrackDangerSprite : sliderTrackNormalSprite;
            if (target != null)
            {
                sliderTrackImage.sprite = target;
            }
        }

        if (sliderFillImage != null)
        {
            Sprite target = danger ? sliderFillDangerSprite : sliderFillNormalSprite;
            if (target != null)
            {
                sliderFillImage.sprite = target;
            }
        }

        if (sliderHandleImage != null)
        {
            Sprite target = danger ? sliderHandleDangerSprite : sliderHandleNormalSprite;
            if (target != null)
            {
                sliderHandleImage.sprite = target;
            }
        }

        ApplyPackageCellSwap(
            packageCellImage,
            danger ? packageCellDangerSprite : packageCellNormalSprite,
            danger ? packageCellDangerColor : defaultPackageCellColor);
        ApplyPackageCellSwap(
            packagePanelImage1,
            danger ? packagePanelDangerSprite : packagePanelNormalSprite,
            danger ? packageCellDangerColor : defaultPackagePanel1Color);
        ApplyPackageCellSwap(
            packagePanelImage2,
            danger ? packagePanelDangerSprite : packagePanelNormalSprite,
            danger ? packageCellDangerColor : defaultPackagePanel2Color);

    }

    private void ResolveReferences()
    {
        if (sanSlider == null)
        {
            Transform slider = FindChild(transform, SliderObjectName);
            if (slider != null)
            {
                sanSlider = slider.GetComponent<Slider>();
            }
        }

        Transform sliderRoot = sanSlider != null ? sanSlider.transform : FindChild(transform, SliderObjectName);
        if (sliderTrackImage == null && sliderRoot != null)
        {
            Transform background = FindChild(sliderRoot, "Background");
            if (background != null)
            {
                sliderTrackImage = background.GetComponent<Image>();
            }
        }

        if (sliderFillImage == null && sliderRoot != null)
        {
            Transform fill = FindChild(sliderRoot, "Fill");
            if (fill != null)
            {
                sliderFillImage = fill.GetComponent<Image>();
            }
        }

        if (sliderHandleImage == null && sliderRoot != null)
        {
            Transform handle = FindChild(sliderRoot, "Handle");
            if (handle != null)
            {
                sliderHandleImage = handle.GetComponent<Image>();
            }
        }

        if (sanNumberText == null)
        {
            Transform text = FindChild(transform, SanNumberObjectName);
            if (text != null)
            {
                sanNumberText = text.GetComponent<Text>();
            }
        }

        if (statusText == null)
        {
            Transform text = FindChild(transform, StatusTextObjectName);
            if (text != null)
            {
                statusText = text.GetComponent<Text>();
            }
        }

        if (sanityText == null)
        {
            Transform text = FindChild(transform, SanityObjectName);
            if (text != null)
            {
                sanityText = text.GetComponent<Text>();
            }
        }

        if (statusText != null && !defaultStatusColorCached)
        {
            defaultStatusColor = statusText.color;
            defaultStatusColorCached = true;
        }

        if (sanNumberText != null && !defaultSanNumberColorCached)
        {
            defaultSanNumberColor = sanNumberText.color;
            defaultSanNumberColorCached = true;
        }

        if (sanityText != null && !defaultSanityColorCached)
        {
            defaultSanityColor = sanityText.color;
            defaultSanityColorCached = true;
        }

        if (packageCellImage == null)
        {
            Transform background = FindChild(transform, "Background");
            if (background != null)
            {
                packageCellImage = background.GetComponent<Image>();
            }
        }

        if (packageCellImage != null && !defaultPackageCellColorCached)
        {
            defaultPackageCellColor = packageCellImage.color;
            defaultPackageCellColorCached = true;
        }

        if (packagePanelImage1 != null && !defaultPackagePanel1ColorCached)
        {
            defaultPackagePanel1Color = packagePanelImage1.color;
            defaultPackagePanel1ColorCached = true;
        }

        if (packagePanelImage2 != null && !defaultPackagePanel2ColorCached)
        {
            defaultPackagePanel2Color = packagePanelImage2.color;
            defaultPackagePanel2ColorCached = true;
        }

        if (packageCellNormalSprite == null && packageCellImage != null)
        {
            packageCellNormalSprite = packageCellImage.sprite;
        }

        if (packagePanelNormalSprite == null && packagePanelImage1 != null)
        {
            packagePanelNormalSprite = packagePanelImage1.sprite;
        }

        if (sliderTrackNormalSprite == null && sliderTrackImage != null)
        {
            sliderTrackNormalSprite = sliderTrackImage.sprite;
        }

        if (sliderFillNormalSprite == null && sliderFillImage != null)
        {
            sliderFillNormalSprite = sliderFillImage.sprite;
        }

        if (sliderHandleNormalSprite == null && sliderHandleImage != null)
        {
            sliderHandleNormalSprite = sliderHandleImage.sprite;
        }
    }

    private static Transform FindChild(Transform parent, string name)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
            {
                return children[i];
            }
        }

        return null;
    }

    private static void ApplyPackageCellSwap(Image image, Sprite sprite, Color color)
    {
        if (image == null)
        {
            return;
        }

        if (sprite != null)
        {
            image.sprite = sprite;
        }

        image.color = color;
    }
}
