using UnityEngine;
using TMPro;

public class CameraHUDDisplay : MonoBehaviour
{
    [Header("References")]
    public CameraController cameraController;   // 拍照控制脚本，里边有 photographyCamera

    [Header("Text Outputs")]
    public TextMeshProUGUI isoText;            // ISO-NUM
    public TextMeshProUGUI focalLengthText;    // LENS / FOCAL 数值
    public TextMeshProUGUI focusDistanceText;  // FD 数值（可选）
    public TextMeshProUGUI apertureText;       // F-NUM 数值（显示 1/8 这种）
    public TextMeshProUGUI shutterText;        // SHUTTER 数值（显示 1/100 这种）

    void LateUpdate()
    {
        if (cameraController == null || cameraController.photographyCamera == null)
            return;

        var cam = cameraController.photographyCamera;

        // ISO：整数
        if (isoText != null)
        {
            int isoInt = Mathf.RoundToInt(cam.iso);
            isoText.text = isoInt.ToString();
        }

        // 焦距：整数（mm）
        if (focalLengthText != null)
        {
            int focalInt = Mathf.RoundToInt(cam.focalLength);
            focalLengthText.text = focalInt.ToString();
        }

        // 对焦距离：整数（米）
        if (focusDistanceText != null)
        {
            int distInt = Mathf.RoundToInt(cam.focusDistance);
            focusDistanceText.text = distInt.ToString();
        }

        // 光圈：显示为 1/8 这种（F 值的倒数）
        if (apertureText != null)
        {
            apertureText.text = FormatAperture(cam.aperture);
        }

        // 快门：0.01s → 1/100, 0.05 → 1/20，>=1s 就直接显示 "1s"、"2s"
        if (shutterText != null)
        {
            shutterText.text = FormatShutter(cam.shutterSpeed);
        }
    }

    // --------- 格式化函数 ---------

    string FormatAperture(float fNumber)
    {
        if (fNumber <= 0f) return "-";

        // 你的项目里 F 基本都是整数，所以直接四舍五入即可
        int denom = Mathf.Max(1, Mathf.RoundToInt(fNumber));
        return "1/" + denom;
    }

    string FormatShutter(float seconds)
    {
        if (seconds <= 0f) return "-";

        // 1 秒及以上，直接显示 "1s"、"2s"
        if (seconds >= 1f)
        {
            int secInt = Mathf.RoundToInt(seconds);
            return secInt.ToString() + "s";
        }

        // 小于 1 秒，显示 1/xx
        float denomFloat = 1f / seconds;
        int denom = Mathf.Max(1, Mathf.RoundToInt(denomFloat));
        return "1/" + denom;
    }
}
