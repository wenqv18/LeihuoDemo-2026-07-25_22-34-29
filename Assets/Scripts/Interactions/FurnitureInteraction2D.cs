using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public sealed class FurnitureInteraction2D : MonoBehaviour
{
    private const int OutlineSortingOrderOffset = 1;
    public static event Action<FurnitureInteraction2D> InteractionPlayed;
    private static readonly List<FurnitureInteraction2D> ActiveInteractions = new List<FurnitureInteraction2D>();

    private enum HoverVisualMode
    {
        SelectedObject,
        MaterialOutline,
        SelectedObjectAndMaterialOutline
    }

    [SerializeField] private string selectedChildName = "Selected";
    [SerializeField] private GameObject selectedObject;
    [SerializeField] private HoverVisualMode hoverVisualMode = HoverVisualMode.MaterialOutline;
    [Header("Sprite Outline")]
    [SerializeField] private SpriteRenderer[] outlineRenderers;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineScreenPixels = 2f;
    [Header("Dialogue")]
    [SerializeField] private TalkPanelButtonHandler talkPanel;
    [TextArea]
    [SerializeField] private string[] dialogueLines = { "\u8FD9\u662F\u4E00\u4E2A\u684C\u5B50" };
    [FormerlySerializedAs("clickDialogueLine")]
    [HideInInspector]
    [SerializeField] private string legacyClickDialogueLine;
    [Header("Click Feedback")]
    [SerializeField] private float clickPulseScaleMultiplier = 1.0333333f;
    [SerializeField] private float clickPulseDuration = 0.08f;
    [SerializeField] private bool requireWorldInteractionAllowed = true;
    [SerializeField] private bool logDiagnostics;

    [Header("Interaction Sound")]
    [SerializeField] private AudioSource interactionAudioSource;
    [SerializeField] private AudioClip genericInteractionSound;
    [SerializeField] private AudioClip paperInteractionSound;
    [SerializeField] private AudioClip waterInteractionSound;
    [SerializeField] private bool playInteractionSound = true;

    private readonly List<SpriteRenderer> rendererBuffer = new List<SpriteRenderer>();
    private readonly List<Vector2> physicsShapeBuffer = new List<Vector2>();

    private Collider2D hoverCollider;
    private Vector3 initialScale;
    private Tween clickPulseTween;
    private SpriteRenderer[] activeOutlineRenderers;
    private LineRenderer[] spriteOutlineRenderers;
    private Material runtimeOutlineMaterial;
    private static int lastDialogueStartFrame = -1;

    public IReadOnlyList<string> DialogueLines => dialogueLines ?? Array.Empty<string>();

    public void SetDialogueLines(IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            dialogueLines = Array.Empty<string>();
            return;
        }

        dialogueLines = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            dialogueLines[i] = lines[i] ?? string.Empty;
        }
    }

    public bool TryPlayFromExternal()
    {
        if (CameraFocusModeController.IsCameraModeActive || !IsInteractionAllowed())
        {
            return false;
        }

        PlayDialogue();
        return true;
    }

    private void Awake()
    {
        hoverCollider = GetComponent<Collider2D>();
        initialScale = transform.localScale;
        EnsureInteractionAudio();
        ResolveSelectedObject();
        SetSelectedVisible(false);
    }

    private void OnEnable()
    {
        if (!ActiveInteractions.Contains(this))
        {
            ActiveInteractions.Add(this);
        }

        ResolveSelectedObject();
        SetSelectedVisible(false);
    }

    private void OnDisable()
    {
        ActiveInteractions.Remove(this);
        StopClickPulse();
        SetSelectedVisible(false);
    }

    private void OnDestroy()
    {
        StopClickPulse();
        DestroyRuntimeOutlineMaterial();
    }

    private void OnValidate()
    {
        outlineScreenPixels = Mathf.Max(0.5f, outlineScreenPixels);
    }

    private void Update()
    {
        if (CameraFocusModeController.IsCameraModeActive)
        {
            SetSelectedVisible(false);
            return;
        }

        Vector2 mouseWorldPosition;
        bool hasMouseWorldPosition = TryGetMouseWorldPosition(out mouseWorldPosition);
        bool pointerOverUi = IsPointerOverUi();
        bool pointerInsideVisionArea = IsPointerInsideVisionArea(hasMouseWorldPosition, mouseWorldPosition);
        bool mouseOverCollider = hasMouseWorldPosition && IsMouseOverCollider(mouseWorldPosition);
        bool interactionAllowed = IsInteractionAllowed();
        bool pointerBlockedByUi = pointerOverUi || pointerInsideVisionArea;
        SetSelectedVisible(!pointerBlockedByUi && mouseOverCollider && interactionAllowed);

        if (Input.GetMouseButtonDown(0))
        {
            if (pointerBlockedByUi)
            {
                return;
            }

            LogClick(mouseOverCollider, interactionAllowed);

            if (mouseOverCollider && interactionAllowed)
            {
                PlayDialogue();
            }
        }
    }

    private void PlayDialogue()
    {
        if (lastDialogueStartFrame == Time.frameCount)
        {
            return;
        }

        lastDialogueStartFrame = Time.frameCount;
        PlayInteractionSound();
        PlayClickPulse();
        ResolveTalkPanel();

        if (talkPanel != null)
        {
            string displayName = FurnitureDisplayName.Get(ResolveDisplayNameSource());
            talkPanel.Play(this, displayName, GetDialogueLines());
        }

        InteractionPlayed?.Invoke(this);
    }

    private void PlayInteractionSound()
    {
        if (!playInteractionSound)
        {
            return;
        }

        AudioClip clip = ResolveInteractionSound();
        if (clip == null)
        {
            return;
        }

        EnsureInteractionAudio();
        if (interactionAudioSource != null)
        {
            interactionAudioSource.PlayOneShot(clip, GameAudioManager.SfxScaleForClip(clip));
        }
    }

    private string ResolveDisplayNameSource()
    {
        WorldPairId pair = GetComponentInParent<WorldPairId>();
        if (pair != null && !string.IsNullOrWhiteSpace(pair.PairId))
        {
            return pair.PairId;
        }

        return transform.parent != null ? transform.parent.name : gameObject.name;
    }

    private AudioClip ResolveInteractionSound()
    {
        if (IsWaterFurniture() && waterInteractionSound != null)
        {
            return waterInteractionSound;
        }

        if (IsPaperFurniture() && paperInteractionSound != null)
        {
            return paperInteractionSound;
        }

        return genericInteractionSound;
    }

    private bool IsPaperFurniture()
    {
        return NameContains("memo", "book", "bookshelf");
    }

    private bool IsWaterFurniture()
    {
        return NameContains("water");
    }

    private bool NameContains(params string[] keywords)
    {
        string objectName = gameObject.name;
        WorldPairId pair = GetComponentInParent<WorldPairId>();
        string pairId = pair != null ? pair.PairId : string.Empty;
        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (objectName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                pairId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureInteractionAudio()
    {
        if (interactionAudioSource == null)
        {
            interactionAudioSource = GetComponent<AudioSource>();
        }

        if (genericInteractionSound == null)
        {
            genericInteractionSound = Resources.Load<AudioClip>("UI/sound/开门声");
        }

        if (paperInteractionSound == null)
        {
            paperInteractionSound = Resources.Load<AudioClip>("UI/sound/翻书声");
        }

        if (waterInteractionSound == null)
        {
            waterInteractionSound = Resources.Load<AudioClip>("UI/sound/水声");
        }
    }

    private void LogClick(bool mouseOverCollider, bool interactionAllowed)
    {
        if (logDiagnostics)
        {
            Debug.Log($"[FurnitureInteraction2D] Click: overCollider={mouseOverCollider}, interactionAllowed={interactionAllowed}, object={name}");
        }
    }

    private string[] GetDialogueLines()
    {
        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            return dialogueLines;
        }

        if (!string.IsNullOrWhiteSpace(legacyClickDialogueLine))
        {
            return new[] { legacyClickDialogueLine };
        }

        return new[] { string.Empty };
    }

    private void ResolveSelectedObject()
    {
        if (selectedObject != null)
        {
            return;
        }

        Transform selected = transform.Find(selectedChildName);
        if (selected == null)
        {
            selected = FindChildContaining(transform, selectedChildName);
        }

        if (selected != null)
        {
            selectedObject = selected.gameObject;
        }
    }

    private void ResolveTalkPanel()
    {
        TalkPanelButtonHandler preferredPanel = TalkPanelButtonHandler.FindPreferredPanel();
        if (preferredPanel != null)
        {
            talkPanel = preferredPanel;
        }
    }

    private bool IsInteractionAllowed()
    {
        if (!requireWorldInteractionAllowed)
        {
            return true;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        return manager == null || manager.IsInteractionAllowedForPlayer(transform);
    }

    /// <summary>
    /// 判断鼠标位置是否落在任意一个“可交互且当前世界允许”的家具上。
    /// 对话推进逻辑用它实现“交互家具优先于点击任意处推进文本”。
    /// </summary>
    public static bool IsMouseOverAnyInteraction(Vector2 mouseWorldPosition)
    {
        for (int i = 0; i < ActiveInteractions.Count; i++)
        {
            FurnitureInteraction2D interaction = ActiveInteractions[i];
            if (interaction == null || !interaction.IsMouseOverCollider(mouseWorldPosition))
            {
                continue;
            }

            if (interaction.IsInteractionAllowed())
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetMouseWorldPosition(out Vector2 mouseWorldPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mouseWorldPosition = default;
            return false;
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPosition = new Vector2(mouseWorld.x, mouseWorld.y);
        return true;
    }

    private bool IsMouseOverCollider(Vector2 mouseWorldPosition)
    {
        if (hoverCollider == null)
        {
            hoverCollider = GetComponent<Collider2D>();
        }

        return hoverCollider != null && hoverCollider.OverlapPoint(mouseWorldPosition);
    }

    private static bool IsPointerInsideVisionArea(bool hasMouseWorldPosition, Vector2 mouseWorldPosition)
    {
        if (!hasMouseWorldPosition)
        {
            return false;
        }

        WorldSwapManager2D manager = WorldSwapManager2D.Instance;
        return manager != null && manager.IsWorldPointInsideActiveArea(mouseWorldPosition);
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    private void SetSelectedVisible(bool visible)
    {
        bool useSelectedObject =
            hoverVisualMode == HoverVisualMode.SelectedObject ||
            hoverVisualMode == HoverVisualMode.SelectedObjectAndMaterialOutline;
        bool useSpriteOutline =
            hoverVisualMode == HoverVisualMode.MaterialOutline ||
            hoverVisualMode == HoverVisualMode.SelectedObjectAndMaterialOutline;

        ResolveSelectedObject();
        bool selectedVisible = visible && useSelectedObject;
        if (selectedObject != null && selectedObject.activeSelf != selectedVisible)
        {
            selectedObject.SetActive(selectedVisible);
        }

        SetSpriteOutlineVisible(visible && useSpriteOutline);
    }

    private void SetSpriteOutlineVisible(bool visible)
    {
        EnsureSpriteOutlineRenderers();
        if (spriteOutlineRenderers == null)
        {
            return;
        }

        if (visible)
        {
            UpdateSpriteOutlines();
        }

        for (int i = 0; i < spriteOutlineRenderers.Length; i++)
        {
            LineRenderer lineRenderer = spriteOutlineRenderers[i];
            if (lineRenderer != null && lineRenderer.gameObject.activeSelf != visible)
            {
                lineRenderer.gameObject.SetActive(visible);
            }
        }
    }

    private void EnsureSpriteOutlineRenderers()
    {
        ResolveOutlineRenderers();
        if (activeOutlineRenderers == null || activeOutlineRenderers.Length == 0)
        {
            return;
        }

        if (spriteOutlineRenderers != null && spriteOutlineRenderers.Length == activeOutlineRenderers.Length)
        {
            return;
        }

        spriteOutlineRenderers = new LineRenderer[activeOutlineRenderers.Length];
        Material material = ResolveOutlineMaterial();
        for (int i = 0; i < activeOutlineRenderers.Length; i++)
        {
            SpriteRenderer sourceRenderer = activeOutlineRenderers[i];
            if (sourceRenderer == null)
            {
                continue;
            }

            Transform existing = sourceRenderer.transform.Find("__HoverSpriteOutline");
            GameObject outlineObject = existing != null ? existing.gameObject : new GameObject("__HoverSpriteOutline");
            outlineObject.transform.SetParent(sourceRenderer.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            LineRenderer lineRenderer = outlineObject.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = outlineObject.AddComponent<LineRenderer>();
            }

            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.numCapVertices = 2;
            lineRenderer.material = material;
            lineRenderer.startColor = outlineColor;
            lineRenderer.endColor = outlineColor;
            lineRenderer.gameObject.SetActive(false);
            spriteOutlineRenderers[i] = lineRenderer;
        }
    }

    private Material ResolveOutlineMaterial()
    {
        if (outlineMaterial != null)
        {
            return outlineMaterial;
        }

        if (runtimeOutlineMaterial != null)
        {
            return runtimeOutlineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            if (logDiagnostics)
            {
                Debug.LogWarning("[FurnitureInteraction2D] Outline material shader not found.", this);
            }

            return null;
        }

        runtimeOutlineMaterial = new Material(shader)
        {
            name = $"{name}_RuntimeSpriteOutlineMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        return runtimeOutlineMaterial;
    }

    private void UpdateSpriteOutlines()
    {
        if (activeOutlineRenderers == null || spriteOutlineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < activeOutlineRenderers.Length && i < spriteOutlineRenderers.Length; i++)
        {
            SpriteRenderer sourceRenderer = activeOutlineRenderers[i];
            LineRenderer lineRenderer = spriteOutlineRenderers[i];
            if (sourceRenderer == null || lineRenderer == null)
            {
                continue;
            }

            Vector3[] points = BuildSpriteOutlinePoints(sourceRenderer);
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
            lineRenderer.startColor = outlineColor;
            lineRenderer.endColor = outlineColor;
            lineRenderer.startWidth = CalculateOutlineLocalLineWidth(sourceRenderer);
            lineRenderer.endWidth = lineRenderer.startWidth;
            lineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            lineRenderer.sortingOrder = sourceRenderer.sortingOrder + OutlineSortingOrderOffset;
        }
    }

    private Vector3[] BuildSpriteOutlinePoints(SpriteRenderer sourceRenderer)
    {
        Sprite sprite = sourceRenderer.sprite;
        if (sprite == null)
        {
            return new Vector3[0];
        }

        int bestShapeIndex = FindLargestPhysicsShape(sprite);
        if (bestShapeIndex >= 0)
        {
            physicsShapeBuffer.Clear();
            sprite.GetPhysicsShape(bestShapeIndex, physicsShapeBuffer);
            Vector3[] points = new Vector3[physicsShapeBuffer.Count];
            for (int i = 0; i < physicsShapeBuffer.Count; i++)
            {
                Vector2 point = physicsShapeBuffer[i];
                if (sourceRenderer.flipX)
                {
                    point.x = -point.x;
                }

                if (sourceRenderer.flipY)
                {
                    point.y = -point.y;
                }

                points[i] = new Vector3(point.x, point.y, 0f);
            }

            return points;
        }

        Bounds bounds = sprite.bounds;
        return new[]
        {
            new Vector3(bounds.min.x, bounds.min.y, 0f),
            new Vector3(bounds.min.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.min.y, 0f)
        };
    }

    private int FindLargestPhysicsShape(Sprite sprite)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();
        int bestShapeIndex = -1;
        float bestArea = 0f;

        for (int i = 0; i < shapeCount; i++)
        {
            physicsShapeBuffer.Clear();
            sprite.GetPhysicsShape(i, physicsShapeBuffer);
            if (physicsShapeBuffer.Count < 3)
            {
                continue;
            }

            float area = Mathf.Abs(CalculatePolygonArea(physicsShapeBuffer));
            if (area > bestArea)
            {
                bestArea = area;
                bestShapeIndex = i;
            }
        }

        return bestShapeIndex;
    }

    private static float CalculatePolygonArea(List<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 current = points[i];
            Vector2 next = points[(i + 1) % points.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
    }

    private float CalculateOutlineLocalLineWidth(SpriteRenderer sourceRenderer)
    {
        Camera mainCamera = Camera.main;
        float worldWidth = 0.035f;
        if (mainCamera != null && Screen.height > 0)
        {
            float worldUnitsPerScreenPixel = mainCamera.orthographicSize * 2f / Screen.height;
            worldWidth = outlineScreenPixels * worldUnitsPerScreenPixel;
        }

        Vector3 scale = sourceRenderer != null ? sourceRenderer.transform.lossyScale : transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 0.001f);
        return worldWidth / maxScale;
    }

    private void ResolveOutlineRenderers()
    {
        if (activeOutlineRenderers != null && activeOutlineRenderers.Length > 0)
        {
            return;
        }

        if (outlineRenderers != null && outlineRenderers.Length > 0)
        {
            activeOutlineRenderers = outlineRenderers;
            return;
        }

        rendererBuffer.Clear();
        GetComponentsInChildren(true, rendererBuffer);
        if (selectedObject == null)
        {
            ResolveSelectedObject();
        }

        Transform selectedTransform = selectedObject != null ? selectedObject.transform : null;
        for (int i = rendererBuffer.Count - 1; i >= 0; i--)
        {
            SpriteRenderer spriteRenderer = rendererBuffer[i];
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                rendererBuffer.RemoveAt(i);
                continue;
            }

            if (spriteRenderer.name == "__HoverOutline" || spriteRenderer.name == "__HoverSpriteOutline")
            {
                rendererBuffer.RemoveAt(i);
                continue;
            }

            if (selectedTransform != null && spriteRenderer.transform.IsChildOf(selectedTransform))
            {
                rendererBuffer.RemoveAt(i);
            }
        }

        activeOutlineRenderers = rendererBuffer.ToArray();
    }

    private void DestroyRuntimeOutlineMaterial()
    {
        if (runtimeOutlineMaterial == null)
        {
            return;
        }

        Destroy(runtimeOutlineMaterial);
        runtimeOutlineMaterial = null;
    }

    private void PlayClickPulse()
    {
        StopClickPulse();

        Vector3 pulseScale = initialScale * clickPulseScaleMultiplier;
        clickPulseTween = DOTween.Sequence()
            .Append(transform.DOScale(pulseScale, clickPulseDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(initialScale, clickPulseDuration).SetEase(Ease.InQuad))
            .SetUpdate(true)
            .OnKill(() => clickPulseTween = null);
    }

    private void StopClickPulse()
    {
        if (clickPulseTween == null)
        {
            return;
        }

        clickPulseTween.Kill();
        clickPulseTween = null;
        transform.localScale = initialScale;
    }

    private static Transform FindChildContaining(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrWhiteSpace(namePart))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.Contains(namePart))
            {
                return child;
            }

            Transform nested = FindChildContaining(child, namePart);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
