using UnityEngine;
using UnityEngine.Networking;
using UniVRM10;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 虚拟人综合控制器 - 整合语音识别、AI回复、表情、口型和肢体动作
/// 状态机流程: Idle → Thinking → ActionSelection → Speaking → FadeOut → Idle
/// </summary>
public class VirtualHumanController : MonoBehaviour
{
    /// <summary>
    /// 虚拟人行为状态机
    /// </summary>
    private enum VirtualHumanState
    {
        Idle,              // 空闲状态 - 中立表情，就绪
        Thinking,          // 思考状态 - 等待AI回复，播放思考动画
        ActionSelection,   // 动作选择状态 - 根据情感和内容选择肢体动作
        Speaking,          // 说话状态 - 表情+口型+肢体动作并行
        FadeOut            // 淡出状态 - 平滑淡出表情，停止动作
    }

    /// <summary>
    /// 肢体动作类型
    /// </summary>
    public enum VirtualHumanAction
    {
        None = 0,       // 无动作
        Nod = 1,        // 点头
        Shake = 2,      // 摇头
        Greet = 3,      // 打招呼/挥手
        Introduce = 4,  // 介绍（手部指向）
        Surprise = 5,   // 惊讶
        Dance = 6,      // 舞蹈
        Thinking = 7    // 思考动作
    }

    [Header("组件引用")]
    [SerializeField] private VRMExpressionController expressionController;
    [SerializeField] private LipSyncManager lipSyncManager;
    [SerializeField] private ChatManager chatManager;
    [SerializeField] private AIManager aiManager;
    [SerializeField] private AudioSource speakingAudioSource;

    [Header("肢体动作")]
    [SerializeField] private Animator bodyAnimator;

    [Header("文本转语音配置")]
    [SerializeField] private string ttsServiceUrl = "http://127.0.0.1:8000/tts";
    [SerializeField] private float minSpeakingDuration = 1f;  // 最小说话时长

    [SerializeField] private float postSpeakFadeDuration = 1.0f;   // 说完后淡出时长

    private VirtualHumanState currentState = VirtualHumanState.Idle;
    private VirtualHumanAction currentAction = VirtualHumanAction.None;
    private bool isSpeaking = false;
    private Queue<string> messageQueue = new Queue<string>();
    private bool isProcessing = false;

    void Start()
    {
        // 自动查找组件
        if (expressionController == null)
            expressionController = GetComponent<VRMExpressionController>();

        if (lipSyncManager == null)
            lipSyncManager = GetComponent<LipSyncManager>();

        if (chatManager == null)
            chatManager = FindObjectOfType<ChatManager>();

        if (aiManager == null)
            aiManager = FindObjectOfType<AIManager>();

        if (speakingAudioSource == null)
            speakingAudioSource = GetComponent<AudioSource>();

        if (bodyAnimator == null)
            bodyAnimator = GetComponent<Animator>();
        if (bodyAnimator == null)
            bodyAnimator = GetComponentInChildren<Animator>();
        if (bodyAnimator == null)
            bodyAnimator = FindObjectOfType<Animator>();

        if (bodyAnimator != null)
            Debug.Log($"VirtualHumanController 已初始化 | Animator 已找到: {bodyAnimator.gameObject.name}");
        else
            Debug.LogError("VirtualHumanController: 找不到 Animator 组件！请在 Inspector 中将 Body Animator 赋值");
    }

    // ============ 预设关键词响应 ============

    /// <summary>
    /// 预设响应：关键词 → 固定回复 + 指定动作，跳过AI调用
    /// </summary>
    [System.Serializable]
    public class PresetResponse
    {
        public string[] keywords;           // 触发关键词（任意一个匹配即触发）
        public string response;             // 固定回复文本
        public VirtualHumanAction action;   // 指定动作
        public ExpressionPreset emotion;    // 指定表情
    }

    private readonly PresetResponse[] presetResponses = new PresetResponse[]
    {
        new PresetResponse {
            keywords = new[] { "跳舞", "跳个舞", "舞蹈", "dance","跳一个" },
            response = "当然会啦！看我的舞姿吧~",
            action = VirtualHumanAction.Dance,
            emotion = ExpressionPreset.happy
        },
        new PresetResponse {
            keywords = new[] { "打个招呼", "挥挥手", "say hello", "wave" },
            response = "你好呀！很高兴见到你~",
            action = VirtualHumanAction.Greet,
            emotion = ExpressionPreset.happy
        },
        new PresetResponse {
            keywords = new[] { "点个头", "点头" },
            response = "好的，没问题！",
            action = VirtualHumanAction.Nod,
            emotion = ExpressionPreset.happy
        },
        new PresetResponse {
            keywords = new[] { "摇头", "摇摇头" },
            response = "不不不，这可不行哦~",
            action = VirtualHumanAction.Shake,
            emotion = ExpressionPreset.sad
        },
        new PresetResponse {
            keywords = new[] { "自我介绍", "介绍一下自己", "你是谁" },
            response = "我是你的虚拟助手！我可以和你聊天、做表情、跳舞，还能回答你的问题哦~",
            action = VirtualHumanAction.Introduce,
            emotion = ExpressionPreset.neutral
        },
    };

    /// <summary>
    /// 检查用户消息是否匹配预设关键词，返回匹配的预设响应，无匹配返回null
    /// </summary>
    private PresetResponse MatchPresetResponse(string userMessage)
    {
        if (string.IsNullOrEmpty(userMessage)) return null;

        string lowerMessage = userMessage.ToLower();
        foreach (var preset in presetResponses)
        {
            foreach (var keyword in preset.keywords)
            {
                if (lowerMessage.Contains(keyword))
                {
                    Debug.Log($"匹配预设关键词: '{keyword}' → 动作={preset.action}, 回复={preset.response}");
                    return preset;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 处理用户消息：先检查预设关键词，无匹配则走AI调用
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

        // 优先检查预设关键词
        PresetResponse preset = MatchPresetResponse(userMessage);
        if (preset != null)
        {
            // 预设响应：直接走固定流程，不调用AI
            HandlePresetResponse(preset);
            return;
        }

        // 无匹配：走AI调用
        if (aiManager != null)
        {
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
    /// 处理预设响应：显示回复 + 直接进入说话流程（带指定动作和表情）
    /// </summary>
    private void HandlePresetResponse(PresetResponse preset)
    {
        if (chatManager != null)
        {
            chatManager.AddOtherMessage(preset.response);
        }

        // 直接进入说话流程，使用预设的动作和表情
        StartCoroutine(SpeakPresetMessage(preset));
    }

    /// <summary>
    /// 预设消息的说话流程（指定动作+表情，不经过AnalyzeEmotion/SelectAction）
    /// </summary>
    private IEnumerator SpeakPresetMessage(PresetResponse preset)
    {
        Debug.Log($"预设响应说话: {preset.response} | 动作={preset.action} | 表情={preset.emotion}");

        yield return null;

        // Speaking 状态
        currentState = VirtualHumanState.Speaking;

        if (expressionController != null)
        {
            expressionController.SetExpression(preset.emotion);
            expressionController.SetMouthActive(true);
        }

        // 播放指定动作
        if (preset.action != VirtualHumanAction.None)
        {
            currentAction = preset.action;
            PlayBodyAction(preset.action);
        }

        // 生成语音并播放
        yield return StartCoroutine(GenerateAndPlaySpeech(preset.response));

        // FadeOut
        currentState = VirtualHumanState.FadeOut;

        if (expressionController != null)
            expressionController.SetMouthActive(false);

        StopBodyAction();
        currentAction = VirtualHumanAction.None;

        yield return new WaitForSeconds(postSpeakFadeDuration);

        if (expressionController != null)
            expressionController.ResetExpression();

        currentState = VirtualHumanState.Idle;
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
    /// 启动思考状态 - 表情思考动画
    /// 注意：肢体思考动作需要Animator中配置ActionType==7的Transition才会生效
    /// </summary>
    private void StartThinkingState()
    {
        currentState = VirtualHumanState.Thinking;

        if (expressionController != null)
            expressionController.StartThinkingAnimation();

        // 如果Animator中配置了Thinking状态(ActionType=7)，则播放思考动作
        if (bodyAnimator != null)
        {
            PlayBodyAction(VirtualHumanAction.Thinking);
        }
    }

    /// <summary>
    /// 停止思考状态（只停止表情动画，不触发Animator Trigger）
    /// Animator的状态切换由SpeakMessage中的PlayBodyAction负责
    /// </summary>
    private void StopThinkingState()
    {
        if (expressionController != null)
            expressionController.StopThinkingAnimation();
    }

    private static readonly string PARAM_ACTION_TYPE = "ActionType";

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
    /// 让虚拟人说话 - 完整状态机流程:
    /// ActionSelection(分析情感+选择动作) → Speaking(表情+口型+动作并行) → FadeOut(淡出) → Idle
    /// </summary>
    private IEnumerator SpeakMessage(string message)
    {
        Debug.Log($"虚拟人说话: {message}");

        // === ActionSelection 状态：分析情感 + 选择肢体动作 ===
        currentState = VirtualHumanState.ActionSelection;
        ExpressionPreset emotion = AnalyzeEmotion(message);
        VirtualHumanAction action = SelectAction(message, emotion);
        Debug.Log($"动作选择完成: 情感={emotion}, 动作={action}");

        // 等待一帧，确保Animator已从上一个状态回到Idle
        yield return null;

        // === Speaking 状态：表情 + 口型 + 肢体动作并行执行 ===
        currentState = VirtualHumanState.Speaking;

        // 设置情感表情
        if (expressionController != null)
        {
            expressionController.SetExpression(emotion);
            expressionController.SetMouthActive(true);
        }

        // 并行启动肢体动作（非阻塞）
        if (action != VirtualHumanAction.None)
        {
            currentAction = action;
            PlayBodyAction(action);
        }

        // 生成语音并播放（主流程等待音频播放完成）
        yield return StartCoroutine(GenerateAndPlaySpeech(message));

        // === FadeOut 状态：平滑淡出表情，停止动作 ===
        currentState = VirtualHumanState.FadeOut;

        // 停止口型
        if (expressionController != null)
            expressionController.SetMouthActive(false);

        // 停止肢体动作
        StopBodyAction();
        currentAction = VirtualHumanAction.None;

        // 等待淡出
        yield return new WaitForSeconds(postSpeakFadeDuration);

        // 回到neutral表情
        if (expressionController != null)
            expressionController.ResetExpression();

        // === 回到 Idle 状态 ===
        currentState = VirtualHumanState.Idle;
    }

    // ============ 动作选择与播放 ============

    /// <summary>
    /// 根据对话内容和情感分析结果选择肢体动作
    /// </summary>
    private VirtualHumanAction SelectAction(string text, ExpressionPreset emotion)
    {
        if (string.IsNullOrEmpty(text)) return VirtualHumanAction.None;

        string lowerText = text.ToLower();

        // 问候/欢迎 → 打招呼
        if (lowerText.Contains("你好") || lowerText.Contains("欢迎") || lowerText.Contains("嗨")
            || lowerText.Contains("hello") || lowerText.Contains("hi"))
            return VirtualHumanAction.Greet;

        // 惊讶情感 → 惊讶动作
        if (emotion == ExpressionPreset.surprised)
            return VirtualHumanAction.Surprise;

        // 否定/道歉/无法回答 → 摇头
        if (lowerText.Contains("不能") || lowerText.Contains("无法") || lowerText.Contains("不行")
            || lowerText.Contains("抱歉") || lowerText.Contains("对不起") || lowerText.Contains("没办法"))
            return VirtualHumanAction.Shake;

        // 介绍/展示 → 介绍动作
        if (lowerText.Contains("介绍") || lowerText.Contains("功能") || lowerText.Contains("这是")
            || lowerText.Contains("首先") || lowerText.Contains("其次"))
            return VirtualHumanAction.Introduce;

        // 明确肯定 → 点头（只在语气较强的肯定时触发）
        if (lowerText.Contains("当然") || lowerText.Contains("没问题") || lowerText.Contains("一定")
            || lowerText.Contains("是的") || lowerText.Contains("对的") || lowerText.Contains("确实"))
            return VirtualHumanAction.Nod;

        return VirtualHumanAction.None;
    }

    /// <summary>
    /// 播放肢体动作（非阻塞，通过Animator触发）
    /// </summary>
    private void PlayBodyAction(VirtualHumanAction action)
    {
        if (bodyAnimator == null) return;

        bodyAnimator.SetInteger(PARAM_ACTION_TYPE, (int)action);
        Debug.Log($"播放肢体动作: {action} (ActionType={(int)action})");
    }

    /// <summary>
    /// 停止肢体动作（回到Idle）
    /// </summary>
    private void StopBodyAction()
    {
        if (bodyAnimator == null) return;

        bodyAnimator.SetInteger(PARAM_ACTION_TYPE, (int)VirtualHumanAction.None);
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

        // 根据返回的格式确定文件扩展名
        string fileExtension = string.IsNullOrEmpty(response.format) ? "wav" : response.format.ToLower();
        string tempAudioPath = System.IO.Path.Combine(Application.persistentDataPath, $"temp_speech.{fileExtension}");

        try
        {
            System.IO.File.WriteAllBytes(tempAudioPath, audioData);
            Debug.Log($"音频已保存到: {tempAudioPath} (格式: {fileExtension})");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存音频失败: {e.Message}");
            yield break;
        }

        // 确定AudioType
        AudioType audioType = fileExtension == "mp3" ? AudioType.MPEG : AudioType.WAV;

        // 加载并播放音频，等待完成
        yield return StartCoroutine(LoadAndPlayAudioFromFile(tempAudioPath, audioType));
    }

    /// <summary>
    /// 从文件加载并播放音频
    /// </summary>
    private IEnumerator LoadAndPlayAudioFromFile(string filePath, AudioType audioType)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{filePath}", audioType))
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

        // 优先级3：高兴（只在真正表达高兴时触发，普通肯定走neutral）
        string[] happyKeywords = { "开心", "高兴", "好棒", "太棒了", "太好了", "哈哈", "喜欢", "爱你",
                                   "真开心", "好厉害", "厉害", "优秀", "完美", "恭喜", "庆祝" };
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
    /// 获取当前状态（用于调试和外部查询）
    /// </summary>
    public string CurrentStateName => currentState.ToString();

    /// <summary>
    /// 获取当前动作（用于调试和外部查询）
    /// </summary>
    public string CurrentActionName => currentAction.ToString();

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
