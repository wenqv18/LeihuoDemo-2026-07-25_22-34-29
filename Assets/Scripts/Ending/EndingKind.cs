/// <summary>
/// 三结局类型。
/// </summary>
public enum EndingKind
{
    None = 0,
    Death = 1, // 结局一：玩家死亡，尸体进入第 9 层
    Clear = 2, // 结局二：通关 9 层
    Fuse = 3   // 结局三：集齐 4 道具并与尸体融合
}
