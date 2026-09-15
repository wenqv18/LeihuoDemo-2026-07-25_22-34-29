using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 扫描 Assets/Resources/UI/sound 下所有音效 clip，实测导入后的响度（active RMS），
/// 计算各 clip 到统一目标响度的补偿增益，写入 SfxLoudnessProfile 资产。
/// 菜单：Tools/Audio/Generate SFX Loudness Profile
/// </summary>
public static class SfxLoudnessProfileGenerator
{
    private const string SoundFolder = "Assets/Resources/UI/sound";
    private const string OutputAsset = SoundFolder + "/SfxLoudnessProfile.asset";
    private const float TargetRmsDb = -18f;
    private const float MinGain = 0.05f;
    private const float MaxGain = 8f;

    [MenuItem("Tools/Audio/Generate SFX Loudness Profile")]
    public static void Generate()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { SoundFolder });
        List<AudioClip> clips = new List<AudioClip>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || IsBgm(clip.name))
            {
                continue;
            }

            clips.Add(clip);
        }

        SfxLoudnessProfile profile = AssetDatabase.LoadAssetAtPath<SfxLoudnessProfile>(OutputAsset);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<SfxLoudnessProfile>();
            AssetDatabase.CreateAsset(profile, OutputAsset);
        }

        profile.targetRmsDb = TargetRmsDb;
        profile.entries.Clear();

        for (int i = 0; i < clips.Count; i++)
        {
            AudioClip clip = clips[i];
            float activeRmsDb = MeasureActiveRmsDb(clip);
            float gain = Mathf.Pow(10f, (TargetRmsDb - activeRmsDb) / 20f);
            gain = Mathf.Clamp(gain, MinGain, MaxGain);
            profile.entries.Add(new SfxLoudnessProfile.ClipEntry
            {
                clipName = clip.name,
                activeRmsDb = activeRmsDb,
                gain = gain
            });
            Debug.Log($"[SfxLoudness] {clip.name}: active={activeRmsDb:F1}dB -> gain={gain:F2}");
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SfxLoudness] Profile generated: {clips.Count} clips, target={TargetRmsDb}dB");
    }

    private static bool IsBgm(string clipName)
    {
        return clipName.Contains("BGM")
            || clipName.Contains("恐怖气氛")
            || clipName == "bgm_crawling_danger";
    }

    private static float MeasureActiveRmsDb(AudioClip clip)
    {
        if (clip == null)
        {
            return -120f;
        }

        if (!clip.LoadAudioData())
        {
            Debug.LogWarning($"[SfxLoudness] LoadAudioData failed: {clip.name}");
        }

        float[] samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0))
        {
            return -120f;
        }

        int frameLen = Mathf.Max(1, (int)(clip.frequency * 0.05f));
        int n = samples.Length;
        double sumPower = 0.0;
        long activeFrames = 0;
        for (int i = 0; i + frameLen <= n; i += frameLen)
        {
            double frameSum = 0.0;
            for (int j = 0; j < frameLen; j++)
            {
                float sample = samples[i + j];
                frameSum += sample * sample;
            }

            double frameRms = System.Math.Sqrt(frameSum / frameLen);
            if (frameRms > 0.001f)
            {
                sumPower += frameRms * frameRms;
                activeFrames++;
            }
        }

        if (activeFrames == 0)
        {
            return -120f;
        }

        double activeRms = System.Math.Sqrt(sumPower / activeFrames);
        return 20f * (float)System.Math.Log10(activeRms + 1e-12);
    }
}
