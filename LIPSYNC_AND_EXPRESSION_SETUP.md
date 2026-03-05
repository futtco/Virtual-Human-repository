# 口型同步和表情控制集成指南

本指南介绍如何在项目中集成uLipSync和VRM表情系统，实现完整的虚拟人口型动画和表情控制。

## 📋 目录
1. [快速开始](#快速开始)
2. [组件说明](#组件说明)
3. [Scene配置](#scene配置)
4. [Python服务扩展](#python服务扩展)
5. [调试技巧](#调试技巧)

---

## 快速开始

### 前置要求
- ✅ Unity 2022.3+ (VRM 10支持)
- ✅ uLipSync 插件已导入
- ✅ VRM 10 包已导入 (com.vrmc.vrm)
- ✅ VRM模型文件 (Assets/Resources/Prefab/*.vrm)
- ✅ Python Xunfei Spark 服务已启动

### 基本步骤

#### 1. 创建场景对象结构
```
VirtualHuman (GameObject)
├── VRM Model (加载的VRM实例)
├── VRMExpressionController (脚本)
├── LipSyncManager (脚本)
├── VirtualHumanController (脚本)
└── AudioSource (用于语音播放)
```

#### 2. 添加脚本组件
```csharp
// 在你的场景中添加:
public class SceneSetup : MonoBehaviour
{
    public void SetupVirtualHuman()
    {
        GameObject humanObject = new GameObject("VirtualHuman");

        // 添加VRM表情控制器
        var expressionController = humanObject.AddComponent<VRMExpressionController>();

        // 添加口型同步管理器
        var lipSyncManager = humanObject.AddComponent<LipSyncManager>();

        // 添加虚拟人综合控制器
        var virtualHumanController = humanObject.AddComponent<VirtualHumanController>();

        // 添加音频源
        var audioSource = humanObject.AddComponent<AudioSource>();

        // 关联VRM实例
        var vrmInstance = humanObject.GetComponent<Vrm10Instance>();
        if (vrmInstance != null)
        {
            expressionController.vrm = vrmInstance;
        }
    }
}
```

---

## 组件说明

### VRMExpressionController
**功能**: 管理VRM模型的表情动画

**主要方法**:
```csharp
// 设置表情
expressionController.SetExpression(ExpressionPreset.happy);
expressionController.SetExpression(ExpressionPreset.angry);
expressionController.SetExpression(ExpressionPreset.sad);

// 设置自定义表情
expressionController.SetExpressionByName("eyes_closed");

// 混合表情
expressionController.BlendExpressions(
    ExpressionPreset.happy,
    ExpressionPreset.angry,
    0.5f  // 混合权重
);

// 重置到中性表情
expressionController.ResetExpression();

// 播放短暂的表情动画
expressionController.PlayEmotionSequence(
    ExpressionPreset.surprised,
    duration: 1.5f
);
```

**可用表情预设**:
```csharp
ExpressionPreset.neutral    // 中性
ExpressionPreset.happy      // 开心
ExpressionPreset.angry      // 愤怒
ExpressionPreset.sad        // 伤心
ExpressionPreset.surprised  // 惊讶
ExpressionPreset.aa         // 张口 (用于口型)
```

### LipSyncManager
**功能**: 处理实时音频到口型的同步

**主要方法**:
```csharp
// 播放音频并自动同步口型
lipSyncManager.PlayAudioWithLipSync(audioClip);

// 停止音频
lipSyncManager.StopAudio();

// 暂停/恢复
lipSyncManager.PauseAudio();
lipSyncManager.ResumeAudio();

// 获取当前音素信息
var info = lipSyncManager.GetCurrentLipSyncInfo();
Debug.Log($"当前音素: {info.phoneme}");  // A, I, U, E, O等
```

**支持的音素** (日语):
```
A (あ行) - 大张口
I (い行) - 微张口
U (う行) - 最小张口
E (え行) - 普通张口
O (お行) - 圆口
```

### VirtualHumanController
**功能**: 整合所有组件的综合控制器

**主要方法**:
```csharp
// 处理用户消息 (自动调用AI, 生成语音, 播放表情和口型)
virtualHumanController.HandleUserMessage("你好");

// 停止说话
virtualHumanController.StopSpeaking();

// 获取说话状态
if (virtualHumanController.IsSpeaking)
{
    Debug.Log("虚拟人正在说话");
}

// 触发特定表情
virtualHumanController.TriggerExpression(ExpressionPreset.happy);
```

---

## Scene配置

### 检查清单

- [ ] **VRM Model**
  - [ ] 模型已加载到场景
  - [ ] Vrm10Instance 组件存在
  - [ ] 模型位置和缩放正确

- [ ] **Audio Sources**
  - [ ] 语音播放音频源已配置
  - [ ] 音量和混音组设置正确

- [ ] **Canvas/UI**
  - [ ] ChatManager 已配置
  - [ ] VoiceInputHandler 已连接

- [ ] **脚本关联**
  - [ ] VRMExpressionController 已关联VRM实例
  - [ ] LipSyncManager 已关联 AudioSource
  - [ ] VirtualHumanController 已关联所有组件
  - [ ] VoiceInputHandler 已引用 VirtualHumanController

### Inspector配置示例

**VRMExpressionController**:
```
✓ Vrm (Vrm10Instance reference)
✓ LipSync (LipSyncContext reference) [可选]
✓ Default Neutral Weight: 0.5
✓ Expression Blend Speed: 0.5
```

**LipSyncManager**:
```
✓ LipSync Context (LipSyncContext reference)
✓ AudioSource (AudioSource reference)
✓ Show Debug Info: true [调试时]
```

**VirtualHumanController**:
```
✓ Expression Controller (VRMExpressionController reference)
✓ LipSync Manager (LipSyncManager reference)
✓ Chat Manager (ChatManager reference)
✓ AI Manager (AIManager reference)
✓ Speaking Audio Source (AudioSource reference)
✓ TTS Service URL: http://127.0.0.1:5000/tts
✓ Auto Expression: true
✓ Min Speaking Duration: 1
✓ Thinking Duration: 0.5
```

---

## Python服务扩展

现有的Python服务需要添加文本转语音(TTS)功能。

### 更新 xunfei_service.py

```python
from flask import Flask, request, jsonify
from xunfei_spark_lite import XunfeiSparkLite
import base64
import requests
import json

app = Flask(__name__)

# 初始化讯飞Spark
spark = XunfeiSparkLite(
    app_id=os.getenv('XUNFEI_APP_ID'),
    api_key=os.getenv('XUNFEI_API_KEY'),
    api_secret=os.getenv('XUNFEI_API_SECRET')
)

# 现有的聊天端点...

@app.route('/tts', methods=['POST'])
def text_to_speech():
    """
    文本转语音端点
    请求: {"text": "要朗读的文本"}
    响应: {"success": true, "audio_data": "base64编码的音频", "format": "wav"}
    """
    try:
        data = request.json
        text = data.get('text', '')

        if not text:
            return jsonify({
                'success': False,
                'error': '文本不能为空'
            }), 400

        # 调用讯飞TTS服务
        # 注: 这里需要集成讯飞的TTS API
        # 示例使用阿里云或其他TTS服务

        audio_data = generate_tts(text)

        # 转换为base64
        audio_base64 = base64.b64encode(audio_data).decode('utf-8')

        return jsonify({
            'success': True,
            'audio_data': audio_base64,
            'format': 'wav'
        })

    except Exception as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500

def generate_tts(text):
    """
    使用讯飞TTS或其他服务生成音频
    返回: 音频字节数据
    """
    # TODO: 实现TTS调用
    # 可选服务:
    # 1. 讯飞语音合成 API
    # 2. Azure Text to Speech
    # 3. Google Cloud Text-to-Speech
    # 4. 本地TTS (gTTS, pyttsx3等)

    pass
```

### 简单本地TTS方案 (使用 gTTS)

```python
# 安装: pip install gtts

from gtts import gTTS
import io

def generate_tts_gtts(text):
    """使用 Google Text-to-Speech 生成语音"""
    tts = gTTS(text=text, lang='zh', slow=False)

    # 生成到内存
    audio_io = io.BytesIO()
    tts.write_to_fp(audio_io)
    audio_io.seek(0)

    return audio_io.getvalue()
```

---

## 调试技巧

### 启用Debug日志

在Inspector中启用各组件的Debug选项:

**VRMExpressionController**:
```csharp
// 添加debug输出
Debug.Log($"当前表情: {currentExpressionKey}");
Debug.Log($"表情权重: {currentExpressionWeight}");
```

**LipSyncManager**:
```csharp
// 启用 Show Debug Info 在Inspector中
// 实时显示当前音素和音量信息
```

### 常见问题

#### Q: 表情不显示
**A**: 检查:
1. VRM模型是否支持该表情预设
2. ExpressionManager 是否正确初始化
3. 权重值是否在 0-1 范围内

#### Q: 口型不同步
**A**: 检查:
1. AudioSource 是否关联
2. uLipSync 是否已初始化
3. OnLipSyncUpdate 回调是否被触发
4. 启用 Show Debug Info 查看当前音素

#### Q: 虚拟人不说话
**A**: 检查:
1. Python TTS 服务是否运行
2. 音频源是否配置和启用
3. 查看Console是否有错误信息

#### Q: 内存持续增长
**A**:
1. 检查是否正确销毁了AudioClip
2. 查看是否有事件监听器未解除
3. 在OnDestroy中正确清理资源

### 性能优化

```csharp
// 1. 表情混合速度过快会导致卡顿
expressionBlendSpeed = 0.3f;  // 降低混合速度

// 2. 限制表情切换频率
private float lastExpressionChangeTime = 0;
private float expressionChangeCooldown = 0.2f;

public void SetExpression(ExpressionPreset preset)
{
    if (Time.time - lastExpressionChangeTime < expressionChangeCooldown)
        return;

    // 设置表情...
    lastExpressionChangeTime = Time.time;
}

// 3. 缓存表情键
private Dictionary<ExpressionPreset, ExpressionKey> expressionCache;
```

---

## 示例: 完整的对话流程

```csharp
public class DialogueExample : MonoBehaviour
{
    [SerializeField] private VirtualHumanController virtualHuman;

    void Start()
    {
        // 1. 用户说话
        // (由VoiceInputHandler捕获)

        // 2. 虚拟人作出反应
        virtualHuman.HandleUserMessage("你叫什么名字?");
    }

    // 流程:
    // 1. VoiceInputHandler 识别语音 -> "你叫什么名字?"
    // 2. VirtualHumanController.HandleUserMessage() 被调用
    // 3. 显示思考表情 (ExpressionPreset.neutral)
    // 4. AIManager 调用讯飞 Spark API
    // 5. 收到AI回复 -> "我叫虚拟小助手"
    // 6. 显示开心表情 (ExpressionPreset.happy)
    // 7. VirtualHumanController 调用Python服务TTS
    // 8. 获取音频数据
    // 9. LipSyncManager 播放音频并同步口型
    // 10. 播放完成后恢复中性表情
}
```

---

## 下一步

1. **TTS服务集成**: 根据上面的Python服务扩展示例，集成文本转语音功能
2. **表情优化**: 根据AI回复内容自动选择合适的表情
3. **音素优化**: 调整uLipSync的灵敏度和口型映射
4. **性能测试**: 在目标设备上测试表情和口型的性能

---

## 参考资源

- [uLipSync GitHub](https://github.com/asumin/uLipSync)
- [VRM 10 规范](https://github.com/vrm-c/vrm-specification)
- [讯飞Spark API文档](https://www.xfyun.cn/doc/platform/concepts)
