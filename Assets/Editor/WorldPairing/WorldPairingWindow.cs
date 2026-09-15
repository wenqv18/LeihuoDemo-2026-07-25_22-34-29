using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldPairingWindow : EditorWindow
{
    private const string NormalRootName = "NormalWorld";
    private const string SpecialRootName = "SpecialWorld";
    private const string NormalResourceRoot = "Assets/Resources/NormalWorld";
    private const string SpecialResourceRoot = "Assets/Resources/SpecialWorld";
    private const string FurniturePrefabPath = "Assets/Prefabs/Environment/NormalWorld/Furniture.prefab";
    private const string ForgeFurniturePrefabPath = "Assets/Prefabs/Environment/NormalWorld/ForgeFurniture.prefab";
    private const string NormalInteractionPrefabPath = "Assets/Prefabs/Environment/NormalWorld/Interaction.prefab";
    private const string SpecialInteractionPrefabPath = "Assets/Prefabs/Environment/SpecialWorld/SpecialInteraction.prefab";
    private const string SpecialInteractionSortingLayer = "SpecialWorldInteractionBackground";
    private const float PositionTolerance = 0.01f;
    private const float SizeTolerance = 0.05f;

    private static readonly string[] WallMountedNameParts =
    {
        "Camera", "ElectricalPanel", "Map", "Memo", "Window"
    };

    private static readonly Dictionary<string, string[]> SpecialSpriteAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Book", new[] { "Books" } },
            { "Box", new[] { "Cup", "Cup1", "Cup2" } },
            { "LeftBookshelf 1", new[] { "LeftBookShelf 1" } },
            { "Map 1", new[] { "Map" } },
            { "MiniBox", new[] { "Cup", "Cup1", "Cup2" } },
            { "RightBookshelf 1", new[] { "RightBookShelf 1" } },
            { "Table", new[] { "SpecialTable_01" } },
            { "Water", new[] { "Water1" } },
            { "Window", new[] { "Window3", "Window2" } }
        };

    private static readonly HashSet<string> StableVariantSpriteGroups =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Box",
            "Cup",
            "MiniBox",
            "Window"
        };

    private Vector2 scrollPosition;
    private string report = "Click Check Differences to inspect the active scene.";
    private bool preserveSpriteAspect;
    private bool syncColliderGeometry = true;
    private bool createMissingSections = true;
    private bool strictSpecialSprites = true;
    private bool reportPrefabLinkageWarnings;

    [MenuItem("Tools/Leihuo/World Pairing")]
    public static void OpenWindow()
    {
        GetWindow<WorldPairingWindow>("World Pairing");
    }

    [MenuItem("Tools/Leihuo/World Pairing/Check Differences")]
    public static void CheckFromMenu()
    {
        WorldPairingWindow window = GetWindow<WorldPairingWindow>("World Pairing");
        window.RunCheck();
    }

    [MenuItem("Tools/Leihuo/World Pairing/Sync From Normal")]
    public static void SyncFromMenu()
    {
        WorldPairingWindow window = GetWindow<WorldPairingWindow>("World Pairing");
        window.RunSync();
    }

    [MenuItem("Tools/Leihuo/World Pairing/Generate Special From Normal")]
    public static void GenerateFromMenu()
    {
        WorldPairingWindow window = GetWindow<WorldPairingWindow>("World Pairing");
        window.RunGenerate();
    }

    [MenuItem("Tools/Leihuo/World Pairing/Align Bookshelves/Tables Only")]
    public static void AlignVisualFurnitureFromMenu()
    {
        WorldPairingWindow window = GetWindow<WorldPairingWindow>("World Pairing");
        window.RunVisualFurnitureAlign();
    }

    [MenuItem("Tools/Leihuo/World Pairing/Sync Environment Prefab Templates")]
    public static void SyncPrefabTemplatesFromMenu()
    {
        WorldPairingWindow window = GetWindow<WorldPairingWindow>("World Pairing");
        window.RunSyncPrefabTemplates();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("NormalWorld -> SpecialWorld", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Check Differences is read-only. NormalWorld is the layout source; SpecialWorld receives position, size, " +
            "collider, sorting, sprite, and script-setup changes. NormalWorld stays unchanged.",
            MessageType.Info);

        preserveSpriteAspect = EditorGUILayout.ToggleLeft(
            "Preserve Special sprite aspect (may leave visual height differences)",
            preserveSpriteAspect);
        syncColliderGeometry = EditorGUILayout.ToggleLeft("Sync matching collider geometry", syncColliderGeometry);
        createMissingSections = EditorGUILayout.ToggleLeft(
            "Create missing SpecialWorld section containers",
            createMissingSections);
        strictSpecialSprites = EditorGUILayout.ToggleLeft(
            "Strict SpecialWorld art (never keep NormalWorld sprites on generated pairs)",
            strictSpecialSprites);
        reportPrefabLinkageWarnings = EditorGUILayout.ToggleLeft(
            "Report legacy prefab linkage warnings",
            reportPrefabLinkageWarnings);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Check Differences (Read Only)", GUILayout.Height(28f)))
            {
                RunCheck();
            }

            if (GUILayout.Button("Align Existing Special Pairs", GUILayout.Height(28f)))
            {
                RunSync();
            }
        }

        if (GUILayout.Button("Align Bookshelves/Tables Only", GUILayout.Height(28f)))
        {
            RunVisualFurnitureAlign();
        }

        if (GUILayout.Button("Generate Missing + Align All", GUILayout.Height(30f)))
        {
            RunGenerate();
        }

        if (GUILayout.Button("Sync Environment Prefab Templates", GUILayout.Height(28f)))
        {
            RunSyncPrefabTemplates();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Latest Report", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunCheck()
    {
        Scene scene = SceneManager.GetActiveScene();
        PairingContext context;
        if (!TryBuildContext(scene, out context, out report))
        {
            Repaint();
            return;
        }

        report = BuildReport(context, "CHECK", null, reportPrefabLinkageWarnings);
        WriteReportFile(report);
        Debug.Log(report);
        Repaint();
    }

    private void RunSync()
    {
        RunSyncInternal("SYNC");
    }

    private void RunGenerate()
    {
        RunSyncInternal("GENERATE");
    }

    private void RunVisualFurnitureAlign()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Visual furniture alignment is only available in Edit Mode.";
            Repaint();
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!TryValidateSceneForSync(scene, false, out report))
        {
            Repaint();
            return;
        }

        string backupPath = BackupScene(scene);
        PairingContext context;
        if (!TryBuildContext(scene, out context, out report))
        {
            report += "\nBackup: " + backupPath;
            Repaint();
            return;
        }

        VisualAlignResult alignResult = AlignVisualFurnitureOnly(context);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        report = BuildVisualAlignReport(context, alignResult) + "\n\nBackup: " + backupPath;
        WriteReportFile(report);
        Debug.Log(report);
        Repaint();
    }

    private void RunSyncInternal(string mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "World pairing sync is only available in Edit Mode.";
            Repaint();
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!TryValidateSceneForSync(scene, mode == "GENERATE" && createMissingSections, out report))
        {
            Repaint();
            return;
        }

        string backupPath = BackupScene(scene);
        if (mode == "GENERATE" && createMissingSections)
        {
            EnsureSpecialRoot(scene);
        }

        PairingContext context;
        if (!TryBuildContext(scene, out context, out report))
        {
            report += "\nBackup: " + backupPath;
            Repaint();
            return;
        }

        SyncResult syncResult = SyncContext(context, mode);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        PairingContext refreshedContext;
        string contextError;
        if (TryBuildContext(scene, out refreshedContext, out contextError))
        {
            report = BuildReport(refreshedContext, mode, syncResult, reportPrefabLinkageWarnings) +
                     "\n\nBackup: " + backupPath;
        }
        else
        {
            report = "Sync completed, but the final report failed: " + contextError +
                     "\nBackup: " + backupPath;
        }

        WriteReportFile(report);
        Debug.Log(report);
        Repaint();
    }

    private SyncResult SyncContext(PairingContext context, string mode)
    {
        SyncResult result = new SyncResult();
        HashSet<string> duplicateNormalPairIds = BuildDuplicatePairIdSet(context.NormalUnits);
        HashSet<string> duplicateSpecialPairIds = BuildDuplicatePairIdSet(context.SpecialUnits);
        Dictionary<string, PairUnit> specialByPairId = BuildPairIdLookup(context.SpecialUnits, duplicateSpecialPairIds);
        Dictionary<string, PairUnit> specialByKey = BuildKeyLookup(context.SpecialUnits);

        Undo.SetCurrentGroupName(mode == "GENERATE"
            ? "Generate SpecialWorld From NormalWorld"
            : "Sync SpecialWorld From NormalWorld");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (PairUnit normalUnit in context.NormalUnits)
        {
            PairUnit specialUnit;
            if (!TryFindPair(normalUnit, specialByPairId, specialByKey, duplicateNormalPairIds, out specialUnit))
            {
                MissingAsset missingAsset;
                if (!CanCreateSpecialUnit(normalUnit, out missingAsset))
                {
                    result.SkippedMissingArt.Add(normalUnit.Key + " -> " + missingAsset.Description);
                    continue;
                }

                Transform specialParent = ResolveSpecialParent(context.SpecialRoot, normalUnit.Section, createMissingSections);
                if (specialParent == null)
                {
                    result.SkippedMissingArt.Add(normalUnit.Key + " -> missing section " + normalUnit.Section);
                    continue;
                }

                string prefabPath;
                GameObject clone = InstantiateSpecialUnitFromPrefab(normalUnit, specialParent, out prefabPath);
                if (clone == null)
                {
                    result.SkippedMissingArt.Add(normalUnit.Key + " -> missing prefab template for " + normalUnit.Section);
                    continue;
                }

                specialUnit = new PairUnit(normalUnit.Section, clone.transform);
                ConvertClonedWorldSpecificComponents(normalUnit, specialUnit);
                ReplaceNormalSpritesWithSpecialSprites(normalUnit, specialUnit, result, strictSpecialSprites);
                specialByKey[specialUnit.Key] = specialUnit;
                result.Created.Add(specialUnit.Key + " via " + prefabPath);
            }
            else
            {
                ConvertExistingWorldSpecificComponents(normalUnit, specialUnit, result);
                ReplaceNormalSpritesWithSpecialSprites(normalUnit, specialUnit, result, strictSpecialSprites);
            }

            if (FindPrimaryRenderer(normalUnit.Root) != null && FindPrimaryRenderer(specialUnit.Root) == null)
            {
                result.SkippedMissingArt.Add(normalUnit.Key + " -> existing SpecialWorld object has no primary sprite");
                continue;
            }

            string pairId = EnsurePairIds(normalUnit, specialUnit, duplicateNormalPairIds);
            if (!string.IsNullOrWhiteSpace(pairId) && !duplicateSpecialPairIds.Contains(pairId))
            {
                specialByPairId[pairId] = specialUnit;
            }
            SyncUnit(normalUnit, specialUnit);
            result.Synchronized.Add(normalUnit.Key);
        }

        Undo.CollapseUndoOperations(undoGroup);
        return result;
    }

    private VisualAlignResult AlignVisualFurnitureOnly(PairingContext context)
    {
        VisualAlignResult result = new VisualAlignResult();
        HashSet<string> duplicateNormalPairIds = BuildDuplicatePairIdSet(context.NormalUnits);
        HashSet<string> duplicateSpecialPairIds = BuildDuplicatePairIdSet(context.SpecialUnits);
        Dictionary<string, PairUnit> specialByPairId = BuildPairIdLookup(context.SpecialUnits, duplicateSpecialPairIds);
        Dictionary<string, PairUnit> specialByKey = BuildKeyLookup(context.SpecialUnits);

        Undo.SetCurrentGroupName("Align Bookshelves/Tables Visuals Only");
        int undoGroup = Undo.GetCurrentGroup();
        foreach (PairUnit normalUnit in context.NormalUnits)
        {
            if (!IsVisualFurnitureAlignCandidate(normalUnit))
            {
                continue;
            }

            PairUnit specialUnit;
            if (!TryFindPair(normalUnit, specialByPairId, specialByKey, duplicateNormalPairIds, out specialUnit))
            {
                result.Skipped.Add(normalUnit.Key + " -> no existing SpecialWorld pair");
                continue;
            }

            SpriteRenderer normalRenderer = FindPrimaryRenderer(normalUnit.Root);
            SpriteRenderer specialRenderer = FindPrimaryRenderer(specialUnit.Root);
            if (normalRenderer == null || specialRenderer == null ||
                normalRenderer.sprite == null || specialRenderer.sprite == null)
            {
                result.Skipped.Add(normalUnit.Key + " -> missing primary sprite");
                continue;
            }

            MatchRendererSize(normalUnit, normalRenderer, specialRenderer, preserveSpriteAspect);
            if (!IsBookshelfPair(normalUnit))
            {
                AlignUnit(normalUnit, normalRenderer, specialRenderer);
            }
            result.Aligned.Add(normalUnit.Key + " -> " + specialUnit.Key);
        }

        Undo.CollapseUndoOperations(undoGroup);
        return result;
    }

    private void RunSyncPrefabTemplates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Prefab template sync is only available in Edit Mode.";
            Repaint();
            return;
        }

        List<string> changes = new List<string>();
        SyncEnvironmentPrefabTemplate(
            NormalInteractionPrefabPath,
            WorldKind2D.Normal,
            false,
            changes);
        SyncEnvironmentPrefabTemplate(
            SpecialInteractionPrefabPath,
            WorldKind2D.Special,
            true,
            changes);
        SyncEnvironmentPrefabTemplate(
            ForgeFurniturePrefabPath,
            WorldKind2D.Normal,
            false,
            changes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("ENVIRONMENT PREFAB TEMPLATE SYNC REPORT");
        AppendSection(builder, "Changes", changes);
        report = builder.ToString().TrimEnd();
        WriteReportFile(report);
        Debug.Log(report);
        Repaint();
    }

    private static void SyncEnvironmentPrefabTemplate(
        string prefabPath,
        WorldKind2D world,
        bool requireSpecialTargetForProximity,
        List<string> changes)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            changes.Add(prefabPath + " -> missing prefab");
            return;
        }

        try
        {
            bool dirty = false;
            WorldPairId pairId = prefabRoot.GetComponent<WorldPairId>();
            if (pairId == null)
            {
                prefabRoot.AddComponent<WorldPairId>();
                changes.Add(prefabPath + " -> added WorldPairId on root");
                dirty = true;
            }

            WorldInteractionTarget2D target = prefabRoot.GetComponent<WorldInteractionTarget2D>();
            if (target == null)
            {
                target = prefabRoot.AddComponent<WorldInteractionTarget2D>();
                changes.Add(prefabPath + " -> added WorldInteractionTarget2D on root");
                dirty = true;
            }

            target.Configure(world, false);
            target.enabled = false;

            if (requireSpecialTargetForProximity)
            {
                RemoveDuplicateFurnitureInteractions(prefabRoot, changes, prefabPath, ref dirty);
                FurnitureInteraction2D interaction = prefabRoot.GetComponentInChildren<FurnitureInteraction2D>(true);
                if (interaction != null)
                {
                    FurnitureProximityInteraction2D proximity =
                        interaction.GetComponent<FurnitureProximityInteraction2D>();
                    if (proximity == null)
                    {
                        proximity = interaction.gameObject.AddComponent<FurnitureProximityInteraction2D>();
                        changes.Add(prefabPath + " -> added FurnitureProximityInteraction2D on interaction body");
                        dirty = true;
                    }

                    SerializedObject serialized = new SerializedObject(proximity);
                    SerializedProperty requireTarget =
                        serialized.FindProperty("requireSpecialWorldTarget");
                    if (requireTarget != null && !requireTarget.boolValue)
                    {
                        requireTarget.boolValue = true;
                        changes.Add(prefabPath + " -> set requireSpecialWorldTarget");
                        dirty = true;
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            else
            {
                changes.Add(prefabPath + " -> already up to date");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void RemoveDuplicateFurnitureInteractions(
        GameObject prefabRoot,
        List<string> changes,
        string prefabPath,
        ref bool dirty)
    {
        FurnitureInteraction2D[] interactions =
            prefabRoot.GetComponentsInChildren<FurnitureInteraction2D>(true);
        Dictionary<GameObject, bool> seenByObject = new Dictionary<GameObject, bool>();
        for (int i = 0; i < interactions.Length; i++)
        {
            FurnitureInteraction2D interaction = interactions[i];
            if (interaction == null)
            {
                continue;
            }

            if (!seenByObject.ContainsKey(interaction.gameObject))
            {
                seenByObject.Add(interaction.gameObject, true);
                continue;
            }

            DestroyImmediate(interaction, true);
            changes.Add(prefabPath + " -> removed duplicate FurnitureInteraction2D");
            dirty = true;
        }
    }

    private void SyncUnit(PairUnit normalUnit, PairUnit specialUnit)
    {
        Transform normal = normalUnit.Root;
        Transform special = specialUnit.Root;

        Undo.RecordObject(special, "Sync paired transform");
        special.position = normal.position;
        special.rotation = normal.rotation;
        special.localScale = normal.localScale;
        special.gameObject.SetActive(normal.gameObject.activeSelf);

        SpriteRenderer normalRenderer = FindPrimaryRenderer(normal);
        SpriteRenderer specialRenderer = FindPrimaryRenderer(special);
        if (normalRenderer != null && specialRenderer != null &&
            normalRenderer.sprite != null && specialRenderer.sprite != null)
        {
            MatchRendererSize(normalUnit, normalRenderer, specialRenderer, preserveSpriteAspect);
            AlignUnit(normalUnit, normalRenderer, specialRenderer);
            NormalizeInteractionBodyTransform(normalUnit, special, normalRenderer, specialRenderer);
            CopyRendererOrdering(normalRenderer, specialRenderer, normalUnit.Section == PairSection.Interactions);
        }

        ApplySpecialSortingLayers(specialUnit);
        if (syncColliderGeometry)
        {
            SyncColliderGeometry(normal, special);
        }
    }

    private static void MatchRendererSize(
        PairUnit normalUnit,
        SpriteRenderer normalRenderer,
        SpriteRenderer specialRenderer,
        bool preserveAspect)
    {
        Bounds normalBounds = normalRenderer.bounds;
        Bounds specialBounds = specialRenderer.bounds;
        if (normalBounds.size.x <= Mathf.Epsilon || normalBounds.size.y <= Mathf.Epsilon ||
            specialBounds.size.x <= Mathf.Epsilon || specialBounds.size.y <= Mathf.Epsilon)
        {
            return;
        }

        float scaleX = normalBounds.size.x / specialBounds.size.x;
        float scaleY = normalBounds.size.y / specialBounds.size.y;
        Bounds normalAlphaBounds;
        Bounds specialAlphaBounds;
        if ((IsWindowVariantPair(normalRenderer.sprite, specialRenderer.sprite) || IsBookshelfPair(normalUnit)) &&
            TryGetAlphaWorldBounds(normalRenderer, out normalAlphaBounds) &&
            TryGetAlphaWorldBounds(specialRenderer, out specialAlphaBounds) &&
            normalAlphaBounds.size.x > Mathf.Epsilon &&
            normalAlphaBounds.size.y > Mathf.Epsilon &&
            specialAlphaBounds.size.x > Mathf.Epsilon &&
            specialAlphaBounds.size.y > Mathf.Epsilon)
        {
            scaleX = normalAlphaBounds.size.x / specialAlphaBounds.size.x;
            scaleY = normalAlphaBounds.size.y / specialAlphaBounds.size.y;
            ApplyIncrementalScale(specialRenderer, scaleX, scaleY);
            return;
        }

        if (normalUnit.Section == PairSection.ForgeGround ||
            IsBottleReplacementPair(normalRenderer.sprite, specialRenderer.sprite))
        {
            if (TrySetAspectPreservingScale(normalBounds, specialRenderer, true))
            {
                return;
            }
        }

        if (preserveAspect)
        {
            if (TrySetAspectPreservingScale(normalBounds, specialRenderer, false))
            {
                return;
            }
        }

        ApplyIncrementalScale(specialRenderer, scaleX, scaleY);
    }

    private static void ApplyIncrementalScale(
        SpriteRenderer specialRenderer,
        float scaleX,
        float scaleY)
    {
        Undo.RecordObject(specialRenderer.transform, "Match paired sprite size");
        Vector3 localScale = specialRenderer.transform.localScale;
        specialRenderer.transform.localScale = new Vector3(
            localScale.x * scaleX,
            localScale.y * scaleY,
            localScale.z);
    }

    private static bool TrySetAspectPreservingScale(
        Bounds targetBounds,
        SpriteRenderer specialRenderer,
        bool matchHeight)
    {
        if (specialRenderer == null || specialRenderer.sprite == null)
        {
            return false;
        }

        Vector2 spriteSize = specialRenderer.sprite.bounds.size;
        if (spriteSize.x <= Mathf.Epsilon || spriteSize.y <= Mathf.Epsilon)
        {
            return false;
        }

        Transform parent = specialRenderer.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        float parentScaleX = Mathf.Abs(parentScale.x);
        float parentScaleY = Mathf.Abs(parentScale.y);
        if (parentScaleX <= Mathf.Epsilon || parentScaleY <= Mathf.Epsilon)
        {
            return false;
        }

        float targetLocalX = targetBounds.size.x / (spriteSize.x * parentScaleX);
        float targetLocalY = targetBounds.size.y / (spriteSize.y * parentScaleY);
        float uniformScale = matchHeight ? targetLocalY : Mathf.Min(targetLocalX, targetLocalY);
        if (uniformScale <= Mathf.Epsilon ||
            float.IsNaN(uniformScale) ||
            float.IsInfinity(uniformScale))
        {
            return false;
        }

        Undo.RecordObject(specialRenderer.transform, "Match paired sprite size");
        Vector3 localScale = specialRenderer.transform.localScale;
        specialRenderer.transform.localScale = new Vector3(
            localScale.x < 0f ? -uniformScale : uniformScale,
            localScale.y < 0f ? -uniformScale : uniformScale,
            localScale.z);
        return true;
    }

    private static void AlignUnit(
        PairUnit normalUnit,
        SpriteRenderer normalRenderer,
        SpriteRenderer specialRenderer)
    {
        AlignmentAnchor anchor = IsWindowVariantPair(normalRenderer.sprite, specialRenderer.sprite)
            ? AlignmentAnchor.AlphaCenter
            : IsBottleReplacementPair(normalRenderer.sprite, specialRenderer.sprite)
            ? AlignmentAnchor.Center
            : GetAlignmentAnchor(normalUnit);
        Vector3 normalAnchor = GetAnchorPoint(normalRenderer, anchor);
        Vector3 specialAnchor = GetAnchorPoint(specialRenderer, anchor);

        Undo.RecordObject(specialRenderer.transform, "Align paired sprite");
        specialRenderer.transform.position += normalAnchor - specialAnchor + GetSpecialAlignmentOffset(normalUnit);
    }

    private static Vector3 GetSpecialAlignmentOffset(PairUnit normalUnit)
    {
        if (normalUnit.Root.name.IndexOf("RightBookShelf", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalUnit.Root.name.IndexOf("RightBookshelf", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Vector3(0f, -0.08f, 0f);
        }

        return Vector3.zero;
    }

    private static void NormalizeInteractionBodyTransform(
        PairUnit normalUnit,
        Transform specialRoot,
        SpriteRenderer normalRenderer,
        SpriteRenderer specialRenderer)
    {
        if (normalUnit.Section != PairSection.Interactions ||
            normalRenderer == null ||
            specialRenderer == null)
        {
            return;
        }

        Transform normalBody = normalRenderer.transform.parent;
        Transform specialBody = specialRenderer.transform.parent;
        if (normalBody == null ||
            specialBody == null ||
            !string.Equals(normalBody.name, "Body", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(specialBody.name, "Body", StringComparison.OrdinalIgnoreCase) ||
            !normalBody.IsChildOf(normalUnit.Root) ||
            !specialBody.IsChildOf(specialRoot))
        {
            return;
        }

        Undo.RecordObject(specialBody, "Normalize interaction body transform");
        specialBody.localPosition = normalBody.localPosition;
        specialBody.localRotation = normalBody.localRotation;
        specialBody.localScale = normalBody.localScale;

        Undo.RecordObject(specialRenderer.transform, "Normalize interaction sprite transform");
        Vector3 matchedScale = specialRenderer.transform.localScale;
        specialRenderer.transform.localPosition = normalRenderer.transform.localPosition;
        specialRenderer.transform.localRotation = normalRenderer.transform.localRotation;
        specialRenderer.transform.localScale = new Vector3(
            matchedScale.x,
            matchedScale.y,
            normalRenderer.transform.localScale.z);
    }

    private static AlignmentAnchor GetAlignmentAnchor(PairUnit normalUnit)
    {
        if (normalUnit.Root.name.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalUnit.Root.name.IndexOf("HangingLamp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AlignmentAnchor.TopCenter;
        }

        if (normalUnit.Root.name.IndexOf("BookShelf", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalUnit.Root.name.IndexOf("Bookshelf", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AlignmentAnchor.SolidBodyBottomCenter;
        }

        bool centerAlign = WallMountedNameParts.Any(
            namePart => normalUnit.Root.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0);
        return centerAlign ? AlignmentAnchor.Center : AlignmentAnchor.BottomCenter;
    }

    private static Vector3 GetAnchorPoint(SpriteRenderer renderer, AlignmentAnchor anchor)
    {
        Bounds bounds = renderer.bounds;
        switch (anchor)
        {
            case AlignmentAnchor.AlphaCenter:
                Bounds alphaBounds;
                if (TryGetAlphaWorldBounds(renderer, out alphaBounds))
                {
                    return alphaBounds.center;
                }

                return bounds.center;
            case AlignmentAnchor.Center:
                return bounds.center;
            case AlignmentAnchor.TopCenter:
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            case AlignmentAnchor.SolidBodyBottomCenter:
                float solidBottomY;
                if (TryGetSolidBodyBottomY(renderer, out solidBottomY))
                {
                    return new Vector3(bounds.center.x, solidBottomY, bounds.center.z);
                }

                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            default:
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
    }

    private static bool TryGetAlphaWorldBounds(SpriteRenderer renderer, out Bounds alphaBounds)
    {
        alphaBounds = default(Bounds);
        AlphaShape shape;
        if (renderer == null ||
            renderer.sprite == null ||
            !TryGetAlphaShape(renderer.sprite, out shape) ||
            shape.SpriteWidth <= 0 ||
            shape.SpriteHeight <= 0 ||
            shape.AlphaWidth <= 0 ||
            shape.AlphaHeight <= 0)
        {
            return false;
        }

        Bounds fullBounds = renderer.bounds;
        float minX = fullBounds.min.x + fullBounds.size.x * shape.AlphaMinX / shape.SpriteWidth;
        float maxX = fullBounds.min.x + fullBounds.size.x * shape.AlphaMaxX / shape.SpriteWidth;
        float minY = fullBounds.min.y + fullBounds.size.y * shape.AlphaMinY / shape.SpriteHeight;
        float maxY = fullBounds.min.y + fullBounds.size.y * shape.AlphaMaxY / shape.SpriteHeight;
        alphaBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, fullBounds.center.z),
            new Vector3(Mathf.Abs(maxX - minX), Mathf.Abs(maxY - minY), fullBounds.size.z));
        return true;
    }

    private static bool TryGetSolidBodyBottomY(SpriteRenderer renderer, out float y)
    {
        y = 0f;
        AlphaShape shape;
        if (renderer == null ||
            renderer.sprite == null ||
            !TryGetAlphaShape(renderer.sprite, out shape) ||
            shape.AlphaHeight <= 0 ||
            shape.SolidBottomOffset < 0)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        float normalizedOffset = shape.SolidBottomOffset / Mathf.Max(shape.SpriteHeight, 1f);
        y = bounds.min.y + bounds.size.y * normalizedOffset;
        return true;
    }

    private static void CopyRendererOrdering(
        SpriteRenderer normalRenderer,
        SpriteRenderer specialRenderer,
        bool isInteraction)
    {
        Undo.RecordObject(specialRenderer, "Sync paired renderer ordering");
        specialRenderer.sortingOrder = normalRenderer.sortingOrder;
        specialRenderer.flipX = normalRenderer.flipX;
        specialRenderer.flipY = normalRenderer.flipY;
        if (!isInteraction)
        {
            specialRenderer.sortingLayerID = normalRenderer.sortingLayerID;
        }
    }

    private static void ApplySpecialSortingLayers(PairUnit unit)
    {
        if (unit.Section != PairSection.Interactions)
        {
            return;
        }

        int layerId = SortingLayer.NameToID(SpecialInteractionSortingLayer);
        foreach (SpriteRenderer renderer in unit.Root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Undo.RecordObject(renderer, "Apply SpecialWorld sorting layer");
            renderer.sortingLayerID = layerId;
        }
    }

    private static void SyncColliderGeometry(Transform normal, Transform special)
    {
        Collider2D[] normalColliders = normal.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D normalCollider in normalColliders)
        {
            string relativePath = AnimationUtility.CalculateTransformPath(normalCollider.transform, normal);
            Transform specialTransform = string.IsNullOrEmpty(relativePath)
                ? special
                : special.Find(relativePath);
            if (specialTransform == null)
            {
                continue;
            }

            Collider2D specialCollider = specialTransform.GetComponent(normalCollider.GetType()) as Collider2D;
            if (specialCollider == null)
            {
                continue;
            }

            Undo.RecordObject(specialCollider, "Sync paired collider");
            specialCollider.offset = normalCollider.offset;
            specialCollider.isTrigger = normalCollider.isTrigger;

            BoxCollider2D normalBox = normalCollider as BoxCollider2D;
            BoxCollider2D specialBox = specialCollider as BoxCollider2D;
            if (normalBox != null && specialBox != null)
            {
                specialBox.size = normalBox.size;
                specialBox.edgeRadius = normalBox.edgeRadius;
            }

            CircleCollider2D normalCircle = normalCollider as CircleCollider2D;
            CircleCollider2D specialCircle = specialCollider as CircleCollider2D;
            if (normalCircle != null && specialCircle != null)
            {
                specialCircle.radius = normalCircle.radius;
            }

            CapsuleCollider2D normalCapsule = normalCollider as CapsuleCollider2D;
            CapsuleCollider2D specialCapsule = specialCollider as CapsuleCollider2D;
            if (normalCapsule != null && specialCapsule != null)
            {
                specialCapsule.size = normalCapsule.size;
                specialCapsule.direction = normalCapsule.direction;
            }
        }
    }

    private static GameObject InstantiateSpecialUnitFromPrefab(
        PairUnit normalUnit,
        Transform specialParent,
        out string prefabPath)
    {
        prefabPath = GetPrefabTemplatePath(normalUnit.Section);
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (template == null)
        {
            return null;
        }

        GameObject clone = PrefabUtility.InstantiatePrefab(template, specialParent) as GameObject;
        if (clone == null)
        {
            clone = UnityEngine.Object.Instantiate(template, specialParent);
        }

        clone.name = normalUnit.Root.name;
        Undo.RegisterCreatedObjectUndo(clone, "Create SpecialWorld prefab pair");
        return clone;
    }

    private static string GetPrefabTemplatePath(PairSection section)
    {
        switch (section)
        {
            case PairSection.Background:
                return FurniturePrefabPath;
            case PairSection.Interactions:
                return SpecialInteractionPrefabPath;
            case PairSection.ForgeGround:
                return ForgeFurniturePrefabPath;
            default:
                return null;
        }
    }

    private static void ConvertClonedWorldSpecificComponents(PairUnit normalUnit, PairUnit specialUnit)
    {
        NormalWorldVisionTarget2D[] copiedNormalTargets =
            specialUnit.Root.GetComponentsInChildren<NormalWorldVisionTarget2D>(true);

        for (int i = 0; i < copiedNormalTargets.Length; i++)
        {
            Undo.DestroyObjectImmediate(copiedNormalTargets[i]);
        }

        RemoveChildUnifiedTargets(specialUnit.Root);
        ConfigureUnifiedTargetPlaceholder(specialUnit.Root, WorldKind2D.Special);
    }

    private static void ConvertExistingWorldSpecificComponents(
        PairUnit normalUnit,
        PairUnit specialUnit,
        SyncResult result)
    {
        NormalWorldVisionTarget2D[] copiedNormalTargets =
            specialUnit.Root.GetComponentsInChildren<NormalWorldVisionTarget2D>(true);
        for (int i = 0; i < copiedNormalTargets.Length; i++)
        {
            Undo.DestroyObjectImmediate(copiedNormalTargets[i]);
            result.FixedWorldScripts.Add(specialUnit.Key + " -> removed NormalWorldVisionTarget2D");
        }

        RemoveChildUnifiedTargets(specialUnit.Root);
        ConfigureUnifiedTargetPlaceholder(specialUnit.Root, WorldKind2D.Special);
        result.FixedWorldScripts.Add(specialUnit.Key + " -> configured WorldInteractionTarget2D placeholder");
    }

    private static void ConfigureUnifiedTargetPlaceholder(Transform root, WorldKind2D world)
    {
        WorldInteractionTarget2D target = root.GetComponent<WorldInteractionTarget2D>();
        if (target == null)
        {
            target = Undo.AddComponent<WorldInteractionTarget2D>(root.gameObject);
        }

        Undo.RecordObject(target, "Configure world interaction target");
        target.Configure(world, false);
        target.enabled = false;
        EditorUtility.SetDirty(target);
    }

    private static void RemoveChildUnifiedTargets(Transform root)
    {
        WorldInteractionTarget2D[] targets = root.GetComponentsInChildren<WorldInteractionTarget2D>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            WorldInteractionTarget2D target = targets[i];
            if (target != null && target.transform != root)
            {
                Undo.DestroyObjectImmediate(target);
            }
        }
    }

    private static bool CanCreateSpecialUnit(PairUnit normalUnit, out MissingAsset missingAsset)
    {
        foreach (SpriteRenderer renderer in normalUnit.Root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || IsSelectionRenderer(renderer))
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(renderer.sprite);
            if (IsSpecialWorldSprite(renderer.sprite))
            {
                continue;
            }

            if (FindSpecialSprite(renderer.sprite, null) == null)
            {
                missingAsset = new MissingAsset(assetPath);
                return false;
            }
        }

        missingAsset = default(MissingAsset);
        return true;
    }

    private static void ReplaceNormalSpritesWithSpecialSprites(
        PairUnit normalUnit,
        PairUnit specialUnit,
        SyncResult result,
        bool strictSprites)
    {
        foreach (SpriteRenderer normalRenderer in normalUnit.Root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (normalRenderer.sprite == null || IsSelectionRenderer(normalRenderer))
            {
                continue;
            }

            string relativePath = AnimationUtility.CalculateTransformPath(normalRenderer.transform, normalUnit.Root);
            Transform specialTransform = string.IsNullOrEmpty(relativePath)
                ? specialUnit.Root
                : specialUnit.Root.Find(relativePath);
            if (specialTransform == null)
            {
                continue;
            }

            SpriteRenderer specialRenderer = specialTransform.GetComponent<SpriteRenderer>();
            if (specialRenderer == null || IsSelectionRenderer(specialRenderer))
            {
                continue;
            }

            Sprite specialSprite = FindSpecialSprite(normalRenderer.sprite, specialUnit.Key + "/" + relativePath);
            if (specialSprite != null)
            {
                if (specialRenderer.sprite != specialSprite &&
                    ShouldReplaceWithSpecialSprite(specialRenderer.sprite, normalRenderer.sprite, specialSprite))
                {
                    Undo.RecordObject(specialRenderer, "Assign SpecialWorld sprite");
                    specialRenderer.sprite = specialSprite;
                    result.ReplacedSprites.Add(specialUnit.Key + "/" + relativePath + " -> " + specialSprite.name);
                }

                continue;
            }

            if (strictSprites && ShouldReplaceWithSpecialSprite(specialRenderer.sprite, normalRenderer.sprite, null))
            {
                result.SkippedMissingArt.Add(
                    specialUnit.Key + "/" + relativePath + " -> no SpecialWorld counterpart for " +
                    AssetDatabase.GetAssetPath(normalRenderer.sprite));
            }
        }
    }

    private static Sprite FindSpecialSprite(Sprite normalSprite, string stableVariantKey)
    {
        if (normalSprite == null)
        {
            return null;
        }

        List<string> candidateNames = BuildSpecialSpriteCandidateNames(normalSprite.name);
        List<Sprite> matchedSprites = FindSpecialSpriteCandidates(candidateNames);
        if (matchedSprites.Count == 0)
        {
            return null;
        }

        List<Sprite> distinctSprites = matchedSprites
            .GroupBy(sprite => AssetDatabase.GetAssetPath(sprite), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctSprites.Count <= 1 || !ShouldUseStableVariant(normalSprite.name, candidateNames))
        {
            return distinctSprites[0];
        }

        int index = Mathf.Abs(GetStableHash(stableVariantKey ?? normalSprite.name)) % distinctSprites.Count;
        return distinctSprites[index];
    }

    private static List<string> BuildSpecialSpriteCandidateNames(string normalSpriteName)
    {
        List<string> candidateNames = new List<string>();
        AddSpriteCandidateName(candidateNames, normalSpriteName);
        AddSpriteCandidateName(candidateNames, StripSpriteSliceSuffix(normalSpriteName));

        foreach (string candidateName in candidateNames.ToArray())
        {
            string[] aliases;
            if (SpecialSpriteAliases.TryGetValue(candidateName, out aliases))
            {
                foreach (string alias in aliases)
                {
                    AddSpriteCandidateName(candidateNames, alias);
                }
            }
        }

        return candidateNames;
    }

    private static void AddSpriteCandidateName(List<string> candidateNames, string candidateName)
    {
        if (!string.IsNullOrWhiteSpace(candidateName) &&
            !candidateNames.Contains(candidateName, StringComparer.OrdinalIgnoreCase))
        {
            candidateNames.Add(candidateName);
        }
    }

    private static string StripSpriteSliceSuffix(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return spriteName;
        }

        return System.Text.RegularExpressions.Regex.Replace(spriteName, @"_\d+$", string.Empty);
    }

    private static List<Sprite> FindSpecialSpriteCandidates(IEnumerable<string> candidateNames)
    {
        List<string> candidates = candidateNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<Sprite> matchedSprites = new List<Sprite>();

        foreach (string candidateName in candidates)
        {
            string[] guids = AssetDatabase.FindAssets(candidateName + " t:Sprite", new[] { SpecialResourceRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null &&
                    (string.Equals(sprite.name, candidateName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(Path.GetFileNameWithoutExtension(path), candidateName, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedSprites.Add(sprite);
                }
            }
        }

        HashSet<string> normalizedCandidates = new HashSet<string>(
            candidates.Select(NormalizeSpriteName),
            StringComparer.OrdinalIgnoreCase);
        string[] allSpecialSprites = AssetDatabase.FindAssets("t:Sprite", new[] { SpecialResourceRoot });
        foreach (string guid in allSpecialSprites)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null &&
                (normalizedCandidates.Contains(NormalizeSpriteName(sprite.name)) ||
                 normalizedCandidates.Contains(NormalizeSpriteName(Path.GetFileNameWithoutExtension(path)))))
            {
                matchedSprites.Add(sprite);
            }
        }

        return matchedSprites
            .GroupBy(sprite => AssetDatabase.GetAssetPath(sprite), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool ShouldUseStableVariant(string normalSpriteName, IEnumerable<string> candidateNames)
    {
        if (StableVariantSpriteGroups.Contains(normalSpriteName))
        {
            return true;
        }

        return candidateNames.Any(candidateName => StableVariantSpriteGroups.Contains(candidateName));
    }

    private static int GetStableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash == int.MinValue ? 0 : hash;
        }
    }

    private static bool TryBuildArtShapeWarning(
        string key,
        Sprite normalSprite,
        Sprite specialSprite,
        out string warning)
    {
        warning = null;
        AlphaShape normalShape;
        AlphaShape specialShape;
        if (!TryGetAlphaShape(normalSprite, out normalShape) ||
            !TryGetAlphaShape(specialSprite, out specialShape) ||
            normalShape.AlphaWidth <= 0 ||
            normalShape.AlphaHeight <= 0)
        {
            return false;
        }

        float widthRatio = specialShape.AlphaWidth / Mathf.Max(normalShape.AlphaWidth, 1f);
        float heightRatio = specialShape.AlphaHeight / Mathf.Max(normalShape.AlphaHeight, 1f);
        if (IsWindowVariantPair(normalSprite, specialSprite))
        {
            return false;
        }

        if (IsBottleReplacementPair(normalSprite, specialSprite))
        {
            if (heightRatio >= 0.85f && heightRatio <= 1.15f)
            {
                return false;
            }

            warning = string.Format(
                CultureInfo.InvariantCulture,
                "{0} | bottle replacement height={1:0.##} | Normal {2} -> Special {3}",
                key,
                heightRatio,
                normalSprite.name,
                specialSprite.name);
            return true;
        }

        if (widthRatio >= 0.85f && widthRatio <= 1.15f &&
            heightRatio >= 0.85f && heightRatio <= 1.15f)
        {
            return false;
        }

        warning = string.Format(
            CultureInfo.InvariantCulture,
            "{0} | alpha body size={1:0.##}x{2:0.##} | Normal {3} -> Special {4}",
            key,
            widthRatio,
            heightRatio,
            normalSprite.name,
            specialSprite.name);
        return true;
    }

    private static bool IsBookshelfPair(PairUnit normalUnit)
    {
        if (normalUnit == null || normalUnit.Root == null)
        {
            return false;
        }

        string rootName = normalUnit.Root.name;
        return rootName.IndexOf("BookShelf", StringComparison.OrdinalIgnoreCase) >= 0 ||
               rootName.IndexOf("Bookshelf", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTablePair(PairUnit normalUnit)
    {
        if (normalUnit == null || normalUnit.Root == null)
        {
            return false;
        }

        return normalUnit.Root.name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsVisualFurnitureAlignCandidate(PairUnit normalUnit)
    {
        if (normalUnit == null || normalUnit.Root == null)
        {
            return false;
        }

        if (HasProtectedRuntimeComponent(normalUnit.Root))
        {
            return false;
        }

        return IsBookshelfPair(normalUnit) || IsTablePair(normalUnit);
    }

    private static bool HasProtectedRuntimeComponent(Transform root)
    {
        if (root == null)
        {
            return true;
        }

        return root.GetComponentInChildren<CorpseInteraction>(true) != null ||
               root.GetComponentInChildren<CorpseFollowerAI>(true) != null ||
               root.GetComponentInChildren<DoorLevelTransition2D>(true) != null ||
               root.GetComponentInChildren<Player2DMovementController>(true) != null ||
               root.GetComponentInChildren<Camera>(true) != null;
    }

    private static bool IsWindowVariantPair(Sprite normalSprite, Sprite specialSprite)
    {
        if (normalSprite == null || specialSprite == null)
        {
            return false;
        }

        string normalName = NormalizeSpriteName(StripSpriteSliceSuffix(normalSprite.name));
        string specialName = NormalizeSpriteName(StripSpriteSliceSuffix(specialSprite.name));
        bool normalIsWindow = string.Equals(normalName, "window", StringComparison.OrdinalIgnoreCase);
        bool specialIsWindow = string.Equals(specialName, "window", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(specialName, "window2", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(specialName, "window3", StringComparison.OrdinalIgnoreCase);
        return normalIsWindow && specialIsWindow;
    }

    private static bool IsBottleReplacementPair(Sprite normalSprite, Sprite specialSprite)
    {
        if (normalSprite == null || specialSprite == null)
        {
            return false;
        }

        string normalName = NormalizeSpriteName(StripSpriteSliceSuffix(normalSprite.name));
        string specialName = NormalizeSpriteName(StripSpriteSliceSuffix(specialSprite.name));
        bool normalIsBox = string.Equals(normalName, "box", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(normalName, "minibox", StringComparison.OrdinalIgnoreCase);
        bool specialIsCup = string.Equals(specialName, "cup", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(specialName, "cup1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(specialName, "cup2", StringComparison.OrdinalIgnoreCase);
        return normalIsBox && specialIsCup;
    }

    private static bool TryGetAlphaShape(Sprite sprite, out AlphaShape shape)
    {
        shape = default(AlphaShape);
        if (sprite == null)
        {
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
        {
            return false;
        }

        byte[] bytes = File.ReadAllBytes(assetPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                return false;
            }

            Rect rect = sprite.rect;
            int minX = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, texture.width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, texture.height - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, texture.width);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, texture.height);
            Color32[] pixels = texture.GetPixels32();
            int alphaMinX = maxX;
            int alphaMinY = maxY;
            int alphaMaxX = minX;
            int alphaMaxY = minY;
            int[] rowCoverage = new int[maxY - minY];
            int maxRowCoverage = 0;

            for (int y = minY; y < maxY; y++)
            {
                int row = y * texture.width;
                int rowAlphaCount = 0;
                for (int x = minX; x < maxX; x++)
                {
                    if (pixels[row + x].a <= 8)
                    {
                        continue;
                    }

                    rowAlphaCount++;
                    alphaMinX = Mathf.Min(alphaMinX, x);
                    alphaMinY = Mathf.Min(alphaMinY, y);
                    alphaMaxX = Mathf.Max(alphaMaxX, x + 1);
                    alphaMaxY = Mathf.Max(alphaMaxY, y + 1);
                }

                rowCoverage[y - minY] = rowAlphaCount;
                maxRowCoverage = Mathf.Max(maxRowCoverage, rowAlphaCount);
            }

            if (alphaMaxX <= alphaMinX || alphaMaxY <= alphaMinY)
            {
                return false;
            }

            shape = new AlphaShape(
                maxX - minX,
                maxY - minY,
                alphaMaxX - alphaMinX,
                alphaMaxY - alphaMinY,
                alphaMinX - minX,
                alphaMinY - minY,
                alphaMaxX - minX,
                alphaMaxY - minY,
                FindSolidBodyBottomOffset(rowCoverage, maxRowCoverage));
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static int FindSolidBodyBottomOffset(int[] rowCoverage, int maxRowCoverage)
    {
        if (rowCoverage == null || rowCoverage.Length == 0 || maxRowCoverage <= 0)
        {
            return -1;
        }

        int coverageThreshold = Mathf.Max(
            Mathf.CeilToInt(maxRowCoverage * 0.35f),
            2);
        for (int index = 0; index < rowCoverage.Length; index++)
        {
            if (rowCoverage[index] >= coverageThreshold)
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeSpriteName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string lowered = name.ToLowerInvariant()
            .Replace("normalworld", string.Empty)
            .Replace("specialworld", string.Empty)
            .Replace("normal", string.Empty)
            .Replace("special", string.Empty)
            .Replace("world", string.Empty);
        StringBuilder builder = new StringBuilder(lowered.Length);
        foreach (char character in lowered)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool IsNormalWorldSprite(Sprite sprite)
    {
        string assetPath = AssetDatabase.GetAssetPath(sprite);
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith(NormalResourceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialWorldSprite(Sprite sprite)
    {
        string assetPath = AssetDatabase.GetAssetPath(sprite);
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith(SpecialResourceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldReplaceWithSpecialSprite(Sprite sprite, Sprite normalSprite, Sprite desiredSpecialSprite)
    {
        if (sprite == null)
        {
            return true;
        }

        if (desiredSpecialSprite != null &&
            IsStableVariantSprite(normalSprite) &&
            IsSpecialCounterpartSprite(sprite, normalSprite))
        {
            return true;
        }

        if (IsSpecialWorldSprite(sprite))
        {
            return !IsSpecialCounterpartSprite(sprite, normalSprite);
        }

        return IsNormalWorldSprite(sprite);
    }

    private static bool IsStableVariantSprite(Sprite normalSprite)
    {
        return normalSprite != null &&
               ShouldUseStableVariant(
                   StripSpriteSliceSuffix(normalSprite.name),
                   BuildSpecialSpriteCandidateNames(normalSprite.name));
    }

    private static bool IsSpecialCounterpartSprite(Sprite specialSprite, Sprite normalSprite)
    {
        if (specialSprite == null || normalSprite == null)
        {
            return false;
        }

        List<Sprite> candidates = FindSpecialSpriteCandidates(BuildSpecialSpriteCandidateNames(normalSprite.name));
        string assetPath = AssetDatabase.GetAssetPath(specialSprite);
        return candidates.Any(candidate =>
            string.Equals(AssetDatabase.GetAssetPath(candidate), assetPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSelectionRenderer(SpriteRenderer renderer)
    {
        return renderer.name.IndexOf("Selected", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static SpriteRenderer FindPrimaryRenderer(Transform unitRoot)
    {
        SpriteRenderer[] renderers = unitRoot.GetComponentsInChildren<SpriteRenderer>(true);
        return renderers.FirstOrDefault(renderer =>
                   renderer.sprite != null && !IsSelectionRenderer(renderer) && renderer.name == "Thing")
               ?? renderers.FirstOrDefault(renderer =>
                   renderer.sprite != null && !IsSelectionRenderer(renderer) && renderer.name == "Map_0")
               ?? renderers.FirstOrDefault(renderer =>
                   renderer.sprite != null && !IsSelectionRenderer(renderer) && renderer.name == "card_background")
               ?? renderers.FirstOrDefault(renderer => renderer.sprite != null && !IsSelectionRenderer(renderer));
    }

    private static string EnsurePairIds(PairUnit normalUnit, PairUnit specialUnit, ISet<string> ignoredNormalPairIds)
    {
        string pairId = normalUnit.PairId;
        if (!string.IsNullOrWhiteSpace(pairId) && ignoredNormalPairIds.Contains(pairId))
        {
            pairId = null;
        }

        if (string.IsNullOrWhiteSpace(pairId))
        {
            pairId = specialUnit.PairId;
        }

        if (string.IsNullOrWhiteSpace(pairId))
        {
            pairId = BuildPairId(normalUnit);
        }

        SetPairId(normalUnit.Root.gameObject, pairId);
        SetPairId(specialUnit.Root.gameObject, pairId);
        return pairId;
    }

    private static void SetPairId(GameObject target, string pairId)
    {
        WorldPairId component = target.GetComponent<WorldPairId>();
        if (component == null)
        {
            component = Undo.AddComponent<WorldPairId>(target);
        }

        if (component.PairId == pairId)
        {
            return;
        }

        Undo.RecordObject(component, "Assign world pair ID");
        component.SetPairIdInEditor(pairId);
        EditorUtility.SetDirty(component);
    }

    private static string BuildPairId(PairUnit unit)
    {
        string raw = unit.Section.ToString().ToLowerInvariant() + "." + unit.Root.name.ToLowerInvariant();
        StringBuilder builder = new StringBuilder(raw.Length);
        bool previousSeparator = false;
        foreach (char character in raw)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousSeparator = false;
            }
            else if (!previousSeparator)
            {
                builder.Append('-');
                previousSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string BuildReport(
        PairingContext context,
        string mode,
        SyncResult syncResult,
        bool includePrefabLinkageWarnings)
    {
        Dictionary<string, PairUnit> normal = BuildKeyLookup(context.NormalUnits);
        HashSet<string> duplicateNormalPairIds = BuildDuplicatePairIdSet(context.NormalUnits);
        HashSet<string> duplicateSpecialPairIds = BuildDuplicatePairIdSet(context.SpecialUnits);
        Dictionary<string, PairUnit> specialByPairId = BuildPairIdLookup(context.SpecialUnits, duplicateSpecialPairIds);
        Dictionary<string, PairUnit> specialByKey = BuildKeyLookup(context.SpecialUnits);
        Dictionary<PairUnit, PairUnit> matched = new Dictionary<PairUnit, PairUnit>();
        foreach (PairUnit normalUnit in context.NormalUnits)
        {
            PairUnit specialUnit;
            if (TryFindPair(normalUnit, specialByPairId, specialByKey, duplicateNormalPairIds, out specialUnit))
            {
                matched[normalUnit] = specialUnit;
            }
        }

        HashSet<PairUnit> matchedSpecial = new HashSet<PairUnit>(matched.Values);
        List<string> missing = context.NormalUnits
            .Where(unit => !matched.ContainsKey(unit))
            .Select(unit => unit.Key)
            .OrderBy(key => key)
            .ToList();
        List<string> extra = context.SpecialUnits
            .Where(unit => !matchedSpecial.Contains(unit))
            .Select(unit => unit.Key)
            .OrderBy(key => key)
            .ToList();
        List<string> differences = new List<string>();
        List<string> aspectWarnings = new List<string>();
        List<string> artShapeWarnings = new List<string>();
        List<string> prefabWarnings = new List<string>();
        List<string> duplicatePairIds = BuildDuplicatePairIdWarnings(context);

        foreach (KeyValuePair<PairUnit, PairUnit> pair in matched.OrderBy(pair => pair.Key.Key))
        {
            PairUnit normalUnit = pair.Key;
            PairUnit specialUnit = pair.Value;
            string key = normalUnit.Key;
            if (includePrefabLinkageWarnings && !IsExpectedPrefabInstance(specialUnit))
            {
                prefabWarnings.Add(key + " | not linked to " + GetPrefabTemplatePath(specialUnit.Section));
            }

            SpriteRenderer normalRenderer = FindPrimaryRenderer(normalUnit.Root);
            SpriteRenderer specialRenderer = FindPrimaryRenderer(specialUnit.Root);
            if (normalRenderer == null || specialRenderer == null ||
                normalRenderer.sprite == null || specialRenderer.sprite == null)
            {
                differences.Add(key + " | missing primary sprite");
                continue;
            }

            float positionDelta = Vector3.Distance(GetAnchor(normalUnit, normalRenderer), GetAnchor(normalUnit, specialRenderer));
            float widthRatio = specialRenderer.bounds.size.x / Mathf.Max(normalRenderer.bounds.size.x, 0.0001f);
            float heightRatio = specialRenderer.bounds.size.y / Mathf.Max(normalRenderer.bounds.size.y, 0.0001f);
            if (positionDelta > PositionTolerance ||
                Mathf.Abs(widthRatio - 1f) > SizeTolerance ||
                Mathf.Abs(heightRatio - 1f) > SizeTolerance)
            {
                differences.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | anchor delta={1:0.###} | size={2:0.##}x{3:0.##}",
                    key,
                    positionDelta,
                    widthRatio,
                    heightRatio));
            }

            float normalAspect = normalRenderer.bounds.size.x / Mathf.Max(normalRenderer.bounds.size.y, 0.0001f);
            float specialAspect = specialRenderer.bounds.size.x / Mathf.Max(specialRenderer.bounds.size.y, 0.0001f);
            float aspectRatio = specialAspect / Mathf.Max(normalAspect, 0.0001f);
            if (aspectRatio < 0.9f || aspectRatio > 1.1f)
            {
                aspectWarnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | aspect ratio difference={1:0.##}",
                    key,
                    aspectRatio));
            }

            string artShapeWarning;
            if (TryBuildArtShapeWarning(key, normalRenderer.sprite, specialRenderer.sprite, out artShapeWarning))
            {
                artShapeWarnings.Add(artShapeWarning);
            }
        }

        List<string> missingArt = new List<string>();
        foreach (string key in missing)
        {
            MissingAsset missingAsset;
            if (!CanCreateSpecialUnit(normal[key], out missingAsset))
            {
                missingArt.Add(key + " -> " + missingAsset.Description);
            }
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("WORLD PAIRING " + mode + " REPORT");
        builder.AppendLine("Scene: " + context.Scene.path);
        builder.AppendLine("Normal units: " + context.NormalUnits.Count);
        builder.AppendLine("Special units: " + context.SpecialUnits.Count);
        builder.AppendLine("Matched units: " + matched.Count);
        AppendSection(builder, "Missing Special units", missing);
        AppendSection(builder, "Special-only units (preserved)", extra);
        AppendSection(builder, "Position/size differences", differences);
        AppendSection(builder, "Aspect warnings", aspectWarnings);
        AppendSection(builder, "Art shape warnings", artShapeWarnings);
        if (includePrefabLinkageWarnings)
        {
            AppendSection(builder, "Prefab linkage warnings", prefabWarnings);
        }
        AppendSection(builder, "Duplicate PairId warnings", duplicatePairIds);
        AppendSection(builder, "Missing Special art", missingArt);

        if (syncResult != null)
        {
            AppendSection(builder, "Created", syncResult.Created);
            AppendSection(builder, "Synchronized", syncResult.Synchronized);
            AppendSection(builder, "Special sprites replaced", syncResult.ReplacedSprites);
            AppendSection(builder, "World-specific scripts fixed", syncResult.FixedWorldScripts);
            AppendSection(builder, "Skipped because art is missing", syncResult.SkippedMissingArt);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildVisualAlignReport(PairingContext context, VisualAlignResult alignResult)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("WORLD PAIRING VISUAL FURNITURE ALIGN REPORT");
        builder.AppendLine("Scene: " + context.Scene.path);
        builder.AppendLine("Scope: existing Bookshelf/Table pairs only");
        builder.AppendLine("Changes: SpecialWorld primary SpriteRenderer transform only");
        builder.AppendLine("Protected: no collider, script, active state, pairId, sprite replacement, or generated objects");
        AppendSection(builder, "Aligned", alignResult.Aligned);
        AppendSection(builder, "Skipped", alignResult.Skipped);
        return builder.ToString().TrimEnd();
    }

    private static Dictionary<string, PairUnit> BuildKeyLookup(IEnumerable<PairUnit> units)
    {
        Dictionary<string, PairUnit> lookup = new Dictionary<string, PairUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (PairUnit unit in units)
        {
            if (!lookup.ContainsKey(unit.Key))
            {
                lookup.Add(unit.Key, unit);
            }
        }

        return lookup;
    }

    private static List<string> BuildDuplicatePairIdWarnings(PairingContext context)
    {
        List<string> warnings = new List<string>();
        AppendDuplicatePairIdWarnings(warnings, "NormalWorld", context.NormalUnits);
        AppendDuplicatePairIdWarnings(warnings, "SpecialWorld", context.SpecialUnits);
        return warnings;
    }

    private static void AppendDuplicatePairIdWarnings(
        List<string> warnings,
        string worldName,
        IEnumerable<PairUnit> units)
    {
        foreach (IGrouping<string, PairUnit> group in units
                     .Where(unit => !string.IsNullOrWhiteSpace(unit.PairId))
                     .GroupBy(unit => unit.PairId, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add(worldName + " pairId '" + group.Key + "' used by " +
                         string.Join(", ", group.Select(unit => unit.Key).OrderBy(key => key)));
        }
    }

    private static HashSet<string> BuildDuplicatePairIdSet(IEnumerable<PairUnit> units)
    {
        return new HashSet<string>(
            units.Where(unit => !string.IsNullOrWhiteSpace(unit.PairId))
                .GroupBy(unit => unit.PairId, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, PairUnit> BuildPairIdLookup(
        IEnumerable<PairUnit> units,
        ISet<string> ignoredPairIds)
    {
        Dictionary<string, PairUnit> lookup = new Dictionary<string, PairUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (PairUnit unit in units)
        {
            if (!string.IsNullOrWhiteSpace(unit.PairId) &&
                !ignoredPairIds.Contains(unit.PairId) &&
                !lookup.ContainsKey(unit.PairId))
            {
                lookup.Add(unit.PairId, unit);
            }
        }

        return lookup;
    }

    private static bool TryFindPair(
        PairUnit normalUnit,
        Dictionary<string, PairUnit> specialByPairId,
        Dictionary<string, PairUnit> specialByKey,
        ISet<string> ignoredNormalPairIds,
        out PairUnit specialUnit)
    {
        if (!string.IsNullOrWhiteSpace(normalUnit.PairId) &&
            !ignoredNormalPairIds.Contains(normalUnit.PairId) &&
            specialByPairId.TryGetValue(normalUnit.PairId, out specialUnit))
        {
            return true;
        }

        return specialByKey.TryGetValue(normalUnit.Key, out specialUnit);
    }

    private static Vector3 GetAnchor(PairUnit normalUnit, SpriteRenderer renderer)
    {
        return GetAnchorPoint(renderer, GetAlignmentAnchor(normalUnit));
    }

    private static bool IsExpectedPrefabInstance(PairUnit unit)
    {
        string expectedPath = GetPrefabTemplatePath(unit.Section);
        if (string.IsNullOrWhiteSpace(expectedPath))
        {
            return true;
        }

        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(unit.Root.gameObject);
        if (root == null)
        {
            return false;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        return string.Equals(prefabPath, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSection(StringBuilder builder, string heading, IList<string> items)
    {
        builder.AppendLine();
        builder.AppendLine(heading + " (" + items.Count + "):");
        if (items.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        foreach (string item in items)
        {
            builder.AppendLine("- " + item);
        }
    }

    private static bool TryBuildContext(Scene scene, out PairingContext context, out string error)
    {
        context = null;
        error = null;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "No valid active scene is loaded.";
            return false;
        }

        Transform normalRoot = FindRoot(scene, NormalRootName);
        Transform specialRoot = FindRoot(scene, SpecialRootName);
        if (normalRoot == null || specialRoot == null)
        {
            error = "The active scene must contain NormalWorld and SpecialWorld root objects.";
            return false;
        }

        context = new PairingContext(scene, normalRoot, specialRoot);
        return true;
    }

    private static bool TryValidateSceneForSync(Scene scene, bool allowCreateSpecialRoot, out string error)
    {
        error = null;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "No valid active scene is loaded.";
            return false;
        }

        Transform normalRoot = FindRoot(scene, NormalRootName);
        if (normalRoot == null)
        {
            error = "The active scene must contain a NormalWorld root object.";
            return false;
        }

        Transform specialRoot = FindRoot(scene, SpecialRootName);
        if (specialRoot == null && !allowCreateSpecialRoot)
        {
            error = "The active scene must contain a SpecialWorld root object.";
            return false;
        }

        return true;
    }

    private static Transform EnsureSpecialRoot(Scene scene)
    {
        Transform existing = FindRoot(scene, SpecialRootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject specialRoot = new GameObject(SpecialRootName);
        Undo.RegisterCreatedObjectUndo(specialRoot, "Create SpecialWorld root");
        SceneManager.MoveGameObjectToScene(specialRoot, scene);
        return specialRoot.transform;
    }

    private static Transform FindRoot(Scene scene, string rootName)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(gameObject => gameObject.name == rootName);
        return root != null ? root.transform : null;
    }

    private static List<PairUnit> CollectUnits(Transform worldRoot)
    {
        List<PairUnit> units = new List<PairUnit>();
        Transform background = FindSection(worldRoot, PairSection.Background);
        if (background != null)
        {
            for (int index = 0; index < background.childCount; index++)
            {
                units.Add(new PairUnit(PairSection.Background, background.GetChild(index)));
            }
        }

        Transform interactions = FindSection(worldRoot, PairSection.Interactions);
        if (interactions != null)
        {
            for (int index = 0; index < interactions.childCount; index++)
            {
                units.Add(new PairUnit(PairSection.Interactions, interactions.GetChild(index)));
            }
        }

        Transform forgeGround = FindSection(worldRoot, PairSection.ForgeGround);
        if (forgeGround != null)
        {
            for (int index = 0; index < forgeGround.childCount; index++)
            {
                units.Add(new PairUnit(PairSection.ForgeGround, forgeGround.GetChild(index)));
            }
        }

        return units;
    }

    private static Transform FindSection(Transform worldRoot, PairSection section)
    {
        foreach (string sectionName in GetSectionNames(section))
        {
            Transform sectionTransform = worldRoot.Find(sectionName);
            if (sectionTransform != null)
            {
                return sectionTransform;
            }
        }

        return null;
    }

    private static Transform ResolveSpecialParent(Transform specialRoot, PairSection section, bool createIfMissing)
    {
        string sectionName = GetPrimarySectionName(section);
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return null;
        }

        Transform existing = FindSection(specialRoot, section);
        if (existing != null || !createIfMissing)
        {
            return existing;
        }

        GameObject sectionObject = new GameObject(sectionName);
        Undo.RegisterCreatedObjectUndo(sectionObject, "Create SpecialWorld section");
        sectionObject.transform.SetParent(specialRoot, false);
        return sectionObject.transform;
    }

    private static string GetPrimarySectionName(PairSection section)
    {
        return GetSectionNames(section).FirstOrDefault();
    }

    private static IEnumerable<string> GetSectionNames(PairSection section)
    {
        switch (section)
        {
            case PairSection.Background:
                return new[] { "Background" };
            case PairSection.Interactions:
                return new[] { "Interactions" };
            case PairSection.ForgeGround:
                return new[] { "ForgeGround", "Foreground", "ForeGround" };
            default:
                return Array.Empty<string>();
        }
    }

    private static string BackupScene(Scene scene)
    {
        if (string.IsNullOrWhiteSpace(scene.path))
        {
            throw new InvalidOperationException("Save the active scene before syncing worlds.");
        }

        if (scene.isDirty)
        {
            EditorSceneManager.SaveScene(scene);
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backupDirectory = Path.Combine(
            projectRoot,
            "_codex_backups",
            "world_sync",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(backupDirectory);
        string destination = Path.Combine(backupDirectory, Path.GetFileName(scene.path));
        File.Copy(Path.Combine(projectRoot, scene.path), destination, false);
        return destination;
    }

    private static void WriteReportFile(string contents)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string reportDirectory = Path.Combine(projectRoot, "_codex_reports");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, "WorldPairingReport.txt"), contents, Encoding.UTF8);
    }

    private enum PairSection
    {
        Background,
        Interactions,
        ForgeGround
    }

    private enum AlignmentAnchor
    {
        BottomCenter,
        Center,
        AlphaCenter,
        TopCenter,
        SolidBodyBottomCenter
    }

    private struct AlphaShape
    {
        public AlphaShape(
            int spriteWidth,
            int spriteHeight,
            int alphaWidth,
            int alphaHeight,
            int alphaMinX,
            int alphaMinY,
            int alphaMaxX,
            int alphaMaxY,
            int solidBottomOffset)
        {
            SpriteWidth = spriteWidth;
            SpriteHeight = spriteHeight;
            AlphaWidth = alphaWidth;
            AlphaHeight = alphaHeight;
            AlphaMinX = alphaMinX;
            AlphaMinY = alphaMinY;
            AlphaMaxX = alphaMaxX;
            AlphaMaxY = alphaMaxY;
            SolidBottomOffset = solidBottomOffset;
        }

        public int SpriteWidth { get; }
        public int SpriteHeight { get; }
        public int AlphaWidth { get; }
        public int AlphaHeight { get; }
        public int AlphaMinX { get; }
        public int AlphaMinY { get; }
        public int AlphaMaxX { get; }
        public int AlphaMaxY { get; }
        public int SolidBottomOffset { get; }
    }

    private sealed class PairUnit
    {
        public PairUnit(PairSection section, Transform root)
        {
            Section = section;
            Root = root;
        }

        public PairSection Section { get; }
        public Transform Root { get; }
        public string Key => Section + "/" + Root.name;
        public string PairId
        {
            get
            {
                WorldPairId component = Root.GetComponent<WorldPairId>();
                return component != null ? component.PairId : null;
            }
        }
    }

    private sealed class PairingContext
    {
        public PairingContext(Scene scene, Transform normalRoot, Transform specialRoot)
        {
            Scene = scene;
            NormalRoot = normalRoot;
            SpecialRoot = specialRoot;
            NormalUnits = CollectUnits(normalRoot);
            SpecialUnits = CollectUnits(specialRoot);
        }

        public Scene Scene { get; }
        public Transform NormalRoot { get; }
        public Transform SpecialRoot { get; }
        public List<PairUnit> NormalUnits { get; }
        public List<PairUnit> SpecialUnits { get; }
    }

    private sealed class SyncResult
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Synchronized = new List<string>();
        public readonly List<string> ReplacedSprites = new List<string>();
        public readonly List<string> FixedWorldScripts = new List<string>();
        public readonly List<string> SkippedMissingArt = new List<string>();
    }

    private sealed class VisualAlignResult
    {
        public readonly List<string> Aligned = new List<string>();
        public readonly List<string> Skipped = new List<string>();
    }

    private struct MissingAsset
    {
        public MissingAsset(string normalAssetPath)
        {
            NormalAssetPath = normalAssetPath;
        }

        private string NormalAssetPath { get; }
        public string Description => "no SpecialWorld counterpart for " + NormalAssetPath;
    }
}
