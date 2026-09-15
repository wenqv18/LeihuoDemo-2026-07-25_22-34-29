using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ButtonClickSoundBinder : MonoBehaviour
{
    private const string ClipResourcesPath = "UI/sound/点击按钮";
    private const string UiSceneName = "UIController";

    private static ButtonClickSoundBinder instance;

    [SerializeField] private AudioClip clickSound;

    private AudioSource audioSource;
    private readonly HashSet<int> boundButtons = new HashSet<int>();

    private void Awake()
    {
        instance = this;
        EnsureAudioSource();
        if (clickSound == null)
        {
            clickSound = Resources.Load<AudioClip>(ClipResourcesPath);
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

    public static void PlayClick()
    {
        if (instance != null)
        {
            instance.PlayClickSound();
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
        if (clickSound == null || audioSource == null)
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

            if (button.GetComponentInParent<DeathPanelButtonBinder>(true) != null)
            {
                continue;
            }

            int id = button.GetHashCode();
            if (boundButtons.Contains(id))
            {
                continue;
            }

            button.onClick.AddListener(PlayClickSound);
            boundButtons.Add(id);
        }
    }

    private void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound, GameAudioManager.SfxScaleForClip(clickSound));
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
