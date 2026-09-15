using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SanBrainUIController : MonoBehaviour
{
    private const string SanObjectName = "San";
    private const string GameUIRootName = "GameUI";
    private const string BrainNamePrefix = "Brain";
    private const string SanNumberObjectName = "SanNumber";
    private const string SliderObjectName = "Slider";
    private const int MaxSanValue = 100;

    private static SanBrainUIController instance;
    private static int pendingRemaining = -1;
    private static int pendingMax = 3;

    [SerializeField] private List<GameObject> brainObjects = new List<GameObject>();
    [SerializeField] private Text sanNumberText;
    [SerializeField] private Slider sanSlider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureControllerInLoadedScenes();
    }

    public static void SetBrainState(int remaining, int max)
    {
        pendingMax = Mathf.Max(1, max);
        pendingRemaining = Mathf.Clamp(remaining, 0, pendingMax);
        EnsureControllerInLoadedScenes();

        if (instance != null)
        {
            instance.ApplyBrainState(pendingRemaining, pendingMax);
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureControllerInLoadedScenes();
    }

    private static void EnsureControllerInLoadedScenes()
    {
        SanBrainUIController existing = UnityEngine.Object.FindAnyObjectByType<SanBrainUIController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            instance.ResolveUIReferencesIfNeeded();
            instance.ApplyPendingOrFullState();
            return;
        }

        GameObject sanObject = FindSceneGameObject(SanObjectName, GameUIRootName);
        if (sanObject == null)
        {
            return;
        }

        instance = sanObject.AddComponent<SanBrainUIController>();
    }

    private static GameObject FindSceneGameObject(string objectName, string preferredRootName = null)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject fallback = null;
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject sceneObject = allObjects[i];
            if (sceneObject == null || sceneObject.name != objectName)
            {
                continue;
            }

            Scene scene = sceneObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (string.IsNullOrEmpty(preferredRootName) ||
                    IsUnderRootNamed(sceneObject.transform, preferredRootName))
                {
                    return sceneObject;
                }

                if (fallback == null)
                {
                    fallback = sceneObject;
                }
            }
        }

        return fallback;
    }

    private void Awake()
    {
        instance = this;
        ResolveUIReferencesIfNeeded();
        ApplyPendingOrFullState();
    }

    private void OnEnable()
    {
        instance = this;
        ResolveUIReferencesIfNeeded();
        ApplyPendingOrFullState();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void ResolveUIReferencesIfNeeded()
    {
        Transform sanRoot = name == SanObjectName && IsUnderRootNamed(transform, GameUIRootName)
            ? transform
            : FindSceneGameObject(SanObjectName, GameUIRootName)?.transform;
        ResolveBrainsIfNeeded(sanRoot);
        ResolveSanValueIfNeeded(sanRoot);
    }

    private static bool IsUnderRootNamed(Transform transform, string rootName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == rootName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ResolveBrainsIfNeeded(Transform sanRoot)
    {
        for (int i = brainObjects.Count - 1; i >= 0; i--)
        {
            if (brainObjects[i] == null)
            {
                brainObjects.RemoveAt(i);
            }
        }

        if (brainObjects.Count > 0)
        {
            return;
        }

        if (sanRoot == null)
        {
            return;
        }

        Transform[] children = sanRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == sanRoot || !child.name.StartsWith(BrainNamePrefix))
            {
                continue;
            }

            brainObjects.Add(child.gameObject);
        }

        brainObjects.Sort(CompareBySiblingIndex);
    }

    private void ResolveSanValueIfNeeded(Transform sanRoot)
    {
        if (sanRoot == null)
        {
            return;
        }

        if (sanNumberText == null)
        {
            Transform sanNumber = FindChildByName(sanRoot, SanNumberObjectName);
            sanNumberText = sanNumber != null ? sanNumber.GetComponent<Text>() : null;
        }

        if (sanSlider == null)
        {
            Transform slider = FindChildByName(sanRoot, SliderObjectName);
            sanSlider = slider != null ? slider.GetComponent<Slider>() : null;
        }
    }

    private void ApplyPendingOrFullState()
    {
        int max = pendingMax;
        int remaining = pendingRemaining >= 0 ? pendingRemaining : max;
        ApplyBrainState(remaining, max);
    }

    private void ApplyBrainState(int remaining, int max)
    {
        ResolveUIReferencesIfNeeded();
        int clampedMax = Mathf.Max(1, max);
        int clampedRemaining = Mathf.Clamp(remaining, 0, clampedMax);

        for (int i = 0; i < brainObjects.Count; i++)
        {
            GameObject brainObject = brainObjects[i];
            if (brainObject != null)
            {
                brainObject.SetActive(i < clampedRemaining);
            }
        }

        float normalizedSan = (float)clampedRemaining / clampedMax;
        int sanValue = Mathf.RoundToInt(normalizedSan * MaxSanValue);
        if (sanNumberText != null)
        {
            sanNumberText.text = sanValue.ToString();
        }

        if (sanSlider != null)
        {
            sanSlider.SetValueWithoutNotify(Mathf.Clamp01(normalizedSan));
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static int CompareBySiblingIndex(GameObject left, GameObject right)
    {
        if (left == null || right == null)
        {
            return left == null ? 1 : -1;
        }

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }
}
