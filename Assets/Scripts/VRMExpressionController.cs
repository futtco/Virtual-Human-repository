using UnityEngine;
using UniVRM10;
using uLipSync;

/// <summary>
/// VRM表情和口型动画控制器
/// 集成uLipSync和VRM表情系统
/// </summary>
public class VRMExpressionController : MonoBehaviour
{
    [Header("VRM相关")]
    [SerializeField] private Vrm10Instance vrm;

    [Header("口型同步")]
    [SerializeField] private uLipSync.uLipSync lipSync;

    [Header("表情配置")]
    [SerializeField] private float emotionBlendSpeed = 2.0f;    // 情感表情切换速度（慢）
    [SerializeField] private float mouthBlendSpeed = 8.0f;      // 口型变化速度（快）

    [Header("口型映射调试")]
    [SerializeField] private float phoneme_A_Weight = 1.0f;     // A/啊
    [SerializeField] private float phoneme_I_Weight = 0.3f;     // I/衣
    [SerializeField] private float phoneme_U_Weight = 0.6f;     // U/乌
    [SerializeField] private float phoneme_E_Weight = 0.5f;     // E/呃
    [SerializeField] private float phoneme_O_Weight = 0.8f;     // O/哦

    [Header("表情时长控制")]
    [SerializeField] private float emotionDisplayDuration = 2.0f;  // 表情播放时长（秒）
    [SerializeField] private float emotionFadeDuration = 1.0f;     // 表情淡出时长（秒）

    private object expressionManager;  // Vrm10ExpressionManager

    // 表情时长控制
    private float emotionDisplayTimer = 0f;      // 已经显示的时长

    // 情感层（neutral, happy, sad, surprised, angry）
    private string currentExpressionName = "neutral";
    private string previousExpressionName = null;
    private float targetExpressionWeight = 1.0f;
    private float currentExpressionWeight = 1.0f;

    // 口型层（aa 独立管理）
    private bool isMouthActive = false;
    private float targetMouthWeight = 0.0f;
    private float currentMouthWeight = 0.0f;

    // 思考动画
    private bool isThinking = false;
    private float thinkingTime = 0f;

    void Start()
    {
        if (vrm == null)
        {
            vrm = GetComponent<Vrm10Instance>();
        }

        if (vrm == null)
        {
            Debug.LogError("VRMExpressionController: 找不到Vrm10Instance组件!");
            return;
        }

        expressionManager = vrm.Runtime?.Expression;

        // 初始化口型同步
        if (lipSync == null)
        {
            lipSync = GetComponent<uLipSync.uLipSync>();
        }

        if (lipSync != null)
        {
            if (!lipSync.enabled)
            {
                lipSync.enabled = true;
            }

            // 订阅uLipSync事件
            try
            {
                lipSync.onLipSyncUpdate.AddListener(OnLipSyncUpdate);
                Debug.Log("✓ 口型同步事件已订阅");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"订阅uLipSync事件失败: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("VRMExpressionController: 找不到uLipSync组件!");
        }

        // 初始化表情为中性
        SetExpression(ExpressionPreset.neutral);
        Debug.Log("✓ VRM表情控制器已初始化");
    }

    /// <summary>
    /// 根据预设设置表情
    /// </summary>
    public void SetExpression(ExpressionPreset preset)
    {
        if (expressionManager == null) return;

        string newExpressionName = preset.ToString().ToLower();

        // 如果表情改变或首次初始化
        if (newExpressionName != currentExpressionName)
        {
            previousExpressionName = currentExpressionName;
            currentExpressionName = newExpressionName;

            // 如果之前有表情，重置currentWeight从0开始Lerp
            // 否则直接设置为目标值（首次初始化）
            if (!string.IsNullOrEmpty(previousExpressionName) && previousExpressionName != "neutral")
            {
                currentExpressionWeight = 0f;  // 从新表情淡入
            }
            else
            {
                currentExpressionWeight = 1.0f;  // 首次或从neutral，直接设置
            }

            targetExpressionWeight = 1.0f;
            Debug.Log($"设置表情: {preset} (from {previousExpressionName}, weight:{currentExpressionWeight}→1.0)");
        }
    }

    /// <summary>
    /// 设置自定义表情（按名称）
    /// </summary>
    public void SetExpressionByName(string expressionName)
    {
        if (expressionManager == null) return;

        currentExpressionName = expressionName;
        targetExpressionWeight = 1.0f;
        Debug.Log($"设置表情: {expressionName}");
    }

    /// <summary>
    /// 混合两个表情
    /// </summary>
    public void BlendExpressions(ExpressionPreset preset1, ExpressionPreset preset2, float weight)
    {
        if (expressionManager == null) return;

        try
        {
            var setWeightMethod = expressionManager.GetType().GetMethod("SetWeight");
            if (setWeightMethod != null)
            {
                var key1 = ExpressionKey.CreateFromPreset(preset1);
                var key2 = ExpressionKey.CreateFromPreset(preset2);

                setWeightMethod.Invoke(expressionManager, new object[] { key1, 1f - weight });
                setWeightMethod.Invoke(expressionManager, new object[] { key2, weight });

                Debug.Log($"混合表情: {preset1} ({1f - weight}) + {preset2} ({weight})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"混合表情失败: {e.Message}");
        }
    }

    /// <summary>
    /// 重置表情到中性（平滑淡出，不立即归零）
    /// </summary>
    public void ResetExpression()
    {
        SetExpression(ExpressionPreset.neutral);
        // 不立即关闭嘴巴，而是触发平滑淡出（targetMouthWeight = 0）
        SetMouthActive(false);
    }

    /// <summary>
    /// uLipSync的回调方法 - uLipSync会自动调用这个方法
    /// 方法名必须是 OnLipSyncUpdate，参数必须是 LipSyncInfo
    /// 只在 isMouthActive=true 时才更新口型
    /// </summary>
    public void OnLipSyncUpdate(LipSyncInfo info)
    {
        // 关键：只有口型层激活时才响应uLipSync
        if (!isMouthActive) return;

        if (info.volume < Mathf.Epsilon)
        {
            targetMouthWeight = 0f;
            return;
        }

        // 根据音素设置口型权重（使用Inspector中可配置的值）
        // info.phoneme 是字符串：A, I, U, E, O
        targetMouthWeight = info.phoneme switch
        {
            "A" => phoneme_A_Weight,
            "I" => phoneme_I_Weight,
            "U" => phoneme_U_Weight,
            "E" => phoneme_E_Weight,
            "O" => phoneme_O_Weight,
            _ => 0.0f
        };
    }

    /// <summary>
    /// 激活/停止口型层
    /// </summary>
    public void SetMouthActive(bool active)
    {
        isMouthActive = active;
        if (active)
        {
            emotionDisplayTimer = 0f;  // 重置表情时长计时器
        }
        else
        {
            targetMouthWeight = 0f;  // 触发平滑关闭
            emotionDisplayTimer = 0f;  // 停止说话时重置计时器
        }
    }

    /// <summary>
    /// 启动思考idle动画 - 持续轻微波动
    /// </summary>
    public void StartThinkingAnimation()
    {
        isThinking = true;
        thinkingTime = 0f;
        // 确保当前表情是neutral
        SetExpression(ExpressionPreset.neutral);
        Debug.Log("✓ 思考动画已启动");
    }

    /// <summary>
    /// 停止思考idle动画
    /// </summary>
    public void StopThinkingAnimation()
    {
        isThinking = false;
        thinkingTime = 0f;
        Debug.Log("✓ 思考动画已停止");
    }

    /// <summary>
    /// 播放情感表情动画序列（保留以兼容）
    /// </summary>
    public void PlayEmotionSequence(ExpressionPreset expression, float duration = 2f)
    {
        StartCoroutine(EmotionSequenceCoroutine(expression, duration));
    }

    private System.Collections.IEnumerator EmotionSequenceCoroutine(ExpressionPreset expression, float duration)
    {
        SetExpression(expression);
        yield return new WaitForSeconds(duration);
        ResetExpression();
    }

    /// <summary>
    /// 获取所有可用的VRM表情
    /// </summary>
    public ExpressionPreset[] GetAvailableExpressions()
    {
        return System.Enum.GetValues(typeof(ExpressionPreset)) as ExpressionPreset[];
    }

    void Update()
    {
        if (expressionManager == null) return;

        try
        {
            var setWeightMethod = expressionManager.GetType().GetMethod("SetWeight");
            if (setWeightMethod == null) return;

            // ===== 情感层：独立渲染 =====
            // 情感层平滑Lerp（速度慢，emotionBlendSpeed）
            currentExpressionWeight = Mathf.Lerp(
                currentExpressionWeight,
                targetExpressionWeight,
                Time.deltaTime * emotionBlendSpeed
            );

            // 思考动画的微妙波动：叠加在情感权重上
            float emotionWeightWithThinking = currentExpressionWeight;
            if (isThinking && currentExpressionName.ToLower() == "neutral")
            {
                thinkingTime += Time.deltaTime;
                float subtleWave = 0.08f * Mathf.Sin(thinkingTime * 0.8f);
                emotionWeightWithThinking = Mathf.Clamp01(currentExpressionWeight + subtleWave);
            }

            // 表情时长控制：表情只显示2秒，然后平滑淡出
            if (isMouthActive)
            {
                emotionDisplayTimer += Time.deltaTime;

                // 判断是否进入淡出阶段
                if (emotionDisplayTimer > emotionDisplayDuration)
                {
                    // 淡出时长内平滑降低权重
                    float fadeElapsedTime = emotionDisplayTimer - emotionDisplayDuration;
                    float fadeFraction = Mathf.Clamp01(fadeElapsedTime / emotionFadeDuration);
                    emotionWeightWithThinking *= (1f - fadeFraction);
                }
            }
            else
            {
                // 停止说话时重置计时器
                emotionDisplayTimer = 0f;
            }

            // 设置当前情感表情
            if (!string.IsNullOrEmpty(currentExpressionName))
            {
                var currentKey = StringToExpressionKey(currentExpressionName);
                try
                {
                    setWeightMethod.Invoke(expressionManager, new object[] { currentKey, emotionWeightWithThinking });
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"设置表情失败: {ex.Message}");
                }
            }

            if (currentExpressionName != "neutral" && emotionWeightWithThinking < 0.01f)
            {
                Debug.Log($"表情 {currentExpressionName} 已淡出，自动切换回neutral");
                currentExpressionName = "neutral";
                currentExpressionWeight = 1.0f;
                targetExpressionWeight = 1.0f;
            }

            // 总是淡出前一个表情（确保旧表情权重归零）
            if (!string.IsNullOrEmpty(previousExpressionName) && previousExpressionName != currentExpressionName && previousExpressionName != "aa")
            {
                var prevKey = StringToExpressionKey(previousExpressionName);
                try
                {
                    setWeightMethod.Invoke(expressionManager, new object[] { prevKey, 0f });
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"淡出表情失败: {ex.Message}");
                }

                // 权重接近目标时，清除previousExpressionName（避免重复淡出）
                if (Mathf.Abs(currentExpressionWeight - targetExpressionWeight) < 0.01f)
                {
                    previousExpressionName = null;
                }
            }

            // ===== 口型层：独立渲染 =====
            // 如果口型未激活，强制目标权重为0
            if (!isMouthActive)
            {
                targetMouthWeight = 0f;
            }

            // 口型层平滑Lerp（速度快，mouthBlendSpeed）
            currentMouthWeight = Mathf.Lerp(
                currentMouthWeight,
                targetMouthWeight,
                Time.deltaTime * mouthBlendSpeed
            );

            // 设置aa表情权重（口型开度）
            var aaKey = ExpressionKey.CreateFromPreset(ExpressionPreset.aa);
            try
            {
                setWeightMethod.Invoke(expressionManager, new object[] { aaKey, currentMouthWeight });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"设置口型权重失败: {ex.Message}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"表情更新异常: {e.Message}");
        }
    }

    /// <summary>
    /// 将字符串转换为ExpressionKey
    /// </summary>
    private ExpressionKey StringToExpressionKey(string expressionName)
    {
        if (System.Enum.TryParse<ExpressionPreset>(expressionName, true, out var preset))
        {
            return ExpressionKey.CreateFromPreset(preset);
        }
        return ExpressionKey.CreateCustom(expressionName);
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (lipSync != null)
        {
            try
            {
                lipSync.onLipSyncUpdate.RemoveListener(OnLipSyncUpdate);
                Debug.Log("✓ 口型同步事件已取消订阅");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"取消订阅uLipSync事件失败: {e.Message}");
            }
        }
    }
}
