# Windows 环境配置指南

本指南适用于在 Windows 电脑上运行虚拟人项目。

## 📋 系统要求

- **操作系统**: Windows 10 或更高版本
- **Python**: Python 3.9 或更高版本
- **Unity**: Unity 2022 LTS 或更高版本
- **网络**: 需要互联网连接（用于讯飞 API 调用）

## 🔧 安装步骤

### 1. 克隆或拉取项目

```bash
git clone <repository-url>
cd Virtual-Human-repository
```

### 2. 安装 Python 依赖

#### 方法 A：使用 pip（推荐）

```bash
pip install flask requests pydub websocket-client edge-tts
```

#### 方法 B：使用 requirements.txt（如果存在）

```bash
pip install -r requirements.txt
```

**验证安装：**
```bash
python --version    # 应该是 3.9 或更高
pip list           # 应该看到上述包
```

### 3. 安装 FFmpeg（用于音频格式转换）

FFmpeg 是必须的，用于将 Edge TTS 生成的 MP3 转换为 WAV 格式。

#### 方法 A：使用 Chocolatey（推荐）

如果已安装 Chocolatey，运行：
```bash
choco install ffmpeg
```

#### 方法 B：使用 Winget（Windows 11）

```bash
winget install FFmpeg
```

#### 方法 C：手动安装

1. 访问 [FFmpeg 官网](https://ffmpeg.org/download.html)
2. 下载 Windows 版本（essentials 足够）
3. 解压到文件夹，例如：`C:\ffmpeg\`
4. 将 `C:\ffmpeg\bin` 添加到系统 PATH：
   - 搜索"环境变量"
   - 点击"编辑系统环境变量"
   - 在"系统变量"中找到 `Path`，点击编辑
   - 点击"新建"，输入 `C:\ffmpeg\bin`
   - 确定保存

**验证安装：**
```bash
ffmpeg -version
```

应该看到 FFmpeg 版本信息。

### 4. 打开 Unity 项目

1. 启动 Unity Hub
2. 点击"打开项目"
3. 选择项目文件夹
4. 等待 Unity 导入资源

## 🚀 运行项目

### 步骤 1：启动 Python TTS 服务

在项目根目录打开命令行，运行：

```bash
python xunfei_tts_service.py
```

你应该看到类似的输出：

```
╔═══════════════════════════════════════════════╗
║     讯飞星火 + TTS 综合服务                    ║
╚═══════════════════════════════════════════════╝

📡 API 端点:
- POST /chat           - 对话 AI
- POST /tts            - 文本转语音
- POST /clear_history  - 清除历史
- GET  /health         - 健康检查

🎯 启动地址: http://127.0.0.1:8000

✓ 讯飞星火 AI 已初始化
✓ Edge TTS 已加载

✓ pydub 已加载 (用于MP3->WAV转换)

📋 已注册的路由:
  /chat -> {'POST', 'OPTIONS'}
  /tts -> {'POST', 'OPTIONS'}
  /clear_history -> {'POST', 'OPTIONS'}
  /health -> {'GET', 'HEAD', 'OPTIONS'}

 * Running on http://127.0.0.1:8000
```

**保持此窗口打开，服务需要一直运行。**

### 步骤 2：验证服务

打开另一个命令行窗口，测试服务是否正常：

```bash
curl http://127.0.0.1:8000/health
```

应该返回：
```json
{"ai_engine":"XunfeiSparkEngine","status":"ok","timestamp":"...","tts_engine":"EdgeTTSEngine"}
```

或者在浏览器中访问：`http://127.0.0.1:8000/health`

### 步骤 3：运行 Unity 项目

1. 在 Unity 中打开场景：`Assets/Scenes/VirtualHuman.unity`
2. 点击 Play 按钮运行
3. 在文本框中输入消息（或使用语音输入）
4. 点击"Send"或说话
5. 虚拟人将显示表情、生成语音并同步口型

## 🧪 功能测试

### 测试 TTS（文本转语音）

```bash
curl -X POST http://127.0.0.1:8000/tts ^
  -H "Content-Type: application/json" ^
  -d "{\"text\": \"你好\"}"
```

**预期响应：** 返回包含 base64 编码音频的 JSON

### 测试 AI 对话

```bash
curl -X POST http://127.0.0.1:8000/chat ^
  -H "Content-Type: application/json" ^
  -d "{\"message\": \"你好\", \"session_id\": \"test\"}"
```

**预期响应：**
```json
{
  "success": true,
  "response": "你好！有什么我可以帮助你的吗？",
  "session_id": "test"
}
```

### 清除对话历史

```bash
curl -X POST http://127.0.0.1:8000/clear_history ^
  -H "Content-Type: application/json" ^
  -d "{\"session_id\": \"test\"}"
```

## ⚙️ Windows 特殊处理

### 1. Python 虚拟环境（推荐）

为了避免全局包污染和版本冲突，建议使用虚拟环境：

```bash
# 创建虚拟环境
python -m venv venv

# 激活虚拟环境
venv\Scripts\activate

# 之后再安装依赖
pip install flask requests pydub websocket-client edge-tts
```

激活后，命令行提示符前面会显示 `(venv)`。

### 2. FFmpeg 路径显式指定

如果 pydub 找不到 FFmpeg（即使已添加到 PATH），需要在 `xunfei_tts_service.py` 中显式指定：

在文件顶部的导入部分后添加：

```python
import os
from pydub import AudioSegment

# Windows 上显式指定 FFmpeg 路径
if os.name == 'nt':  # Windows
    AudioSegment.converter = r"C:\ffmpeg\bin\ffmpeg.exe"
    AudioSegment.ffprobe = r"C:\ffmpeg\bin\ffprobe.exe"
```

如果 FFmpeg 安装在不同位置，修改路径即可。

### 3. 临时文件路径

Unity 在 Windows 上的临时文件目录为：`C:\Users\<username>\AppData\LocalLow\<company>\<product>\`

如果临时音频文件写入失败，可以：

1. 检查 `VirtualHumanController.cs` 中的 `Application.persistentDataPath`
2. 或者修改为项目内的相对路径（例如 `StreamingAssets/temp/`）

### 4. 查看端口占用

如果遇到"端口 8000 已被占用"，使用以下命令查看：

```bash
# 查看占用 8000 端口的进程
netstat -ano | findstr :8000

# 如果需要强制关闭进程（替换 PID）
taskkill /PID <PID> /F
```

### 5. 行尾符号（CRLF vs LF）

Git 会自动处理行尾符号，但如果出现问题：

```bash
# 重新标准化行尾符号
git add --renormalize .
git commit -m "Normalize line endings"
```

## 🐛 故障排查

### 问题 1：Python 命令找不到

**症状：** `'python' 不是内部或外部命令`

**解决方案：**
1. 确保已安装 Python 3.9+
2. 检查 Python 是否在 PATH 中
3. 尝试用 `python3` 代替 `python`
4. 或使用完整路径：`C:\Users\<username>\AppData\Local\Programs\Python\Python39\python.exe`

### 问题 2：pip 安装超时

**症状：** `pip install` 超时或连接错误

**解决方案：**
```bash
# 使用国内源（例如阿里源）
pip install -i https://mirrors.aliyun.com/pypi/simple/ flask requests pydub websocket-client edge-tts

# 或增加超时时间
pip install --default-timeout=1000 flask requests pydub websocket-client edge-tts
```

### 问题 3：FFmpeg 找不到

**症状：** `pydub` 转换音频时出错

**解决方案：**
1. 验证 FFmpeg 已安装：`ffmpeg -version`
2. 如果未安装，参考上面的"安装 FFmpeg"部分
3. 确保 FFmpeg 在 PATH 中（重启命令行后生效）

### 问题 4：端口 8000 已被占用

**症状：** `Address already in use`

**解决方案：**

修改 `xunfei_tts_service.py` 最后一行：

```python
# 原来：
app.run(host='127.0.0.1', port=8000, debug=False)

# 改为：
app.run(host='127.0.0.1', port=8001, debug=False)
```

然后在 Unity 场景中更新：
- 选择 Hierarchy 中的 AIManager
- 在 Inspector 中找到 "Local Service Url"
- 改为 `http://127.0.0.1:8001`

### 问题 5：讯飞 API 连接失败

**症状：** `HMAC signature does not match` 或 `WebSocket 连接失败`

**解决方案：**
1. 检查网络连接
2. 确保凭证正确（在代码中已硬编码）
3. 检查防火墙是否阻止了 WebSocket 连接（wss://spark-api.xf-yun.com）
4. 尝试升级 websocket-client：`pip install --upgrade websocket-client`

### 问题 6：Unity 无法连接到 Python 服务

**症状：** Unity Console 显示"请求失败"或"连接超时"

**解决方案：**
1. 确保 Python 服务在运行：检查命令行窗口
2. 验证 URL 正确：http://127.0.0.1:8000
3. 检查防火墙设置（允许 Python 出站连接）
4. 尝试用 curl 测试：`curl http://127.0.0.1:8000/health`

### 问题 7：音频播放无声或不同步

**症状：** 生成了音频但没有声音，或口型不同步

**解决方案：**
1. 检查 Unity 的 AudioSource 是否配置正确
2. 检查音量设置（Windows 音量、Unity 音量）
3. 确保 uLipSync 已正确安装
4. 检查 LipSyncManager 是否正确关联

### 问题 8：pydub 找不到 FFmpeg

**症状：** `pydub.exceptions.CouldntDecodeError` 或 `Couldn't find ffmpeg`

**解决方案：**

虽然已将 FFmpeg 添加到 PATH，但 pydub 有时仍无法找到。修改 `xunfei_tts_service.py`：

```python
import os
from pydub import AudioSegment

# 在 if __name__ == '__main__': 之前添加
if os.name == 'nt':  # Windows 系统
    ffmpeg_path = r"C:\ffmpeg\bin\ffmpeg.exe"
    if os.path.exists(ffmpeg_path):
        AudioSegment.converter = ffmpeg_path
```

替换路径为你的 FFmpeg 实际安装位置。

### 问题 9：临时音频文件保存失败

**症状：** `PermissionError` 或 "保存音频失败"

**解决方案：**

1. 检查 `Application.persistentDataPath` 目录是否存在和可写
2. 在 `VirtualHumanController.cs` 中修改临时文件路径为项目内目录：

```csharp
// 原来：
string tempAudioPath = System.IO.Path.Combine(Application.persistentDataPath, "temp_speech.wav");

// 改为（确保 Assets/StreamingAssets/temp 目录已存在）：
string tempAudioPath = System.IO.Path.Combine(Application.streamingAssetsPath, "temp", "temp_speech.wav");
```

## 📁 项目文件结构

```
Virtual-Human-repository/
├── Assets/
│   ├── Scripts/
│   │   ├── AIManager.cs              # AI 对话管理
│   │   ├── VirtualHumanController.cs # 虚拟人主控制器
│   │   ├── LipSyncManager.cs         # 口型同步
│   │   ├── VRMExpressionController.cs # 表情控制
│   │   └── ...
│   ├── Scenes/
│   │   └── VirtualHuman.unity        # 主场景
│   └── Resources/
│       └── Prefab/
│           ├── Canvas.prefab         # UI 画布
│           └── Manager.prefab        # 管理器预制件
├── xunfei_tts_service.py             # Python 后端服务
├── WINDOWS_SETUP_GUIDE.md            # 本文件
├── XUNFEI_SERVICE_GUIDE.md           # 讯飞服务文档
└── LIPSYNC_AND_EXPRESSION_SETUP.md   # 口型和表情设置

```

## 💡 使用提示

### 后台运行 Python 服务

如果不想一直开着命令行窗口，可以用 Windows 任务计划程序或创建 bat 文件：

**创建 `run_service.bat`：**
```batch
@echo off
cd /d "%~dp0"
python xunfei_tts_service.py
pause
```

双击运行即可。

### 自动启动服务

创建快捷方式设为开机启动：

1. 右键 `run_service.bat`，创建快捷方式
2. 按 `Win + R`，输入 `shell:startup`
3. 将快捷方式移到此文件夹

## 🔄 与 Mac 版本同步

如果在多台电脑上使用：

1. **凭证**：已在代码中硬编码（`xunfei_tts_service.py`），无需额外配置
2. **依赖**：相同的 Python 包和 FFmpeg
3. **端口**：默认都是 8000（可自定义）
4. **Git**：正常的 push/pull 即可

## 📞 获取帮助

如果遇到问题：

1. 查看本指南的"故障排查"部分
2. 检查 `XUNFEI_SERVICE_GUIDE.md` 和 `LIPSYNC_AND_EXPRESSION_SETUP.md`
3. 查看 Python 服务的输出日志
4. 查看 Unity Console 的错误信息

## ✅ 检查清单

运行项目前，确保：

- [ ] Python 3.9+ 已安装
- [ ] 所有 Python 包已安装（`pip list` 检查）
- [ ] FFmpeg 已安装并在 PATH 中
- [ ] 可以运行 `python xunfei_tts_service.py` 并看到启动信息
- [ ] `curl http://127.0.0.1:8000/health` 返回成功响应
- [ ] Unity 项目已打开并加载 VirtualHuman.unity 场景
- [ ] 网络连接正常

完成以上步骤后，就可以在 Windows 上运行虚拟人项目了！

祝你使用愉快！🎉
