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
    [SerializeField] private float expressionBlendSpeed = 0.5f;  // 表情切换速度

    private object expressionManager;  // Vrm10ExpressionManager
    private string currentExpressionName = "neutral";
    private string previousExpressionName = null;
    private float targetExpressionWeight;
    private float currentExpressionWeight;

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

        currentExpressionName = preset.ToString().ToLower();
        targetExpressionWeight = 1.0f;
        Debug.Log($"设置表情: {preset}");
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
    /// 重置表情到中性
    /// </summary>
    public void ResetExpression()
    {
        SetExpression(ExpressionPreset.neutral);
        // 同时关闭嘴巴
        SetMouthOpen(0f);
    }

    /// <summary>
    /// uLipSync的回调方法 - uLipSync会自动调用这个方法
    /// 方法名必须是 OnLipSyncUpdate，参数必须是 LipSyncInfo
    /// </summary>
    public void OnLipSyncUpdate(LipSyncInfo info)
    {
        if (expressionManager == null) return;

        if (info.volume < Mathf.Epsilon) return;  // 音量太小，忽略

        // 根据音素设置口型
        // info.phoneme 是字符串：A, I, U, E, O
        float targetMouthOpen = info.phoneme switch
        {
            "A" => 1.0f,      // A - 张口
            "I" => 0.3f,
            "U" => 0.2f,
            "E" => 0.4f,
            "O" => 0.8f,      // O - 张口
            _ => 0.0f
        };

        SetMouthOpen(targetMouthOpen);
    }

    /// <summary>
    /// 设置嘴巴开度
    /// </summary>
    private void SetMouthOpen(float openAmount)
    {
        if (expressionManager == null) return;

        try
        {
            var setWeightMethod = expressionManager.GetType().GetMethod("SetWeight");
            if (setWeightMethod != null)
            {
                // 使用 "aa" (张口) 表情来控制嘴巴开度
                // aa是VRM10的标准口型表情预设
                var aaKey = ExpressionKey.CreateFromPreset(ExpressionPreset.aa);
                setWeightMethod.Invoke(expressionManager, new object[] { aaKey, openAmount });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"设置嘴巴开度失败: {e.Message}");
        }
    }

    /// <summary>
    /// 播放情感表情动画序列
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
        // 平滑切换表情权重
        if (expressionManager != null && !string.IsNullOrEmpty(currentExpressionName))
        {
            currentExpressionWeight = Mathf.Lerp(
                currentExpressionWeight,
                targetExpressionWeight,
                Time.deltaTime * expressionBlendSpeed
            );

            try
            {
                var setWeightMethod = expressionManager.GetType().GetMethod("SetWeight");
                if (setWeightMethod != null)
                {
                    // 重置前一个表情
                    if (!string.IsNullOrEmpty(previousExpressionName) && previousExpressionName != currentExpressionName)
                    {
                        var prevKey = StringToExpressionKey(previousExpressionName);
                        setWeightMethod.Invoke(expressionManager, new object[] { prevKey, 0f });
                    }

                    // 设置当前表情
                    var currentKey = StringToExpressionKey(currentExpressionName);
                    setWeightMethod.Invoke(expressionManager, new object[] { currentKey, currentExpressionWeight });

                    // 如果权重接近目标，更新前一个表情名称
                    if (Mathf.Abs(currentExpressionWeight - targetExpressionWeight) < 0.01f)
                    {
                        previousExpressionName = currentExpressionName;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"更新表情失败: {e.Message}");
            }
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
