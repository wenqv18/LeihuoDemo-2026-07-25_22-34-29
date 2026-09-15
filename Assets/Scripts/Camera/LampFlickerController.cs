using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 非规律灯管闪烁。只用于 SpecialWorld 的 Lamp 灯光。
/// 每次开灯/关灯都会触发事件，并提供可选音效播放接口。
/// </summary>
[DisallowMultipleComponent]
public sealed class LampFlickerController : MonoBehaviour
{
    /// <summary>全局音效接口：任意灯管每次开/关灯时触发（参数：控制器, 是否开灯）。</summary>
    public static event Action<LampFlickerController, bool> LampLightStateChanged;

    /// <summary>实例音效接口：本灯管每次开/关灯时触发（参数：是否开灯）。</summary>
    public event Action<bool> StateChanged;

    [SerializeField] private Light2D targetLight;

    [Header("Flicker Timing")]
    [SerializeField, Min(0f)] private float minOnDuration = 0.15f;
    [SerializeField, Min(0f)] private float maxOnDuration = 2.5f;
    [SerializeField, Min(0f)] private float minOffDuration = 0.02f;
    [SerializeField, Min(0f)] private float maxOffDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float offIntensityRatio = 0f;

    [Header("Irregular Patterns")]
    [SerializeField, Range(0f, 1f)] private float longOffChance = 0.08f;
    [SerializeField, Min(0f)] private float minLongOffDuration = 0.6f;
    [SerializeField, Min(0f)] private float maxLongOffDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float stutterChance = 0.25f;
    [SerializeField, Min(1)] private int stutterMinBursts = 2;
    [SerializeField, Min(1)] private int stutterMaxBursts = 5;

    [Header("Audio Interface")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip turnOnSound;
    [SerializeField] private AudioClip turnOffSound;
    [SerializeField] private bool playSounds = true;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

    private float originalIntensity = 1f;
    private float nextChangeTime;
    private bool lightOn = true;
    private int stutterRemaining;

    public Light2D TargetLight => targetLight;
    public bool IsLightOn => lightOn && targetLight != null && targetLight.enabled;

    private void Awake()
    {
        ResolveLight();
        if (targetLight != null)
        {
            originalIntensity = targetLight.intensity;
        }

        if (turnOnSound == null)
        {
            turnOnSound = Resources.Load<AudioClip>("UI/sound/灯故障声");
        }

        if (turnOffSound == null)
        {
            turnOffSound = turnOnSound;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        ResolveLight();
        lightOn = true;
        stutterRemaining = 0;
        nextChangeTime = Time.time + UnityEngine.Random.Range(0.1f, 1.2f);
        ApplyIntensity(lightOn);
    }

    private void OnDisable()
    {
        RestoreIntensity();
    }

    private void Update()
    {
        if (targetLight == null || !targetLight.enabled || !targetLight.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Time.time < nextChangeTime)
        {
            return;
        }

        if (stutterRemaining > 0)
        {
            stutterRemaining--;
            SwitchState(0.03f, 0.12f);
            return;
        }

        if (lightOn)
        {
            float roll = UnityEngine.Random.value;
            if (roll < stutterChance)
            {
                stutterRemaining = UnityEngine.Random.Range(stutterMinBursts, stutterMaxBursts + 1);
                SwitchState(0.03f, 0.12f);
            }
            else if (roll < stutterChance + longOffChance)
            {
                SwitchState(minLongOffDuration, maxLongOffDuration);
            }
            else
            {
                SwitchState(minOffDuration, maxOffDuration);
            }
        }
        else
        {
            SwitchState(minOnDuration, maxOnDuration);
        }
    }

    private void SwitchState(float minDuration, float maxDuration)
    {
        lightOn = !lightOn;
        nextChangeTime = Time.time + UnityEngine.Random.Range(minDuration, Mathf.Max(minDuration, maxDuration));
        ApplyIntensity(lightOn);

        StateChanged?.Invoke(lightOn);
        LampLightStateChanged?.Invoke(this, lightOn);
        if (playSounds)
        {
            if (lightOn)
            {
                PlayOnSound();
            }
            else
            {
                PlayOffSound();
            }
        }
    }

    /// <summary>手动触发一次开灯音效。</summary>
    public void PlayOnSound()
    {
        PlayClip(turnOnSound);
    }

    /// <summary>手动触发一次关灯音效。</summary>
    public void PlayOffSound()
    {
        PlayClip(turnOffSound);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = soundVolume;
        audioSource.PlayOneShot(clip, GameAudioManager.SfxScaleForClip(clip));
    }

    private void ApplyIntensity(bool on)
    {
        if (targetLight == null)
        {
            return;
        }

        targetLight.intensity = on ? originalIntensity : originalIntensity * offIntensityRatio;
    }

    private void RestoreIntensity()
    {
        if (targetLight != null)
        {
            targetLight.intensity = originalIntensity;
        }
    }

    private void ResolveLight()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light2D>();
        }
    }
}
