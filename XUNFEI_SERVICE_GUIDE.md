# Xunfei Spark Lite AI 服务 - 使用指南

## 架构说明

```
Unity 游戏 (AIManager.cs)
    ↓ HTTP 请求
本地 Python 服务 (xunfei_spark_service.py)
    ↓ WebSocket
讯飞星火 API (wss://spark-api.xf-yun.com/v1.1/chat)
```

## 优势

✅ **简化认证** - Python 官方 SDK 已处理 HMAC 签名
✅ **支持多轮对话** - 自动管理对话历史
✅ **易于调试** - HTTP 接口清晰明了
✅ **跨平台支持** - iOS、Android、Windows、Mac 都能用
✅ **低延迟** - 本地服务，无需经过互联网

## 安装步骤

### 1. 安装 Python 依赖

```bash
cd /Users/futingting/Documents/GitHub/Virtual-Human-repository
pip install -r requirements.txt
```

所需库：
- Flask 3.0.0
- websocket-client 1.7.0

### 2. 设置环境变量

在您的 shell 配置文件中（.bash_profile、.zshrc 等）添加：

```bash
export XUNFEI_APP_ID="your_app_id"
export XUNFEI_API_KEY="your_api_key"
export XUNFEI_API_SECRET="your_api_secret"
```

然后重新加载配置：

```bash
source ~/.zshrc  # 或 source ~/.bash_profile
```

验证设置是否成功：

```bash
echo $XUNFEI_APP_ID
```

### 3. 启动 Python 服务

```bash
cd /Users/futingting/Documents/GitHub/Virtual-Human-repository
python3 xunfei_spark_service.py
```

您应该看到这样的输出：

```
============================================================
讯飞星火 AI 服务启动
============================================================
地址: http://127.0.0.1:5000
模型: Spark Lite

API 端点:
  POST /chat - 发送消息
  POST /clear_history - 清除历史
  GET  /health - 健康检查
  GET  /sessions - 查看会话列表（调试）
============================================================
```

**保持这个窗口打开，服务需要一直运行**

## Unity 配置

AIManager.cs 已经配置好，无需修改。默认：
- 本地服务地址：`http://127.0.0.1:5000`
- 会话 ID：`default`（可在 Inspector 中修改）

## 测试服务

### 方法 1：使用 curl 测试

```bash
curl -X POST http://127.0.0.1:5000/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"你好","session_id":"test123"}'
```

预期响应：

```json
{
  "success": true,
  "response": "你好！很高兴认识你。有什么我可以帮助的吗？",
  "session_id": "test123",
  "error": null
}
```

### 方法 2：使用 Python 测试

创建 `test_service.py`：

```python
import requests
import json

url = "http://127.0.0.1:5000/chat"
data = {
    "message": "你好",
    "session_id": "test123"
}

response = requests.post(url, json=data)
print(json.dumps(response.json(), indent=2, ensure_ascii=False))
```

运行：

```bash
python3 test_service.py
```

### 方法 3：在 Unity 中测试

在 Unity Inspector 中：
1. 确保 AIManager 脚本已挂载在某个 GameObject 上
2. 在 Play 模式下调用 `GetAIResponse()` 方法
3. 查看 Console 输出日志

## API 端点说明

### 1. POST /chat - 发送消息

**请求：**

```json
{
  "message": "你好，你是谁？",
  "session_id": "user123"
}
```

**响应（成功）：**

```json
{
  "success": true,
  "response": "我是讯飞星火 AI 助手...",
  "session_id": "user123",
  "error": null
}
```

**响应（失败）：**

```json
{
  "success": false,
  "response": null,
  "session_id": "user123",
  "error": "获取 AI 回复失败"
}
```

### 2. POST /clear_history - 清除对话历史

**请求：**

```json
{
  "session_id": "user123"
}
```

**响应：**

```json
{
  "success": true,
  "message": "历史已清除: user123"
}
```

### 3. GET /health - 健康检查

**响应：**

```json
{
  "status": "ok"
}
```

### 4. GET /sessions - 查看会话列表（调试用）

**响应：**

```json
{
  "sessions": [
    {
      "session_id": "user123",
      "message_count": 5
    }
  ],
  "total": 1
}
```

## 故障排查

### 问题 1：连接拒绝 (Connection refused)

**原因：** Python 服务未启动或已关闭

**解决：**
```bash
python3 xunfei_spark_service.py
```

### 问题 2：缺少环境变量错误

**原因：** XUNFEI_APP_ID、XUNFEI_API_KEY 或 XUNFEI_API_SECRET 未设置

**解决：**
```bash
export XUNFEI_APP_ID="你的_APP_ID"
export XUNFEI_API_KEY="你的_API_KEY"
export XUNFEI_API_SECRET="你的_API_SECRET"
```

### 问题 3：WebSocket 连接超时

**原因：** 讯飞 API 服务响应缓慢或网络问题

**解决：**
- 检查网络连接
- 增加超时时间（在 xunfei_spark_service.py 中修改 `timeout=30`）
- 检查凭证是否正确

### 问题 4：收到错误响应 (code != 0)

**常见错误码：**

| 错误码 | 含义 |
|------|-----|
| 10013 | AppID 不存在 |
| 10014 | APIKey 或 APISecret 错误 |
| 10015 | 余额不足 |
| 10016 | 服务异常 |

**解决：** 检查凭证和账户余额

## 对话历史管理

- **自动管理** - 服务会自动维护每个会话的对话历史
- **自动裁剪** - 当消息过多时，自动删除旧的对话以保持 token 限制
- **手动清除** - 调用 `/clear_history` 端点清除特定会话的历史

```csharp
// 在 C# 中清除历史
aiManager.ClearHistory();
```

## 性能参数

**当前配置（Spark Lite）：**
- 模型：Spark Lite
- 最大 tokens：1024
- 温度（creativity）：0.5
- 对话历史：最多保留 10 条消息

如需调整，修改 `xunfei_spark_service.py` 中的：

```python
sessions[session_id] = ChatSession(
    ...
    temperature=0.5,      # 改为 0.3-0.8
    max_tokens=1024       # 改为 1-4096
)
```

## 部署到生产环境

### 后台运行服务

使用 `nohup`：

```bash
nohup python3 xunfei_spark_service.py > xunfei_service.log 2>&1 &
```

查看日志：

```bash
tail -f xunfei_service.log
```

### 使用 systemd（推荐）

创建 `/etc/systemd/system/xunfei-spark.service`：

```ini
[Unit]
Description=Xunfei Spark AI Service
After=network.target

[Service]
Type=simple
User=your_username
WorkingDirectory=/Users/futingting/Documents/GitHub/Virtual-Human-repository
Environment="XUNFEI_APP_ID=your_app_id"
Environment="XUNFEI_API_KEY=your_api_key"
Environment="XUNFEI_API_SECRET=your_api_secret"
ExecStart=/usr/bin/python3 /Users/futingting/Documents/GitHub/Virtual-Human-repository/xunfei_spark_service.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

然后：

```bash
sudo systemctl start xunfei-spark
sudo systemctl enable xunfei-spark
```

## 相关文件

- `xunfei_spark_service.py` - Python 服务主文件
- `requirements.txt` - Python 依赖
- `Assets/Scripts/AIManager.cs` - Unity C# 调用接口
- `Assets/Scripts/VoiceInputHandler.cs` - 语音输入触发 AI
- `Assets/Scripts/UIManager.cs` - 文本输入触发 AI
