using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制聊天面板的展开/收起，挂载在切换按钮上
/// </summary>
public class CanvasToggle : MonoBehaviour
{
    [Header("需要切换显示的面板")]
    [SerializeField] private GameObject chatPanel;

    [SerializeField] private Text buttonText;

    [Header("初始状态")]
    [SerializeField] private bool startHidden = true;

    [Header("按钮文字设置")]
    [SerializeField] private string expandText = "展开";
    [SerializeField] private string collapseText = "收起";

    private bool isExpanded;

    void Start()
    {
        if (chatPanel == null)
        {
            Debug.LogError("CanvasToggle: 请在Inspector中设置chatPanel引用");
            return;
        }

        // 设置初始状态
        isExpanded = !startHidden;
        chatPanel.SetActive(isExpanded);
        UpdateButtonText();

        // 自动绑定按钮点击事件
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(Toggle);
        }
    }

    /// <summary>
    /// 切换面板显示/隐藏
    /// </summary>
    public void Toggle()
    {
        isExpanded = !isExpanded;
        chatPanel.SetActive(isExpanded);
        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (buttonText != null)
        {
            buttonText.text = isExpanded ? collapseText : expandText;
        }
    }
}
