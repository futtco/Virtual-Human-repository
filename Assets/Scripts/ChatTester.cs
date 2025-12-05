using UnityEngine;

public class ChatTester : MonoBehaviour
{
    [SerializeField] private ChatManager chatManager;

    // 测试用的消息列表
    private string[] testMessages = new string[]
    {
        "你好！",
        "今天天气怎么样？",
        "我正在开发一个Unity项目，使用了虚拟人技术，感觉很有趣！",
        "好的，没问题",
        "这是一条很长很长很长很长很长很长很长很长很长很长很长很长很长很长很长的消息，用来测试气泡框的自动换行和高度调整功能。"
    };

    private int messageIndex = 0;

    void Start()
    {
        if (chatManager == null)
        {
            chatManager = FindObjectOfType<ChatManager>();
        }
    }

    void Update()
    {
        // 按 O 键发送对方的消息
        if (Input.GetKeyDown(KeyCode.O))
        {
            SendOtherMessage();
        }

        // 按 S 键发送自己的消息
        if (Input.GetKeyDown(KeyCode.S))
        {
            SendSelfMessage();
        }

        // 按 C 键清空所有消息
        if (Input.GetKeyDown(KeyCode.C))
        {
            chatManager.ClearAllMessages();
            messageIndex = 0;
            Debug.Log("已清空所有消息");
        }
    }

    private void SendOtherMessage()
    {
        if (chatManager != null && messageIndex < testMessages.Length)
        {
            chatManager.AddOtherMessage(testMessages[messageIndex]);
            Debug.Log($"对方发送: {testMessages[messageIndex]}");
            messageIndex++;
        }
        else if (messageIndex >= testMessages.Length)
        {
            chatManager.AddOtherMessage("这是额外的测试消息");
            Debug.Log("对方发送: 这是额外的测试消息");
        }
    }

    private void SendSelfMessage()
    {
        if (chatManager != null && messageIndex < testMessages.Length)
        {
            chatManager.AddSelfMessage(testMessages[messageIndex]);
            Debug.Log($"自己发送: {testMessages[messageIndex]}");
            messageIndex++;
        }
        else if (messageIndex >= testMessages.Length)
        {
            chatManager.AddSelfMessage("这是额外的测试消息");
            Debug.Log("自己发送: 这是额外的测试消息");
        }
    }
}
