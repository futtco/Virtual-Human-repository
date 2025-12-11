using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ChatManager : MonoBehaviour
{
    [Header("聊天配置")]
    [SerializeField] private Transform chatContainer;  // 聊天消息容器
    [SerializeField] private GameObject otherTalkPrefab;  // 对方消息预制体
    [SerializeField] private GameObject selfTalkPrefab;   // 自己消息预制体
    [SerializeField] private ScrollRect scrollRect;  // ScrollView组件

    [Header("气泡框配置")]
    [SerializeField] private float minBubbleHeight = 50f;  // 气泡框最小高度
    [SerializeField] private float maxBubbleWidth = 500f;  // 气泡框最大宽度
    [SerializeField] private float bubbleSpacing = 10f;    // 气泡之间的间隔
    [SerializeField] private float bubblePadding = 10f;    // 气泡内边距
    [SerializeField] private float avatarSize = 50f;       // 头像尺寸（用于计算消息高度）

    private List<GameObject> chatMessages = new List<GameObject>();
    private float currentYPosition = 0f;

    void Start()
    {
        if (chatContainer == null)
        {
            Debug.LogError("ChatContainer 未设置！请在Inspector中设置聊天容器");
        }
    }

    /// <summary>
    /// 添加对方发送的消息
    /// </summary>
    public void AddOtherMessage(string message)
    {
        AddMessage(message, true);
    }

    /// <summary>
    /// 添加自己发送的消息
    /// </summary>
    public void AddSelfMessage(string message)
    {
        AddMessage(message, false);
    }

    /// <summary>
    /// 添加消息到聊天界面
    /// </summary>
    private void AddMessage(string message, bool isOther)
    {
        GameObject prefab = isOther ? otherTalkPrefab : selfTalkPrefab;

        if (prefab == null)
        {
            Debug.LogError($"预制体未设置！{(isOther ? "OtherTalkPrefab" : "SelfTalkPrefab")}");
            return;
        }

        GameObject messageObj = Instantiate(prefab, chatContainer);
        chatMessages.Add(messageObj);

        RectTransform messageRect = messageObj.GetComponent<RectTransform>();
        Transform bubbleTransform = messageObj.transform.Find("Bubble");

        if (bubbleTransform == null)
        {
            Debug.LogError("预制体中未找到 Bubble 对象！");
            Destroy(messageObj);
            return;
        }

        RectTransform bubbleRect = bubbleTransform.GetComponent<RectTransform>();
        Transform textTransform = bubbleTransform.Find("Text (Legacy)");

        if (textTransform == null)
        {
            Debug.LogError("预制体中未找到 Text (Legacy) 对象！");
            Destroy(messageObj);
            return;
        }

        Text textComponent = textTransform.GetComponent<Text>();

        if (textComponent == null)
        {
            Debug.LogError("Text 组件未找到！");
            Destroy(messageObj);
            return;
        }

        textComponent.text = message;

        // 先临时设置气泡框为最大宽度，以便正确计算文本换行
        bubbleRect.sizeDelta = new Vector2(maxBubbleWidth, bubbleRect.sizeDelta.y);

        // 强制更新文本布局以获取准确的尺寸
        Canvas.ForceUpdateCanvases();

        // 计算气泡框需要的宽度和高度
        float textPreferredWidth = textComponent.preferredWidth;
        float preferredWidth = Mathf.Min(textPreferredWidth + bubblePadding * 2, maxBubbleWidth);

        // 如果文本超过最大宽度，需要重新计算换行后的高度
        float preferredHeight;
        if (textPreferredWidth + bubblePadding * 2 > maxBubbleWidth)
        {
            // 文本会换行，使用最大宽度
            preferredWidth = maxBubbleWidth;
            Canvas.ForceUpdateCanvases();
            preferredHeight = textComponent.preferredHeight + bubblePadding * 2;
        }
        else
        {
            // 文本不换行，根据实际宽度计算
            preferredHeight = textComponent.preferredHeight + bubblePadding * 2;
        }

        // 确保高度不小于最小值
        preferredHeight = Mathf.Max(preferredHeight, minBubbleHeight);

        // 更新 Bubble 的 LayoutElement，让 HorizontalLayoutGroup 根据这个来布局
        LayoutElement bubbleLayout = bubbleTransform.GetComponent<LayoutElement>();
        bubbleLayout.preferredWidth = preferredWidth;
        bubbleLayout.preferredHeight = preferredHeight;

        // 设置气泡框的实际尺寸
        bubbleRect.sizeDelta = new Vector2(preferredWidth, preferredHeight);

        // 强制刷新布局
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageRect);

        // 计算消息整体高度（取气泡和头像高度的较大值）
        float messageHeight = Mathf.Max(preferredHeight, avatarSize);

        // 获取容器宽度
        RectTransform containerRect = chatContainer.GetComponent<RectTransform>();
        float containerWidth = containerRect.rect.width;

        // 设置消息的Y位置和宽度
        messageRect.anchoredPosition = new Vector2(0, currentYPosition);
        messageRect.sizeDelta = new Vector2(containerWidth, messageHeight);

        // 更新下一个消息的Y位置
        currentYPosition -= messageHeight + bubbleSpacing;

        // 更新容器的高度
        UpdateContainerHeight();

        // 自动滚动到底部
        ScrollToBottom();
    }

    /// <summary>
    /// 更新聊天容器的高度
    /// </summary>
    private void UpdateContainerHeight()
    {
        RectTransform containerRect = chatContainer.GetComponent<RectTransform>();

        // 计算所有消息的总高度
        float totalHeight = 0f;
        foreach (GameObject msg in chatMessages)
        {
            RectTransform msgRect = msg.GetComponent<RectTransform>();
            totalHeight += msgRect.sizeDelta.y + bubbleSpacing;
        }

        // 设置容器高度
        containerRect.sizeDelta = new Vector2(containerRect.sizeDelta.x, totalHeight);
    }

    /// <summary>
    /// 滚动到底部（显示最新消息）
    /// </summary>
    private void ScrollToBottom()
    {
        if (scrollRect != null)
        {
            // 使用协程延迟一帧执行，确保布局已经更新完成
            StartCoroutine(ScrollToBottomCoroutine());
        }
    }

    /// <summary>
    /// 滚动到底部的协程
    /// </summary>
    private IEnumerator ScrollToBottomCoroutine()
    {
        // 等待一帧，确保布局更新完成
        yield return null;

        // 设置垂直滚动位置为0（底部）
        scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 清空所有聊天消息
    /// </summary>
    public void ClearAllMessages()
    {
        foreach (GameObject msg in chatMessages)
        {
            Destroy(msg);
        }
        chatMessages.Clear();
        currentYPosition = 0f;
        UpdateContainerHeight();
    }
}
