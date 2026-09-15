using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包“人物”页签的男生/女生选择：点击切换玩家美术（PlayerAppearanceController），
/// 并高亮当前选择，默认男生。
/// </summary>
public sealed class PlayerSelectBinder : MonoBehaviour
{
    private const string PlayerPanelPath = "Center/Package1/Center/Player";
    private const string PlayerPanelName = "Player";
    private const string PackageRootName = "Package";
    private const string BoyName = "Boy";
    private const string GirlName = "Girl";

    [SerializeField] private Button boyButton;
    [SerializeField] private Button girlButton;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        RefreshState();
    }

    private void Start()
    {
        // Awake 时机兜底：确保按钮引用在层级完全就绪后绑定。
        ResolveReferences();
        BindButtons();
        RefreshState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtons();
        RefreshState();
    }

    private void ResolveReferences()
    {
        Transform panel = ResolvePlayerPanel();
        if (panel == null)
        {
            return;
        }

        boyButton = boyButton != null ? boyButton : FindButton(panel, BoyName);
        girlButton = girlButton != null ? girlButton : FindButton(panel, GirlName);
    }

    private void BindButtons()
    {
        if (boyButton != null)
        {
            boyButton.onClick.RemoveListener(SelectBoy);
            boyButton.onClick.AddListener(SelectBoy);
        }

        if (girlButton != null)
        {
            girlButton.onClick.RemoveListener(SelectGirl);
            girlButton.onClick.AddListener(SelectGirl);
        }
    }

    private void SelectBoy()
    {
        PlayerAppearanceController.SetAppearance(PlayerAppearanceController.MaleIndex);
        RefreshState();
    }

    private void SelectGirl()
    {
        PlayerAppearanceController.SetAppearance(PlayerAppearanceController.FemaleIndex);
        RefreshState();
    }

    private void RefreshState()
    {
        int index = PlayerAppearanceController.CurrentIndex;
        SetButtonSelected(boyButton, index == PlayerAppearanceController.MaleIndex);
        SetButtonSelected(girlButton, index == PlayerAppearanceController.FemaleIndex);
    }

    private static void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
        }

        if (image != null)
        {
            image.color = selected ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }
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

    private Transform ResolvePlayerPanel()
    {
        Transform direct = transform.Find(PlayerPanelPath);
        if (direct != null)
        {
            return direct;
        }

        Transform nearby = FindDescendant(transform, PlayerPanelName);
        if (nearby != null)
        {
            return nearby;
        }

        Transform current = transform.parent;
        while (current != null)
        {
            Transform fromParent = current.Find(PlayerPanelPath) ?? FindDescendant(current, PlayerPanelName);
            if (fromParent != null)
            {
                return fromParent;
            }

            current = current.parent;
        }

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            Transform rootTransform = root.transform;
            if (root.name == PackageRootName)
            {
                Transform fromPackageRoot = rootTransform.Find(PlayerPanelPath) ??
                                            FindDescendant(rootTransform, PlayerPanelName);
                if (fromPackageRoot != null)
                {
                    return fromPackageRoot;
                }
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
