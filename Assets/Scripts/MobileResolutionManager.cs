using UnityEngine;
using UnityEngine.UI;

public class MobileResolutionManager : MonoBehaviour
{
    [Header("Canvas References")]
    public CanvasScaler canvasScaler;
    public Canvas mainCanvas;

    [Header("Resolution Settings")]
    [Tooltip("�ؼЪ��V�e����")]
    public float targetAspect = 9f / 16f;

    [Tooltip("�ѦҸѪR��")]
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
        // WebGL���x �B �����/���O�]��
        return Application.platform == RuntimePlatform.WebGLPlayer &&
               (SystemInfo.deviceType == DeviceType.Handheld);
    }

    void ConfigureMobileResolution()
    {
        // �j��V
        Screen.orientation = ScreenOrientation.Portrait;

        // �����e�ù��e��
        int width = Screen.width;
        int height = Screen.height;

        // �p���e�ù��e����
        float currentAspect = (float)width / height;

        // �ھڥؼмe����վ�e�שΰ���
        if (currentAspect > targetAspect)
        {
            width = Mathf.RoundToInt(height * targetAspect);
        }
        else
        {
            height = Mathf.RoundToInt(width / targetAspect);
        }

        // �]�m�ѪR��
        Screen.SetResolution(width, height, true);

        // �t�m CanvasScaler
        if (canvasScaler != null)
        {
            // �ϥοù��j�p�@���Ѧ�
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = width > height ? 0f : 1f;
        }

        // �T�OCanvas���T�Y��
        if (mainCanvas != null)
        {
            mainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        // ����V�v
        Application.targetFrameRate = 30;

        Debug.Log($"Mobile Resolution Configured: {width}x{height}, Aspect: {(float)width / height}");
    }

    // �B�~���ոդ�k
    void OnValidate()
    {
        // �b�s�边���Y�ɹw��
        if (Application.isPlaying && IsMobilePlatform())
        {
            ConfigureMobileResolution();
        }
    }
}