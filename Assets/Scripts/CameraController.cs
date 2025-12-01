using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("摄像头环绕的目标对象")]
    [SerializeField] private Transform target;

    [Tooltip("如果没有指定目标，使用这个位置作为环绕中心")]
    [SerializeField] private Vector3 targetPosition = Vector3.zero;

    [Header("Rotation Settings")]
    [Tooltip("右键旋转速度")]
    [SerializeField] private float rotationSpeed = 5f;

    [Tooltip("最小俯仰角（向下看）")]
    [SerializeField] private float minVerticalAngle = -80f;

    [Tooltip("最大俯仰角（向上看）")]
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Pan Settings")]
    [Tooltip("左键平移速度")]
    [SerializeField] private float panSpeed = 0.5f;

    [Header("Zoom Settings")]
    [Tooltip("滚轮缩放速度")]
    [SerializeField] private float zoomSpeed = 2f;

    [Tooltip("最小距离")]
    [SerializeField] private float minDistance = 1f;

    [Tooltip("最大距离")]
    [SerializeField] private float maxDistance = 10f;

    [Tooltip("初始距离")]
    [SerializeField] private float initialDistance = 5f;

    [Header("Smooth Settings")]
    [Tooltip("平滑移动速度")]
    [SerializeField] private float smoothSpeed = 10f;

    // 内部状态
    private float currentDistance;
    private float targetDistance;
    private float yaw; // 水平旋转角度
    private float pitch; // 垂直旋转角度
    private Vector3 currentTargetPosition;
    private Vector3 desiredTargetPosition;

    void Start()
    {
        // 初始化
        currentDistance = initialDistance;
        targetDistance = initialDistance;

        // 获取目标位置
        currentTargetPosition = GetTargetPosition();
        desiredTargetPosition = currentTargetPosition;

        // 计算初始旋转角度
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        // 设置初始位置
        UpdateCameraPosition();
    }

    void LateUpdate()
    {
        HandleInput();
        UpdateCameraPosition();
    }

    /// <summary>
    /// 处理鼠标输入
    /// </summary>
    private void HandleInput()
    {
        // 右键 - 旋转
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        // 左键 - 平移
        if (Input.GetMouseButton(0))
        {
            float mouseX = -Input.GetAxis("Mouse X") * panSpeed * (currentDistance / 5f);
            float mouseY = -Input.GetAxis("Mouse Y") * panSpeed * (currentDistance / 5f);

            // 根据摄像头的方向计算平移向量
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            // 只在水平面上移动
            right.y = 0;
            right.Normalize();
            up = Vector3.up;

            desiredTargetPosition += right * mouseX + up * mouseY;
        }

        // 滚轮 - 缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }

    /// <summary>
    /// 更新摄像头位置
    /// </summary>
    private void UpdateCameraPosition()
    {
        // 平滑插值距离
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * smoothSpeed);

        // 平滑插值目标位置
        currentTargetPosition = Vector3.Lerp(currentTargetPosition, desiredTargetPosition, Time.deltaTime * smoothSpeed);

        // 计算旋转
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);

        // 计算摄像头位置（在目标后方）
        Vector3 offset = rotation * Vector3.back * currentDistance;
        Vector3 desiredPosition = currentTargetPosition + offset;

        // 更新摄像头位置和旋转
        transform.position = desiredPosition;
        transform.rotation = rotation;
    }

    /// <summary>
    /// 获取目标位置
    /// </summary>
    private Vector3 GetTargetPosition()
    {
        if (target != null)
        {
            return target.position;
        }
        return targetPosition;
    }

    /// <summary>
    /// 设置新的目标
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        desiredTargetPosition = GetTargetPosition();
    }

    /// <summary>
    /// 设置目标位置
    /// </summary>
    public void SetTargetPosition(Vector3 position)
    {
        target = null;
        targetPosition = position;
        desiredTargetPosition = position;
    }

    /// <summary>
    /// 重置摄像头到初始状态
    /// </summary>
    public void ResetCamera()
    {
        targetDistance = initialDistance;
        desiredTargetPosition = GetTargetPosition();
    }

    /// <summary>
    /// 聚焦到目标（可选动画）
    /// </summary>
    public void FocusOnTarget(Transform newTarget, bool instant = false)
    {
        SetTarget(newTarget);
        if (instant)
        {
            currentTargetPosition = desiredTargetPosition;
        }
    }

    // 可视化调试信息
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        // 绘制目标点
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(currentTargetPosition, 0.2f);

        // 绘制摄像头到目标的连线
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, currentTargetPosition);
    }
}
