using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 结局页签内容（End 容器）：点击 End1 / End2 / End3 弹出 props 1 并显示对应结局文本；
/// 悬停结局按钮轻微放大，点击有缩放反馈。
/// 结局文本先用占位，后续通过 <see cref="SetEndingTexts"/> 接入真实数据（EndingService）。
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingTabController : MonoBehaviour
{
    [SerializeField] private Button[] endingButtons;
    [SerializeField] private GameObject propsRoot;
    [SerializeField] private Text propsText;
    [SerializeField] private Button backButton;
    [SerializeField] private string[] endingImagePaths =
    {
        "UI/Endings/Ending_Death",
        "UI/Endings/Ending_Clear",
        "UI/Endings/Ending_Fuse"
    };
    [SerializeField, TextArea] private string[] endingTexts =
    {
        "侵蚀\n\n勘探失败。你的身体被留在楼内，成为后来者能遇见的异常之一。\n\n档案备注：第 9 层出现新的呼吸记录。",
        "撤离\n\n你穿过全部楼层，带着完整记录离开大楼。研究所接收了资料，隐性侵蚀暂时被遏制。\n\n档案备注：外勤任务完成。",
        "融合\n\n你回应了第 9 层的尸体。意识没有离开，而是与楼内的侵蚀结构接在了一起。\n\n档案备注：新的核心存在已生成。"
    };
    [SerializeField, TextArea] private string lockedText = "这个结局尚未完成。\n继续探索楼层，或尝试不同的通关方式。";
    [SerializeField] private Color completedColor = new Color(0.12f, 1f, 0.45f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float clickPunchScale = 1.14f;
    [SerializeField] private float clickPunchDuration = 0.08f;

    private readonly List<Tween> activeTweens = new List<Tween>();
    private Vector3[] baseScales;
    private Sprite[] endingSprites;
    private Sprite[] defaultButtonSprites;

    private void Awake()
    {
        NormalizeEndingTexts();
        NormalizeEndingImagePaths();
        ResolveReferences();
        LoadEndingSprites();
        Bind();
        RefreshEndingState();
    }

    private void OnEnable()
    {
        RefreshEndingState();
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    /// <summary>预留接口：后续从 EndingService 注入真实结局文本（下标对应 End1/2/3）。</summary>
    public void SetEndingTexts(string[] texts)
    {
        if (texts != null)
        {
            endingTexts = texts;
            NormalizeEndingTexts();
        }
    }

    private void ResolveReferences()
    {
        if (endingButtons == null || endingButtons.Length == 0)
        {
            endingButtons = new[]
            {
                FindButton(transform, "End1"),
                FindButton(transform, "End2"),
                FindButton(transform, "End3")
            };
        }

        if (propsRoot == null)
        {
            Transform props = transform.Find("props 1");
            propsRoot = props != null ? props.gameObject : null;
        }

        if (propsText == null && propsRoot != null)
        {
            Transform text = propsRoot.transform.Find("Background/Text");
            propsText = text != null ? text.GetComponent<Text>() : null;
        }

        if (backButton == null && propsRoot != null)
        {
            Transform back = propsRoot.transform.Find("Background/back");
            backButton = back != null ? back.GetComponent<Button>() : null;
        }
    }

    private void Bind()
    {
        baseScales = new Vector3[endingButtons.Length];
        defaultButtonSprites = new Sprite[endingButtons.Length];
        for (int i = 0; i < endingButtons.Length; i++)
        {
            Button button = endingButtons[i];
            if (button == null)
            {
                continue;
            }

            baseScales[i] = button.transform.localScale;
            Image image = GetButtonImage(button);
            if (image != null)
            {
                defaultButtonSprites[i] = image.sprite;
            }

            int index = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnEndingClicked(index));
            AddHover(button, index);
        }

        if (propsRoot != null)
        {
            propsRoot.SetActive(false);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(CloseProps);
            backButton.onClick.AddListener(CloseProps);
        }
    }

    private void OnEndingClicked(int index)
    {
        PlayClickFeedback(index);
        RefreshEndingState();
        bool completed = IsEndingCompleted(index);
        if (propsRoot != null)
        {
            propsRoot.SetActive(true);
        }

        if (propsText != null)
        {
            propsText.text = completed ? GetEndingText(index) : lockedText;
            propsText.color = completed ? completedColor : lockedColor;
        }
    }

    private string GetEndingText(int index)
    {
        if (endingTexts != null && index >= 0 && index < endingTexts.Length)
        {
            return endingTexts[index];
        }

        return string.Empty;
    }

    private void RefreshEndingState()
    {
        for (int i = 0; i < endingButtons.Length; i++)
        {
            Button button = endingButtons[i];
            if (button == null)
            {
                continue;
            }

            SetButtonVisual(button, i, IsEndingCompleted(i));
        }
    }

    private static bool IsEndingCompleted(int index)
    {
        return EndingService.HasUnlockedEnding(IndexToEndingKind(index));
    }

    private static EndingKind IndexToEndingKind(int index)
    {
        switch (index)
        {
            case 0:
                return EndingKind.Death;
            case 1:
                return EndingKind.Clear;
            case 2:
                return EndingKind.Fuse;
            default:
                return EndingKind.None;
        }
    }

    private void SetButtonVisual(Button button, int index, bool completed)
    {
        Image image = GetButtonImage(button);
        if (image == null)
        {
            return;
        }

        if (completed)
        {
            Sprite sprite = GetEndingSprite(index);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
                return;
            }

            image.color = completedColor;
            return;
        }

        if (defaultButtonSprites != null && index >= 0 && index < defaultButtonSprites.Length)
        {
            image.sprite = defaultButtonSprites[index];
        }

        image.color = lockedColor;
        image.preserveAspect = false;
    }

    private static Image GetButtonImage(Button button)
    {
        if (button == null)
        {
            return null;
        }

        Image image = button.targetGraphic as Image;
        return image != null ? image : button.GetComponent<Image>();
    }

    private void CloseProps()
    {
        if (propsRoot != null)
        {
            propsRoot.SetActive(false);
        }
    }

    private void AddHover(Button button, int index)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener(_ => HoverIn(index));

        EventTrigger.Entry exit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exit.callback.AddListener(_ => HoverOut(index));

        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private void HoverIn(int index)
    {
        Button button = GetButton(index);
        if (button == null)
        {
            return;
        }

        KillTween(index);
        activeTweens.Add(button.transform.DOScale(baseScales[index] * hoverScale, hoverDuration).SetEase(Ease.OutQuad));
    }

    private void HoverOut(int index)
    {
        Button button = GetButton(index);
        if (button == null)
        {
            return;
        }

        KillTween(index);
        activeTweens.Add(button.transform.DOScale(baseScales[index], hoverDuration).SetEase(Ease.OutQuad));
    }

    private void PlayClickFeedback(int index)
    {
        Button button = GetButton(index);
        if (button == null)
        {
            return;
        }

        KillTween(index);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(button.transform.DOScale(baseScales[index] * clickPunchScale, clickPunchDuration).SetEase(Ease.OutQuad));
        sequence.Append(button.transform.DOScale(baseScales[index], clickPunchDuration).SetEase(Ease.InQuad));
        activeTweens.Add(sequence);
    }

    private Button GetButton(int index)
    {
        if (endingButtons == null || index < 0 || index >= endingButtons.Length)
        {
            return null;
        }

        return endingButtons[index];
    }

    private void KillTween(int index)
    {
        Button button = GetButton(index);
        Transform target = button != null ? button.transform : null;
        for (int i = activeTweens.Count - 1; i >= 0; i--)
        {
            Tween tween = activeTweens[i];
            if (tween == null || (target != null && tween.target as Transform == target))
            {
                tween?.Kill();
                activeTweens.RemoveAt(i);
            }
        }
    }

    private void KillAllTweens()
    {
        List<Tween> toKill = new List<Tween>(activeTweens);
        activeTweens.Clear();
        for (int i = 0; i < toKill.Count; i++)
        {
            toKill[i]?.Kill();
        }
    }

    private void NormalizeEndingTexts()
    {
        if (endingTexts == null || endingTexts.Length < 3)
        {
            endingTexts = new string[3];
        }

        for (int i = 0; i < 3; i++)
        {
            if (IsPlaceholderText(endingTexts[i]))
            {
                endingTexts[i] = GetDefaultEndingText(i);
            }
        }
    }

    private static bool IsPlaceholderText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.Contains("占位") || text.Trim() == "Text";
    }

    private static string GetDefaultEndingText(int index)
    {
        switch (index)
        {
            case 0:
                return "侵蚀\n\n勘探失败。你的身体被留在楼内，成为后来者能遇见的异常之一。\n\n档案备注：第 9 层出现新的呼吸记录。";
            case 1:
                return "撤离\n\n你穿过全部楼层，带着完整记录离开大楼。研究所接收了资料，隐性侵蚀暂时被遏制。\n\n档案备注：外勤任务完成。";
            case 2:
                return "融合\n\n你回应了第 9 层的尸体。意识没有离开，而是与楼内的侵蚀结构接在了一起。\n\n档案备注：新的核心存在已生成。";
            default:
                return string.Empty;
        }
    }

    private void NormalizeEndingImagePaths()
    {
        string[] defaults =
        {
            "UI/Endings/Ending_Death",
            "UI/Endings/Ending_Clear",
            "UI/Endings/Ending_Fuse"
        };

        if (endingImagePaths == null || endingImagePaths.Length < 3)
        {
            endingImagePaths = defaults;
            return;
        }

        for (int i = 0; i < defaults.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(endingImagePaths[i]))
            {
                endingImagePaths[i] = defaults[i];
            }
        }
    }

    private void LoadEndingSprites()
    {
        endingSprites = new Sprite[endingImagePaths.Length];
        for (int i = 0; i < endingImagePaths.Length; i++)
        {
            endingSprites[i] = LoadSprite(endingImagePaths[i]);
        }
    }

    private Sprite GetEndingSprite(int index)
    {
        if (endingSprites == null || index < 0 || index >= endingSprites.Length)
        {
            return null;
        }

        return endingSprites[index];
    }

    private static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private static Button FindButton(Transform root, string name)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == name)
            {
                return buttons[i];
            }
        }

        return null;
    }
}
