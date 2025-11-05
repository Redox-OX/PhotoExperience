using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 3f;
    public CameraController cameraController;  // 拍照控制脚本

    private CharacterController controller;
    private float yaw;    // 水平旋转角度（绕Y轴）
    private float pitch;  // 垂直旋转角度（绕X轴）

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;

        // 初始化角度为当前朝向
        yaw = transform.localEulerAngles.y;
        pitch = transform.localEulerAngles.x;
    }

    void Update()
    {
        // 移动
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 moveDir = transform.forward * v + transform.right * h;
        controller.Move(moveDir * moveSpeed * Time.deltaTime);

        // 读取鼠标输入
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 累加旋转角度
        yaw   += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -80f, 80f);  // 限制俯仰范围

        // 应用旋转：注意使用 Quaternion 防止欧拉角累积问题
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        // 鼠标左键拍照
        if (Input.GetMouseButtonDown(1) && cameraController != null)
        {
            cameraController.CapturePhoto();
        }
    }
}