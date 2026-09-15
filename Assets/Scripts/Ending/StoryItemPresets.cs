/// <summary>
/// 结局三所需的 4 个预设道具（id 1~4）。名称/描述为占位，图片先留空。
/// </summary>
public static class StoryItemPresets
{
    public static void RegisterAll(StoryInventoryManager manager)
    {
        if (manager == null)
        {
            return;
        }

        manager.RegisterItemData(new StoryInventoryItemData
        {
            id = 1,
            itemName = "颅骨解剖记录",
            description = "封面上印着骷髅头骨的实验记录本，纸页上沾满了暗红色的血迹。",
            imagePath = "UI/Items/RecordBook_01"
        });
        manager.RegisterItemData(new StoryInventoryItemData
        {
            id = 2,
            itemName = "肌肉组织标本记录",
            description = "详细绘制着腿部肌肉与骨骼的标本记录，边角磨损严重，有暗红色的手印。",
            imagePath = "UI/Items/RecordBook_02"
        });
        manager.RegisterItemData(new StoryInventoryItemData
        {
            id = 3,
            itemName = "胸腔解剖记录",
            description = "记录着人体胸腔肋骨结构的实验档案，封面上有飞溅的血迹。",
            imagePath = "UI/Items/RecordBook_03"
        });
        manager.RegisterItemData(new StoryInventoryItemData
        {
            id = 4,
            itemName = "神经通路实验记录",
            description = "画满神经元分支图案的禁忌研究记录，红色的线条像血管一样蔓延。",
            imagePath = "UI/Items/RecordBook_04"
        });
    }
}
