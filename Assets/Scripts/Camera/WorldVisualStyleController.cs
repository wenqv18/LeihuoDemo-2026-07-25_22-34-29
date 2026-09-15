using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class WorldVisualStyleController : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Volume styleVolume;
    [SerializeField] private VolumeProfile normalWorldProfile;
    [SerializeField] private VolumeProfile specialWorldProfile;

    [Header("Signal Overlay")]
    [SerializeField] private SignalInterferenceOverlay signalOverlay;
    [SerializeField] private bool enableSignalOverlay = true;

    [Header("Fallback")]
    [SerializeField] private WorldKind2D editModePreviewWorld = WorldKind2D.Special;
    [SerializeField] private WorldKind2D runtimeFallbackWorld = WorldKind2D.Normal;

    [Header("Normal World Signal")]
    [SerializeField] private float normalNoiseAlpha = 0.018f;
    [SerializeField] private float normalScanlineAlpha = 0.025f;
    [SerializeField] private float normalWaveAlpha = 0.008f;
    [SerializeField] private float normalJitterPixels = 0.18f;
    [SerializeField] private float normalFlicker = 0.035f;

    [Header("Special World Signal")]
    [SerializeField] private float specialNoiseAlpha = 0.075f;
    [SerializeField] private float specialScanlineAlpha = 0.065f;
    [SerializeField] private float specialWaveAlpha = 0.038f;
    [SerializeField] private float specialJitterPixels = 1.1f;
    [SerializeField] private float specialFlicker = 0.16f;
    [SerializeField] private float specialSignalAlphaScale = 0.55f;

    [Header("Transition")]
    [SerializeField] private bool useCrossfade = true;
    [SerializeField] private Volume normalStyleVolume;
    [SerializeField] private Volume specialStyleVolume;

    private WorldKind2D currentWorld = (WorldKind2D)(-1);
    private bool isCrossfading;
    private WorldKind2D crossfadeTarget;
    private float crossfadeDuration;
    private float crossfadeElapsed;
    private float crossfadeStartNoise;
    private float crossfadeStartScanline;
    private float crossfadeStartWave;
    private float crossfadeStartJitter;
    private float crossfadeStartFlicker;
    private float currentNoiseAlpha;
    private float currentScanlineAlpha;
    private float currentWaveAlpha;
    private float currentJitterPixels;
    private float currentFlicker;

    private void Awake()
    {
        ResolveReferences();
        EnsureVolumes();
        EnsureCameraPostProcessing();
        ApplyCurrentWorld(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureVolumes();
        EnsureCameraPostProcessing();
        ApplyCurrentWorld(true);
    }

    private void Update()
    {
        if (isCrossfading)
        {
            crossfadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(crossfadeElapsed / crossfadeDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            bool targetNormal = crossfadeTarget == WorldKind2D.Normal;
            if (normalStyleVolume != null)
            {
                normalStyleVolume.weight = targetNormal ? smooth : 1f - smooth;
            }

            if (specialStyleVolume != null)
            {
                specialStyleVolume.weight = targetNormal ? 1f - smooth : smooth;
            }

            ApplySignalLerp(smooth, targetNormal);
            if (t >= 1f)
            {
                isCrossfading = false;
                currentWorld = crossfadeTarget;
                ApplyVolumeState(crossfadeTarget);
                ApplySignalProfile(crossfadeTarget);
            }

            return;
        }

        ApplyCurrentWorld(false);
    }

    public void RefreshNow()
    {
        ResolveReferences();
        EnsureVolumes();
        EnsureCameraPostProcessing();
        ApplyCurrentWorld(true);
    }

    public static void RefreshAll()
    {
        WorldVisualStyleController[] controllers = FindObjectsByType<WorldVisualStyleController>(
            FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].RefreshNow();
            }
        }
    }

    public static void ApplyEndingSignalPreset(EndingKind ending)
    {
        SignalInterferenceOverlay[] overlays = FindObjectsByType<SignalInterferenceOverlay>(
            FindObjectsInactive.Include);
        for (int i = 0; i < overlays.Length; i++)
        {
            if (overlays[i] != null)
            {
                overlays[i].gameObject.SetActive(true);
                overlays[i].ConfigureEndingSignal(ending);
            }
        }
    }

    private void ResolveReferences()
    {
        if (styleVolume == null)
        {
            styleVolume = FindAnyObjectByType<Volume>(FindObjectsInactive.Include);
        }

        if (signalOverlay == null)
        {
            signalOverlay = FindAnyObjectByType<SignalInterferenceOverlay>(FindObjectsInactive.Include);
        }
    }

    private void ApplyCurrentWorld(bool force)
    {
        if (isCrossfading)
        {
            return;
        }

        WorldKind2D world = GetCurrentWorld();
        if (!force && world == currentWorld)
        {
            return;
        }

        currentWorld = world;
        ApplyVolumeState(world);
        ApplySignalProfile(world);
    }

    private WorldKind2D GetCurrentWorld()
    {
        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        if (manager != null)
        {
            return manager.GetPrimaryWorld();
        }

        WorldSwapManager2D sceneManager = FindAnyObjectByType<WorldSwapManager2D>(FindObjectsInactive.Include);
        if (sceneManager != null)
        {
            return sceneManager.GetPrimaryWorld();
        }

        return Application.isPlaying ? runtimeFallbackWorld : editModePreviewWorld;
    }

    /// <summary>
    /// 开始世界风格过渡：两个 Volume 的权重交叉淡化 + 信号层参数渐变。
    /// 由 WorldSwapManager2D 在“返回 NormalWorld”时调用。
    /// </summary>
    public void BeginCrossfade(WorldKind2D target, float duration)
    {
        if (!useCrossfade)
        {
            ApplyCurrentWorld(true);
            return;
        }

        EnsureVolumes();
        crossfadeTarget = target;
        crossfadeDuration = Mathf.Max(0.01f, duration);
        crossfadeElapsed = 0f;
        crossfadeStartNoise = currentNoiseAlpha;
        crossfadeStartScanline = currentScanlineAlpha;
        crossfadeStartWave = currentWaveAlpha;
        crossfadeStartJitter = currentJitterPixels;
        crossfadeStartFlicker = currentFlicker;
        isCrossfading = true;
    }

    private void EnsureVolumes()
    {
        if (normalStyleVolume == null)
        {
            normalStyleVolume = styleVolume;
        }

        if (normalStyleVolume == null)
        {
            normalStyleVolume = FindAnyObjectByType<Volume>(FindObjectsInactive.Include);
        }

        if (normalStyleVolume == null && normalWorldProfile != null)
        {
            GameObject normalObject = new GameObject("__WorldVisualStyleVolume_Normal");
            normalObject.transform.SetParent(transform, false);
            normalStyleVolume = normalObject.AddComponent<Volume>();
        }

        if (normalStyleVolume != null)
        {
            normalStyleVolume.isGlobal = true;
            normalStyleVolume.priority = 100f;
            if (normalWorldProfile != null)
            {
                normalStyleVolume.sharedProfile = normalWorldProfile;
            }
        }

        if (specialStyleVolume == null)
        {
            GameObject specialObject = new GameObject("__WorldVisualStyleVolume_Special");
            specialObject.transform.SetParent(transform, false);
            specialStyleVolume = specialObject.AddComponent<Volume>();
            specialStyleVolume.weight = 0f;
        }

        if (specialStyleVolume != null)
        {
            specialStyleVolume.isGlobal = true;
            specialStyleVolume.priority = 100f;
            if (specialWorldProfile != null)
            {
                specialStyleVolume.sharedProfile = specialWorldProfile;
            }
        }
    }

    private void ApplyVolumeState(WorldKind2D world)
    {
        EnsureVolumes();
        bool normal = world == WorldKind2D.Normal;
        if (normalStyleVolume != null)
        {
            normalStyleVolume.weight = normal ? 1f : 0f;
        }

        if (specialStyleVolume != null)
        {
            specialStyleVolume.weight = normal ? 0f : 1f;
        }
    }

    private void ApplySignalProfile(WorldKind2D world)
    {
        if (signalOverlay == null)
        {
            return;
        }

        signalOverlay.gameObject.SetActive(enableSignalOverlay);
        if (!enableSignalOverlay)
        {
            return;
        }

        if (world == WorldKind2D.Special)
        {
            currentNoiseAlpha = specialNoiseAlpha * specialSignalAlphaScale;
            currentScanlineAlpha = specialScanlineAlpha * specialSignalAlphaScale;
            currentWaveAlpha = specialWaveAlpha * specialSignalAlphaScale;
            currentJitterPixels = specialJitterPixels;
            currentFlicker = specialFlicker;
            signalOverlay.Configure(
                currentNoiseAlpha,
                currentScanlineAlpha,
                currentWaveAlpha,
                currentJitterPixels,
                currentFlicker);
            return;
        }

        currentNoiseAlpha = normalNoiseAlpha;
        currentScanlineAlpha = normalScanlineAlpha;
        currentWaveAlpha = normalWaveAlpha;
        currentJitterPixels = normalJitterPixels;
        currentFlicker = normalFlicker;
        signalOverlay.Configure(
            currentNoiseAlpha,
            currentScanlineAlpha,
            currentWaveAlpha,
            currentJitterPixels,
            currentFlicker);
    }

    private void ApplySignalLerp(float t, bool targetNormal)
    {
        if (signalOverlay == null)
        {
            return;
        }

        float targetNoise = targetNormal ? normalNoiseAlpha : specialNoiseAlpha * specialSignalAlphaScale;
        float targetScanline = targetNormal ? normalScanlineAlpha : specialScanlineAlpha * specialSignalAlphaScale;
        float targetWave = targetNormal ? normalWaveAlpha : specialWaveAlpha * specialSignalAlphaScale;
        float targetJitter = targetNormal ? normalJitterPixels : specialJitterPixels;
        float targetFlicker = targetNormal ? normalFlicker : specialFlicker;

        currentNoiseAlpha = Mathf.Lerp(crossfadeStartNoise, targetNoise, t);
        currentScanlineAlpha = Mathf.Lerp(crossfadeStartScanline, targetScanline, t);
        currentWaveAlpha = Mathf.Lerp(crossfadeStartWave, targetWave, t);
        currentJitterPixels = Mathf.Lerp(crossfadeStartJitter, targetJitter, t);
        currentFlicker = Mathf.Lerp(crossfadeStartFlicker, targetFlicker, t);
        signalOverlay.Configure(
            currentNoiseAlpha,
            currentScanlineAlpha,
            currentWaveAlpha,
            currentJitterPixels,
            currentFlicker);
    }

    private static void EnsureCameraPostProcessing()
    {
        Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
        }
    }
}
