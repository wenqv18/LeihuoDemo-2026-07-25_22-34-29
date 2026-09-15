using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Start Scene 的 LastRecordPanel 结果展示：
/// Result 显示上一轮结局；Hint 独立显示当前是否存在可追踪尸体。
/// 在 TypewriterEffect 打字机启动前写入对应文本。
/// </summary>
[DisallowMultipleComponent]
public sealed class LastRecordPanelController : MonoBehaviour
{
    [SerializeField] private Text resultText;
    [SerializeField] private Text hintText;
    [SerializeField] private TypewriterEffect typewriter;

    private void Awake()
    {
        ResolveReferences();
        ApplyResult();
    }

    private void ResolveReferences()
    {
        if (resultText == null)
        {
            Transform result = transform.Find("Result");
            resultText = result != null ? result.GetComponent<Text>() : null;
        }

        if (hintText == null)
        {
            Transform hint = transform.Find("Hint");
            hintText = hint != null ? hint.GetComponent<Text>() : null;
        }

        if (typewriter == null)
        {
            typewriter = GetComponent<TypewriterEffect>();
        }
    }

    private void ApplyResult()
    {
        ApplyLine(resultText, BuildResultText());
        ApplyLine(hintText, BuildHintText());
    }

    private void ApplyLine(Text target, string text)
    {
        if (target == null)
        {
            return;
        }

        if (typewriter != null && typewriter.fullTexts != null && typewriter.textLines != null)
        {
            for (int i = 0; i < typewriter.textLines.Length && i < typewriter.fullTexts.Length; i++)
            {
                if (typewriter.textLines[i] == target)
                {
                    typewriter.fullTexts[i] = text;
                    return;
                }
            }
        }

        target.text = text;
    }

    private static string BuildResultText()
    {
        EndingKind ending = EndingService.GetLastEnding();
        if (ending == EndingKind.Clear)
        {
            return "你通关了 9 层";
        }

        if (ending == EndingKind.Fuse)
        {
            return "你与尸体融合";
        }

        if (ending == EndingKind.Death || RunSaveService.GetLatestCorpse() != null)
        {
            int floor = EndingService.GetLastDeathFloor();
            if (floor <= 0)
            {
                // 兼容旧存档：尸体没有 diedFloor 时回退到尸体所在楼层。
                floor = RunSaveService.GetLatestCorpseFloor();
            }

            return floor > 0 ? $"你在第 {floor} 楼死亡" : "你在关卡中死亡";
        }

        return "无侵蚀体";
    }

    private static string BuildHintText()
    {
        CorpseSaveData corpse = RunSaveService.GetLatestCorpse();
        if (corpse == null)
        {
            return "未检测到遗留尸体";
        }

        int diedFloor = corpse.diedFloor > 0
            ? corpse.diedFloor
            : EndingService.GetLastDeathFloor();
        return diedFloor > 0
            ? $"第 9 层存在一具来自第 {diedFloor} 楼的尸体"
            : "第 9 层存在一具遗留尸体";
    }
}
