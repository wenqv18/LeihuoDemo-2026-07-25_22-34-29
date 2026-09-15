using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 关卡模板读取入口。
/// 项目把易变的关卡文本、目标路线、难度配置放在 Resources/LevelData 下，
/// 运行时代码只通过这个仓库读取，避免每个系统自己拼路径或解析 JSON。
/// </summary>
public static class LevelTemplateRepository
{
    private const string ResourceFolder = "LevelData/";

    /// <summary>
    /// 按场景名读取同名 JSON。
    /// 主要用于编辑器校验和单场景预览：打开 Level_01 时，先找 LevelData/Level_01.json。
    /// </summary>
    public static bool TryLoad(string sceneName, out LevelTemplateData template)
    {
        template = null;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        TextAsset asset = Resources.Load<TextAsset>(ResourceFolder + sceneName);
        if (asset == null)
        {
            return false;
        }

        try
        {
            template = JsonUtility.FromJson<LevelTemplateData>(asset.text);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[LevelTemplateRepository] Invalid JSON for {sceneName}: {exception.Message}");
            return false;
        }

        return template != null && !string.IsNullOrWhiteSpace(template.templateId);
    }

    /// <summary>
    /// 按稳定 templateId 查找模板。
    /// 存档里记录的是 templateId，而不是 JSON 文件名；这样文件重命名时更容易做兼容。
    /// </summary>
    public static bool TryLoadById(string templateId, out LevelTemplateData template)
    {
        template = LoadAll().FirstOrDefault(candidate =>
            string.Equals(candidate.templateId, templateId, StringComparison.OrdinalIgnoreCase));
        return template != null;
    }

    /// <summary>
    /// 找出能运行在指定 Unity 场景上的所有模板。
    /// 九层流程复用三张场景，所以同一个 sceneName 下面可能对应多个模板。
    /// </summary>
    public static IReadOnlyList<LevelTemplateData> LoadCompatibleWithScene(string sceneName)
    {
        return LoadAll()
            .Where(template => string.Equals(template.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// 加载所有模板并按 templateId 去重。
    /// 去重是一个保护层：如果 Resources 里意外出现重复模板，运行时取第一个稳定结果。
    /// </summary>
    public static IReadOnlyList<LevelTemplateData> LoadAll()
    {
        List<LevelTemplateData> templates = new List<LevelTemplateData>();
        TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourceFolder.TrimEnd('/'));
        for (int i = 0; i < assets.Length; i++)
        {
            TextAsset asset = assets[i];
            try
            {
                LevelTemplateData template = JsonUtility.FromJson<LevelTemplateData>(asset.text);
                if (template != null && !string.IsNullOrWhiteSpace(template.templateId))
                {
                    templates.Add(template);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LevelTemplateRepository] Invalid JSON in {asset.name}: {exception.Message}");
            }
        }

        return templates
            .GroupBy(template => template.templateId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(template => template.templateId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
