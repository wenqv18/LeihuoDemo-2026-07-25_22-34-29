using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class FurnitureDisplayName
{
    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        { "printer", "打印机" },
        { "table", "桌子" },
        { "chair", "椅子" },
        { "computer", "电脑" },
        { "cup", "杯子" },
        { "door", "门" },
        { "electricalpanel", "配电箱" },
        { "hanginglamp", "吊灯" },
        { "lamp", "吊灯" },
        { "bookshelf", "书架" },
        { "map", "地图" },
        { "memo", "便签" },
        { "minibox", "小箱子" },
        { "box", "箱子" },
        { "book", "书" },
        { "securitycamera", "监控" },
        { "camera", "监控" },
        { "forgefurniture", "储物柜" },
        { "shelf", "置物架" },
        { "water", "饮水机" },
        { "window", "窗户" },
    };

    private static readonly List<KeyValuePair<string, string>> SortedMap = Map
        .OrderByDescending(kv => kv.Key.Length)
        .ToList();

    public static string Get(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return string.Empty;
        }

        string normalized = Normalize(objectName);

        foreach (var kv in SortedMap)
        {
            if (normalized.Contains(kv.Key))
            {
                return kv.Value;
            }
        }

        return objectName;
    }

    private static string Normalize(string name)
    {
        string result = name.Trim();
        result = Regex.Replace(result, @"\d+$", "");
        result = Regex.Replace(result, "Special", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "Left", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "Right", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"[\s_]+", "");
        return result.ToLower();
    }
}
