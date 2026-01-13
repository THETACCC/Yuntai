# Dialogue System & Localization

一个功能完整的 Unity 对话系统，支持可视化对话树编辑、多语言本地化、条件分支、选项系统和事件触发。

## 目录

- [功能特性](#功能特性)
- [快速开始](#快速开始)
- [核心组件](#核心组件)
- [本地化系统](#本地化系统)
- [对话树编辑器](#对话树编辑器)
- [高级功能](#高级功能)
- [API 参考](#api-参考)

---

## 功能特性

### 对话系统
- ✅ **可视化对话树编辑器** - 节点式对话流程设计
- ✅ **分支对话** - 支持玩家选择和多分支剧情
- ✅ **条件判断** - 基于游戏状态的动态对话流程
- ✅ **事件系统** - 在对话过程中触发游戏事件
- ✅ **角色管理** - 统一管理对话角色和头像
- ✅ **打字机效果** - 可自定义速度的文字显示动画

### 本地化系统
- 🌍 **多语言支持** - English / 中文 / 日本語
- 📊 **Google Sheets 集成** - 从云端表格导入翻译
- 🔄 **自动 Fallback** - 缺失翻译时自动降级显示
- 🎨 **字体切换** - 根据语言自动切换字体
- 📝 **混合模式** - 支持 ID 引用和直接输入两种方式

---

## 快速开始

### 1. 打开管理窗口

```
Unity 菜单栏 → Tools → Dialogue System → Manager Window
```

### 2. 设置本地化（可选）

在管理窗口顶部：
1. 输入 Google Sheets 的 CSV 公开链接
2. 点击 **Load** 加载本地化数据
3. 系统会自动缓存数据供编辑器使用

### 3. 创建角色

在 **Characters** 区域：
1. 点击 **+ Add Character**
2. 设置角色名称和头像路径
3. 选择是否为玩家角色（用于UI区分）

### 4. 创建对话树

1. 在 **Folder Tree** 区域点击 **+ Create Tree**
2. 输入对话树文件名
3. 双击打开对话树编辑器

### 5. 在场景中使用

将 `DialogueController` 预制体添加到场景，然后：

```csharp
// 加载对话数据
DialogueController.instance.LoadDialogueFromFile(dialogueJsonFile);

// 开始对话
DialogueController.instance.StartDialogue();
```

---

## 核心组件

### DialogueController

对话系统的核心控制器，负责对话流程、UI更新和事件触发。

**主要职责：**
- 管理对话状态和流程
- 处理用户输入（空格/鼠标点击）
- 显示对话内容和选项
- 执行条件判断和事件调用

**关键方法：**

```csharp
// 加载对话JSON文件
void LoadDialogueFromFile(TextAsset dialogueJsonFile)

// 开始对话
void StartDialogue()

// 设置当前对话节点
void SetDialogueIndex(int index)

// 移动到下一个对话节点
void NextDialogueIndex()
```

### DialogueDisplaySettings

对话显示设置，控制UI外观和语言切换。

**主要功能：**
- 管理多语言字体配置
- 控制玩家/NPC分离显示模式
- 响应语言切换事件

**配置示例：**

```csharp
public bool separatePlayerAndNPC;  // 是否分离玩家和NPC的UI
public Color inactiveAvatarColor;  // 非激活状态头像颜色
```

---

## 本地化系统

### LocalizedText 类

存储多语言文本的核心数据结构。

**使用方式：**

```csharp
// 创建本地化文本
LocalizedText text = new LocalizedText();
text.SetText("en", "Hello");
text.SetText("zh", "你好");
text.SetText("ja", "こんにちは");

// 获取当前语言的文本
string displayText = text.GetText(Settings.instance.currentLanguage);

// 直接从字符串创建（默认为英语）
LocalizedText simple = new LocalizedText("Hello World");
```

**Fallback 机制：**

如果当前语言缺失文本，系统会按以下顺序尝试：
1. 当前语言
2. English
3. 中文
4. 日本語

### DialogueLocalization

从 Google Sheets 加载和管理本地化数据。

**CSV 格式要求：**

```csv
ID,中文,English,日本語
greeting_hello,你好,Hello,こんにちは
greeting_goodbye,再见,Goodbye,さようなら
```

**使用流程：**

1. **准备 Google Sheets**
   - 创建新表格，按上述格式填写
   - 文件 → 共享 → 发布到网络 → 选择CSV格式
   - 复制公开链接

2. **在编辑器中加载**
   ```
   Manager Window → 输入CSV URL → 点击 Load
   ```

3. **在代码中使用**
   ```csharp
   // 根据ID获取文本
   string text = DialogueLocalization.GetText("greeting_hello", Language.ChineseSimplified);
   
   // 检查ID是否存在
   if (DialogueLocalization.HasId("greeting_hello")) {
       // ...
   }
   ```

### 混合使用模式

系统支持两种文本管理方式：

**方式1：ID引用模式**（推荐用于需要频繁翻译更新的内容）
```csharp
node.useContentId = true;
node.contentId = "dialogue_intro_001";
// 运行时从 DialogueLocalization 获取文本
```

**方式2：直接输入模式**（适合一次性内容或测试）
```csharp
node.useContentId = false;
node.content.SetText("en", "Hello!");
node.content.SetText("zh", "你好！");
```

---

## 对话树编辑器

### 节点类型

**基础对话节点**
- 显示文本内容
- 设置角色和头像
- 配置下一个节点

**选项节点**
- 添加多个选择项
- 每个选项可跳转到不同节点
- 支持条件显示

**条件分支节点**
- 根据游戏状态自动跳转
- 支持优先级排序
- 多条件逻辑（AND/OR）

### 创建对话流程

1. **添加节点**
   - 右键 → Create Node 或使用工具栏
   - 设置节点内容和角色

2. **连接节点**
   - 从输出端口拖动到输入端口
   - 绿线=默认连接，黄线=选项连接，紫线=条件分支

3. **添加选项**
   - 在节点上点击 **+ Add Choice**
   - 设置选项文本和条件
   - 连接到目标节点

4. **添加事件**
   - 点击节点的事件按钮
   - 选择目标对象和方法
   - 设置触发时机

5. **导出运行时数据**
   - 点击工具栏的 **Export** 按钮
   - 选择保存位置
   - 生成 JSON 文件供游戏使用

---

## 高级功能

### 条件系统

对话选项和分支可以根据游戏状态显示/隐藏。

**条件类型：**

| 比较类型 | 符号 | 支持类型 |
|---------|------|---------|
| Equal | == | int, float, bool |
| NotEqual | != | int, float, bool |
| Greater | > | int, float |
| Less | < | int, float |
| GreaterOrEqual | >= | int, float |
| LessOrEqual | <= | int, float |

**示例配置：**

```csharp
ChoiceCondition condition = new ChoiceCondition {
    targetObjectID = "player_stats_id",  // 推荐：使用唯一ID
    // targetObjectName = "Player",      // 向后兼容：使用名称
    componentTypeName = "PlayerStats",
    variableName = "level",
    comparison = ComparisonType.GreaterOrEqual,
    compareValue = "5"
};
```

**多条件逻辑：**

```csharp
// AND 逻辑：所有条件都必须满足
choice.conditionLogic = ConditionLogic.AND;

// OR 逻辑：满足任一条件即可
choice.conditionLogic = ConditionLogic.OR;
```

### 事件系统

在对话过程中调用游戏中的方法。

**触发时机：**

- `OnDialogueStart` - 对话节点显示时立即触发
- `OnDialogueEnd` - 玩家点击继续后触发
- `OnDialogueDisappear` - 对话框完全消失后触发（仅最后一个节点）

**支持的参数类型：**

- None - 无参数方法
- String - 字符串参数
- Int - 整数参数
- Float - 浮点数参数
- Bool - 布尔参数

**配置示例：**

```csharp
DialogueEventCall eventCall = new DialogueEventCall {
    targetObjectID = "quest_manager_id",     // 推荐：使用唯一ID
    // targetObjectName = "QuestManager",    // 向后兼容：使用名称
    componentTypeName = "QuestManager",
    methodName = "StartQuest",
    parameterType = ParameterType.String,
    stringParameter = "main_quest_01",
    triggerTiming = EventTriggerTiming.OnDialogueEnd
};
```

**在组件中定义可调用方法：**

```csharp
public class QuestManager : MonoBehaviour {
    // 无参数方法
    public void CompleteCurrentQuest() {
        // ...
    }
    
    // 字符串参数方法
    public void StartQuest(string questId) {
        // ...
    }
    
    // 整数参数方法
    public void AddExperience(int amount) {
        // ...
    }
}
```

### 角色系统

统一管理所有对话角色。

**角色数据结构：**

```csharp
public class CharacterData {
    public string id;                    // 唯一ID（自动生成）
    public string character;             // 显示名称
    public bool useNameId;               // 是否使用本地化ID
    public string nameId;                // 本地化ID
    public LocalizedText characterName;  // 直接输入的名称
    public string avatarAssetPath;       // 头像资源路径
    public bool isPlayer;                // 是否为玩家
}
```

**使用角色：**

1. 在 Manager Window 创建角色
2. 在对话节点中选择角色
3. 导出时自动关联角色信息

### 分离式UI模式

支持区分玩家和NPC的对话显示。

**启用方式：**

```csharp
DialogueDisplaySettings.instance.separatePlayerAndNPC = true;
```

**效果：**
- 玩家对话：左侧头像高亮，右侧头像变暗
- NPC对话：右侧头像高亮，左侧头像变暗
- 名称标签自动切换显示位置

---

## API 参考

### DialogueController API

```csharp
// 单例访问
DialogueController.instance

// 加载对话数据
void LoadDialogueFromFile(TextAsset dialogueJsonFile)

// 开始对话
void StartDialogue()

// 设置对话索引
void SetDialogueIndex(int index)

// 移动到下一句
void NextDialogueIndex()

// 状态查询
bool isDialogueActive     // 对话是否正在进行
bool isDialogueFinished   // 对话是否已结束
```

### LocalizedText API

```csharp
// 创建
LocalizedText text = new LocalizedText();
LocalizedText text = new LocalizedText("Default Text");

// 设置文本
void SetText(string languageCode, string text)
void SetText(Language language, string text)

// 获取文本（带Fallback）
string GetText(string languageCode)
string GetText(Language language)

// 直接获取（不Fallback）
string GetTextDirect(string languageCode)
string GetTextDirect(Language language)

// 检查
bool HasAnyText()

// 语言代码
"en" / "english" → English
"zh" / "chinese" → 中文
"ja" / "japanese" → 日本語
```

### DialogueLocalization API

```csharp
// 加载数据
IEnumerator LoadFromGoogleSheets(Action<bool, string> onComplete)

// 获取文本
string GetText(string id, Language language)

// 检查ID
bool HasId(string id)

// 获取所有语言
Dictionary<Language, string> GetAllLanguages(string id)

// 查询状态
bool IsLoaded

// 清空缓存
void Clear()
```

### 数据结构

```csharp
// 对话数据
public class Conversation {
    public int index;
    public LocalizedText name;
    public string avatarAddr;
    public bool isPlayer;
    public LocalizedText content;
    public ConditionalBranch[] conditionalBranches;
    public Choice[] choices;
    public int nextIndex;
    public List<DialogueEventCall> eventCalls;
}

// 选项数据
public struct Choice {
    public LocalizedText text;
    public int targetIndex;
    public List<ChoiceCondition> conditions;
    public ConditionLogic conditionLogic;
}

// 条件数据
public class ChoiceCondition {
    public string targetObjectID;
    public string targetObjectName;
    public string componentTypeName;
    public string variableName;
    public ComparisonType comparison;
    public string compareValue;
}

// 事件数据
public class DialogueEventCall {
    public string targetObjectID;
    public string targetObjectName;
    public string componentTypeName;
    public string methodName;
    public ParameterType parameterType;
    public string stringParameter;
    public int intParameter;
    public float floatParameter;
    public bool boolParameter;
    public EventTriggerTiming triggerTiming;
}
```

---

## 最佳实践

### 1. 对话设计

- ✅ 使用有意义的节点名称便于管理
- ✅ 对长对话进行分段，避免单节点文本过长
- ✅ 合理使用条件分支创建动态对话
- ✅ 为重要选择添加清晰的提示

### 2. 本地化管理

- ✅ 使用 ID 引用模式管理需要频繁翻译的内容
- ✅ 在 Google Sheets 中保持ID命名规范（如 `dialogue_chapter1_001`）
- ✅ 定期备份本地化表格
- ✅ 翻译缺失时利用 Fallback 机制先显示英文

### 3. 性能优化

- ✅ 避免在对话中频繁调用复杂事件
- ✅ 对话树导出后缓存 JSON，避免重复加载
- ✅ 使用对象池管理选项按钮
- ✅ 条件判断优先使用 targetObjectID 而非名称查找

### 4. 调试技巧

- ✅ 使用 Debug.Log 追踪对话流程
- ✅ 在编辑器中测试所有分支路径
- ✅ 检查条件逻辑的边界情况
- ✅ 验证事件调用的目标对象和方法存在

---

## 常见问题

**Q: 为什么本地化加载失败？**

A: 检查以下几点：
1. Google Sheets 是否已发布为CSV
2. URL 是否正确（应包含 `/pub?output=csv`）
3. 表格第一行是否为 `ID,中文,English,日本語`
4. 网络连接是否正常

**Q: 如何处理引号内包含逗号的文本？**

A: 系统已支持CSV标准格式，使用双引号包裹含逗号的文本：
```csv
ID,中文,English,日本語
greeting,"你好，世界","Hello, World","こんにちは、世界"
```

**Q: 条件判断不生效？**

A: 确认：
1. targetObjectID 或 targetObjectName 正确
2. 组件类型名称拼写正确（区分大小写）
3. 变量名称正确且为 public 字段或属性
4. 变量类型与 compareValue 匹配

**Q: 事件调用失败？**

A: 检查：
1. 目标对象在场景中存在且激活
2. 组件已正确添加到对象上
3. 方法是 public 且参数类型匹配
4. 方法不是静态方法

**Q: 如何支持更多语言？**

A: 在 `DialogueDataTypes.cs` 中：
1. 在 `Language` 枚举中添加新语言
2. 在 `LocalizedText` 类中添加对应字段
3. 更新 `GetText` 和 `SetText` 方法
4. 在 Google Sheets 中添加对应列

---

## 技术栈

- **Unity 版本**: 2020.3+ (推荐 2021.3 LTS)
- **依赖**:
  - TextMeshPro
  - Unity UI
  - UnityEngine.Networking (本地化加载)

---

## 版本历史

### Current Version
- ✅ 多语言支持（中英日）
- ✅ Google Sheets 集成
- ✅ 可视化对话树编辑器
- ✅ 条件系统和事件系统
- ✅ 角色管理系统
- ✅ 分离式UI模式

---

## 许可证

本项目为内部工具，版权归项目所有者所有。

---

## 贡献

如有问题或建议，请联系项目维护者。

---

**最后更新**: 2025-01-13
