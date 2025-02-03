using UnityEngine;
using UnityEngine.UI;

public class MobileResolutionManager : MonoBehaviour
{
    [Header("Canvas References")]
    public CanvasScaler canvasScaler;
    public Canvas mainCanvas;

    [Header("Resolution Settings")]
    [Tooltip("目標直向寬高比")]
    public float targetAspect = 9f / 16f;

    [Tooltip("參考解析度")]
    public Vector2 referenceResolution = new Vector2(720, 1280);

    void Start()
    {
        if (IsMobilePlatform())
        {
            ConfigureMobileResolution();
        }
    }

    bool IsMobilePlatform()
    {
        // WebGL平台 且 為手持/平板設備
        return Application.platform == RuntimePlatform.WebGLPlayer &&
               (SystemInfo.deviceType == DeviceType.Handheld);
    }

    void ConfigureMobileResolution()
    {
        // 強制直向
        Screen.orientation = ScreenOrientation.Portrait;

        // 獲取當前螢幕寬高
        int width = Screen.width;
        int height = Screen.height;

        // 計算當前螢幕寬高比
        float currentAspect = (float)width / height;

        // 根據目標寬高比調整寬度或高度
        if (currentAspect > targetAspect)
        {
            width = Mathf.RoundToInt(height * targetAspect);
        }
        else
        {
            height = Mathf.RoundToInt(width / targetAspect);
        }

        // 設置解析度
        Screen.SetResolution(width, height, true);

        // 配置 CanvasScaler
        if (canvasScaler != null)
        {
            // 使用螢幕大小作為參考
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = width > height ? 0f : 1f;
        }

        // 確保Canvas正確縮放
        if (mainCanvas != null)
        {
            mainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        // 限制幀率
        Application.targetFrameRate = 30;

        Debug.Log($"Mobile Resolution Configured: {width}x{height}, Aspect: {(float)width / height}");
    }

    // 額外的調試方法
    void OnValidate()
    {
        // 在編輯器中即時預覽
        if (Application.isPlaying && IsMobilePlatform())
        {
            ConfigureMobileResolution();
        }
    }
}