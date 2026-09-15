# LeihuoDemo 渲染系统全链路

> 最后更新：2026-08-19 | 基于 Level_01 场景完整调查

---

## 一、渲染管线总览

```
URP Pipeline Asset (First.asset)
  └── Renderer2D Data (First_Renderer.asset)
        ├── Post Process Data (guid 41439944...)
        ├── 2D Lights (Global Light 2D)
        └── Transparency Sort: Axis Y (0,1,0)

Main Camera (orthographic, URP)
  ├── UniversalAdditionalCameraData: PostProcessing ON, AA OFF
  ├── CameraFollow2D (平滑跟随 + 冲击抖动)
  └── Volume Layer Mask: Default (仅 Default 层 Volume 生效)

Volume System (双世界切换)
  ├── __WorldVisualStyleVolume_Level01 (场景中, NormalProfile 运行时覆盖)
  └── __WorldVisualStyleVolume_Special (运行时动态创建)

Signal Overlay (ScreenSpace Canvas, SortingOrder 32000)
  ├── Noise (RawImage + SignalNoise.png)
  ├── Scanlines (RawImage + SignalScanlines.png)
  └── WaveBands (RawImage + SignalWaveBands.png)

World Swap (SpriteMask 视界领域)
  └── ActiveSwapArea (运行时创建, SpriteMask 白色 1x1)
```

---

## 二、URP 管线配置

### Pipeline Asset
- **路径**: `Assets/Volume/First.asset`
- **类型**: UniversalRenderPipelineAsset
- **Renderer**: `First_Renderer.asset` (Renderer2DData)
- **HDR**: 开启
- **MSAA**: 关闭 (1x)
- **Render Scale**: 1.0
- **主光**: Per Vertex, 阴影开启, 2048
- **附加光**: Per Vertex, 每对象最多 4 盏, 无阴影
- **阴影距离**: 50
- **Opaque Texture**: 关闭 (相机级 Option=2 即 UsePipelineSettings)
- **Depth Texture**: 关闭 (同上)

### Renderer2D Data
- **路径**: `Assets/Volume/First_Renderer.asset`
- **Transparency Sort Mode**: Default (按 Axis Y)
- **Light Render Texture Scale**: 0.5
- **Max Light RT Count**: 16
- **2D Light Blend Styles**: Multiply / Additive / Multiply+Mask / Additive+Mask
- **Renderer Features**: 无
- **Default Material**: Sprites-Default (guid a97c1056...)

> 注意：`Assets/Settings/Renderer2D.asset` 存在但未被 Pipeline Asset 引用，是遗留文件。

---

## 三、相机

### Main Camera (1)
- **场景 GameObject ID**: 1084394204
- **Tag**: MainCamera
- **Camera 组件**: Orthographic
- **UniversalAdditionalCameraData**:
  - `renderPostProcessing`: 1 (开启后期)
  - `antialiasing`: 0 (关闭)
  - `volumeLayerMask`: 1 (仅 Default 层)
  - `requiresDepthTextureOption`: 2 (UsePipelineSettings)
  - `requiresOpaqueTextureOption`: 2 (UsePipelineSettings)
  - `rendererIndex`: -1 (默认)
- **挂载脚本**: `CameraFollow2D`
  - smoothTime: 0.15s
  - PlayImpactShake(duration, amplitude): 正弦波抖动, X 轴全幅, Y 轴 45%
  - 目标自动查找 Player2DMovementController 或 "Player"

---

## 四、灯光

### Global Light 2D
- **父对象**: Global Light (空 GameObject, 仅 Transform)
- **Light2D 类型**: 4 = Global
- **颜色**: (0.685, 0.685, 0.685) 中性灰
- **Intensity**: 1
- **Falloff Intensity**: 0.5
- **Blend Style**: 0 (Multiply)
- 无其他点光源/聚光灯

---

## 五、双世界视觉系统 (WorldVisualStyle)

### 核心控制器
- **脚本**: `Assets/Scripts/Camera/WorldVisualStyleController.cs`
- **场景对象**: `__WorldVisualStyleController_Level01` (GameObject 2055368952)
- **运行时流程**:
  1. `Awake/OnEnable`: ResolveReferences → EnsureVolumes → EnsureCameraPostProcessing → ApplyCurrentWorld(true)
  2. `Update`: 非交叉淡化时每帧检查当前世界, 变化时切换 Volume 权重 + Signal 参数
  3. 交叉淡化时: 每帧 lerp 两个 Volume 权重 + Signal 5 参数

### Volume 管理
- **场景中 Volume**: `__WorldVisualStyleVolume_Level01` (GameObject 1943573451)
  - isGlobal: true, priority: 100, weight: 1
  - 编辑时 sharedProfile = SpecialWorldProfile
  - **运行时被覆盖为 NormalWorldProfile** (EnsureVolumes 中设置)
- **运行时创建 Volume**: `__WorldVisualStyleVolume_Special`
  - 动态创建为 Controller 子对象
  - isGlobal: true, priority: 100, weight: 0
  - sharedProfile = SpecialWorldProfile

### VolumeProfile 对比

| 参数 | NormalWorld | SpecialWorld |
|------|-------------|--------------|
| **ColorAdjustments** | | |
| postExposure | -0.65 | -1.0 |
| contrast | 34 | 10 |
| saturation | +6 | -15 |
| hueShift | 0 | -15 |
| colorFilter | 纯白 | 偏红 (1, 0.84, 0.84) |
| **FilmGrain** | | |
| intensity | 0.037 | 0.369 |
| response | 0.436 | 0.8 |
| type | 0 (内置) | 0 |
| **Vignette** | | |
| intensity | 0.47 | 0.529 |
| color | 纯黑 | 暗红 (0.088, 0, 0) |
| smoothness | 1 | 1 |
| **Bloom** | 无 | threshold 0.9, intensity 0.2 |

- **NormalProfile 路径**: `Assets/Settings/PostProcessing/Level01_NormalWorld_Profile.asset`
- **SpecialProfile 路径**: `Assets/Settings/PostProcessing/Level01_SpecialWorld_Profile.asset`

### 世界状态获取
- 优先: `WorldSwapManager2D.Instance.GetPrimaryWorld()`
- 回退: 场景中查找 WorldSwapManager2D
- 运行时无 Manager: `runtimeFallbackWorld = Normal`
- 编辑模式预览: `editModePreviewWorld = Special`

### 交叉淡化 (Crossfade)
- 由 `WorldSwapManager2D.StartWorldTransition(Normal, 1.6f)` 调用
- `BeginCrossfade(target, duration)`: SmoothStep 插值
- 同时 lerp: normal/special Volume 权重 + Signal 5 参数
- 仅返回 NormalWorld 时触发; 进入 SpecialWorld 用 WorldTransitionFX 冲击特效

---

## 六、信号干扰叠加层 (SignalInterferenceOverlay)

### 核心脚本
- **脚本**: `Assets/Scripts/Camera/SignalInterferenceOverlay.cs`
- **场景对象**: `__SignalInterferenceOverlay_Level01` (GameObject 1826853297)
- **Canvas**: ScreenSpaceOverlay, SortingOrder 32000 (最顶层)
- **CanvasScaler**: 1920x1080, ScaleWithScreenSize, Match 0.5

### 三个子层 (均为 RawImage, 默认 UI Material)

| 子对象 | Texture | 初始颜色 | 初始 Alpha |
|--------|---------|----------|------------|
| Noise | SignalNoise.png | 白 | 0.031 |
| Scanlines | SignalScanlines.png | 青灰 (0.75,1,0.9) | 0.008 |
| WaveBands | SignalWaveBands.png | 青绿 (0.65,1,0.86) | 0.012 |

- **纹理路径**: `Assets/Settings/PostProcessing/Signal{Noise,Scanlines,WaveBands}.png`

### 动画机制 (Update 每帧)
- **Noise**: uvRect 滚动, x=time*9.7, y=time*13.1, tiling 4.5x3.2
- **Scanlines**: uvRect y 滚动 time*0.42, tiling 1x42
- **WaveBands**: uvRect x 正弦偏移 sin(time*0.77)*0.03, y 滚动 time*0.11, tiling 1.4x1.15
- **Jitter**: RectTransform.anchoredPosition x += (sin(18.3t)+sin(41.7t+0.8)) * jitterPixels * 0.5
- **Flicker**: PerlinNoise 调制 noise/wave 的 alpha

### 双世界参数 (由 WorldVisualStyleController 配置)

| 参数 | Normal | Special |
|------|--------|---------|
| noiseAlpha | 0.009 | 0.037 |
| scanlineAlpha | 0.012 | 0.0325 |
| waveAlpha | 0.004 | 0.019 |
| jitterPixels | 0.09 | 0.55 |
| flickerAmount | 0.017 | 0.08 |

> SpecialWorld 的信号强度约为 NormalWorld 的 3-5 倍。

---

## 七、世界切换系统 (WorldSwapManager2D)

### 核心脚本
- **脚本**: `Assets/Scripts/Camera/WorldSwapManager2D.cs`
- **单例**: `WorldSwapManager2D.Instance`
- **世界根节点**: 运行时查找场景根 "NormalWorld" 和 "SpecialWorld"

### 视界领域 (SpriteMask)
- 运行时创建子对象 "ActiveSwapArea", 挂 SpriteMask
- Sprite: 运行时生成 1x1 白色纹理
- `isCustomRangeActive = true`: front=UI层32767, back=Default层-32768
- 预览时: Normal 世界 SpriteRenderer `VisibleOutsideMask`, Special 世界 `VisibleInsideMask`
- 面积大小: 匹配相机准星尺寸 (CameraFocusModeController.TryGetReticleScreenSize)

### 渲染状态切换 (ApplyWorldState)
- 主世界 SpriteRenderer: enabled=true, maskInteraction=None
- 非主世界 SpriteRenderer: enabled=false (无领域时)
- 有领域时: 主世界 VisibleOutsideMask, 非主世界 VisibleInsideMask
- `RefreshSpriteBinding`: Unity 6 兼容, mask 状态变化后重置 sprite 引用
- 拍照揭示目标: hiddenRenderers (Normal目标隐藏) + revealedRenderers (Special对应物强制显示)

### 切换过渡
- **进入 SpecialWorld**: `WorldTransitionFX.PlayEnterSpecialImpact`
  - 0-0.08s: 黑幕 alpha 0.88 + 暗红 alpha 0.4 快速压上
  - 峰值回调: SetPrimaryWorld(Special) + ApplyWorldState
  - 0.08-0.35s: 黑红淡出 + 屏幕抖动 (14px, 18频率)
- **返回 NormalWorld**: `WorldTransitionFX.PlayReturnNormalRecovery(1.6s)`
  - 黑红侵蚀残留 (alpha 0.55) 缓慢淡出
  - 轻微抖动 (2.5px, 6频率)
  - 同时 `WorldVisualStyleController.BeginCrossfade(Normal, 1.6f)`

### WorldTransitionFX
- **脚本**: `Assets/Scripts/Camera/WorldTransitionFX.cs`
- 运行时创建 ScreenSpaceOverlay Canvas (SortingOrder = short.MaxValue-10)
- 两个 Image 层: DarkLayer (黑), RedLayer (暗红 0.32,0.02,0.05)
- 依赖 DOTween

---

## 八、自定义 Shader

### SpriteOutline2D
- **路径**: `Assets/Shaders/SpriteOutline2D.shader`
- **Shader名**: "Leihuo/SpriteOutline2D"
- **用途**: 家具交互时的轮廓高亮
- **参数**: _OutlineColor, _OutlineThickness
- **实现**: 8 方向 alpha 采样取最大值, 减去本体 alpha 得到外轮廓
- **渲染**: Transparent Queue, ZWrite Off, 无光照

### VideoBlackKey
- **路径**: `Assets/Shaders/VideoBlackKey.shader`
- **用途**: 视频黑边抠像 (转场视频)

### ErosionLogo
- **路径**: `Assets/Resources/UI/Animation/ErosionLogo.shader`
- **用途**: 主菜单 Logo 侵蚀效果

---

## 九、渲染资源索引

### 配置文件
| 资源 | 路径 | guid |
|------|------|------|
| URP Pipeline Asset | Assets/Volume/First.asset | 037a660a... |
| Renderer2D Data | Assets/Volume/First_Renderer.asset | 8a833cff... |
| URP Global Settings | Assets/UniversalRenderPipelineGlobalSettings.asset | 93b439a3... |
| Normal VolumeProfile | Assets/Settings/PostProcessing/Level01_NormalWorld_Profile.asset | 55314aa9... |
| Special VolumeProfile | Assets/Settings/PostProcessing/Level01_SpecialWorld_Profile.asset | 44bdc636... |

### 信号纹理
| 资源 | 路径 | guid |
|------|------|------|
| SignalNoise | Assets/Settings/PostProcessing/SignalNoise.png | 227dbc6a... |
| SignalScanlines | Assets/Settings/PostProcessing/SignalScanlines.png | 0f9dfeb6... |
| SignalWaveBands | Assets/Settings/PostProcessing/SignalWaveBands.png | f19aedb5... |

### 场景对象 ID (Level_01)
| 对象 | GameObject ID | 关键组件 ID |
|------|---------------|-------------|
| Main Camera (1) | 1084394204 | Camera 1084394207, UACD 1084394205 |
| Global Light | 1009083524 | - |
| Global Light 2D | 1014131581 | Light2D 1014131583 |
| __WorldVisualStyleVolume_Level01 | 1943573451 | Volume 1943573452 |
| __WorldVisualStyleController_Level01 | 2055368952 | Controller 2055368953 |
| __SignalInterferenceOverlay_Level01 | 1826853297 | Canvas 1826853300, Overlay 1826853298 |
| ├ Noise | 813206090 | RawImage 813206091 |
| ├ Scanlines | 1691883623 | RawImage 1691883624 |
| └ WaveBands | 1397910584 | RawImage 1397910585 |

---

## 十、关键脚本索引

| 脚本 | 路径 | 职责 |
|------|------|------|
| WorldVisualStyleController | Assets/Scripts/Camera/ | 双世界 Volume 权重切换 + Signal 参数配置 + 交叉淡化 |
| SignalInterferenceOverlay | Assets/Scripts/Camera/ | 信号干扰三层 UV 动画 + jitter + flicker |
| WorldSwapManager2D | Assets/Scripts/Camera/ | 双世界 SpriteRenderer 显隐 + SpriteMask 视界领域 + 切换触发 |
| WorldTransitionFX | Assets/Scripts/Camera/ | 世界切换全屏 Overlay 冲击/恢复特效 |
| CameraFollow2D | Assets/Scripts/Camera/ | 相机平滑跟随 + 冲击抖动 |
| CameraFocusModeController | Assets/Scripts/Player/ | 相机模式/准星 (WorldSwap 面积匹配来源) |

---

## 十一、渲染优化切入点速查

| 想优化什么 | 去哪里改 |
|-----------|---------|
| 画面整体色调/曝光/对比 | VolumeProfile (Normal/Special) 的 ColorAdjustments |
| 颗粒感强度 | VolumeProfile 的 FilmGrain.intensity |
| 暗角 | VolumeProfile 的 Vignette |
| 泛光 | SpecialProfile 的 Bloom (Normal 无 Bloom) |
| 信号噪点强度 | WorldVisualStyleController 的 noiseAlpha (Normal/Special 两组) |
| 信号抖动幅度 | WorldVisualStyleController 的 jitterPixels |
| 信号闪烁 | WorldVisualStyleController 的 flickerAmount |
| 信号纹理/样式 | SignalInterferenceOverlay 子对象的 Texture + 脚本中 uvRect 动画参数 |
| 世界切换速度 | WorldSwapManager2D.StartWorldTransition 中的 duration (返回 Normal 1.6s) |
| 切换冲击效果 | WorldTransitionFX 的 DOTween 序列 |
| 相机跟随平滑度 | CameraFollow2D.smoothTime |
| 相机抖动 | CameraFollow2D.PlayImpactShake |
| 全局光亮度/颜色 | Global Light 2D 的 Light2D 组件 |
| 渲染分辨率/质量 | First.asset (Pipeline Asset) 的 RenderScale / MSAA / HDR |
| 2D 光照 | First_Renderer.asset 的 Light RT Scale / Max Light Count |
| 家具描边 | SpriteOutline2D.shader + 对应材质的 _OutlineThickness |
