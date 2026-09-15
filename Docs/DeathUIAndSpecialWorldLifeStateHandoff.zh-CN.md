# 死亡界面与侵蚀世界生命状态修复交接

本文用于把本次问题的真实原因、修复边界和验证方式交给后续模型。不要继续在按钮上叠加临时监听，也不要把“电闸是否使用”写回楼层永久状态。

## 必须保持的行为

- `Death/Leave`：恢复 `Time.timeScale = 1`，加载 `Start Scene`。
- `Death/Restart`：开始一条新生命，加载 `Level_01`。
- 新生命保留本轮已经生成的九层配置、楼层随机种子、目标配置和最近一具尸体。
- 新生命更换 `lifeId`，清空本生命所有楼层的电闸使用记录，并从第一层开始。
- 同一生命、同一层的 SpecialWorld 电闸只能使用一次。
- 玩家进入 SpecialWorld 后，在尚未使用电闸时，即使再次收到 SAN 耗尽事件也不能提前死亡；只能由 20 秒超时判死。
- 使用目标电闸后 SAN 恢复到 33，并返回 NormalWorld。
- 本生命已经使用过电闸后，再次耗尽 SAN 才立即死亡。

## 之前几次修改为什么不稳定

### 0. 死亡界面的全屏 Background 吃掉了真实点击

这是死亡按钮“事件存在但鼠标无反应”的直接原因。`Death/Background` 原本排在 Leave 和 Restart 后面，绘制深度最高，并且 `Image.raycastTarget = true`。鼠标点在 Restart 上时，EventSystem 的真实首个命中对象实际是：

`Death/Background`

它不是 Button，也没有 `IPointerClickHandler`，所以事件不会继续落到视觉上位于它下面的按钮。直接调用 `button.onClick.Invoke()` 会成功，因此之前只验证代码调用时会误以为按钮已经修好。

修复后：

- `Death/Background` 被放到死亡面板的第一个子节点，确保视觉层级在按钮后面。
- Background 的 Raycast Target 永久关闭。
- `DeathPanelButtonBinder` 激活时会再次扫描面板，自动关闭所有不属于 Button 的装饰 Graphic 的 Raycast Target，防止后续美术调整重新制造透明遮挡。
- 验证必须使用 `EventSystem.RaycastAll + ExecuteEvents.pointerClickHandler`，不能只调用 `Button.onClick.Invoke()`。

### 1. 死亡按钮有两个所有者

`GameUIController` 和 `DeathPanelButtonBinder` 都在运行时给同一按钮添加监听，导致每个按钮存在两份 runtime listener。场景里又可能存在手动 Persistent Listener，形成第三条路径。重复场景加载时，这种结构很难判断到底哪条监听仍有效。

修复后：死亡按钮只由 `DeathPanelButtonBinder` 负责；`GameUIController` 只负责打开面板和场景业务，不再绑定死亡按钮。

### 2. EventSystem 被多个脚本争抢

`LevelSceneBootstrapper` 创建关卡 EventSystem，`UIControllerSceneLoader` 又禁用 UI 场景自己的 EventSystem，而旧版 `DeathPanelButtonBinder` 会在面板激活时重新启用或新建 EventSystem。运行时因此出现 `There can be only one active Event System`，鼠标事件可能被错误实例接收。

修复后：死亡面板不创建、不启用 EventSystem。关卡运行时只使用 `Level_01` 的 EventSystem；`UIController` 自带的 EventSystem 在场景中保持 inactive。

### 3. Inspector 不能绑定 static 方法

Unity Button 的 On Click 下拉列表通常不显示普通 `public static` 方法。手动绑定必须把 `Death` 物体拖入槽位，选择实例组件：

- `DeathPanelButtonBinder.OnLeaveClicked()`
- `DeathPanelButtonBinder.OnRestartClicked()`

当前 `UIController.unity` 已经保存了这两个 Persistent Listener，不需要开发者再次手动绑定。

### 4. 电闸状态曾混入楼层永久数据

楼层配置需要跨死亡保持不变，但电闸次数属于一条生命。把二者放进同一个 `FloorSaveData` 并一起保存，会导致上一条生命用过后下一条生命仍不可用。

修复后采用两层数据：

- `RunSaveData.floors`：九层的固定配置、随机种子、Normal/Special 目标和尸体。
- `RunSaveData.attemptState`：当前生命的 `lifeId` 与 `specialWorldRecoveryUsedFloors`。

`RunSaveService.RestartCurrentRun()` 只重建 `attemptState`，不会重抽九层配置。

### 5. SAN 耗尽事件错误地绕过了 20 秒规则

进入 SpecialWorld 时 SAN 已经为 0。如果这时再次触发一次失败，旧逻辑在看到 `args.World == Special` 后立即死亡，因此玩家明明还没使用电闸，也没有等满 20 秒，就进入死亡界面。

修复后的判断顺序：

1. 如果本生命本层已经使用过恢复，SAN 耗尽立即死亡。
2. 如果尚未使用且玩家已经在 SpecialWorld，设置 `CancelDefaultResolution = true`，保持或启动 20 秒倒计时，不死亡。
3. 尚未进入 SpecialWorld 的正常 SAN 耗尽，交回 `WorldSwapManager2D` 执行进入侵蚀世界。

## 关键文件

- `Assets/Scripts/UI/DeathPanelButtonBinder.cs`：死亡按钮的唯一事件所有者。
- `Assets/Scripts/UI/GameUIController.cs`：显示死亡面板、暂停、离开和重开入口。
- `Assets/Scripts/Save/RunSaveService.cs`：九层配置与单生命状态的边界，重点是 `RestartCurrentRun()`。
- `Assets/Scripts/Level/SpecialWorldSurvivalController2D.cs`：20 秒倒计时、电闸恢复和死亡判定。
- `Assets/Scripts/Camera/WorldSwapManager2D.cs`：世界切换、SAN 状态和恢复到 33。
- `Assets/Scenes/UIController.unity`：死亡按钮 Persistent Listener 与 inactive EventSystem。

## 修改后的验证清单

每次改动上述链路后，至少完整验证以下流程：

1. 从 `Level_01` 进入 Play Mode，确认只有一个 active EventSystem，Console 不出现重复 EventSystem 错误。
2. 打开 Death 后，在 Restart 和 Leave 中心执行 UI Raycast，首个命中对象必须位于对应按钮层级内，不能是 `Death/Background`。
3. 进入 SpecialWorld，确认倒计时从 20 秒开始。
4. 在未用电闸时再次制造 SAN 耗尽，确认 `CancelDefaultResolution == true`、死亡面板未出现、倒计时仍运行。
5. 与启用的 `SpecialWorldInteractionTarget2D` 电闸交互，确认 SAN 为 33、世界为 Normal、当前生命本层被标记已使用。
6. 再次耗尽 SAN，确认死亡面板出现并且 `Time.timeScale == 0`。
7. 通过 PointerClick 点击 Restart，等待场景加载完成，确认 `lifeId` 改变、电闸记录清空、九层配置签名不变、`Time.timeScale == 1`，并确认关卡 EventSystem 已重新建立。
8. 再次进入 SpecialWorld，确认电闸可重新使用一次。
9. 通过 PointerClick 点击 Leave，确认最终活动场景为 `Start Scene`。

## 禁止回退到的做法

- 不要让 `GameUIController` 和 `DeathPanelButtonBinder` 同时绑定死亡按钮。
- 不要在死亡面板脚本里创建或重新启用 EventSystem。
- 不要依赖按钮名称在全场景盲搜后直接绑定；优先使用 `Death` 上序列化的 Button 引用。
- 不要让全屏背景、暗色遮罩或其他非 Button Graphic 保持 Raycast Target；透明度为 0 也照样会拦截点击。
- 不要用 `FloorSaveData.specialWorldRecoveryUsed` 作为当前生命的真实判断来源；该字段只为旧存档兼容保留。
- 不要在 Restart 时调用会重新生成 `runId`、`runSeed` 和九层配置的“新游戏”逻辑。
- 不要把 SpecialWorld 中任意一次 SAN 耗尽直接等价为死亡；必须先判断本生命恢复是否已经使用。
