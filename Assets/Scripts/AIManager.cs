using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class AIManager : MonoBehaviour
{
    [Header("讯飞星火配置")]
    [SerializeField] private string localServiceUrl = "http://127.0.0.1:8000";
    [SerializeField] private string sessionId = "default";

    private bool isWaitingForResponse = false;

    void Start()
    {
        Debug.Log("✓ AI Manager 已初始化 (连接本地 Xunfei 服务)");
        Debug.Log($"  服务地址: {localServiceUrl}");
        Debug.Log($"  会话 ID: {sessionId}");
    }

    /// <summary>
    /// 获取AI回复 - 调用本地 Python 服务
    /// </summary>
    public void GetAIResponse(string userMessage, System.Action<string> onComplete, System.Action<string> onError)
    {
        if (isWaitingForResponse)
        {
            Debug.LogWarning("已有请求在处理中，请等待");
            onError?.Invoke("已有请求在处理中");
            return;
        }

        StartCoroutine(RequestAIResponse(userMessage, onComplete, onError));
    }

    private IEnumerator RequestAIResponse(string userMessage, System.Action<string> onComplete, System.Action<string> onError)
    {
        isWaitingForResponse = true;

        // 构建请求
        var requestData = new ChatRequest
        {
            message = userMessage,
            session_id = sessionId
        };

        string jsonBody = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest($"{localServiceUrl}/chat", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            isWaitingForResponse = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<ChatResponse>(responseText);

                    if (response.success && !string.IsNullOrEmpty(response.response))
                    {
                        onComplete?.Invoke(response.response);
                    }
                    else
                    {
                        Debug.LogError($"AI 错误: {response.error}");
                        onError?.Invoke(response.error ?? "获取回复失败");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"解析 AI 响应失败: {e.Message}");
                    onError?.Invoke($"解析响应失败: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"AI 请求失败 ({request.responseCode}): {request.error}");
                string errorMsg = request.downloadHandler?.text ?? request.error;
                onError?.Invoke($"请求失败: {errorMsg}");
            }
        }
    }

    /// <summary>
    /// 清除对话历史
    /// </summary>
    public void ClearHistory()
    {
        StartCoroutine(ClearHistoryCoroutine());
    }

    private IEnumerator ClearHistoryCoroutine()
    {
        var requestData = new ClearHistoryRequest { session_id = sessionId };
        string jsonBody = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest($"{localServiceUrl}/clear_history", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[AI] 对话历史已清除");
            }
            else
            {
                Debug.LogError($"[AI] 清除历史失败: {request.error}");
            }
        }
    }

    public bool IsWaitingForResponse => isWaitingForResponse;

    // ============ 数据结构 ============

    [System.Serializable]
    public class ChatRequest
    {
        public string message;
        public string session_id;
    }

    [System.Serializable]
    public class ChatResponse
    {
        public bool success;
        public string response;
        public string session_id;
        public string error;
    }

    [System.Serializable]
    public class ClearHistoryRequest
    {
        public string session_id;
    }
}
