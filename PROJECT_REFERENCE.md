# LeihuoDemo 项目完整参考

> 最后更新：2026-08-19 | 基于全量代码审查（75 个脚本 + 9 个场景 + Resources/配置）

---

## 一、项目概述

2D 俯视角双世界恐怖探索游戏。玩家在 NormalWorld 中探索，通过相机拍照揭示 SpecialWorld 的视界领域，进入 SpecialWorld 后有 SAN 值限制和倒计时，需找到恢复目标返回。共 9 层（当前实现 3 个关卡场景循环使用 18 个 JSON 模板），三个结局。

**核心技术栈**：Unity 6000.4.10f1 + URP 2D Renderer + DOTween + 自定义 PNG 序列帧动画（非 Mecanim）+ JSON 数据驱动关卡 + PlayerPrefs/JSON 存档。

---

## 二、场景清单

| 场景 | 用途 | 状态 |
|------|------|------|
| **Start Scene** | 主菜单：新的轮回/继续/背包/设置/教程 5 按钮，LastRecordPanel 播报上轮死亡楼层 | ✅ 正式 |
| **Level_01** | 第 1/4/7 层：有 NormalWorld、Player、Door×2、相机/灯光/Volume/信号层，**无 SpecialWorld 根节点** | ✅ 正式 |
| **Level_02** | 第 2/5/8 层：有 NormalWorld + SpecialWorld 双根节点 | ✅ 正式 |
| **Level_03** | 第 3/6/9 层：第 9 层有尸体交互触发结局 | ✅ 正式 |
| **TutorialLevel** | 教程关：NPC 分阶段教学拍照+点击桌子，与 run-save 隔离，死亡直接重开本场景 | ✅ 正式 |
| **UIController** | UI 叠加场景：Additive 加载，含 GameUI/SpecialGameUI/Settings/Package/Death/Talk/SpecialTalk1 等 Canvas | ✅ 正式 |
| MainScence | 旧版 UI 场景（有 Settings/Content/DetailData/Eecord），已被 Start Scene + UIController 取代 | ⚠️ 遗留 |
| SampleScene | Unity 默认空场景 | ❌ 无用 |
| New Scene | 只有一个 Main Camera 的空场景 | ❌ 无用 |

### 场景加载流程
```
Start Scene → (新的轮回/继续) → Level_01 (Single)
                                  ↓ UIControllerSceneLoader 自动 Additive 加载 UIController
                             门交互 F → RunSaveService.TryAdvanceToNextFloor → Level_02/03
                             第 9 层门 → EndingService.TryTriggerEnding(Clear)
死亡 → PlayerVideoAnimator.PlayDeath → RunSaveService.RecordCurrentPlayerDeath → Death 面板
结局三(Fuse) → CorpseInteraction → EndingService.TryTriggerEnding(Fuse)
```

---

## 三、脚本完整索引（75 个，按系统分类）

### 3.1 关卡与场景引导（Scripts/Level/）

| 脚本 | 职责 |
|------|------|
| **SceneNames** | 常量类：StartScene/UIScene/FirstLevel/SecondLevel/ThirdLevel/TutorialLevel，IsGameplayScene 判断 |
| **LevelSceneBootstrapper** | 运行时关卡引导：EnsureRuntimeForScene 在场景加载后创建 Player/Camera 等缺失对象；编辑器模式下也可触发 |
| **DoorLevelTransition2D** | 门交互：玩家进入触发器按 F，需先找到视界目标，调用 RunSaveService.TryAdvanceToNextFloor 加载下一关；教程关阻止离开；第 9 层触发通关结局 |
| **WorldInteractionTarget2D** | 世界交互目标标记：Normal 世界的 normalVisionTarget（拍照目标）和 Special 世界的 specialRecoveryTarget（恢复目标），含覆盖率计算 |
| **WorldPairId** | 双世界配对 ID：挂在家具上，pairId 关联 Normal/Special 两个世界的同一物体 |
| **SpecialWorldSurvivalController2D** | SpecialWorld 生存机制：20s 倒计时、SAN 耗尽死亡、长按鼠标左键 1s 在恢复目标 3m 内恢复 SAN 到 33、每层只能恢复一次、恢复后返回 NormalWorld |

### 3.2 关卡数据（Scripts/Level/Data/）

| 脚本 | 职责 |
|------|------|
| **LevelTemplateData** | JSON 模板数据结构：templateId/sceneName/difficulty/targetPairIds/targetRoutes/furniture[] |
| **LevelTemplateRepository** | 模板仓库：Resources.LoadAll 加载 LevelData/ 下 18 个 JSON，按 difficulty 分组，GetTemplateForFloor(floor) 按楼层选模板 |
| **LevelRuntimeConfigurator** | 运行时配置器：根据当前楼层选模板，将家具对话数据注入 FurnitureInteraction2D.dialogueLines，配置视界目标/恢复目标的激活状态 |

### 3.3 存档系统（Scripts/Save/）

| 脚本 | 职责 |
|------|------|
| **RunSaveData** | 存档数据结构：currentFloor、playerGender、sanCount、corpseFloors[]、specialWorldRecoveryUsedFloors[]、hasSave；含 DeepCopy 和 IsCorpseOnFloor |
| **RunSaveService** | 存档核心单例：persistentDataPath/run-save.json 读写；StartNewRun/ContinueRun/TryAdvanceToNextFloor/RecordCurrentPlayerDeath/RestartCurrentRun；MaxFloorCount=9；FirstLevel=Level_01；currentFloor 从 0 开始 |
| **CorpseRuntimeSpawner** | 尸体运行时生成器：加载存档后在每层生成玩家尸体（SpriteRenderer + CorpseInteraction + CorpseFollowerAI），尸体使用 Triangle 立绘 |

### 3.4 玩家系统（Scripts/Player/）

| 脚本 | 职责 |
|------|------|
| **Player2DMovementController** | 玩家移动：WASD/方向键，moveSpeed=2f，Rigidbody2D 物理移动，拍照/恢复时长按锁定移动，WorldSwapManager 交互权限控制 |
| **PlayerVideoAnimator** | 自定义序列帧动画（非 Mecanim）：Idle/Walk/Photo/Death 四状态，PNG 序列帧从 Resources/Player/AnimationTransparent/ 加载；Photo 三阶段状态机（Entering→Holding→Exiting 倒放）；Death 后回调 |
| **PlayerAppearanceController** | 玩家外观：男生/女生切换（PlayerPrefs 存储），男生用运行时创建的 Player0SequenceRenderer，女生用子对象 Triangle 立绘 |

### 3.5 相机与世界切换（Scripts/Camera/）

| 脚本 | 职责 |
|------|------|
| **CameraFollow2D** | 相机平滑跟随玩家（smoothTime=0.15s），PlayImpactShake 正弦波抖动 |
| **CameraFocusModeController** | 相机模式管理：进入/退出拍照模式，IsCameraModeActive 静态属性，准星尺寸供 WorldSwapManager 匹配 SpriteMask 面积 |
| **WorldSwapHoldController2D** | 拍照长按控制器：拍照模式下鼠标移动实时预览视界领域（SpriteMask），长按左键 1.2s 确认拍照，找到目标后退出相机模式 |
| **WorldSwapManager2D** | **核心单例**：双世界切换管理。维护 primaryWorld，SpriteMask 视界领域预览/确认，NormalWorldVisionTarget2D 覆盖率检测，SAN 值管理（初始 3，耗尽事件），进入 SpecialWorld 播放冲击特效，返回 NormalWorld 播放恢复特效+Volume 交叉淡化 |
| **WorldTransitionFX** | 世界切换全屏特效：DOTween 黑红两层 Image，进入 Special 0.35s 冲击+抖动，返回 Normal 1.6s 恢复 |
| **WorldVisualStyleController** | 双世界视觉风格：管理两个 URP Volume 权重交叉淡化，配置 SignalInterferenceOverlay 参数，Normal/Special 两组信号参数 |
| **SignalInterferenceOverlay** | 信号干扰叠加层：全屏 Canvas（SortingOrder 32000），Noise/Scanlines/WaveBands 三层 RawImage UV 滚动+jitter+flicker |
| **WorldLampLightController** | 双世界灯光控制：收集 NormalWorld/SpecialWorld 下名字含 "Lamp" 的 Light2D，按当前世界开关 |
| **LampFlickerController** | 灯管闪烁：SpecialWorld 的 Lamp 灯光非规律闪烁（短闪/长灭/连发），带开关灯音效 |
| **NormalWorldVisionTarget2D** | NormalWorld 视界目标：被 SpriteMask 覆盖率超过阈值时触发 VisionTargetFound 事件 |
| **SpecialWorldInteractionTarget2D** | 空标记类，用于 SpecialWorld 交互对象识别 |

### 3.6 交互系统（Scripts/Interactions/）

| 脚本 | 职责 |
|------|------|
| **FurnitureInteraction2D** | 家具交互：鼠标点击（非 F 键），2D 多边形碰撞检测，SpriteOutline2D 描边高亮，按世界/是否目标从 dialogueLines 四套对话中选一套播放；IsMouseOverAnyInteraction 静态方法供对话系统判断点击优先级 |
| **FurnitureProximityInteraction2D** | 家具靠近交互（F 键）：与 NPC/门类似的触发器机制，用于需要靠近才能交互的家具 |

### 3.7 NPC 与掉落物

| 脚本 | 位置 | 职责 |
|------|------|------|
| **NpcInteraction2D** | Scripts/NPC/ | NPC 对话：靠近按 F，播放 dialogueLines，离开关闭 |
| **DropPickupInteraction2D** | Scripts/Drops/ | 掉落物拾取：靠近按 F，加入 StoryInventoryManager 后销毁 |

### 3.8 结局系统（Scripts/Ending/）

| 脚本 | 职责 |
|------|------|
| **EndingKind** | 枚举：None=0/Death=1/Clear=2/Fuse=3 |
| **EndingService** | 结局服务单例：RecordEnding 存 PlayerPrefs，GetLastEnding/GetLastDeathFloor，TryTriggerEnding 触发结局（Clear/Fuse 记录后显示占位提示），HasAllFuseItems 检查 4 个道具 |
| **CorpseInteraction** | 第 9 层尸体交互：靠近按 F 弹出"是否回应侵蚀体"，点击对话框确认——道具足够→Fuse 结局，不足→死亡 |
| **CorpseFollowerAI** | 尸体游荡 AI：在玩家周围 1.5-3.5m 随机取点缓慢移动 |
| **LastRecordPanelController** | 主菜单上轮回播报：根据 EndingService/RunSaveService 生成"你在第 N 楼死亡"等文本，替换 TypewriterEffect 对应行 |
| **StoryItemPresets** | 结局三所需 4 个预设道具（id 1-4，占位名称/描述） |

### 3.9 背包系统（Scripts/Inventory/）

| 脚本 | 职责 |
|------|------|
| **StoryInventoryManager** | 背包单例（DontDestroyOnLoad）：ownedItemIds 列表，PlayerPrefs 持久化，RegisterItemData/AddItem/HasItem，Changed 事件 |
| **StoryInventoryUIController** | 背包 UI：动态生成 cell 列表，选中显示名称/描述/图标，ESC 关闭 |
| **StoryInventoryCellUI** | 背包格子：显示道具名/图标，选中高亮，点击通知 owner |
| **StoryInventorySpriteLoader** | 道具图标加载：Resources.Load<Sprite>，编辑器下回退 AssetDatabase |

### 3.10 UI 系统（Scripts/UI/）

| 脚本 | 职责 |
|------|------|
| **GameUIController** | **UI 总控单例**：RuntimeInitializeOnLoadMethod 自动初始化，Additive 加载 UIController 场景，管理 GameUI/SpecialGameUI/Settings/Package/Death 五个面板显隐，ESC 设置/B 背包快捷键，打开面板时 Time.timeScale=0，世界切换时关闭对话 |
| **UIControllerSceneLoader** | UIController 场景加载器：Additive 加载后禁用该场景的 Camera/AudioListener/EventSystem |
| **TalkPanelButtonHandler** | 对话框核心：打字机效果（DOTween DOText），点击任意处推进（家具优先），FindPreferredPanel 按当前世界选 Talk/SpecialTalk1，BoxClickOnly 模式（尸体确认），PlayAutoClosingLine 单行自动关闭 |
| **TypewriterEffect** | 多行打字机（主菜单 LastRecordPanel 用）：逐行逐字显示，带打字音效 |
| **SanBrainUIController** | SAN 值 UI：SetBrainState(remaining,max) 控制 Brain 图标显隐+数字+Slider |
| **SanSliderImageToggle** | SAN 滑块视觉切换：根据 SAN 值切换滑块/背包格子的正常/危险 Sprite 和文字颜色（稳定/不稳定/危险） |
| **SpecialSanNumberGlitch** | SpecialWorld SAN 数字乱码：随机数字快速跳动 |
| **DeathPanelButtonBinder** | 死亡面板按钮绑定：Leave→回主菜单，Restart→重开，自动创建 InputBlocker 拦截点击 |
| **PackageTabController** | 背包三页签：背包/结局/人物切换 |
| **EndingTabController** | 结局页签：End1/2/3 按钮悬停放大+点击弹出 props 显示结局文本（占位） |
| **PlayerSelectBinder** | 人物页签：Boy/Girl 按钮切换 PlayerAppearanceController |
| **SettingsPanel** | 主菜单设置面板：三个音量滑块（总/音效/BGM），DOTween 缩放开关 |
| **ButtonClickSoundBinder** | 按钮点击音效自动绑定：每 0.75s 扫描 UIController 场景所有 Button 绑定点击音 |
| **ButtonHoverSoundBinder** | 按钮悬停音效自动绑定：扫描 Button，给有 HoverDisplay/HoverFade 的按钮设 hoverSound，给有其他悬停效果的加 HoverSoundTrigger |
| **ButtonHoverFadeBackground** | 按钮悬停背景淡入淡出（DOTween） |
| **SettingsContinueLeaveHover** | 设置面板 Continue/Leave 悬停联动：Leave 悬停时背景切红色+文字变红+填充动画 |
| **ErosionLogoShaderDriver** | 主菜单 Logo 侵蚀动画：驱动 ErosionLogo.shader 的 _ErosionAmount/_GlitchAmount/_SignalFlash |
| **HoverDisplay** | 主菜单按钮悬停展开：targetRect 宽度从 0 扩展到 maxWidth，显示 backgroundObj |
| **HoverFade** | 通用悬停淡入淡出：target Graphic alpha 渐变 |
| **RestartTransition** | 新的轮回确认流程：播放 RestartAnim（按钮滑出+LastRecordPanel 滑入），显示确认面板，No 重置/Yes 开始新游戏 |
| **VideoTransition** | 继续游戏转场：播放 GCanimation + 视频，视频结束后加载关卡 |
| **TutorialLevelButton** | 教程按钮：点击加载 TutorialLevel 场景 |

### 3.11 教程系统（Scripts/Tutorial/）

| 脚本 | 职责 |
|------|------|
| **TutorialLevelRuntimeInstaller** | 运行时给 TutorialLevel 的 NPCBody 挂 TutorialNpcDialogue2D |
| **TutorialTaskRegistry** | 教程任务注册表（静态字典）：SetCompleted/IsCompleted，场景加载清空 |
| **TutorialNpcDialogue2D** | 教程 NPC 分阶段对话：两阶段（拍照教学→点击桌子教学），每阶段有开始/未完成/完成三套对话，最终完成后 0.5s 返回主菜单 |
| **TutorialTaskCompleters** | 教程任务自动完成器：监听 FurnitureInteraction2D.InteractionPlayed（ClickTable）、NormalWorldVisionTarget2D.VisionTargetFound（TableFound）、WorldSwapManager2D.SpecialWorldEntered |
| **TutorialVisionUseTaskCompleter** | 拍照任务完成器：监听 VisionUseConfirmed 事件标记 TakePhoto 完成 |
| **TutorialTaskMarker** | 任务标记器：OnEnable/Start 时标记任务完成（用于场景中放置的触发器） |

### 3.12 音频系统（Scripts/Audio/）

| 脚本 | 职责 |
|------|------|
| **GameAudioManager** | 全局音频单例：BGM（Normal/Special 切换）+ Ambient（SpecialWorld 恐怖氛围）+ SFX 三通道，音量滑块 0-100（50=基准），PlayerPrefs 持久化，AudioListener.volume 控总音量 |
| **SfxLoudnessProfile** | ScriptableObject：每个音效的 RMS 响度补偿增益，统一音效音量 |

### 3.13 效果脚本（Scripts/Effects/）

| 脚本 | 职责 |
|------|------|
| **FloatingPromptTween** | 上下浮动动画（DOTween Yoyo），用于交互提示 |
| **CardHoverHideChild2D** | 鼠标悬停隐藏子对象（卡牌背景），OnMouseEnter/Exit |

### 3.14 编辑器工具（Scripts/Editor/）

| 脚本 | 职责 |
|------|------|
| **PlayerSpriteImportFixer** | 编辑器：批量修正玩家序列帧导入设置 |
| **SfxLoudnessProfileGenerator** | 编辑器：Tools/Audio 菜单，分析音效 RMS 生成 SfxLoudnessProfile |

### 3.15 遗留脚本（Scripts/Legacy/）

| 脚本 | 职责 |
|------|------|
| **MainMenuController** | 旧版主菜单控制器，已被 RestartTransition/VideoTransition/SettingsPanel 取代 |
| **MainMenuSettingsButton** | 旧版设置按钮 |
| **LevelPauseMenuController** | 旧版暂停菜单，已被 GameUIController 取代 |

---

## 四、核心系统架构

### 4.1 游戏主循环

```
Start Scene
  ├─ RestartTransition (新的轮回) → RunSaveService.StartNewRun → Level_01
  ├─ VideoTransition (继续) → RunSaveService.ContinueRun → 上次楼层
  ├─ TutorialLevelButton → TutorialLevel
  ├─ PackageTabController (背包按钮) → GameUIController.OpenPackageFromMainMenu
  └─ SettingsPanel (设置按钮) → GameUIController.OpenSettingsFromMainMenu

Level_XX (Single) + UIController (Additive, 自动)
  ├─ Player (WASD 移动 + 序列帧动画)
  ├─ NormalWorld/SpecialWorld (双世界根节点，Level_01 无 SpecialWorld)
  │   ├─ Furniture (WorldPairId + FurnitureInteraction2D + 4套对话)
  │   ├─ NPC (NpcInteraction2D)
  │   ├─ Door (DoorLevelTransition2D)
  │   ├─ Drop (DropPickupInteraction2D)
  │   └─ Lamp (Light2D + LampFlickerController)
  ├─ Camera (CameraFollow2D + WorldSwapHoldController2D + CameraFocusModeController)
  ├─ Global Light 2D
  ├─ __WorldVisualStyleController + Volume + SignalOverlay
  ├─ SpecialWorldSurvivalController2D (倒计时/恢复/死亡)
  └─ WorldLampLightController

UIController (Additive)
  ├─ GameUI (Normal HUD: SAN Brain + Slider)
  ├─ SpecialGameUI (Special HUD: SAN 乱码数字)
  ├─ Settings (ESC)
  ├─ Package (B: 背包/结局/人物三页签)
  ├─ Death (死亡面板)
  ├─ Talk (NormalWorld 对话框)
  └─ SpecialTalk1 (SpecialWorld 对话框)
```

### 4.2 双世界切换链路

```
玩家按住右键进入拍照模式 (CameraFocusModeController)
  → 鼠标移动: WorldSwapManager2D.PreviewArea(mousePos)
    → SpriteMask 位置/尺寸更新，光圈内显示 SpecialWorld
  → 长按左键 1.2s: WorldSwapManager2D.ConfirmArea(mousePos)
    → 检测 NormalWorldVisionTarget2D 覆盖率 ≥ 10%
    → 找到目标: HasFoundVisionTarget=true, 揭示 SpecialWorld 对应物
    → 未找到目标: 拍照失败音效
  → 松开左键或退出拍照

玩家在 SpecialWorld 中:
  → SpecialWorldSurvivalController2D 启动 20s 倒计时
  → SAN 随时间消耗，耗尽→死亡（若未使用恢复）
  → 靠近 specialRecoveryTarget 3m 内长按左键 1s → 恢复 SAN 到 33
  → 每层只能恢复一次，第二次 SAN 耗尽→死亡
  → 恢复后 WorldSwapManager2D.ReturnToNormalWorld
    → WorldTransitionFX 1.6s 恢复特效
    → WorldVisualStyleController 1.6s Volume+Signal 交叉淡化
```

### 4.3 对话系统链路

```
交互入口                    面板选择                     显示
─────────                   ──────                      ────
FurnitureInteraction2D ─┐
NpcInteraction2D ───────┤
DoorLevelTransition2D ───┼→ TalkPanelButtonHandler ─→ DOText 打字机
CorpseInteraction ───────┤   .FindPreferredPanel()      点击推进
TutorialNpcDialogue2D ───┘   按当前世界选 Talk/SpecialTalk1
                             BoxClickOnly (尸体确认)
                             PlayAutoClosingLine (单行2s自动关)
```

对话文本来源：
- 家具：LevelTemplateData.furniture[].dialogueLines（4套：normal/special × 普通/目标），由 LevelRuntimeConfigurator 注入
- NPC：Inspector 配置的 dialogueLines
- 门/教程：代码内硬编码或 Inspector 配置

### 4.4 存档数据流

```
StartNewRun → RunSaveData { currentFloor=0, sanCount=3, corpseFloors=[] } → run-save.json
TryAdvanceToNextFloor → currentFloor++ → 选模板 → LoadScene
RecordCurrentPlayerDeath → corpseFloors.Add(currentFloor) → EndingService.RecordEnding(Death, floor+1)
ContinueRun → 读 run-save.json → currentFloor 对应场景
CorpseRuntimeSpawner → 每层检查 corpseFloors → 生成尸体
```

### 4.5 结局触发

| 结局 | 触发条件 | 触发位置 |
|------|---------|---------|
| Death | SAN 耗尽/倒计时结束/尸体回应道具不足 | SpecialWorldSurvivalController2D / CorpseInteraction |
| Clear | 第 9 层门交互 | DoorLevelTransition2D |
| Fuse | 第 9 层尸体回应 + 集齐 4 道具 | CorpseInteraction |

---

## 五、关卡数据系统

### JSON 模板
- **路径**: `Assets/Resources/LevelData/`
- **数量**: 18 个（D1×6 + D2×6 + D3×6）
- **命名**: `{D1/D2/D3}_{sceneName}_{序号}.json`
- **选择逻辑**: `LevelTemplateRepository.GetTemplateForFloor(floor)` — floor%3 决定难度(D1/D2/D3)，floor/3 决定同难度内第几个模板

### 模板结构
```json
{
  "templateId": "D1_Level_01_01",
  "sceneName": "Level_01",
  "difficulty": 1,
  "targetPairIds": ["pair_id_1"],
  "targetRoutes": ["..."],
  "furniture": [
    {
      "pairId": "furniture_pair_id",
      "dialogueLines": {
        "normalLines": ["..."],
        "specialLines": ["..."],
        "normalTargetLines": ["..."],
        "specialTargetLines": ["..."]
      }
    }
  ]
}
```

---

## 六、渲染系统速查

详见 `RENDER_PIPELINE_REFERENCE.md`。关键点：
- URP 2D Renderer，HDR 开，MSAA 关，RenderScale 1.0
- 双 Volume 交叉淡化（NormalProfile/SpecialProfile）
- SignalInterferenceOverlay 全屏三层 RawImage
- 自定义 Shader：SpriteOutline2D（描边）、VideoBlackKey（视频抠像）、ErosionLogo（Logo 侵蚀）

---

## 七、资源目录结构

```
Assets/
├── Scenes/                    # 9 个场景
├── Scripts/                   # 75 个 C# 脚本（见上方索引）
├── Resources/
│   ├── LevelData/             # 18 个关卡 JSON 模板
│   ├── Player/AnimationTransparent/  # 玩家序列帧（idle/walk/camera_raise/death）
│   ├── UI/
│   │   ├── sound/             # 所有音效（BGM/SFX/按钮/脚步/门/灯/怪物）
│   │   └── Animation/         # ErosionLogo shader + 动画剪辑
│   └── ...
├── Settings/
│   ├── PostProcessing/        # 两个 VolumeProfile + 3 张信号纹理
│   ├── Renderer2D.asset       # 遗留 Renderer（未使用）
│   └── ...
├── Volume/
│   ├── First.asset            # URP Pipeline Asset（实际使用）
│   └── First_Renderer.asset   # Renderer2D Data（实际使用）
├── Shaders/                   # SpriteOutline2D.shader, VideoBlackKey.shader
├── Art/                       # 美术资源（Sprite/材质等）
└── ...
```

---

## 八、关键单例与静态入口

| 单例/静态类 | 获取方式 | 生命周期 |
|------------|---------|---------|
| RunSaveService | RunSaveService.Instance / Current | 场景加载时 EnsureInitialized |
| WorldSwapManager2D | WorldSwapManager2D.Instance | 场景中对象 |
| GameAudioManager | GameAudioManager.Instance | RuntimeInitializeOnLoadMethod |
| GameUIController | GameUIController 静态方法 | RuntimeInitializeOnLoadMethod |
| StoryInventoryManager | StoryInventoryManager.Instance | DontDestroyOnLoad |
| EndingService | EndingService 静态方法 | 纯静态 PlayerPrefs |
| CameraFocusModeController | .Instance / .IsCameraModeActive | 场景中对象 |

---

## 九、输入控制汇总

| 按键 | 功能 | 锁定条件 |
|------|------|---------|
| WASD/方向键 | 移动 | 拍照模式/恢复长按/面板打开 |
| 鼠标左键 | 拍照长按确认 / SpecialWorld 恢复长按 / 对话推进 / UI 点击 | — |
| 鼠标右键 | 进入拍照模式 | — |
| F | 家具/NPC/门/掉落物交互 | 拍照模式/非当前世界 |
| ESC | 设置面板 | — |
| B | 背包面板 | — |

---

## 十、PlayerPrefs 键值

| Key | 内容 |
|-----|------|
| Leihuo.RunSaveV2 | run-save JSON（currentFloor/corpseFloors/sanCount/gender） |
| Leihuo.EndingState | 结局类型 (int) |
| Leihuo.DeathFloor | 死亡楼层 (int) |
| Leihuo.Audio.Master/Sfx/Bgm | 音量设置 (float 0-100) |
| StoryInventory.Items | 背包道具 ID 列表 JSON |
| PlayerGender | 玩家性别 (int 0=男/1=女) |
