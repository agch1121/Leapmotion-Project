using UnityEngine;

/// <summary>
/// 실시간 쥐는 강도 디버깅 텍스트 표시 시스템
/// 화면 상단에 간단한 텍스트로 실시간 정보 표시
/// </summary>
public class GripDebugDisplay : MonoBehaviour
{
    [Header("디버그 설정")]
    public bool enableDebugDisplay = true;
    public KeyCode toggleKey = KeyCode.F1;
    public float updateInterval = 0.1f;

    [Header("표시 옵션")]
    public bool showFingerDetails = true;
    public bool showForceInfo = true;
    public bool showSystemInfo = true;

    // 시스템 참조
    private GripCalculator gripCalculator;
    private HandController handController;
    private ForceCalculator forceCalculator;

    // 업데이트 제어
    private float lastUpdateTime = 0f;
    private bool isVisible = true;

    // 표시할 정보 저장
    private string debugText = "";
    private readonly string[] fingerNames = { "엄지", "검지", "중지", "약지", "새끼" };

    void Start()
    {
        InitializeDebugDisplay();
    }

    void InitializeDebugDisplay()
    {
        gripCalculator = FindFirstObjectByType<GripCalculator>();
        handController = FindFirstObjectByType<HandController>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();

        if (gripCalculator == null)
        {
            Debug.LogWarning("GripCalculator를 찾을 수 없습니다. 기본 정보만 표시됩니다.");
        }

        Debug.Log("GripDebugDisplay 초기화 완료 - F1키로 토글");
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDisplay();
        }

        if (enableDebugDisplay && isVisible && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateDebugText();
            lastUpdateTime = Time.time;
        }
    }

    void UpdateDebugText()
    {
        debugText = "";

        debugText += "=== GRIP DEBUG ===\n";

        if (handController != null)
        {
            debugText += $"쥐는 강도 시스템: {handController.GetGripSystemInfo()}\n";
            debugText += $"기본 쥐는 강도: {handController.RightHandGrabStrength:F3}\n";
        }

        if (gripCalculator != null)
        {
            debugText += $"커스텀 쥐는 강도: {gripCalculator.CustomGrabStrength:F3}\n";
            debugText += $"민감도: {gripCalculator.sensitivity:F1}\n";

            if (showFingerDetails)
            {
                debugText += "\n--- 개별 손가락 ---\n";
                for (int i = 0; i < fingerNames.Length && i < gripCalculator.FingerCurlValues.Length; i++)
                {
                    float fingerValue = gripCalculator.FingerCurlValues[i];
                    string bar = CreateProgressBar(fingerValue, 10);
                    debugText += $"{fingerNames[i]}: {fingerValue:F2} {bar}\n";
                }
            }
        }

        if (showForceInfo && forceCalculator != null)
        {
            debugText += "\n--- 힘 계산 ---\n";
            debugText += $"최종 힘: {forceCalculator.CurrentForce:F3} ({forceCalculator.CurrentForceLevel})\n";
            debugText += $"쥐는 강도 기여: {forceCalculator.NormalizedGripStrength:F2}\n";
            debugText += $"속도 기여: {forceCalculator.NormalizedVelocity:F2}\n";

            string forceBar = CreateProgressBar(forceCalculator.CurrentForce, 15);
            string forceColor = GetForceColorText(forceCalculator.CurrentForceLevel);
            debugText += $"힘 표시: {forceBar} {forceColor}\n";
        }

        if (showSystemInfo && handController != null)
        {
            debugText += "\n--- 시스템 상태 ---\n";
            debugText += $"손 속도: {handController.RightHandVelocity.magnitude:F2} m/s\n";
            debugText += $"타격 감지: {(handController.IsStrikeDetected ? "예" : "아니오")}\n";

            if (gripCalculator != null)
            {
                debugText += $"엄지 가중치: {gripCalculator.thumbWeight:F1}\n";
                debugText += $"최소 임계값: {gripCalculator.minimumThreshold:F2}\n";
            }
        }

        debugText += "\n[F1: 토글]";
    }

    string CreateProgressBar(float value, int length)
    {
        int filledLength = Mathf.RoundToInt(value * length);
        string bar = "[";

        for (int i = 0; i < length; i++)
        {
            if (i < filledLength)
                bar += "█";
            else
                bar += "░";
        }

        bar += "]";
        return bar;
    }

    string GetForceColorText(ForceCalculator.ForceLevel level)
    {
        switch (level)
        {
            case ForceCalculator.ForceLevel.Weak:
                return "안전";
            case ForceCalculator.ForceLevel.Medium:
                return "주의";
            case ForceCalculator.ForceLevel.Strong:
                return "위험";
            default:
                return "알수없음";
        }
    }

    void ToggleDisplay()
    {
        isVisible = !isVisible;
        Debug.Log($"Grip Debug Display: {(isVisible ? "활성화" : "비활성화")}");
    }

    void OnGUI()
    {
        if (!enableDebugDisplay || !isVisible || string.IsNullOrEmpty(debugText))
            return;

        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = CreateColorTexture(new Color(0, 0, 0, 0.8f));

        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 14;
        textStyle.normal.textColor = Color.white;
        textStyle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textStyle.padding = new RectOffset(10, 10, 10, 10);

        float panelWidth = 350f;
        float panelHeight = CalculateTextHeight(debugText, textStyle) + 20f;

        Rect backgroundRect = new Rect(10, 10, panelWidth, panelHeight);
        Rect textRect = new Rect(15, 15, panelWidth - 10, panelHeight - 10);

        GUI.Box(backgroundRect, "", backgroundStyle);
        GUI.Label(textRect, debugText, textStyle);
    }

    float CalculateTextHeight(string text, GUIStyle style)
    {
        int lineCount = text.Split('\n').Length;
        return lineCount * style.fontSize * 1.2f;
    }

    Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    [ContextMenu("현재 상태 출력")]
    public void PrintCurrentState()
    {
        UpdateDebugText();
        Debug.Log(debugText);
    }

    [ContextMenu("세부 정보 토글")]
    public void ToggleDetailedInfo()
    {
        showFingerDetails = !showFingerDetails;
        showForceInfo = !showForceInfo;
        showSystemInfo = !showSystemInfo;

        Debug.Log($"세부 정보 표시: {(showFingerDetails ? "활성화" : "비활성화")}");
    }
}