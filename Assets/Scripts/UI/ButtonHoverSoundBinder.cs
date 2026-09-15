using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ButtonHoverSoundBinder : MonoBehaviour
{
    private const string ClipResourcesPath = "UI/sound/slider_tick";
    private const string UiSceneName = "UIController";

    private static ButtonHoverSoundBinder instance;

    [SerializeField] private AudioClip hoverSound;

    private AudioSource audioSource;
    private readonly HashSet<int> boundButtons = new HashSet<int>();

    private void Awake()
    {
        instance = this;
        EnsureAudioSource();
        if (hoverSound == null)
        {
            hoverSound = Resources.Load<AudioClip>(ClipResourcesPath);
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        StartCoroutine(BindLoop());
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    public static void PlayHoverSound()
    {
        if (instance != null)
        {
            instance.Play();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindAll();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (scene.name == UiSceneName)
        {
            boundButtons.Clear();
        }
    }

    private IEnumerator BindLoop()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.75f);
        while (true)
        {
            BindAll();
            yield return wait;
        }
    }

    private void BindAll()
    {
        EnsureAudioSource();
        if (hoverSound == null || audioSource == null)
        {
            return;
        }

        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.gameObject.scene.name != UiSceneName)
            {
                continue;
            }

            int id = button.GetHashCode();
            if (boundButtons.Contains(id))
            {
                continue;
            }

            if (TryBindExistingHoverSound(button))
            {
                boundButtons.Add(id);
                continue;
            }

            if (HasHoverEffectWithoutSound(button))
            {
                if (button.gameObject.GetComponent<HoverSoundTrigger>() == null)
                {
                    button.gameObject.AddComponent<HoverSoundTrigger>();
                }
                boundButtons.Add(id);
            }
        }
    }

    private bool TryBindExistingHoverSound(Button button)
    {
        HoverDisplay hoverDisplay = button.GetComponent<HoverDisplay>();
        if (hoverDisplay != null)
        {
            hoverDisplay.hoverSound = hoverSound;
            return true;
        }

        HoverFade hoverFade = button.GetComponent<HoverFade>();
        if (hoverFade != null)
        {
            hoverFade.hoverSound = hoverSound;
            return true;
        }

        return false;
    }

    private static bool HasHoverEffectWithoutSound(Button button)
    {
        return button.GetComponent<SettingsContinueLeaveHover>() != null
            || button.GetComponent<ButtonHoverFadeBackground>() != null;
    }

    private void Play()
    {
        if (audioSource != null && hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound, GameAudioManager.SfxScaleForClip(hoverSound));
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            return;
        }
    }
}

public sealed class HoverSoundTrigger : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        ButtonHoverSoundBinder.PlayHoverSound();
    }
}
