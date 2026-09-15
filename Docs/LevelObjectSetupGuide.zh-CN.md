# 关卡物体制作与脚本使用说明

本文档说明当前项目里如何制作关卡内常用物体：视界领域目标、可交互家具、NPC、掉落物、门、玩家与 UI。目标是让之后继续搭关卡时，能直接知道该给物体挂什么脚本、放在哪个世界、每个字段大概是什么意思。

## 一、关卡基础结构

正式关卡建议使用如下基础结构：

```text
Level_01
├─ Player
├─ Main Camera
├─ NormalWorld
│  └─ ...
├─ SpecialWorld
│  └─ ...
└─ Door / 其他关卡物体
```

关卡场景名建议使用 `Level_01`、`Level_02` 这种格式。`TutorialLevel` 是教学关卡，也会被当作游戏关卡处理。

关卡里必须有：

- `NormalWorld`：正常世界根节点。
- `SpecialWorld`：特殊世界根节点。
- `Player`：玩家物体，带玩家移动和碰撞相关组件。
- `Main Camera`：主摄像机，Tag 应为 `MainCamera`。

运行时 `LevelSceneBootstrapper` 会自动补齐一些系统：

- `UIControllerSceneLoader`：加载 `UIController` 场景。
- `WorldSwapManager2D`：视界领域与世界切换管理器。
- `WorldSwapHoldController2D`：鼠标长按确认视界领域。
- `CameraFollow2D`：摄像机跟随玩家。
- `EventSystem`：UI 点击所需。
- `Global Light 2D`：如果场景内没有 2D 全局光，会尝试补一个。

## 二、视界领域目标

当前用来制作视界领域目标的脚本是：

```text
Assets/Scripts/Camera/NormalWorldVisionTarget2D.cs
```

### 如何制作目标

1. 在 `NormalWorld` 下找到“特殊世界目标对应的正常世界物体”。
2. 给这个物体添加 `NormalWorldVisionTarget2D`。
3. 确保该物体或其子物体有 `Collider2D` 或 `SpriteRenderer`。
4. `Coverage Threshold` 默认是 `0.1`，表示视界领域覆盖目标范围 10% 以上就算成功。
5. `Log On Found` 可以保持开启，成功时会在 Console 输出成功信息。

注意：虽然玩法上玩家是在找 SpecialWorld 里的异常目标，但当前代码的检测逻辑是在 `WorldSwapManager2D.TryFindCoveredVisionTarget` 中扫描 `NormalWorld` 下的 `NormalWorldVisionTarget2D`。也就是说，现在要让视界领域成功，目标标记必须挂在 `NormalWorld` 对应物体上。

### SpecialWorld 返回目标标记

另一个脚本是：

```text
Assets/Scripts/Camera/SpecialWorldInteractionTarget2D.cs
```

这个脚本目前只是标记，没有行为逻辑。它的用途不是参与视界领域检测，而是标记 `SpecialWorld` 中之后用于“玩家回到 NormalWorld”的目标物体。

也就是说，现在同一个关卡目标通常会有两个标记：

- `NormalWorld` 对应物体：挂 `NormalWorldVisionTarget2D`，用于判断视界领域是否成功找到目标。
- `SpecialWorld` 对应物体：挂 `SpecialWorldInteractionTarget2D`，暂时只作为之后回到 NormalWorld 的标记，不和视界领域交互。

如果 SpecialWorld 里的目标还需要被点击、对话或拾取，需要另外再挂对应交互脚本，例如 `FurnitureInteraction2D`、`NpcInteraction2D` 或 `DropPickupInteraction2D`。

### 玩家如何使用视界领域

1. 在关卡内按键盘上方数字 `1` 进入摄像头/视界模式。
2. 鼠标会变成摄像头框。
3. 长按鼠标左键约 `1.2s` 进行确认。
4. 如果覆盖到了 `NormalWorldVisionTarget2D` 且覆盖率达到阈值，则成功：
   - 生成视界领域矩形区域。
   - `WorldSwapManager2D.HasFoundVisionTarget` 变为 true。
   - Door 可以允许进入下一关。
5. 如果没有找到目标，则失败：
   - 不生成视界领域。
   - San UI 扣除一次。
   - 摄像机会震动一次。
   - 失败 3 次后主副世界反转。

## 三、视界领域与交互限制

所有重要交互都应该遵守同一个原则：

```text
只有和玩家当前所在世界一致，并且没有被视界领域区域碰到，才允许交互。
```

当前 `WorldSwapManager2D.IsInteractionAllowedForPlayer(transform)` 负责这个判断。

已接入这个判断的脚本：

- `FurnitureInteraction2D`
- `NpcInteraction2D`
- `DropPickupInteraction2D`
- `DoorLevelTransition2D`

所以如果一个物体被视界领域覆盖到，即使只覆盖一小部分，也不应该继续对话、点击或拾取。

## 四、可交互家具

脚本位置：

```text
Assets/Scripts/Interactions/FurnitureInteraction2D.cs
```

适合对象：

- 桌子
- 书架
- 水桶
- 可检查家具
- 鼠标点击后弹出一句或多句文本的物体

### 制作步骤

1. 把家具放到正确世界根节点下，通常是 `NormalWorld`。
2. 给家具根物体添加 `Collider2D`。
3. 给家具根物体添加 `FurnitureInteraction2D`。
4. 在 `Dialogue Lines` 中填写对话文本。
5. 保持 `Require World Interaction Allowed` 开启。
6. 如果需要悬停白边，保持 `Hover Visual Mode = MaterialOutline`。
7. 如果白边不准，优先检查 Sprite 的 Physics Shape。

### 行为

- 鼠标悬停到碰撞体范围内，会显示白色轮廓。
- 鼠标左键点击，会打开 `UIController` 中的 `Talk` 对话框。
- 单段文本播放完会自动关闭。
- 多段文本需要点击对话框切换下一段，最后一段再点击关闭。
- 点击家具会有轻微缩放反馈。

### 常见字段

- `Dialogue Lines`：家具对话内容，可以一段或多段。
- `Outline Screen Pixels`：白边线宽，当前建议较小值，例如 4。
- `Require World Interaction Allowed`：必须开启，否则 SpecialWorld 的家具可能在不该交互时也能交互。
- `Log Diagnostics`：排查点击问题时开启。

## 五、NPC

脚本位置：

```text
Assets/Scripts/NPC/NpcInteraction2D.cs
```

适合对象：

- 普通 NPC
- 宝箱式交互物
- 玩家靠近后按 `F` 触发对话的对象

### 制作步骤

1. NPC 放到对应世界根节点下。
2. 根物体添加 `Collider2D`，建议设为 Trigger。
3. 添加 `NpcInteraction2D`。
4. 子物体命名为 `Button` 或 `Name`，作为头顶提示。
5. 在 `Dialogue Lines` 中填写对话。
6. `Player Layer Name` 保持 `Player`，并确保 Player 的 Layer 是 `Player`。

### 行为

- 玩家碰到 NPC 碰撞体时，显示 `Button` 或 `Name` 提示。
- 玩家离开时提示隐藏，对话框关闭。
- 玩家靠近且交互允许时，按 `F` 播放对话。
- 处于摄像头/视界模式时，不显示提示，也不能交互。

## 六、掉落物

脚本位置：

```text
Assets/Scripts/Drops/DropPickupInteraction2D.cs
```

适合对象：

- 剧情道具
- 可拾取物
- 之后要进入背包的物品

### 制作步骤

1. 掉落物放到它所属的世界下，例如 `SpecialWorld`。
2. 根物体添加 `Collider2D`，脚本会在 Awake 中强制设为 Trigger。
3. 添加 `DropPickupInteraction2D`。
4. 子物体命名为 `Name` 或 `Button`，作为靠近提示。
5. 设置物品数据：
   - `Item Id`
   - `Item Name`
   - `Item Description`
   - `Image Path`
6. 保持 `Fit Collider To Drop Sprite` 开启，脚本会尝试按可见 Sprite 调整 BoxCollider2D。

### 行为

- 玩家靠近且世界允许交互时显示提示。
- 按 `F` 后：
  - 注册物品数据到 `StoryInventoryManager`。
  - 把 `Item Id` 加入背包。
  - 销毁掉落物 GameObject。

注意：如果掉落物在 `SpecialWorld`，玩家必须处于 SpecialWorld 作为当前主世界时才能交互。

## 七、门与通关

脚本位置：

```text
Assets/Scripts/Level/DoorLevelTransition2D.cs
```

适合对象：

- 关卡出口
- 门
- 进入下一关的触发物

### 制作步骤

1. Door 添加 `Collider2D`，脚本会设为 Trigger。
2. 添加 `DoorLevelTransition2D`。
3. 子物体命名为 `Button` 或 `Name`，作为靠近提示。
4. 设置 `Next Scene Name`，例如 `Level_02`。
5. `Require Vision Target Found` 保持开启。

### 行为

- 玩家靠近时显示提示。
- 按 `F` 时：
  - 如果 `Require Vision Target Found` 开启，会检查 `WorldSwapManager2D.HasFoundVisionTarget`。
  - 找到目标后进入下一关。
  - 没找到目标则不进入，并在 Console 输出提示。

如果 `Next Scene Name` 留空，脚本会尝试从当前场景名推断下一关，例如 `Level_01` 推断为 `Level_02`。

## 八、对话框

脚本位置：

```text
Assets/Scripts/TalkPanelButtonHandler.cs
```

对话框主要在 `UIController` 场景里的 `Talk` 使用。

### 行为

- 自动优先寻找 `UIController` 场景里的 `Talk`。
- 支持 DOTween 打字机效果。
- 单段文本播放完后延迟自动关闭。
- 多段文本需要点击对话框进入下一段。
- 家具、NPC、教学 NPC 都会复用这个对话框。

制作交互物时，一般不需要手动拖 `TalkPanel`，脚本会通过 `FindPreferredPanel()` 自动找。

## 九、背包

核心脚本：

```text
Assets/Scripts/StoryInventoryManager.cs
Assets/Scripts/StoryInventoryUIController.cs
Assets/Scripts/StoryInventoryCellUI.cs
```

### 运行方式

- `GameUIController` 负责按 `B` 打开/关闭背包。
- `StoryInventoryManager` 保存已拥有物品 id。
- `StoryInventoryUIController` 负责刷新 `Package` UI。
- `StoryInventoryCellUI` 负责每个格子的名称、图片和选中状态。

### 物品数据

目前物品只需要：

- `id`
- `imagePath`
- `itemName`
- `description`

掉落物拾取时会自动把这些数据注册进背包。

## 十、UIController 与游戏内 UI

核心脚本：

```text
Assets/Scripts/UI/GameUIController.cs
Assets/Scripts/UIControllerSceneLoader.cs
Assets/Scripts/UI/SanBrainUIController.cs
```

### 当前规则

- 正式关卡运行时会 additive 加载 `UIController`。
- `GameUI` 是关卡长期显示 UI。
- `San` 显示视界失败次数。
- `Settings` 是 ESC 菜单。
- `Package` 是背包。
- `Death` 是死亡界面。

### 按键

- `ESC`：打开/关闭游戏内设置菜单。
- `B`：打开/关闭背包。
- `1`：进入/退出摄像头视界模式。

## 十一、教学关卡 NPC

脚本位置：

```text
Assets/Scripts/Tutorial/TutorialNpcDialogue2D.cs
```

适合对象：

- 只用于 `TutorialLevel` 的教学 NPC。

### 用法

`TutorialNpcDialogue2D` 使用阶段制：

- `Start Dialogue`：阶段开始时播放。
- `Required Task Id`：当前阶段需要完成的任务 id。
- `Incomplete Dialogue`：任务没完成时再次对话播放。
- `Complete Dialogue`：任务完成后交付时播放。

完成某阶段后，会立刻把下一阶段的 `Start Dialogue` 接在后面播放。

当前内置任务例子：

- `TakePhoto`：玩家使用一次视界领域后完成。
- `ClickTable`：需要其他脚本或物体调用 `TutorialTaskRegistry.SetCompleted("ClickTable", true)`。

## 十二、推荐制作流程

### 普通可检查家具

1. 放到 `NormalWorld`。
2. 添加 `Collider2D`。
3. 添加 `FurnitureInteraction2D`。
4. 填写 `Dialogue Lines`。
5. 确认 `Require World Interaction Allowed` 开启。

### NPC

1. 放到正确世界。
2. 添加 Trigger `Collider2D`。
3. 添加 `NpcInteraction2D`。
4. 子物体添加 `Button` 或 `Name` 提示。
5. 填写 `Dialogue Lines`。

### 掉落物

1. 放到正确世界，例如 `SpecialWorld`。
2. 添加 `Collider2D`。
3. 添加 `DropPickupInteraction2D`。
4. 填写 item 数据。
5. 子物体添加 `Name` 或 `Button` 提示。

### 通关门

1. 添加 Trigger `Collider2D`。
2. 添加 `DoorLevelTransition2D`。
3. 填写 `Next Scene Name`。
4. 保持 `Require Vision Target Found` 开启。

### 视界领域目标

1. 找到 `NormalWorld` 中与 SpecialWorld 异常目标位置对应的物体。
2. 添加 `NormalWorldVisionTarget2D`。
3. 保证有 `Collider2D` 或 `SpriteRenderer`。
4. `Coverage Threshold` 设为 `0.1` 左右。
5. 在 SpecialWorld 的对应物体上额外挂 `SpecialWorldInteractionTarget2D`，作为之后玩家回到 NormalWorld 的标记；它暂时不参与视界领域成功判定。

## 十三、常见问题

### 视界领域盖住目标但失败

检查目标脚本是否挂在 `NormalWorld` 下。当前代码只扫描 `NormalWorld` 的 `NormalWorldVisionTarget2D`。

### 目标覆盖率不准

`NormalWorldVisionTarget2D` 优先用 `Collider2D.bounds` 判断范围，没有碰撞体时才用 `Renderer.bounds`。如果判断范围不对，优先调整目标物体的 Collider2D。

### SpecialWorld 的家具不该交互却能交互

检查 `FurnitureInteraction2D.Require World Interaction Allowed` 是否开启。

### NPC 或掉落物不显示 Name/Button

检查：

- 子物体是否叫 `Name` 或 `Button`。
- Player Layer 是否为 `Player`。
- 当前玩家世界是否和物体世界一致。
- 物体是否被视界领域区域碰到。
- 是否处于摄像头视界模式。

### Door 靠近后不进入下一关

检查：

- 是否已经成功找到视界目标。
- `Next Scene Name` 是否在 Build Settings 中。
- `Require Vision Target Found` 是否符合当前关卡需求。

### 背包不显示拾取物

检查：

- 掉落物是否成功调用 `DropPickupInteraction2D.Pickup()`。
- `Item Id` 是否大于等于 0 且没有重复逻辑问题。
- `UIController` 场景里的 `Package` 结构是否仍然保留 `Content`、`Thing`、详情文本和 ESC 关闭按钮。
