using System;
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
