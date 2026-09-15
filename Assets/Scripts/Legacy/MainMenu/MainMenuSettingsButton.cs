using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MainMenuSettingsButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        EnsureVisible();
        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    private void OnEnable()
    {
        EnsureVisible();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        Debug.Log("\u0053\u0065\u0074\u0074\u0069\u006e\u0067\u0073\u88ab\u70b9\u51fb\u4e86");
    }

    private void EnsureVisible()
    {
        gameObject.SetActive(true);

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.gameObject.SetActive(true);
            graphic.enabled = true;

            if (graphic.gameObject == gameObject || graphic.gameObject.name.StartsWith("GameObject"))
            {
                continue;
            }

            Color color = graphic.color;
            color.a = Mathf.Max(color.a, 1f);
            graphic.color = color;
        }
    }
}
