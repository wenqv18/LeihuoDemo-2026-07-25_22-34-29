# 视界区域与对话系统：故障总结和后续实现指南

本文档记录 `SampleScene` 中已经出现并解决的两个核心问题，供后续 Codex、其他模型或开发者接手时快速定位。

不要只根据 Inspector 表面状态猜原因。两个问题都出现过“字段看起来正确，但运行结果错误”的情况，必须在 Play Mode 中检查真实渲染和状态变化。

## 一、系统边界和术语

### 1. 视界区域

- 玩家长按鼠标左键 2 秒后确认一个矩形区域。
- 未确认前不预览另一个世界。
- 同一时间最多存在一个视界区域。
- 新确认区域会替换旧区域。
- 每成功确认一次计数一次。
- 第三次确认后，NormalWorld 和 SpecialWorld 的主副关系反转，并清空当前视界区域。
- 主世界在区域外显示，副世界在区域内显示。
- 区域内必须显示副世界的完整结果，包括背景和前景，不是只替换某一个物体。

### 2. 世界与交互

- `NormalWorld` 和 `SpecialWorld` 是两套平行世界内容。
- 交互物体只有在它当前属于玩家可接触的世界时才能交互。
- 世界管理器可以决定交互物体是否有效，但不直接管理全局 UI。

### 3. 对话框

- `Canvas/Talk` 是全局 UI。
- 它不属于 `NormalWorld` 或 `SpecialWorld`。
- 它不能参与 SpriteMask，也不能被世界 Renderer 开关直接控制。
- 对话框可以因为“发起对话的交互物体失效”而关闭，但这是交互状态导致的关闭，不是把 UI 当成世界内容管理。

当前关键脚本：

- `Assets/Scripts/WorldSwapManager2D.cs`
- `Assets/Scripts/WorldSwapHoldController2D.cs`
- `Assets/Scripts/TalkInteractionTrigger2D.cs`
- `Assets/Scripts/TalkPanelButtonHandler.cs`

---

## 二、问题一：世界反转后出现微型背景图

### 现象

出现过以下现象：

1. 视界区域只覆盖宝箱一部分时，宝箱的局部看起来像变成了背景贴图。
2. 第三次确认、主副世界反转后，小人仍在，但小人位置还出现一张微型背景图。
3. 移动相关 SpriteRenderer，再执行 Undo，有时异常会消失。
4. Inspector 中 `SpecialWorld/Front/GameObject` 的 Sprite 始终显示为 `C01`，没有真的变成背景 Sprite。

### 已验证的事实

运行态隔离 Renderer 后得到以下结论：

- 关闭 `SpecialWorld/Front/GameObject` 的 SpriteRenderer，微型背景立即消失。
- SpriteRenderer 的 `sprite` 仍然是 `C01`。
- Sprite 对应纹理仍然是 `C01`。
- 仅切换 SpriteRenderer 的 `enabled` 无法修复。
- 将 Sprite 暂时设为 `null`，再重新赋回同一个 Sprite，异常立即消失，小人正常显示。

因此这不是以下问题：

- Sprite 资源被修改。
- Transform 位置错误。
- NormalWorld 宝箱没有关闭。
- SpecialWorld 背景真的生成了一个额外对象。
- 单纯的 Sorting Layer 配置错误。

### 根因

当前实现通过修改每个 SpriteRenderer 的 `maskInteraction` 在以下状态之间切换：

- `None`
- `VisibleInsideMask`
- `VisibleOutsideMask`

Unity 6000.4 的 URP 2D Renderer 在 SpriteMask 状态切换后，可能保留错误的内部纹理或批处理绑定。

此时脚本和 Inspector 读取到的 Sprite 都是正确的，但 GPU 实际绘制仍可能使用上一个批次中的背景纹理。移动物体或 Undo 会触发 Renderer 重建，所以异常偶尔自行消失。

### 当前解决方案

每次改变 SpriteRenderer 的世界显示状态后，重新绑定当前 Sprite：

```csharp
private static void RefreshSpriteBinding(SpriteRenderer spriteRenderer)
{
    Sprite sprite = spriteRenderer.sprite;
    if (sprite == null)
    {
        return;
    }

    spriteRenderer.sprite = null;
    spriteRenderer.sprite = sprite;
}
```

调用顺序必须是：

1. 设置 `spriteRenderer.enabled`。
2. 设置 `spriteRenderer.maskInteraction`。
3. 调用 `RefreshSpriteBinding(spriteRenderer)`。

当前调用位置在 `WorldSwapManager2D.ApplyFrontState()`。

### 不要采用的修补方式

- 不要直接替换 Sprite 资源。
- 不要通过移动 Transform 再移回来刷新。
- 不要依赖 Undo。
- 不要每帧创建新 Material。
- 不要在 `Update()` 中每帧重新绑定 Sprite。
- 不要把背景排除在视界区域之外，因为设计要求区域内显示完整的另一个世界。

### 当前方案的适用范围

当前重绑方案适合：

- SpriteRenderer 数量不大。
- 遮罩状态只在玩家确认视界区域时变化。
- 场景主要由普通 SpriteRenderer 构成。

如果后续加入大量以下内容，应考虑升级渲染方案：

- Animated SpriteRenderer
- TilemapRenderer
- ParticleSystemRenderer
- Shader Graph 特殊材质
- 大量动态 Sprite
- 多个复杂形状的视界区域

### 推荐的长期方案

当项目进入正式制作阶段，推荐用双 Camera 或 RenderTexture 合成：

1. NormalWorld 由 Normal Camera 渲染。
2. SpecialWorld 由 Special Camera 渲染。
3. 两套结果分别进入 RenderTexture。
4. 最终由一个屏幕空间材质按照矩形遮罩合成。
5. 全局 UI 在合成之后单独渲染。

优点：

- 不需要逐个修改 SpriteRenderer 的 `maskInteraction`。
- 背景、前景、Tilemap、粒子可以统一裁切。
- 不会出现单个 Renderer 继承错误纹理绑定的问题。
- 世界和 UI 的渲染边界更清楚。

在当前原型阶段，现有 SpriteMask 加 Sprite 重绑方案可以继续使用。

### 验收步骤

每次修改世界切换后至少验证：

1. 第一次确认区域，区域内完整显示 SpecialWorld。
2. 区域只覆盖宝箱一部分，边界两侧分别显示对应世界内容。
3. 移动视界区域，旧区域完全恢复。
4. 第三次确认后区域清空。
5. 第三次确认后 SpecialWorld 全屏显示。
6. 小人显示为 `C01`，其位置没有微型背景。
7. Unity Console 没有 Error 或 Warning。

---

## 三、问题二：Button 出现但对话框无法正常打开

### 现象

运行时曾出现：

- 玩家与 NormalWorld 宝箱碰撞体重叠。
- `playerInside == true`。
- `worldInteractionAllowed == true`。
- Button 提示可以显示。
- 按 `F` 后看起来没有任何对话。
- 手动调用 `TalkPanelButtonHandler.Play()` 时，`Talk.activeSelf` 短暂变为 `true`，下一帧又变回 `false`。
- 即使 `Talk.activeSelf == true`，它也可能仍然看不见。

这个问题实际由两个独立原因叠加造成。

### 根因 A：两个世界的触发器争用同一个对话框

NormalWorld 和 SpecialWorld 的 `Front/GameObject` 都挂有 `TalkInteractionTrigger2D`，并且都引用同一个 `Canvas/Talk`。

原逻辑在交互物体不可用时每帧执行：

```csharp
talkPanel.Close();
```

结果：

1. NormalWorld 触发器检测到玩家按 `F`。
2. NormalWorld 打开 `Canvas/Talk`。
3. SpecialWorld 触发器因为当前不可交互而执行 `Close()`。
4. 对话框在下一帧被关闭。

这是一种共享 UI 的所有权竞争，不是输入系统错误，也不是碰撞检测错误。

### 根因 B：根 Canvas 自身处于关闭状态

场景中的 `Canvas` 根物体序列化为 inactive。

原逻辑只执行：

```csharp
Talk.gameObject.SetActive(true);
```

这只能改变 `Talk.activeSelf`，不能激活 inactive 的父物体。

因此可能出现：

```text
Talk.activeSelf == true
Talk.activeInHierarchy == false
```

文本已经正确设置为“你好。”，但整个 Canvas 仍然不参与渲染，所以玩家看不到。

### 当前解决方案 A：对话框记录发起者

`TalkPanelButtonHandler` 保存当前对话的 owner：

```csharp
private TalkInteractionTrigger2D owner;
```

打开时传入发起者：

```csharp
public void Play(
    TalkInteractionTrigger2D dialogueOwner,
    string[] dialogueLines)
{
    owner = dialogueOwner;
    // 初始化并显示对话。
}
```

关闭时检查请求者：

```csharp
public void Close(TalkInteractionTrigger2D dialogueOwner)
{
    if (owner != null && owner != dialogueOwner)
    {
        return;
    }

    Close();
}
```

交互触发器必须使用：

```csharp
talkPanel.Play(this, dialogueLines);
talkPanel.Close(this);
```

不能再由任意触发器调用无条件 `Close()`。

### 当前解决方案 B：打开对话时激活所属 Canvas

`TalkPanelButtonHandler` 在打开前查找并激活父 Canvas：

```csharp
private void ResolveParentCanvas()
{
    if (parentCanvas == null)
    {
        parentCanvas = GetComponentInParent<Canvas>(true);
    }
}
```

打开时：

```csharp
ResolveParentCanvas();
if (parentCanvas != null)
{
    parentCanvas.gameObject.SetActive(true);
}

gameObject.SetActive(true);
```

关闭时只关闭 `Talk`，不要关闭整个 Canvas。

原因：

- Canvas 是全局 UI 容器。
- 后续可能还有其他 UI 放在同一个 Canvas 下。
- 对话结束不应该误伤其他 UI。

### 正确的职责边界

`WorldSwapManager2D`：

- 判断某个世界中的交互物体当前是否可交互。
- 不直接显示或隐藏 `Canvas/Talk`。
- 不对 Canvas 设置 SpriteMask。
- 不把 Canvas 收集进 NormalWorld 或 SpecialWorld 的 Renderer 列表。

`TalkInteractionTrigger2D`：

- 检测玩家是否进入交互范围。
- 根据世界状态决定当前交互是否有效。
- 控制自己的 Button 提示。
- 按 `F` 时以自己为 owner 发起对话。
- 玩家离开或该交互物体失效时，请求关闭自己发起的对话。

`TalkPanelButtonHandler`：

- 管理全局对话 UI。
- 记录当前 owner。
- 管理文本数组和当前行号。
- 点击时显示下一句。
- 最后一行之后关闭。
- 拒绝其他交互物体关闭当前对话。

### “对话跟随 Button 消失”的准确含义

以下情况应关闭当前对话：

- 玩家离开发起者的碰撞范围。
- 发起者被视界区域切换到玩家不可交互的世界。
- 发起者所在世界不再是玩家当前可交互世界。
- 玩家点击播放完最后一句。

以下情况不应关闭当前对话：

- 另一个世界中的无关触发器处于 disabled interaction 状态。
- 另一个交互物体离开范围。
- 世界管理器刷新了其他物体的状态。
- 其他 UI 打开或关闭。

### 当前三句测试文本

```text
你好。
这里是第一关。
前面有危险。
```

运行验证结果：

1. 第一次打开显示“你好。”。
2. 第一次点击显示“这里是第一关。”。
3. 第二次点击显示“前面有危险。”。
4. 第三次点击关闭对话框。
5. 玩家离开时 Button 和对话框关闭。
6. 视界区域覆盖宝箱时 Button 和对话框关闭。
7. SpecialWorld 的无效触发器不会关闭 NormalWorld 正在播放的对话。

---

## 四、后续增加多个交互物体时的推荐

### 数据放在哪里

每个交互物体保存自己的对话内容。

当前原型可以继续使用：

```csharp
[TextArea]
[SerializeField] private string[] dialogueLines;
```

不要把所有关卡、所有 NPC 的文本硬编码进 `TalkPanelButtonHandler`。

当文本量增加后，推荐改成：

- 每个交互物体引用一个 `DialogueData` ScriptableObject。
- `DialogueData` 保存对话 ID、说话者、文本行、头像和后续事件。
- `TalkPanelButtonHandler` 只负责显示，不负责决定内容。

### 推荐的数据结构

```csharp
[CreateAssetMenu(menuName = "Dialogue/Dialogue Data")]
public sealed class DialogueData : ScriptableObject
{
    public string dialogueId;
    public string speakerName;

    [TextArea]
    public string[] lines;
}
```

交互物体：

```csharp
[SerializeField] private DialogueData dialogue;
```

### 推荐的进一步解耦

当交互类型增加后，可以建立统一接口：

```csharp
public interface IInteractable
{
    bool CanInteract(GameObject player);
    void Interact(GameObject player);
}
```

玩家只负责寻找当前候选交互物体并调用 `Interact()`。

宝箱、NPC、门、检查点分别实现自己的行为。对话只是其中一种交互，不要让玩家控制脚本直接依赖所有具体类型。

### 多个交互物体同时重叠

以后同一位置可能同时有多个触发器。不能让它们全部响应一次 `F`。

推荐建立一个 `InteractionCoordinator`：

1. 收集玩家范围内所有可交互物体。
2. 根据距离、朝向或优先级选择一个当前目标。
3. 只显示当前目标的 Button。
4. `F` 只发送给当前目标。
5. 视界区域变化后重新选择目标。

当前原型只有一个主要交互物体，可以暂时不建立该系统。

---

## 五、低上下文模型的排查顺序

后续模型遇到相似问题时，按以下顺序检查，不要一开始重写系统。

### A. 世界渲染异常

1. 确认当前主世界。
2. 确认是否存在活动视界区域。
3. 列出两个世界所有 SpriteRenderer 的：
   - `enabled`
   - `maskInteraction`
   - `sortingLayerName`
   - `sortingOrder`
   - `sprite.name`
   - `sprite.texture.name`
4. 单独关闭可疑 Renderer，确认异常由哪个 Renderer 画出。
5. 如果 Sprite 字段正确但画面错误，尝试重新绑定同一个 Sprite。
6. 修改后必须截图验证区域边界和第三次反转。

### B. 对话无法打开

1. 确认玩家碰撞体与触发器碰撞体真实重叠。
2. 检查：
   - `playerInside`
   - `worldInteractionAllowed`
   - Button 的 `activeSelf`
3. 检查 `talkPanel` 引用是否存在。
4. 调用播放后同时检查：
   - `Talk.activeSelf`
   - `Talk.activeInHierarchy`
   - 父 Canvas 的 `activeInHierarchy`
5. 等待几帧，再检查 Talk 是否被其他触发器关闭。
6. 检查当前 owner 是否为发起对话的触发器。
7. 检查文本内容是否已更新。
8. 最后再怀疑输入系统。

### C. 必须做的最终验证

- Unity Editor 不在编译状态。
- Play Mode 中实际验证，不只看 Inspector。
- 检查 Unity Console 的 Error 和 Warning。
- 验证无视界区域状态。
- 验证区域覆盖交互物体状态。
- 验证第三次世界反转状态。
- 验证玩家离开交互范围状态。
- 验证多句文本点击流程。

---

## 六、禁止破坏的项目约束

- 不要把 `Canvas/Talk` 移入 `NormalWorld` 或 `SpecialWorld`。
- 不要让世界 SpriteMask 影响 UI。
- 不要让任意交互物体无条件关闭共享对话框。
- 不要通过关闭 Collider2D 来表达世界不可交互，容易破坏 Trigger 状态。
- 世界不可交互应使用明确的逻辑状态，例如 `worldInteractionAllowed`。
- 不要只根据 `activeSelf` 判断 UI 是否可见，必须检查 `activeInHierarchy`。
- 不要只根据 `sprite.name` 判断实际绘制纹理一定正确。
- 不要在没有运行态验证的情况下修改 Sorting Layer、材质或 Sprite 资源。

本文档描述的是当前已验证行为。后续如果改成双 Camera/RenderTexture 合成，仍应保留“世界内容、交互资格、全局 UI”三者互相独立的架构边界。
