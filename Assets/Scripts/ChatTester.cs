using UnityEngine;

public class ChatTester : MonoBehaviour
{
    [SerializeField] private ChatManager chatManager;

    void Start()
    {
        if (chatManager == null)
        {
            chatManager = FindObjectOfType<ChatManager>();
        }
    }

    void Update()
    {
        // 按 ESC 键清空所有消息
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (chatManager != null)
            {
                chatManager.ClearAllMessages();
                Debug.Log("已清空所有消息");
            }
        }
    }
}
