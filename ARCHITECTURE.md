# 系统架构文档 - 虚拟人对话系统

## 🎯 完整数据流

```
┌─────────────────────────────────────────────────────────────────┐
│                          用户交互流程                              │
└─────────────────────────────────────────────────────────────────┘

                        用户按住语音按钮
                              ↓
                    ┌─────────────────────┐
                    │  VoiceInputHandler  │
                    │  - 录制音频          │
                    │  - 显示音浪动画      │
                    └─────────────────────┘
                              ↓
                     释放按钮停止录音
                              ↓
                    ┌─────────────────────┐
                    │  Whisper (STT)      │
                    │  - 语音转文字        │
                    │  - 识别中文          │
                    └─────────────────────┘
                              ↓
                       生成文本消息
                              ↓
          ┌──────────────────────────────────────┐
          ↓                                      ↓
    ┌────────────────┐              ┌────────────────────┐
    │  ChatManager   │              │  AIManager         │
    │  - 显示用户消息 │              │  - 调用 OpenAI API │
    │  - 更新聊天UI  │              │  - 获取 AI 回复    │
    └────────────────┘              └────────────────────┘
          ↓                                      ↓
          │                          等待 API 响应
          │                                      ↓
          │                          获得 AI 生成文本
          │                                      ↓
          │                         ┌──────────────────┐
          │                         │  添加到对话历史  │
          │                         │  保持上下文关联  │
          │                         └──────────────────┘
          │                                      ↓
          └──────────────────┬───────────────────┘
                            ↓
                  ┌─────────────────────┐
                  │   ChatManager       │
                  │  - 显示 AI 回复     │
                  │  - 自动滚动到底部   │
                  └─────────────────────┘
                            ↓
                      消息显示在UI中
```

---

## 📦 组件通信图

```
                    ┌──────────────────┐
                    │   UIManager      │
                    │  - 控制输入模式   │
                    │  - 发送/接收事件  │
                    └────────┬─────────┘
                            ↓
        ┌───────────────────────────────────────┐
        │                                       │
        ↓                                       ↓
    ┌──────────────┐                  ┌─────────────────┐
    │InputField    │                  │VoiceInputHandler│
    │- 文字输入    │                  │- 语音输入       │
    └──────┬───────┘                  └────────┬────────┘
           │                                   │
           └───────────────┬───────────────────┘
                          ↓
                    ┌────────────────┐
                    │  ChatManager   │ ◄──── 接收新消息
                    │                │
                    │- 添加消息      │
                    │- 管理布局      │
                    │- 自动滚动      │
                    │- 显示气泡      │
                    └────────┬───────┘
                            ↓
                  ┌──────────────────────┐
                  │  ScrollRect 显示    │
                  │  OtherTalk.prefab   │
                  │  SelfTalk.prefab    │
                  └──────────────────────┘

              ↓ VoiceInputHandler 调用
    ┌─────────────────────────────────┐
    │      AIManager                  │
    │                                 │
    │- 缓存对话历史                   │
    │- 发送 API 请求                  │
    │- 解析响应                       │
    └────────────┬────────────────────┘
                 ↓ 异步回调
          ┌────────────────┐
          │  ChatManager   │
          │  显示 AI 回复   │
          └────────────────┘

              ↓ 调用 Avatar
    ┌─────────────────────────────────┐
    │  SimpleAvatarLoader             │
    │  - Ready Player Me 集成         │
    │  - 加载3D头像                   │
    └─────────────────────────────────┘
```

---

## 🔧 核心类说明

### 1. **AIManager** (新增)
```csharp
public class AIManager : MonoBehaviour
{
    // 配置
    private string apiKey;           // OpenAI API Key
    private string model = "gpt-4o-mini";
    private float temperature = 0.7f;
    private int maxTokens = 500;

    // 关键方法
    public void GetAIResponse(
        string userMessage,          // 用户输入
        Action<string> onComplete,   // 成功回调
        Action<string> onError       // 失败回调
    );

    public void ClearHistory();      // 清空对话历史

    // 内部状态
    private List<ChatMessage> conversationHistory;  // 维持对话上下文
    private bool isWaitingForResponse;              // 防止并发请求
}
```

### 2. **VoiceInputHandler** (已修改)
```csharp
[SerializeField] private AIManager aiManager;  // 新增

private async void SendVoiceAsText()
{
    // 1. 识别语音 → 获取文本
    var result = await whisperManager.GetTextAsync(...);

    // 2. 显示用户消息
    chatManager.AddSelfMessage(result.Result);

    // 3. 调用 AI 获取回复 (新增)
    aiManager.GetAIResponse(
        result.Result,
        onComplete: (aiResponse) => {
            chatManager.AddOtherMessage(aiResponse);
        },
        onError: (error) => {
            chatManager.AddOtherMessage($"抱歉，出错了: {error}");
        }
    );
}
```

### 3. **ChatManager** (无需修改)
- 已有的 `AddOtherMessage()` 方法用于显示 AI 回复
- 自动处理布局和滚动

---

## 🌐 OpenAI API 集成细节

### 请求格式
```json
POST https://api.openai.com/v1/chat/completions

{
    "model": "gpt-4o-mini",
    "messages": [
        {"role": "system", "content": "你是一个友好的虚拟人助手..."},
        {"role": "user", "content": "你好"},
        {"role": "assistant", "content": "你好！很高兴认识你..."},
        {"role": "user", "content": "今天天气怎样？"}
    ],
    "temperature": 0.7,
    "max_tokens": 500
}
```

### 响应格式
```json
{
    "choices": [
        {
            "message": {
                "role": "assistant",
                "content": "根据当前位置和时间，我可以告诉你..."
            },
            "finish_reason": "stop"
        }
    ]
}
```

### Token 成本计算
```
输入消息  100 tokens × $0.15/百万 = $0.000015
输出回复  100 tokens × $0.60/百万 = $0.000060
单次对话成本 ≈ ¥0.0005
```

---

## 🔐 API Key 安全方案

### 推荐：环境变量
```bash
# 设置一次，到处可用
export OPENAI_API_KEY="sk-xxxx..."

# 代码中：
string apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
```

### 不推荐：直接在代码中
```csharp
[SerializeField] private string apiKey = "sk-xxxx...";  // ❌ 危险！
```

**Why?** Git 会记录历史，即使删除也能恢复。

---

## 📊 性能指标

| 指标 | 数值 | 备注 |
|------|------|------|
| 语音识别延迟 | 2-5秒 | Whisper STT |
| AI 响应延迟 | 1-3秒 | OpenAI API |
| 总系统延迟 | 3-8秒 | 包括网络波动 |
| 内存占用 | ~150MB | 运行时 |
| 对话历史限制 | 无（Token 受限）| 需防止超出token限制 |

---

## 🎨 自定义系统提示

修改 AIManager.cs 的 Start() 方法来改变 AI 人格：

```csharp
// 原始：通用助手
conversationHistory.Add(new ChatMessage
{
    role = "system",
    content = "你是一个友好的虚拟人助手。使用中文回答用户问题。回复简洁，最多2-3句话，适合语音交互。"
});

// 修改为：博物馆讲解员
conversationHistory.Add(new ChatMessage
{
    role = "system",
    content = "你是一个专业的博物馆讲解员。为参观者详细介绍展品的历史、文化价值和制作工艺。使用简洁的中文，适合口头讲解。"
});

// 修改为：虚拟客服
conversationHistory.Add(new ChatMessage
{
    role = "system",
    content = "你是一个耐心的客服代表。帮助用户解决问题，提供产品信息。始终礼貌且专业。"
});
```

---

## 🚀 未来扩展方向

1. **文字转语音 (TTS)**
   - 添加 Google Cloud TTS 或 Azure Speech Service
   - 让虚拟人说出回复

2. **情感识别**
   - 分析用户语音中的情感
   - 调整 AI 回复的语气

3. **表情和肢体动画**
   - 根据 AI 回复内容触发相应动画
   - 提升交互自然度

4. **多语言支持**
   - 支持英文、日文等多语言对话
   - 自动语言检测

5. **长期记忆**
   - 保存用户偏好信息
   - 实现个性化对话

---

## 📝 文件清单

```
Assets/Scripts/
├── AIManager.cs ✨ (新增)
├── VoiceInputHandler.cs ✏️ (已修改)
├── ChatManager.cs (无需修改)
├── UIManager.cs
├── SimpleAvatarLoader.cs
├── VoiceWaveAnimation.cs
├── CameraController.cs
└── (其他文件)

文档/
├── SETUP_GPT4O_MINI.md (详细指南)
├── AI_QUICK_START.md (快速开始)
└── ARCHITECTURE.md (本文档)
```

---

## ✅ 测试清单

- [ ] 环境变量正确设置
- [ ] 语音输入正常工作
- [ ] Whisper STT 识别准确
- [ ] AI 能正确接收用户消息
- [ ] API 返回有效响应
- [ ] 回复正确显示在 UI 中
- [ ] 对话历史保持一致
- [ ] 处理错误情况（网络、配额等）

---

祝你毕设顺利！🎉
