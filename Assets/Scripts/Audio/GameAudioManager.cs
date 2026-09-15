using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音频管理器（BGM 与 SFX 分通道）：
/// - BGM：背景音乐（主世界 / SpecialWorld），可叠加氛围层（恐怖气氛）；
/// - SFX：一次性音效（过渡、失败、脚步、怪物等）。
/// 音量分开控制，为后续设置界面预留 SetBgmVolume / SetSfxVolume。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    /// <summary>设置滑块以 0~100 表示，50 对应当前基准音量。</summary>
    public const float ReferenceSliderValue = 50f;
    public const string MasterVolumePrefsKey = "Leihuo.Audio.Master";
    public const string SfxVolumePrefsKey = "Leihuo.Audio.Sfx";
    public const string BgmVolumePrefsKey = "Leihuo.Audio.Bgm";

    // BGM 整体降低 20%：0.8 -> 0.64，气氛音 0.44 -> 0.352。
    private const float BgmBaseVolume = 0.64f;
    private const float AmbientBaseVolume = 0.352f;
    private const string NormalBgmPath = "UI/sound/主世界BGM";
    private const string SpecialBgmPath = "UI/sound/SpecialWorldBGM";
    private const string SpecialAmbientPath = "UI/sound/恐怖气氛（SpecialWorld的背景音）";
    private const string TransitionToSpecialPath = "UI/sound/N转S音效";
    private const string TransitionToNormalPath = "UI/sound/S转N音效";
    private const string PhotoFailPath = "UI/sound/拍照失败";
    private const string StepSoundPath = "UI/sound/走路声";
    private const string MonsterRoarPath = "UI/sound/怪物叫声";
    private const string BgmSourceName = "BGM Source";
    private const string AmbientSourceName = "Ambient Source";
    private const string SfxSourceName = "SFX Source";

    private AudioSource bgmSource;
    private AudioSource ambientSource;
    private AudioSource sfxSource;

    private AudioClip normalBgm;
    private AudioClip specialBgm;
    private AudioClip specialAmbient;
    private AudioClip transitionToSpecial;
    private AudioClip transitionToNormal;
    private AudioClip photoFail;
    private AudioClip stepSound;
    private AudioClip monsterRoar;
    private SfxLoudnessProfile sfxLoudnessProfile;

    private float masterVolumeSetting = ReferenceSliderValue;
    private float sfxVolumeSetting = ReferenceSliderValue;
    private float bgmVolumeSetting = ReferenceSliderValue;

    /// <summary>总音量增益：滑块 50→1 倍，100→2 倍。</summary>
    public float MasterGain => masterVolumeSetting / ReferenceSliderValue;

    /// <summary>音效通道增益（不含总音量）：滑块 50→1 倍，100→2 倍。
    /// 总音量由 AudioListener 统一放大，两者相乘即“总音量100+音效100→4 倍”。</summary>
    public float SfxChannelGain => sfxVolumeSetting / ReferenceSliderValue;

    /// <summary>BGM 通道增益（不含总音量）：滑块 50→1 倍，100→2 倍。</summary>
    public float BgmChannelGain => bgmVolumeSetting / ReferenceSliderValue;

    /// <summary>供场景内各预置音源（家具/脚步/灯/门/UI 按钮）按音效通道增益播放（总音量走 AudioListener）。</summary>
    public static float SfxChannelGainMultiplier => Instance != null ? Instance.SfxChannelGain : 1f;

    /// <summary>音效播放统一增益 = 音效通道滑块增益 × 该 clip 的统一分贝补偿。</summary>
    public static float SfxScaleForClip(AudioClip clip)
    {
        float channelGain = SfxChannelGainMultiplier;
        GameAudioManager manager = Instance;
        if (manager == null || manager.sfxLoudnessProfile == null || clip == null)
        {
            return channelGain;
        }

        return channelGain * manager.sfxLoudnessProfile.GetGain(clip.name);
    }

    public float MasterVolumeSetting => masterVolumeSetting;
    public float SfxVolumeSetting => sfxVolumeSetting;
    public float BgmVolumeSetting => bgmVolumeSetting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureInstance();
    }

    public static GameAudioManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameAudioManager existing = FindAnyObjectByType<GameAudioManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject(nameof(GameAudioManager));
        return managerObject.AddComponent<GameAudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveAudioSources();
        LoadClips();
        SubscribeEvents();
        UpdateSceneBgm(SceneManager.GetActiveScene().name);
        LoadSettings();
        ApplyVolumes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeEvents();
    }

    public void SetMasterVolume(float sliderValue)
    {
        masterVolumeSetting = Mathf.Clamp(sliderValue, 0f, 100f);
        SaveSettings();
        ApplyVolumes();
    }

    public void SetSfxVolume(float sliderValue)
    {
        sfxVolumeSetting = Mathf.Clamp(sliderValue, 0f, 100f);
        SaveSettings();
    }

    public void SetBgmVolume(float sliderValue)
    {
        bgmVolumeSetting = Mathf.Clamp(sliderValue, 0f, 100f);
        SaveSettings();
        ApplyVolumes();
    }

    public void PlayNormalBgm()
    {
        PlayBgm(normalBgm);
        StopAmbient();
    }

    public void EnterSpecialWorld()
    {
        PlayBgm(specialBgm);
        PlayAmbient(specialAmbient);
    }

    public void ReturnToNormalWorld()
    {
        PlayBgm(normalBgm);
        StopAmbient();
    }

    public void PlayTransitionToSpecial()
    {
        PlaySfx(transitionToSpecial);
    }

    public void PlayTransitionToNormal()
    {
        PlaySfx(transitionToNormal);
    }

    public void PlayPhotoFail()
    {
        PlaySfx(photoFail);
    }

    public void PlayStep()
    {
        PlaySfx(stepSound);
    }

    public void PlayMonsterRoar()
    {
        PlaySfx(monsterRoar);
    }

    public void UpdateSceneBgm(string sceneName)
    {
        if (sceneName == SceneNames.StartScene ||
            sceneName == SceneNames.TutorialLevel ||
            sceneName == SceneNames.FirstLevel ||
            sceneName == SceneNames.SecondLevel ||
            sceneName == SceneNames.ThirdLevel)
        {
            PlayNormalBgm();
            return;
        }

        StopBgm();
        StopAmbient();
    }

    private void PlayBgm(AudioClip clip)
    {
        if (clip == null || bgmSource == null)
        {
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    private void PlayAmbient(AudioClip clip)
    {
        if (clip == null || ambientSource == null)
        {
            return;
        }

        if (ambientSource.clip == clip && ambientSource.isPlaying)
        {
            return;
        }

        ambientSource.clip = clip;
        ambientSource.loop = true;
        ambientSource.volume = AmbientBaseVolume * BgmChannelGain;
        ambientSource.Play();
    }

    private void StopBgm()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    private void StopAmbient()
    {
        if (ambientSource != null)
        {
            ambientSource.Stop();
            ambientSource.clip = null;
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, SfxScaleForClip(clip));
    }

    private void ResolveAudioSources()
    {
        bgmSource = ResolveSource(BgmSourceName, true, BgmBaseVolume);
        ambientSource = ResolveSource(AmbientSourceName, true, AmbientBaseVolume);
        sfxSource = ResolveSource(SfxSourceName, false, 1f);

        if (bgmSource == null)
        {
            // 预放置音源缺失时的兜底（例如直接从某个关卡进入 Play）。
            bgmSource = CreateSource(BgmSourceName, true, BgmBaseVolume);
        }

        if (ambientSource == null)
        {
            ambientSource = CreateSource(AmbientSourceName, true, AmbientBaseVolume);
        }

        if (sfxSource == null)
        {
            sfxSource = CreateSource(SfxSourceName, false, 1f);
        }

        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        // 总音量包含音效与 BGM（以及 UI 音效）：用 AudioListener 统一放大。
        AudioListener.volume = MasterGain;

        if (bgmSource != null)
        {
            // AudioSource.volume 在 Unity 中上限为 1：BGM 100% 时最多到 1.0。
            bgmSource.volume = BgmBaseVolume * BgmChannelGain;
        }

        if (ambientSource != null)
        {
            ambientSource.volume = AmbientBaseVolume * BgmChannelGain;
        }
    }

    private void LoadSettings()
    {
        masterVolumeSetting = Mathf.Clamp(PlayerPrefs.GetFloat(MasterVolumePrefsKey, ReferenceSliderValue), 0f, 100f);
        sfxVolumeSetting = Mathf.Clamp(PlayerPrefs.GetFloat(SfxVolumePrefsKey, ReferenceSliderValue), 0f, 100f);
        bgmVolumeSetting = Mathf.Clamp(PlayerPrefs.GetFloat(BgmVolumePrefsKey, ReferenceSliderValue), 0f, 100f);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MasterVolumePrefsKey, masterVolumeSetting);
        PlayerPrefs.SetFloat(SfxVolumePrefsKey, sfxVolumeSetting);
        PlayerPrefs.SetFloat(BgmVolumePrefsKey, bgmVolumeSetting);
    }

    private static AudioSource ResolveSource(string sourceName, bool loop, float volume)
    {
        Transform sourceRoot = FindSceneObject(sourceName);
        if (sourceRoot == null)
        {
            return null;
        }

        AudioSource source = sourceRoot.GetComponent<AudioSource>();
        if (source == null)
        {
            return null;
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        return source;
    }

    private static Transform FindSceneObject(string name)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate != null && candidate.name == name &&
                candidate.scene.IsValid() && candidate.scene.isLoaded)
            {
                return candidate.transform;
            }
        }

        return null;
    }

    private AudioSource CreateSource(string sourceName, bool loop, float volume)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        return source;
    }

    private void LoadClips()
    {
        normalBgm = Resources.Load<AudioClip>(NormalBgmPath);
        specialBgm = Resources.Load<AudioClip>(SpecialBgmPath);
        specialAmbient = Resources.Load<AudioClip>(SpecialAmbientPath);
        transitionToSpecial = Resources.Load<AudioClip>(TransitionToSpecialPath);
        transitionToNormal = Resources.Load<AudioClip>(TransitionToNormalPath);
        photoFail = Resources.Load<AudioClip>(PhotoFailPath);
        stepSound = Resources.Load<AudioClip>(StepSoundPath);
        monsterRoar = Resources.Load<AudioClip>(MonsterRoarPath);
        sfxLoudnessProfile = Resources.Load<SfxLoudnessProfile>("UI/sound/SfxLoudnessProfile");
    }

    private void SubscribeEvents()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        WorldSwapManager2D.SpecialWorldEntered -= HandleSpecialWorldEntered;
        WorldSwapManager2D.SpecialWorldEntered += HandleSpecialWorldEntered;
        WorldSwapManager2D.ReturnedToNormalWorld -= HandleReturnedToNormalWorld;
        WorldSwapManager2D.ReturnedToNormalWorld += HandleReturnedToNormalWorld;
    }

    private void UnsubscribeEvents()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        WorldSwapManager2D.SpecialWorldEntered -= HandleSpecialWorldEntered;
        WorldSwapManager2D.ReturnedToNormalWorld -= HandleReturnedToNormalWorld;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        UpdateSceneBgm(next.name);
    }

    private void HandleSpecialWorldEntered()
    {
        EnterSpecialWorld();
    }

    private void HandleReturnedToNormalWorld()
    {
        ReturnToNormalWorld();
    }
}
