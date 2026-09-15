using UnityEngine;

/// <summary>
/// 控制两个 props 面板：
///   read 1  -> OpenProps   打开 props 1
///   read 2  -> OpenProps2  打开 props 2
///   back（props 1 里）-> CloseProps   关闭 props 1
///   back（props 2 里）-> CloseProps2  关闭 props 2
/// 打开其中一个时会自动关闭另一个，保证同一时间只显示一个面板。
/// </summary>
public sealed class PropsPanelController : MonoBehaviour
{
    [Tooltip("props 1（read 1 打开的面板）")]
    [SerializeField] private GameObject propsRoot;

    [Tooltip("props 2（read 2 打开的面板）")]
    [SerializeField] private GameObject props2Root;

    private void Awake()
    {
        // 运行时默认隐藏两个面板，避免打开背包时它们跟着显示。
        if (propsRoot != null)
        {
            propsRoot.SetActive(false);
        }

        if (props2Root != null)
        {
            props2Root.SetActive(false);
        }
    }

    public void OpenProps()
    {
        if (props2Root != null)
        {
            props2Root.SetActive(false);
        }

        if (propsRoot != null)
        {
            propsRoot.SetActive(true);
        }
    }

    public void CloseProps()
    {
        if (propsRoot != null)
        {
            propsRoot.SetActive(false);
        }
    }

    public void OpenProps2()
    {
        if (propsRoot != null)
        {
            propsRoot.SetActive(false);
        }

        if (props2Root != null)
        {
            props2Root.SetActive(true);
        }
    }

    public void CloseProps2()
    {
        if (props2Root != null)
        {
            props2Root.SetActive(false);
        }
    }
}
