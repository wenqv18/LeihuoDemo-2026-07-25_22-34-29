using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Settings 面板 Continue / Leave 的悬停联动（鼠标悬停触发，不是点击）：
/// 1. 悬停 Continue -> Settings/Center/Background 切成 panel-slice（普通）
///    悬停 Leave   -> 同一 Background 切成 panel-slice-red（红色）
/// 2. 按钮里的覆盖层（优先名为 GameObject / tx 的子物体，其次第一个 Image 子物体）
///    悬停时从 0 宽度缓慢撑满整颗按钮（btn-default 背景图大小），移开后收回并隐藏。
/// 3. 悬停 Leave -> Settings 里的联动文字（TMP）变成红色；悬停 Continue / 移开 -> 恢复原色。
/// 挂到 Continue / Leave 按钮上即可，组件会自动向上找到 Settings 面板。
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SettingsContinueLeaveHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Background（Settings/Center/Background）")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite redSprite;

    [Header("填满动画")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float fillDuration = 0.35f;

    [Header("联动文字（Settings 里的 TMP/Text）")]
    [SerializeField] private Graphic[] panelTexts;
    [SerializeField] private Color redTextColor = new Color(0.8627451f, 0.37254903f, 0.3372549f, 1f);

    [Header("调试")]
    [SerializeField] private bool logEvents;

    private readonly List<Color> defaultTextColors = new List<Color>();
    private Sprite defaultBackgroundSprite;
    private Tween sizeTween;
    private Tween fadeTween;
    private bool ready;
    private float visibleAlpha = 1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SetHiddenImmediate();
    }

    private void OnDisable()
    {
        KillTweens();
        RestoreBackground();
        RestoreTextColors();
        SetHiddenImmediate();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    public void ResolveReferences()
    {
        ready = false;
        if (panelBackground == null)
        {
            panelBackground = FindSettingsBackground();
        }
        if (normalSprite == null && panelBackground != null && panelBackground.sprite != null)
        {
            normalSprite = panelBackground.sprite;
        }
        if (fillImage == null)
        {
            fillImage = FindFillImage();
        }

        if (panelBackground != null && defaultBackgroundSprite == null)
        {
            defaultBackgroundSprite = panelBackground.sprite;
        }
        if (fillImage != null)
        {
            // 尊重覆盖层原本的透明度（如 slider-fill 图），没有才用 fillColor
            if (fillImage.color.a > 0.01f)
            {
                visibleAlpha = fillImage.color.a;
            }
            else
            {
                visibleAlpha = fillColor.a;
            }
            PrepareFillImage();
        }

        defaultTextColors.Clear();
        if (panelTexts != null)
        {
            foreach (Graphic text in panelTexts)
            {
                defaultTextColors.Add(text != null ? text.color : Color.white);
            }
        }

        ready = panelBackground != null && fillImage != null;
        if (logEvents)
        {
            Debug.Log($"[Hover] {name} 组件就绪: ready={ready} fill={(fillImage != null ? fillImage.gameObject.name : "null")} bg={(panelBackground != null ? panelBackground.gameObject.name : "null")} texts={(panelTexts != null ? panelTexts.Length : 0)}");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (logEvents)
        {
            Debug.Log($"[Hover] {name} 鼠标进入 -> 开始填满 (ready={ready})");
        }
        if (!ready || !isActiveAndEnabled)
        {
            return;
        }

        bool isLeave = gameObject.name.StartsWith("Leave");
        Sprite target = isLeave ? redSprite : normalSprite;
        if (panelBackground != null && target != null)
        {
            panelBackground.sprite = target;
        }

        if (isLeave)
        {
            SetTextsColor(redTextColor);
        }
        else
        {
            RestoreTextColors();
        }

        PlayFill();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (logEvents)
        {
            Debug.Log($"[Hover] {name} 鼠标移开 -> 收回");
        }
        if (!isActiveAndEnabled)
        {
            return;
        }

        RestoreBackground();
        RestoreTextColors();
        PlayUnfill();
    }

    private void PlayFill()
    {
        if (fillImage == null)
        {
            return;
        }

        Color color = fillImage.color;
        color.a = 0f;
        fillImage.color = color;
        SetFillSize(0f);
        fillImage.rectTransform.anchoredPosition = Vector2.zero;

        KillTweens();
        sizeTween = DOTween.To(GetFillSize, SetFillSize, GetFillSizeTarget(), fillDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
        fadeTween = fillImage
            .DOFade(visibleAlpha, fillDuration * 0.65f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void PlayUnfill()
    {
        if (fillImage == null)
        {
            return;
        }

        KillTweens();
        sizeTween = DOTween.To(GetFillSize, SetFillSize, 0f, fillDuration * 0.7f)
            .SetEase(Ease.InCubic)
            .SetUpdate(true);
        fadeTween = fillImage
            .DOFade(0f, fillDuration * 0.5f)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);
    }

    private void SetHiddenImmediate()
    {
        if (fillImage == null)
        {
            return;
        }
        Color color = fillImage.color;
        color.a = 0f;
        fillImage.color = color;
        SetFillSize(0f);
    }

    private void RestoreBackground()
    {
        if (panelBackground != null && defaultBackgroundSprite != null)
        {
            panelBackground.sprite = defaultBackgroundSprite;
        }
    }

    private void SetTextsColor(Color color)
    {
        if (panelTexts == null)
        {
            return;
        }
        for (int i = 0; i < panelTexts.Length; i++)
        {
            if (panelTexts[i] == null)
            {
                continue;
            }
            Color c = color;
            if (i < defaultTextColors.Count)
            {
                c.a = defaultTextColors[i].a; // 保留原来的透明度
            }
            panelTexts[i].color = c;
        }
    }

    private void RestoreTextColors()
    {
        if (panelTexts == null)
        {
            return;
        }
        for (int i = 0; i < panelTexts.Length; i++)
        {
            if (panelTexts[i] == null)
            {
                continue;
            }
            if (i < defaultTextColors.Count)
            {
                panelTexts[i].color = defaultTextColors[i];
            }
        }
    }

    private void KillTweens()
    {
        if (sizeTween != null && sizeTween.IsActive())
        {
            sizeTween.Kill();
        }
        sizeTween = null;
        if (fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }
        fadeTween = null;
    }

    private float GetFillSize()
    {
        return fillImage != null ? fillImage.rectTransform.rect.width : 0f;
    }

    private float GetFillSizeTarget()
    {
        RectTransform parent = transform as RectTransform;
        return parent != null ? parent.rect.width : 0f;
    }

    private void SetFillSize(float width)
    {
        if (fillImage == null)
        {
            return;
        }
        Vector2 size = fillImage.rectTransform.sizeDelta;
        size.x = width;
        fillImage.rectTransform.sizeDelta = size;
    }

    private void PrepareFillImage()
    {
        if (fillImage == null)
        {
            return;
        }

        RectTransform rect = fillImage.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);   // 左对齐、上下拉伸，宽度由 sizeDelta.x 控制
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private Image FindFillImage()
    {
        Transform fill = transform.Find("GameObject");
        if (fill != null)
        {
            Image img = fill.GetComponent<Image>();
            if (img != null)
            {
                return img;
            }
        }
        fill = transform.Find("tx");
        if (fill != null)
        {
            Image img = fill.GetComponent<Image>();
            if (img != null)
            {
                return img;
            }
        }
        for (int i = 0; i < transform.childCount; i++)
        {
            Image img = transform.GetChild(i).GetComponent<Image>();
            if (img != null)
            {
                return img;
            }
        }
        return null;
    }

    private Image FindSettingsBackground()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "Settings")
            {
                Transform center = FindChild(current, "Center");
                if (center != null)
                {
                    Transform background = FindChild(center, "Background");
                    if (background != null)
                    {
                        return background.GetComponent<Image>();
                    }
                }
                break;
            }
            current = current.parent;
        }
        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
            Transform nested = FindChild(child, name);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }
}
