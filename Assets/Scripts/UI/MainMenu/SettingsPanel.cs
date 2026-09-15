using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SettingsPanel : MonoBehaviour
{
    private const string MainCanvasName = "Canvas";
    private const string MainMenuCenterName = "Center";
    private const string SettingsButtonName = "Settings";

    public GameObject settingsPanel;
    public Slider volumeSlider;
    public Slider sfxSlider;
    public Slider bgmSlider;
    public AudioClip tickSound;
    public AudioClip doneSound;

    private AudioSource audioSource;
    private Button settingsButton;
    private float lastTickTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        ResolveSettingsReferences();
        SetSettingsPanelVisible(false);
        EnsureSettingsButtonVisible();
    }

    private void Start()
    {
        ResolveSettingsReferences();
        SetSettingsPanelVisible(false);
        EnsureSettingsButtonVisible();

        // 三个滑块：总音量 / 音效 / BGM。
        // 滑块任意范围（0~1 或 0~100）均可，中间值=当前基准音量；
        // 总音量 100% + 音效 100% 时音效放大 4 倍（总音量 100% + BGM 100% 同理）。
        SetSliderDefault(volumeSlider, GameAudioManager.MasterVolumePrefsKey);
        SetSliderDefault(sfxSlider, GameAudioManager.SfxVolumePrefsKey);
        SetSliderDefault(bgmSlider, GameAudioManager.BgmVolumePrefsKey);

        volumeSlider?.onValueChanged.RemoveAllListeners();
        volumeSlider?.onValueChanged.AddListener(v =>
        {
            GameAudioManager.EnsureInstance()?.SetMasterVolume(NormalizeSlider(v, volumeSlider));
            PlayTick();
        });

        sfxSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.AddListener(v =>
        {
            GameAudioManager.EnsureInstance()?.SetSfxVolume(NormalizeSlider(v, sfxSlider));
            PlayTick();
        });

        bgmSlider?.onValueChanged.RemoveAllListeners();
        bgmSlider?.onValueChanged.AddListener(v =>
        {
            GameAudioManager.EnsureInstance()?.SetBgmVolume(NormalizeSlider(v, bgmSlider));
            PlayTick();
        });
    }

    private void LateUpdate()
    {
        EnsureSettingsButtonVisible();
    }

    public void OpenPanel()
    {
        ResolveSettingsReferences();
        if (settingsPanel == null)
        {
            return;
        }

        Debug.Log("Settings\u88ab\u70b9\u51fb\u4e86");

        settingsPanel.transform.localScale = Vector3.zero;
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
        settingsPanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    public void ClosePanel()
    {
        ResolveSettingsReferences();
        if (settingsPanel == null)
        {
            return;
        }

        PlayDoneSound();
        settingsPanel.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack)
            .OnComplete(() => settingsPanel.SetActive(false));
    }

    private void PlayTick()
    {
        if (tickSound == null) return;
        if (Time.time - lastTickTime < 0.04f) return;
        lastTickTime = Time.time;
        audioSource.PlayOneShot(tickSound, GameAudioManager.SfxScaleForClip(tickSound));
    }

    private void PlayDoneSound()
    {
        if (doneSound == null) return;
        audioSource.PlayOneShot(doneSound, GameAudioManager.SfxScaleForClip(doneSound));
    }

    /// <summary>把任意范围（0~1 或 0~100）的滑块值归一化为 0~100。</summary>
    private static float NormalizeSlider(float value, Slider slider)
    {
        float max = slider != null ? Mathf.Max(slider.maxValue, 0.0001f) : 1f;
        return Mathf.Clamp(value / max * 100f, 0f, 100f);
    }

    /// <summary>滑块默认停在“当前基准音量”对应的位置（即 50/100 或 0.5）。</summary>
    private static void SetSliderDefault(Slider slider, string prefsKey)
    {
        if (slider == null)
        {
            return;
        }

        // stored 为 0~100（内部统一刻度），50 对应当前基准音量。
        float stored = PlayerPrefs.GetFloat(prefsKey, GameAudioManager.ReferenceSliderValue);
        float normalized = Mathf.Clamp(stored / 100f, 0f, 1f);
        slider.value = slider.minValue + (slider.maxValue - slider.minValue) * normalized;
    }

    private void ResolveSettingsReferences()
    {
        if (settingsPanel == null)
        {
            settingsPanel = FindSettingsPanel();
        }

        if (settingsButton == null)
        {
            settingsButton = FindMainMenuSettingsButton();
        }

        if (volumeSlider == null)
        {
            volumeSlider = FindSlider(settingsPanel, "VolumeSlider");
        }

        if (sfxSlider == null)
        {
            sfxSlider = FindSlider(settingsPanel, "SFXSlider");
        }

        if (bgmSlider == null)
        {
            bgmSlider = FindSlider(settingsPanel, "BGM");
        }
    }

    private static Slider FindSlider(GameObject root, string sliderName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.transform)
        {
            if (child.name != sliderName)
            {
                continue;
            }

            Slider slider = child.GetComponent<Slider>();
            if (slider != null)
            {
                return slider;
            }
        }

        return null;
    }

    private void SetSettingsPanelVisible(bool visible)
    {
        if (settingsPanel == null)
        {
            settingsPanel = FindSettingsPanel();
        }

        if (settingsPanel == null)
        {
            return;
        }

        settingsPanel.transform.localScale = visible ? Vector3.one : Vector3.zero;
        settingsPanel.SetActive(visible);
    }

    private void EnsureSettingsButtonVisible()
    {
        if (settingsButton == null)
        {
            settingsButton = FindMainMenuSettingsButton();
        }

        if (settingsButton == null)
        {
            return;
        }

        if (!settingsButton.gameObject.activeSelf)
        {
            Debug.Log("[SettingsPanel] Settings button was hidden and has been restored.");
            settingsButton.gameObject.SetActive(true);
        }

        settingsButton.interactable = true;

        var buttonImage = settingsButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.enabled = true;
            buttonImage.raycastTarget = true;
        }

        foreach (var graphic in settingsButton.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || ShouldLeaveHoverGraphicHidden(graphic.transform))
            {
                continue;
            }

            graphic.gameObject.SetActive(true);
            graphic.enabled = true;

            if (graphic.transform != settingsButton.transform && graphic.color.a <= 0f)
            {
                var color = graphic.color;
                color.a = 1f;
                graphic.color = color;
            }
        }
    }

    private Button FindMainMenuSettingsButton()
    {
        var center = FindMainMenuCenter();
        if (center == null)
        {
            return null;
        }

        foreach (Transform child in center)
        {
            if (child.name != SettingsButtonName)
            {
                continue;
            }

            var button = child.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private Transform FindMainMenuCenter()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != MainCanvasName)
            {
                continue;
            }

            foreach (Transform child in root.transform)
            {
                if (child.name == MainMenuCenterName)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private GameObject FindSettingsPanel()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != MainCanvasName)
            {
                continue;
            }

            foreach (Transform child in root.transform)
            {
                if (child.name == MainMenuCenterName)
                {
                    continue;
                }

                if (child.name.Contains(SettingsButtonName))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private bool ShouldLeaveHoverGraphicHidden(Transform target)
    {
        return target.name == "Background"
            || target.name == "GameObject"
            || target.name == "04";
    }
}
