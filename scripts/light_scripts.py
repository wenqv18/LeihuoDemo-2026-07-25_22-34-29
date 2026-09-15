"""Deploy lamp light scripts (world visibility + flicker) through Unity MCP."""

import sys
import time

from unity_mcp import UnityMcp


WORLD_LAMP_LIGHT_CONTROLLER = r"""using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class WorldLampLightController : MonoBehaviour
{
    private const string NormalWorldRootName = "NormalWorld";
    private const string SpecialWorldRootName = "SpecialWorld";

    private readonly List<Light2D> normalLampLights = new List<Light2D>();
    private readonly List<Light2D> specialLampLights = new List<Light2D>();
    private bool collected;

    private void OnEnable()
    {
        WorldSwapManager2D.PrimaryWorldChanged += HandlePrimaryWorldChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Refresh();
        ApplyWorldState();
    }

    private void OnDisable()
    {
        WorldSwapManager2D.PrimaryWorldChanged -= HandlePrimaryWorldChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void Refresh()
    {
        normalLampLights.Clear();
        specialLampLights.Clear();
        Transform normalRoot = FindLoadedSceneRoot(NormalWorldRootName);
        Transform specialRoot = FindLoadedSceneRoot(SpecialWorldRootName);
        CollectLampLights(normalRoot, normalLampLights);
        CollectLampLights(specialRoot, specialLampLights);
        collected = true;
    }

    public void ApplyWorldState()
    {
        if (!collected)
        {
            Refresh();
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        bool normalIsPrimary = manager == null || manager.GetPrimaryWorld() == WorldKind2D.Normal;
        SetLightsEnabled(normalLampLights, normalIsPrimary);
        SetLightsEnabled(specialLampLights, !normalIsPrimary);
    }

    private void HandlePrimaryWorldChanged(WorldKind2D world)
    {
        ApplyWorldState();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Refresh();
        ApplyWorldState();
    }

    private static void CollectLampLights(Transform worldRoot, List<Light2D> results)
    {
        if (worldRoot == null)
        {
            return;
        }

        Light2D[] lights = worldRoot.GetComponentsInChildren<Light2D>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light2D light = lights[i];
            if (light != null && HasLampAncestor(light.transform))
            {
                results.Add(light);
            }
        }
    }

    private static bool HasLampAncestor(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void SetLightsEnabled(List<Light2D> lights, bool enabled)
    {
        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i] != null)
            {
                lights[i].enabled = enabled;
            }
        }
    }

    private static Transform FindLoadedSceneRoot(string rootName)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (roots[rootIndex].name == rootName)
                {
                    return roots[rootIndex].transform;
                }
            }
        }

        return null;
    }
}
"""


LAMP_FLICKER_CONTROLLER = r"""using System;
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

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = soundVolume;
        audioSource.PlayOneShot(clip);
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
"""


def main():
    client = UnityMcp()
    for path, contents in [
        ("Assets/Scripts/Camera/WorldLampLightController.cs", WORLD_LAMP_LIGHT_CONTROLLER),
        ("Assets/Scripts/Camera/LampFlickerController.cs", LAMP_FLICKER_CONTROLLER),
    ]:
        print("Creating", path, "...")
        result = client.call_json(
            "create_script",
            {"path": path, "contents": contents},
        )
        print(result)


if __name__ == "__main__":
    main()
