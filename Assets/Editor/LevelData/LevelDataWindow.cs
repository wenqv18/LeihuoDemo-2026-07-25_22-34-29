using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡 JSON 与当前 Unity 场景的一致性校验窗口。
/// 它面向的是开发流程：在运行前检查 pairId、目标标记、家具文本和双世界根节点，
/// 减少策划/程序手动配置后才进 Play Mode 发现错误的成本。
/// </summary>
public sealed class LevelDataWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private string latestReport = "No validation has been run.";

    [MenuItem("Tools/Leihuo/Level Data")]
    private static void Open()
    {
        GetWindow<LevelDataWindow>("Level Data");
    }

    private void OnGUI()
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorGUILayout.LabelField("Active Scene", scene.name);
        EditorGUILayout.HelpBox(
            "Dialogue and target selection live in Resources/LevelData. World-specific components stay on their own scene objects.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Active Scene", GUILayout.Height(32f)))
            {
                latestReport = ValidateScene(scene);
            }

            if (GUILayout.Button("Create Derived Template", GUILayout.Height(32f)))
            {
                CreateDerivedTemplate(scene);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Latest Report", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.TextArea(latestReport, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static string ValidateScene(Scene scene)
    {
        LevelTemplateData template;
        if (!LevelTemplateRepository.TryLoad(scene.name, out template))
        {
            return $"FAIL\nNo level template found at Resources/LevelData/{scene.name}.json";
        }

        Transform normalRoot = FindRoot(scene, "NormalWorld");
        Transform specialRoot = FindRoot(scene, "SpecialWorld");
        if (normalRoot == null || specialRoot == null)
        {
            return "FAIL\nNormalWorld or SpecialWorld root is missing.";
        }

        Dictionary<string, WorldPairId> normalPairs = CollectInteractionPairs(normalRoot);
        Dictionary<string, WorldPairId> specialPairs = CollectInteractionPairs(specialRoot);
        HashSet<string> furniturePairIds = CollectFurniturePairIds(template);
        List<string> errors = new List<string>();

        // 先校验“JSON 文本里出现的家具 pairId，在两个世界场景对象中是否都存在”。
        foreach (FurnitureDialogueData dialogue in template.furniture ?? new List<FurnitureDialogueData>())
        {
            if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.pairId))
            {
                errors.Add("Template contains an empty furniture pair ID.");
                continue;
            }

            if (!normalPairs.ContainsKey(dialogue.pairId))
            {
                errors.Add("NormalWorld is missing " + dialogue.pairId);
            }

            if (!specialPairs.ContainsKey(dialogue.pairId))
            {
                errors.Add("SpecialWorld is missing " + dialogue.pairId);
            }
        }

        foreach (string pairId in GetNormalTargetCandidatePairIds(template))
        {
            // NormalWorld 候选目标必须有文本，也不能误挂 SpecialWorld 的目标组件。
            ValidateTargetHasDialogue(pairId, furniturePairIds, errors);
            ValidateNormalTargetPair(pairId, normalPairs, errors);
        }

        foreach (string pairId in GetSpecialTargetCandidatePairIds(template))
        {
            // SpecialWorld 候选目标同样必须有文本，也不能误挂 NormalWorld 的目标组件。
            ValidateTargetHasDialogue(pairId, furniturePairIds, errors);
            ValidateSpecialTargetPair(pairId, specialPairs, errors);
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(errors.Count == 0 ? "PASS" : "FAIL");
        report.AppendLine("Template: " + template.templateId);
        report.AppendLine("Difficulty: " + Mathf.Clamp(template.difficulty, 1, 3));
        report.AppendLine("Normal interactions: " + normalPairs.Count);
        report.AppendLine("Special interactions: " + specialPairs.Count);
        report.AppendLine("Dialogue entries: " + (template.furniture?.Count ?? 0));
        report.AppendLine("Target routes: " + (template.targetRoutes?.Count ?? 0));
        report.AppendLine("Normal target candidates: " + GetNormalTargetCandidatePairIds(template).Count());
        report.AppendLine("Special target candidates: " + GetSpecialTargetCandidatePairIds(template).Count());
        report.AppendLine("Errors: " + errors.Count);
        foreach (string error in errors)
        {
            report.AppendLine("- " + error);
        }

        return report.ToString().TrimEnd();
    }

    private void CreateDerivedTemplate(Scene scene)
    {
        LevelTemplateData source;
        if (!LevelTemplateRepository.TryLoad(scene.name, out source))
        {
            latestReport = "Create failed: validate a scene with an existing template first.";
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Create Derived Level Template",
            "Level_02",
            "json",
            "Choose the future scene name for the derived level data.",
            "Assets/Resources/LevelData");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string sceneName = Path.GetFileNameWithoutExtension(path);
        LevelTemplateData derived = JsonUtility.FromJson<LevelTemplateData>(JsonUtility.ToJson(source));
        // 派生模板复制已有结构，再让开发者手动调整目标路线和文本，降低从零写 JSON 的出错率。
        derived.sceneName = SceneNames.FirstLevel;
        derived.templateId = sceneName.Replace('_', '-').ToLowerInvariant() + "-base";
        File.WriteAllText(path, JsonUtility.ToJson(derived, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        latestReport = $"Created {path}\nTemplate ID: {derived.templateId}\nRuntime scene: {derived.sceneName}\nEdit targetRoutes or targetPairIds, plus each world's dialogue lines.";
    }

    private static Dictionary<string, WorldPairId> CollectInteractionPairs(Transform worldRoot)
    {
        // 只收集带 FurnitureInteraction2D 的 pair，避免纯装饰物体影响关卡文本校验。
        return worldRoot.GetComponentsInChildren<WorldPairId>(true)
            .Where(pair => !string.IsNullOrWhiteSpace(pair.PairId) &&
                           pair.GetComponentInChildren<FurnitureInteraction2D>(true) != null)
            .GroupBy(pair => pair.PairId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> CollectFurniturePairIds(LevelTemplateData template)
    {
        return (template?.furniture ?? new List<FurnitureDialogueData>())
            .Where(dialogue => dialogue != null && !string.IsNullOrWhiteSpace(dialogue.pairId))
            .Select(dialogue => dialogue.pairId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateTargetHasDialogue(
        string pairId,
        HashSet<string> furniturePairIds,
        List<string> errors)
    {
        if (!furniturePairIds.Contains(pairId))
        {
            errors.Add("Target candidate is missing a furniture dialogue entry: " + pairId);
        }
    }

    private static void ValidateNormalTargetPair(
        string pairId,
        Dictionary<string, WorldPairId> normalPairs,
        List<string> errors)
    {
        if (!normalPairs.TryGetValue(pairId, out WorldPairId normal))
        {
            errors.Add("Normal target candidate is missing " + pairId);
        }
        else
        {
            WorldInteractionTarget2D unifiedTarget = normal.GetComponent<WorldInteractionTarget2D>();
            if (unifiedTarget != null && unifiedTarget.World == WorldKind2D.Special)
            {
                errors.Add("Special unified target marker is attached to NormalWorld " + pairId);
            }

            if (normal.GetComponent<SpecialWorldInteractionTarget2D>() != null)
            {
                errors.Add("Special target marker is attached to NormalWorld " + pairId);
            }
        }
    }

    private static void ValidateSpecialTargetPair(
        string pairId,
        Dictionary<string, WorldPairId> specialPairs,
        List<string> errors)
    {
        if (!specialPairs.TryGetValue(pairId, out WorldPairId special))
        {
            errors.Add("Special target candidate is missing " + pairId);
        }
        else
        {
            WorldInteractionTarget2D unifiedTarget = special.GetComponent<WorldInteractionTarget2D>();
            if (unifiedTarget != null && unifiedTarget.World == WorldKind2D.Normal)
            {
                errors.Add("Normal unified target marker is attached to SpecialWorld " + pairId);
            }

            if (special.GetComponent<NormalWorldVisionTarget2D>() != null)
            {
                errors.Add("Normal target marker is attached to SpecialWorld " + pairId);
            }
        }
    }

    private static IEnumerable<string> GetNormalTargetCandidatePairIds(LevelTemplateData template)
    {
        if (template?.targetRoutes != null && template.targetRoutes.Count > 0)
        {
            // 新格式优先：targetRoutes 可以明确表达普通目标和特殊目标的一一对应。
            return template.targetRoutes
                .Where(route => route != null && !string.IsNullOrWhiteSpace(route.normalTargetPairId))
                .Select(route => route.normalTargetPairId)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        return (template?.targetPairIds ?? new List<string>())
            .Where(pairId => !string.IsNullOrWhiteSpace(pairId))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetSpecialTargetCandidatePairIds(LevelTemplateData template)
    {
        if (template?.targetRoutes != null && template.targetRoutes.Count > 0)
        {
            // SpecialWorld 候选目标也从 targetRoutes 读取，保证校验和运行时选择逻辑一致。
            return template.targetRoutes
                .Where(route => route != null && !string.IsNullOrWhiteSpace(route.specialTargetPairId))
                .Select(route => route.specialTargetPairId)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        List<string> candidates = template?.specialTargetPairIds != null && template.specialTargetPairIds.Count > 0
            ? template.specialTargetPairIds
            : template?.targetPairIds;
        return (candidates ?? new List<string>())
            .Where(pairId => !string.IsNullOrWhiteSpace(pairId))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static Transform FindRoot(Scene scene, string rootName)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(gameObject => gameObject.name == rootName);
        return root != null ? root.transform : null;
    }
}
