using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单“TutorialLevel”按钮：点击直接进入新手教学关卡。
/// 教学关独立于 JSON 关卡配置，不会创建/读取 run-save。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class TutorialLevelButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.RemoveListener(StartTutorial);
        button.onClick.AddListener(StartTutorial);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(StartTutorial);
        }
    }

    public void StartTutorial()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.TutorialLevel, LoadSceneMode.Single);
    }
}
