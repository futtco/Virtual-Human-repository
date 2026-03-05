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
    [Header("组件引用")]
    [SerializeField] private VRMExpressionController expressionController;
    [SerializeField] private LipSyncManager lipSyncManager;
    [SerializeField] private ChatManager chatManager;
    [SerializeField] private AIManager aiManager;
    [SerializeField] private AudioSource speakingAudioSource;

    [Header("文本转语音配置")]
    [SerializeField] private string ttsServiceUrl = "http://127.0.0.1:8000/tts";
    [SerializeField] private float minSpeakingDuration = 1f;  // 最小说话时长

    [Header("表情配置")]
    [SerializeField] private bool autoExpression = true;      // 自动根据内容选择表情
    [SerializeField] private float thinkingDuration = 0.5f;   // 思考表情时长

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
            // 显示思考表情
            if (autoExpression && expressionController != null)
            {
                expressionController.PlayEmotionSequence(ExpressionPreset.neutral, thinkingDuration);
            }

            aiManager.GetAIResponse(
                userMessage,
                onComplete: (aiResponse) =>
                {
                    Debug.Log($"收到AI回复: {aiResponse}");
                    HandleAIResponse(aiResponse);
                },
                onError: (error) =>
                {
                    Debug.LogError($"AI调用失败: {error}");
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
    /// 让虚拟人说话 - 调用TTS生成语音
    /// </summary>
    private IEnumerator SpeakMessage(string message)
    {
        Debug.Log($"虚拟人说话: {message}");

        // 显示说话表情
        if (autoExpression && expressionController != null)
        {
            expressionController.SetExpression(ExpressionPreset.happy);
        }

        // 生成语音
        yield return StartCoroutine(GenerateAndPlaySpeech(message));

        // 恢复中性表情
        if (autoExpression && expressionController != null)
        {
            expressionController.ResetExpression();
        }
    }

    /// <summary>
    /// 调用TTS服务生成语音并播放
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
                ProcessTTSResponse(request);
            }
            else
            {
                Debug.LogError($"TTS请求失败: {request.error} (状态码: {request.responseCode})");
            }
        }

        isSpeaking = false;
    }

    /// <summary>
    /// 处理TTS响应（单独方法避免yield问题）
    /// </summary>
    private void ProcessTTSResponse(UnityWebRequest request)
    {
        string responseText = request.downloadHandler.text;
        TTSResponse response = JsonUtility.FromJson<TTSResponse>(responseText);

        if (response == null || !response.success)
        {
            Debug.LogError($"TTS生成失败: {response?.error ?? "未知错误"}");
            return;
        }

        if (string.IsNullOrEmpty(response.audio_data))
        {
            Debug.LogError("TTS返回的音频数据为空");
            return;
        }

        try
        {
            // 解码base64音频数据
            byte[] audioData = System.Convert.FromBase64String(response.audio_data);

            // 将音频数据写入临时文件
            string tempAudioPath = System.IO.Path.Combine(Application.persistentDataPath, "temp_speech.wav");
            System.IO.File.WriteAllBytes(tempAudioPath, audioData);

            Debug.Log($"音频已保存到: {tempAudioPath}");

            // 异步加载并播放音频
            StartCoroutine(LoadAndPlayAudioFromFile(tempAudioPath));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理音频失败: {e.Message}");
        }
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
