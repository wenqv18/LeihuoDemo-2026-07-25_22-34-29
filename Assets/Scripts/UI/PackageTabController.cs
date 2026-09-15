using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包面板页签控制器（Package1/Top/Background 的 背包 / 结局 / 人物 三个按钮）：
/// 点击页签切换 Center 下对应的内容容器（Package / End / Player），
/// 并给页签按钮设置选中/未选中外观。
/// </summary>
[DisallowMultipleComponent]
public sealed class PackageTabController : MonoBehaviour
{
    private const string TabBarPath = "Center/Package1/Top/Background";
    private const string ContentRootPath = "Center/Package1/Center";

    private enum TabKind
    {
        Package,
        End,
        Player
    }

    [SerializeField] private Button packageTabButton;
    [SerializeField] private Button endTabButton;
    [SerializeField] private Button changePlayerTabButton;
    [SerializeField] private GameObject packageContent;
    [SerializeField] private GameObject endContent;
    [SerializeField] private GameObject playerContent;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(0.72f, 0.72f, 0.72f, 1f);

    private TabKind currentTab;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        ApplyTab(TabKind.Package);
    }

    private void OnEnable()
    {
        EnsurePanelStructureActive();
        ResolveReferences();
        BindButtons();
        ApplyTab(currentTab);
    }

    public void ShowPackageTab()
    {
        EnsurePanelStructureActive();
        ResolveReferences();
        BindButtons();
        ApplyTab(TabKind.Package);
    }

    public void ShowEndTab()
    {
        ApplyTab(TabKind.End);
    }

    public void ShowPlayerTab()
    {
        ApplyTab(TabKind.Player);
    }

    private void ResolveReferences()
    {
        EnsurePanelStructureActive();

        Transform tabBar = transform.Find(TabBarPath);
        if (tabBar != null)
        {
            packageTabButton = packageTabButton != null ? packageTabButton : FindButton(tabBar, "Package");
            endTabButton = endTabButton != null ? endTabButton : FindButton(tabBar, "End");
            changePlayerTabButton = changePlayerTabButton != null
                ? changePlayerTabButton
                : FindButton(tabBar, "ChangePlayer");
        }

        Transform contentRoot = transform.Find(ContentRootPath);
        if (contentRoot != null)
        {
            packageContent = packageContent != null ? packageContent : FindChild(contentRoot, "Package");
            endContent = endContent != null ? endContent : FindChild(contentRoot, "End");
            playerContent = playerContent != null ? playerContent : FindChild(contentRoot, "Player");
        }
    }

    private void EnsurePanelStructureActive()
    {
        SetPathActive("Center", true);
        SetPathActive("Center/Package1", true);
        SetPathActive("Center/Package1/Top", true);
        SetPathActive("Center/Package1/Top/Background", true);
        SetPathActive(ContentRootPath, true);
    }

    private void SetPathActive(string path, bool active)
    {
        Transform target = transform.Find(path);
        if (target != null && target.gameObject.activeSelf != active)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void BindButtons()
    {
        if (packageTabButton != null)
        {
            packageTabButton.onClick.RemoveListener(ShowPackageTab);
            packageTabButton.onClick.AddListener(ShowPackageTab);
        }

        if (endTabButton != null)
        {
            endTabButton.onClick.RemoveListener(ShowEndTab);
            endTabButton.onClick.AddListener(ShowEndTab);
        }

        if (changePlayerTabButton != null)
        {
            changePlayerTabButton.onClick.RemoveListener(ShowPlayerTab);
            changePlayerTabButton.onClick.AddListener(ShowPlayerTab);
        }
    }

    private void ApplyTab(TabKind kind)
    {
        currentTab = kind;
        SetActive(packageContent, kind == TabKind.Package);
        SetActive(endContent, kind == TabKind.End);
        SetActive(playerContent, kind == TabKind.Player);
        SetTabButtonState(packageTabButton, kind == TabKind.Package);
        SetTabButtonState(endTabButton, kind == TabKind.End);
        SetTabButtonState(changePlayerTabButton, kind == TabKind.Player);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void SetTabButtonState(Button button, bool selected)
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
            image.color = selected ? selectedColor : unselectedColor;
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

    private static GameObject FindChild(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
