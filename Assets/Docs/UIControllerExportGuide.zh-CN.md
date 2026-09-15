# UIController 导出说明

本文档说明 `UIController` 场景与相关脚本的用途，方便导入到其他工程或交给其他开发者继续维护。

## 导出内容

核心场景：

- `Assets/Scenes/UIController.unity`

核心脚本：

- `Assets/Scripts/UI/GameUIController.cs`
- `Assets/Scripts/UI/Runtime/UIControllerSceneLoader.cs`
- `Assets/Scripts/UI/DeathPanelButtonBinder.cs`
- `Assets/Scripts/UI/SanBrainUIController.cs`
- `Assets/Scripts/UI/SpecialSanNumberGlitch.cs`
- `Assets/Scripts/UI/Dialogue/TalkPanelButtonHandler.cs`
- `Assets/Scripts/UI/MainMenu/HoverDisplay.cs`
- `Assets/Scripts/Inventory/StoryInventoryUIController.cs`
- `Assets/Scripts/Inventory/StoryInventoryCellUI.cs`
- `Assets/Scripts/StoryInventoryManager.cs`

导出包使用 Unity 的 `IncludeDependencies` 方式生成，所以会一并包含 UI 场景引用到的字体、图片、视频、材质、DOTween 相关依赖和其它必要资源。

## UIController 场景根节点

`GameUI`

NormalWorld 下默认使用的游戏内 HUD。包含 SAN 显示等正常世界 UI。

`SpecialGameUI`

SpecialWorld 下使用的游戏内 HUD。进入侵蚀世界后由 `GameUIController` 自动切换显示。其 `SanNumber` 挂有 `SpecialSanNumberGlitch`，会在 `0~200` 之间高速跳数，形成失控/乱码感。

`Talk`

NormalWorld 使用的对话面板。交互家具、NPC、教学 NPC 等通过 `TalkPanelButtonHandler.FindPreferredPanel()` 自动选择它。

`SpecialTalk`

SpecialWorld 使用的对话面板。玩家处于 SpecialWorld 时，对话系统会优先选择这个面板。

`Settings`

游戏内设置界面。由 `GameUIController` 监听 `ESC` 打开或关闭。

`Package`

背包界面。由 `GameUIController` 监听 `B` 打开或关闭，并通过背包控制器刷新内容。

`Death`

死亡界面。由 `GameUIController.ShowDeath()` 打开。`DeathPanelButtonBinder` 负责绑定 Restart / Leave 两个按钮，并创建透明 `InputBlocker`，确保死亡时除这两个按钮以外的鼠标操作不会穿透到游戏或其它 UI。

`UIRuntime`

运行时 UI 管理对象，承载背包等长期存在的 UI 管理组件。

## 运行时连接方式

正式关卡不直接把 UI 做在关卡里，而是在进入玩法场景后由 `UIControllerSceneLoader` additive 加载：

```text
Level_01.unity -> additive load -> UIController.unity
```

`GameUIController` 是全局入口，负责：

- 加载 UIController 场景
- 控制 `GameUI` / `SpecialGameUI` 切换
- 控制 `Settings` / `Package` / `Death` 的打开关闭
- 暂停与恢复 `Time.timeScale`
- 处理离开主菜单和重新开始

## NormalWorld / SpecialWorld UI 切换

`WorldSwapManager2D` 切换主世界时会触发 `PrimaryWorldChanged`。

`GameUIController` 监听该事件：

- 当前世界为 `Normal`：显示 `GameUI`，隐藏 `SpecialGameUI`
- 当前世界为 `Special`：显示 `SpecialGameUI`，隐藏 `GameUI`
- 切换时关闭当前打开的 Talk，避免普通对话框残留到另一个世界

## 对话面板选择规则

`TalkPanelButtonHandler.FindPreferredPanel()` 会根据当前玩家世界选择面板：

- `NormalWorld`：选择 `Talk/Talk`
- `SpecialWorld`：选择 `SpecialTalk/Talk`

交互脚本一般不需要手动拖对话框引用。它们会在运行时调用 `FindPreferredPanel()`。

## SAN 显示

普通 SAN 控制器 `SanBrainUIController` 只绑定 `GameUI/San`，不会误绑定 `SpecialGameUI/San`。

SpecialWorld 的 SAN 数字由 `SpecialSanNumberGlitch` 独立控制：

- 默认范围：`0~200`
- 默认变化间隔：`0.025~0.08` 秒
- 使用 `Time.unscaledTime`，即使暂停或 UI 时间变化也会继续跳动

## 死亡界面输入规则

死亡界面打开时：

- `Death` Canvas 使用很高的 sorting order
- `CanvasGroup.blocksRaycasts = true`
- 自动确保存在透明全屏 `InputBlocker`
- `Restart` / `Leave` 按钮会排在 InputBlocker 之上

这意味着死亡状态下，除 Restart / Leave 两个按钮以外，玩家不能点击场景物体、背包、设置或其它 UI。

## 菜单按钮悬停动画

`HoverDisplay` 是统一的 DOTween 菜单按钮悬停脚本，用于 Start Scene 和 Death 菜单按钮：

- 默认隐藏白色横向背景
- 鼠标进入时展开到 `maxWidth`
- 鼠标离开时收回并隐藏
- 使用 `SetUpdate(true)`，所以死亡暂停界面中也能播放
- `OnDisable` / `OnDestroy` 会清理 tween，避免 DOTween 残留

## 导入注意事项

- 导入工程需要有 DOTween。
- 需要保留 `SceneNames.UIScene` 指向 `UIController`。
- 正式玩法场景需要由 `LevelSceneBootstrapper` 或等价逻辑确保加载 `UIController`。
- 如果其它工程没有 `WorldSwapManager2D`，Normal/Special UI 切换事件需要重新接入。
- 如果只想复用 UI 外观，不需要运行时逻辑，可以只使用 `UIController.unity` 的 Canvas 根节点。
