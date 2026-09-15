using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效统一分贝配置：记录每个音效相对统一目标响度（active RMS）所需的线性补偿增益。
/// 生成方式：Unity 菜单 Tools/Audio/Generate SFX Loudness Profile。
/// </summary>
[CreateAssetMenu(fileName = "SfxLoudnessProfile", menuName = "Audio/Sfx Loudness Profile")]
public sealed class SfxLoudnessProfile : ScriptableObject
{
    [Serializable]
    public sealed class ClipEntry
    {
        public string clipName;
        public float activeRmsDb;
        public float gain = 1f;
    }

    [Tooltip("统一目标响度（active RMS dBFS）")]
    public float targetRmsDb = -18f;

    public List<ClipEntry> entries = new List<ClipEntry>();

    private Dictionary<string, ClipEntry> lookup;

    public float GetGain(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
        {
            return 1f;
        }

        if (lookup == null)
        {
            lookup = new Dictionary<string, ClipEntry>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                ClipEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.clipName) || lookup.ContainsKey(entry.clipName))
                {
                    continue;
                }

                lookup.Add(entry.clipName, entry);
            }
        }

        return lookup.TryGetValue(clipName, out ClipEntry found) ? found.gain : 1f;
    }
}
