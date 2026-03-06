using UnityEngine;
using UnityEngine.Networking;
using UniVRM10;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 虚拟人综合控制器 - 整合语音识别、AI回复、表情和口型
/// </summary>
public class VirtualHumanController : MonoBehaviour
{
    /// <summary>
    /// 虚拟人的说话状态
    /// </summary>
    private enum VirtualHumanState
    {
        Idle,      // 空闲状态
        Thinking,  // 等待AI回复中
        Speaking,  // 正在说话
        FadeOut    // 说话结束淡出
    }
    [Header("组件引用")]
    [SerializeField] private VRMExpressionController expressionController;
    [SerializeField] private LipSyncManager lipSyncManager;
    [SerializeField] private ChatManager chatManager;
    [SerializeField] private AIManager aiManager;
    [SerializeField] private AudioSource speakingAudioSource;

    [Header("文本转语音配置")]
    [SerializeField] private string ttsServiceUrl = "http://127.0.0.1:8000/tts";
    [SerializeField] private float minSpeakingDuration = 1f;  // 最小说话时长

    [SerializeField] private float postSpeakFadeDuration = 1.0f;   // 说完后淡出时长

    private VirtualHumanState currentState = VirtualHumanState.Idle;
    private bool isSpeaking = false;
    private Queue<string> messageQueue = new Queue<string>();
    private bool isProcessing = false;

    void Start()
    {
        // 自动查找组件
        if (expressionController == null)
        {
            expressionController = GetComponent<VRMExpressionController>();
        }

        if (lipSyncManager == null)
        {
            lipSyncManager = GetComponent<LipSyncManager>();
        }

        if (chatManager == null)
        {
            chatManager = FindObjectOfType<ChatManager>();
        }

        if (aiManager == null)
        {
            aiManager = FindObjectOfType<AIManager>();
        }

        if (speakingAudioSource == null)
        {
            speakingAudioSource = GetComponent<AudioSource>();
        }

        Debug.Log("✓ 虚拟人控制器已初始化");
    }

    /// <summary>
    /// 处理用户消息并获取AI回复
    /// </summary>
    public void HandleUserMessage(string userMessage)
    {
        if (string.IsNullOrEmpty(userMessage))
        {
            Debug.LogWarning("用户消息为空");
            return;
        }

        Debug.Log($"处理用户消息: {userMessage}");

        // 显示用户消息
        if (chatManager != null)
        {
            chatManager.AddSelfMessage(userMessage);
        }

        // 获取AI回复
        if (aiManager != null)
        {
            // 启动持续思考状态（直到AI回复完成）
            StartThinkingState();

            aiManager.GetAIResponse(
                userMessage,
                onComplete: (aiResponse) =>
                {
                    Debug.Log($"收到AI回复: {aiResponse}");
                    StopThinkingState();
                    HandleAIResponse(aiResponse);
                },
                onError: (error) =>
                {
                    Debug.LogError($"AI调用失败: {error}");
                    StopThinkingState();
                    if (chatManager != null)
                    {
                        chatManager.AddOtherMessage($"抱歉，我遇到了问题: {error}");
                    }
                }
            );
        }
    }

    /// <summary>
    /// 处理AI回复 - 显示消息、生成语音、播放动画
    /// </summary>
    private void HandleAIResponse(string aiResponse)
    {
        if (chatManager != null)
        {
            chatManager.AddOtherMessage(aiResponse);
        }

        // 将回复加入队列
        messageQueue.Enqueue(aiResponse);

        // 处理队列中的消息
        if (!isProcessing)
        {
            StartCoroutine(ProcessMessageQueue());
        }
    }

    /// <summary>
    /// 启动思考状态 - 持续idle动画
    /// </summary>
    private void StartThinkingState()
    {
        currentState = VirtualHumanState.Thinking;
        if (expressionController != null)
        {
            expressionController.StartThinkingAnimation();
        }
    }

    /// <summary>
    /// 停止思考状态
    /// </summary>
    private void StopThinkingState()
    {
        currentState = VirtualHumanState.Speaking;
        if (expressionController != null)
        {
            expressionController.StopThinkingAnimation();
        }
    }

    /// <summary>
    /// 处理消息队列 - 按顺序生成语音并播放
    /// </summary>
    private IEnumerator ProcessMessageQueue()
    {
        isProcessing = true;

        while (messageQueue.Count > 0)
        {
            string message = messageQueue.Dequeue();
            yield return StartCoroutine(SpeakMessage(message));
        }

        isProcessing = false;
    }

    /// <summary>
    /// 让虚拟人说话 - 调用TTS生成语音，带表情和口型动画
    /// </summary>
    private IEnumerator SpeakMessage(string message)
    {
        Debug.Log($"虚拟人说话: {message}");

        // 第1步：根据内容分析情感表情
        ExpressionPreset emotion = AnalyzeEmotion(message);
        if (expressionController != null)
        {
            expressionController.SetExpression(emotion);
            expressionController.SetMouthActive(true);
        }

        // 第2步：生成语音并播放
        yield return StartCoroutine(GenerateAndPlaySpeech(message));

        // 第3步：停止口型动画（平滑关闭嘴巴）
        if (expressionController != null)
        {
            expressionController.SetMouthActive(false);
        }

        // 第4步：等待淡出时长
        yield return new WaitForSeconds(postSpeakFadeDuration);

        // 第5步：回到neutral表情
        if (expressionController != null)
        {
            expressionController.ResetExpression();
        }
    }

    /// <summary>
    /// 调用TTS服务生成语音并播放（等待音频播放完成）
    /// </summary>
    private IEnumerator GenerateAndPlaySpeech(string text)
    {
        if (speakingAudioSource == null)
        {
            Debug.LogWarning("没有配置音频源，跳过语音生成");
            yield break;
        }

        isSpeaking = true;

        // 构建TTS请求
        var ttsRequest = new TTSRequest { text = text };
        string jsonBody = JsonUtility.ToJson(ttsRequest);

        using (UnityWebRequest request = new UnityWebRequest(ttsServiceUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 处理TTS响应并等待音频播放完成
                yield return StartCoroutine(ProcessTTSResponse(request));
            }
            else
            {
                Debug.LogError($"TTS请求失败: {request.error} (状态码: {request.responseCode})");
            }
        }

        isSpeaking = false;
    }

    /// <summary>
    /// 处理TTS响应（等待音频播放完成）
    /// </summary>
    private IEnumerator ProcessTTSResponse(UnityWebRequest request)
    {
        string responseText = request.downloadHandler.text;
        TTSResponse response = JsonUtility.FromJson<TTSResponse>(responseText);

        if (response == null || !response.success)
        {
            Debug.LogError($"TTS生成失败: {response?.error ?? "未知错误"}");
            yield break;
        }

        if (string.IsNullOrEmpty(response.audio_data))
        {
            Debug.LogError("TTS返回的音频数据为空");
            yield break;
        }

        // 解码base64音频数据
        byte[] audioData;
        try
        {
            audioData = System.Convert.FromBase64String(response.audio_data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解码音频失败: {e.Message}");
            yield break;
        }

        // 将音频数据写入临时文件
        string tempAudioPath = System.IO.Path.Combine(Application.persistentDataPath, "temp_speech.wav");
        try
        {
            System.IO.File.WriteAllBytes(tempAudioPath, audioData);
            Debug.Log($"音频已保存到: {tempAudioPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存音频失败: {e.Message}");
            yield break;
        }

        // 加载并播放音频，等待完成
        yield return StartCoroutine(LoadAndPlayAudioFromFile(tempAudioPath));
    }

    /// <summary>
    /// 从文件加载并播放音频
    /// </summary>
    private IEnumerator LoadAndPlayAudioFromFile(string filePath)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{filePath}", AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);

                if (audioClip != null && audioClip.length > 0)
                {
                    Debug.Log($"音频加载成功: {audioClip.length} 秒");

                    // 播放音频并进行口型同步
                    if (lipSyncManager != null)
                    {
                        lipSyncManager.PlayAudioWithLipSync(audioClip);
                    }
                    else
                    {
                        speakingAudioSource.PlayOneShot(audioClip);
                    }

                    // 等待音频播放完成
                    yield return new WaitForSeconds(Mathf.Max(audioClip.length, minSpeakingDuration));
                }
                else
                {
                    Debug.LogError("加载的音频无效");
                }
            }
            else
            {
                Debug.LogError($"音频加载失败: {www.error}");
            }
        }
    }


    /// <summary>
    /// 根据文本内容分析情感，返回对应的表情预设
    /// 使用关键词匹配，优先级从高到低
    /// </summary>
    private ExpressionPreset AnalyzeEmotion(string text)
    {
        if (string.IsNullOrEmpty(text)) return ExpressionPreset.neutral;

        string lowerText = text.ToLower();

        // 优先级1：悲伤/道歉（最高优先）
        string[] sadKeywords = { "对不起", "抱歉", "遗憾", "难过", "伤心", "可惜", "失望" };
        foreach (var keyword in sadKeywords)
        {
            if (lowerText.Contains(keyword))
            {
                return ExpressionPreset.sad;
            }
        }

        // 优先级2：惊讶（强反应）
        string[] surprisedKeywords = { "哇", "真的吗", "不会吧", "太神奇", "居然", "竟然", "没想到" };
        foreach (var keyword in surprisedKeywords)
        {
            if (lowerText.Contains(keyword))
            {
                return ExpressionPreset.surprised;
            }
        }

        // 优先级3：高兴/肯定
        string[] happyKeywords = { "开心", "高兴", "好棒", "太棒了", "喜欢", "爱", "不错",
                                   "好的", "好啊", "当然", "没问题", "欢迎", "感谢", "谢谢", "很好", "优秀" };
        foreach (var keyword in happyKeywords)
        {
            if (lowerText.Contains(keyword))
            {
                return ExpressionPreset.happy;
            }
        }

        // 优先级4：生气/警告（最低优先但明确）
        string[] angryKeywords = { "不对", "错误", "不行", "危险", "警告" };
        foreach (var keyword in angryKeywords)
        {
            if (lowerText.Contains(keyword))
            {
                return ExpressionPreset.angry;
            }
        }

        // 默认：中性
        return ExpressionPreset.neutral;
    }

    /// <summary>
    /// 停止说话
    /// </summary>
    public void StopSpeaking()
    {
        if (lipSyncManager != null)
        {
            lipSyncManager.StopAudio();
        }

        if (speakingAudioSource != null)
        {
            speakingAudioSource.Stop();
        }

        isSpeaking = false;
    }

    /// <summary>
    /// 获取虚拟人是否正在说话
    /// </summary>
    public bool IsSpeaking => isSpeaking;

    /// <summary>
    /// 触发特定表情
    /// </summary>
    public void TriggerExpression(ExpressionPreset emotion)
    {
        if (expressionController != null)
        {
            expressionController.PlayEmotionSequence(emotion, 1.5f);
        }
    }

    // ============ 数据结构 ============

    [System.Serializable]
    public class TTSRequest
    {
        public string text;
    }

    [System.Serializable]
    public class TTSResponse
    {
        public bool success;
        public string audio_data;  // base64编码的音频数据
        public string format;       // wav, mp3等
        public string error;
    }
}
