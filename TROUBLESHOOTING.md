# 口型动画和表情控制 - 故障排除指南

## ✅ 编译错误修复

### 已解决的问题

1. **uLipSync 类型引用错误**
   - 原因：uLipSync 包可能命名空间不同或版本差异
   - 解决：使用反射方式与 uLipSync 通信，避免直接类型依赖
   - 影响的文件：
     - `LipSyncManager.cs` - 使用反射获取 LipSyncContext
     - `VRMExpressionController.cs` - 使用反射注册事件回调

2. **VRM 10 命名空间**
   - 确保 UniVRM10 包已正确导入
   - VRM 表情相关类型应该自动可用

### 编译错误检查清单

- [ ] `LipSyncManager.cs` - 无编译错误
- [ ] `VRMExpressionController.cs` - 无编译错误
- [ ] `VirtualHumanController.cs` - 无编译错误
- [ ] `VoiceInputHandler.cs` - 无编译错误
- [ ] 所有其他脚本 - 无编译错误

---

## 🔧 运行时配置

### 场景设置检查

1. **GameObject 结构**
   ```
   VirtualHuman
   ├── VRM Model (Vrm10Instance 组件)
   ├── VRMExpressionController 脚本
   ├── LipSyncManager 脚本
   ├── VirtualHumanController 脚本
   └── AudioSource 组件
   ```

2. **uLipSync 组件**
   - [ ] VRM Model GameObject 上有 `uLipSync.LipSyncContext` 组件
   - [ ] LipSyncContext 已配置 AudioSource
   - [ ] LipSyncContext 已配置相应的 viseme 或表情映射

3. **VRM 模型**
   - [ ] VRM 模型支持表情系统
   - [ ] 模型已正确加载到场景
   - [ ] Vrm10Instance 可以访问 Runtime.Expression

4. **Audio 组件**
   - [ ] AudioSource 已正确挂载
   - [ ] AudioSource 的音量不为 0
   - [ ] 音频输出目标正确

---

## 📝 常见问题

### Q1: "找不到 LipSyncContext 组件"
**症状**: 启动时输出 "LipSyncManager: 缺少必要组件"

**解决步骤**:
1. 确保 uLipSync 包已安装
2. 在 VRM Model GameObject 上添加 uLipSync.LipSyncContext 组件
3. 检查 Scene 中是否有多个 AudioSource，确保关联的是正确的

```csharp
// 手动配置 (如果自动查找失败)
[SerializeField] private MonoBehaviour lipSyncComponent;

void OnEnable()
{
    if (lipSyncComponent == null)
    {
        lipSyncComponent = FindObjectOfType("uLipSync.LipSyncContext");
    }
}
```

### Q2: 口型没有同步
**症状**: 音频播放但嘴巴不动

**检查项**:
1. uLipSync.LipSyncContext 组件是否启用
2. AudioSource 中是否正在播放音频
3. VRM 模型是否支持 "aa" (张口) 表情
4. 在 Inspector 中启用 `showDebugInfo` 查看调试信息

```csharp
// 测试代码
void TestLipSync()
{
    var info = lipSyncManager.GetCurrentLipSyncInfo();
    Debug.Log($"当前音素信息: {info}");
}
```

### Q3: 表情不显示
**症状**: 调用 SetExpression 但虚拟人没有反应

**检查项**:
1. VRM 模型是否支持所请求的表情预设
2. Vrm10Instance.Runtime.Expression 是否为 null
3. 检查表情权重是否在 0-1 范围内

```csharp
// 调试：列出可用的表情
void DebugExpressions(Vrm10Instance vrm)
{
    if (vrm != null && vrm.Runtime != null)
    {
        Debug.Log("VRM 表情系统就绪");
    }
    else
    {
        Debug.LogError("VRM 表情系统未初始化");
    }
}
```

### Q4: 语音生成失败 (TTS)
**症状**: "TTS处理失败" 或 "无可用的TTS引擎"

**解决步骤**:
1. 确保 Python 服务运行中：
   ```bash
   python xunfei_tts_service.py
   ```

2. 检查 TTS 引擎依赖：
   ```bash
   pip install gtts          # Google TTS
   # 或
   pip install pyttsx3       # 本地TTS
   ```

3. 验证服务端点：
   ```csharp
   [SerializeField] private string ttsServiceUrl = "http://127.0.0.1:5000/tts";
   ```

4. 测试 TTS 端点：
   ```bash
   curl -X POST http://127.0.0.1:5000/tts \
     -H "Content-Type: application/json" \
     -d '{"text": "测试"}'
   ```

---

## 🐛 调试技巧

### 启用调试信息

在 Inspector 中启用各脚本的调试选项：
- LipSyncManager: 启用 `Show Debug Info`
- VirtualHumanController: 查看 Console 输出

### Console 日志级别

监听这些关键日志：
```
✓ 口型同步管理器已初始化
✓ VRM表情控制器已初始化
[AI] 用户: ...
[AI] 助手: ...
播放音频并进行口型同步: ...
虚拟人说话: ...
```

### 性能监控

检查以下性能指标：
- 表情切换：每帧消耗的 GC 内存
- 口型同步：uLipSync 处理时间
- 语音播放：音频缓冲区使用

### 反射调用调试

如果反射方式出现问题，可以在 Start 方法添加调试：

```csharp
void DebugReflection()
{
    var type = System.Type.GetType("uLipSync.LipSyncContext");
    if (type != null)
    {
        Debug.Log($"找到类型: {type.FullName}");
        var methods = type.GetMethods();
        Debug.Log($"可用方法: {string.Join(", ", methods.Select(m => m.Name))}");
    }
    else
    {
        Debug.LogError("未找到 uLipSync.LipSyncContext");
    }
}
```

---

## 📚 参考资源

- [uLipSync 文档](https://github.com/asumin/uLipSync)
- [VRM 10 规范](https://github.com/vrm-c/vrm-specification)
- [UniVRM 文档](https://vrm.dev/docs/)
- [讯飞 Spark API](https://www.xfyun.cn/doc/platform/concepts)

---

## 💡 最佳实践

1. **Always check for null**
   ```csharp
   if (expressionController != null && expressionManager != null)
   {
       expressionController.SetExpression(ExpressionPreset.happy);
   }
   ```

2. **Use try-catch for reflection**
   ```csharp
   try
   {
       // Reflection code
   }
   catch (System.Exception e)
   {
       Debug.LogError($"反射失败: {e.Message}");
   }
   ```

3. **Cleanup in OnDestroy**
   ```csharp
   void OnDestroy()
   {
       // Remove event handlers
       // Release resources
       // Stop coroutines
   }
   ```

4. **Test individually**
   - 先测试 VRM 表情
   - 再测试 uLipSync 口型
   - 最后测试整体流程

---

## 获取帮助

如果问题仍未解决：

1. 检查 Console 中的完整错误信息
2. 查看 LIPSYNC_AND_EXPRESSION_SETUP.md 的配置部分
3. 在相关脚本中添加更多 Debug.Log 进行追踪
4. 检查 uLipSync 和 VRM 包的版本兼容性
