# Virtual Human Repository

Unity 虚拟人项目

## 项目设置

### 必需的模型文件

本项目使用 Whisper 语音识别模型，由于模型文件较大（超过 100MB），未包含在 Git 仓库中。

**首次设置时需要手动下载模型文件：**

1. 下载 Whisper Small 模型文件
   - 文件名：`ggml-small.bin`
   - 下载地址：[Hugging Face - Whisper Models](https://huggingface.co/ggerganov/whisper.cpp/tree/main)
   - 或者：[whisper.cpp GitHub Releases](https://github.com/ggerganov/whisper.cpp/tree/master/models)

2. 将下载的模型文件放置到以下目录：
   ```
   Assets/StreamingAssets/whisper/models/ggml-small.bin
   ```

3. 确保目录结构正确：
   ```
   Assets/
   └── StreamingAssets/
       └── whisper/
           └── models/
               └── ggml-small.bin
   ```

### 开发环境

- Unity 版本：（请填写你的 Unity 版本）
- 支持平台：Windows, macOS

## 开始使用

1. 克隆本仓库
2. 下载并放置 Whisper 模型文件（见上方说明）
3. 在 Unity 中打开项目
4. 打开场景：`Assets/Scenes/STT.unity`

## 注意事项

- 模型文件 (`*.bin`) 已添加到 `.gitignore`，不会被提交到仓库
- 每台新设备都需要手动下载模型文件
- 请勿尝试将模型文件提交到 Git
