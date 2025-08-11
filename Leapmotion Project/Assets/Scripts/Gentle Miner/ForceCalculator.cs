using UnityEngine;

/// <summary>
/// 기획서의 하이브리드 힘 계산 시스템 - 커스텀 쥐는 강도 연동
/// 최종 채굴 힘 = 주먹 쥠 강도 + 손 움직임 속도
/// 약함(0~55%), 보통(55~85%), 강함(85~100%)
/// </summary>
public class ForceCalculator : MonoBehaviour
{
    [Header("힘 계산 설정")]
    [Range(0f, 1f)]
    public float gripStrengthWeight = 0.6f; // 쥠 강도 가중치 (60%)
    [Range(0f, 1f)]
    public float velocityWeight = 0.4f; // 속도 가중치 (40%)

    [Header("커스텀 쥐는 강도 연동")]
    public bool useCustomGripCalculator = true;
    public float customGripMultiplier = 1.2f;

    [Header("속도 정규화 설정")]
    public float maxVelocityThreshold = 2f;
    public float minVelocityThreshold = 0.03f;

    [Header("힘 단계 구분")]
    [Range(0f, 1f)]
    public float weakForceThreshold = 0.3f; // 약함 상한선 (55%)
    [Range(0f, 1f)]
    public float strongForceThreshold = 0.7f; // 강함 하한선 (85%)

    [Header("보석 보호 시스템 연동")]
    public float forceMultiplierForGems = 30f;

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool showForceVisualization = true;

    // 시스템 참조
    private HandController handController;
    private GripCalculator gripCalculator;
    private UIManager uiManager;

    // 계산된 힘 데이터
    public float CurrentForce { get; private set; }
    public ForceLevel CurrentForceLevel { get; private set; }
    public float NormalizedGripStrength { get; private set; }
    public float NormalizedVelocity { get; private set; }

    // 커스텀 쥐는 강도 관련 데이터
    public float RawGripStrength { get; private set; }
    public float AdjustedGripStrength { get; private set; }

    /// <summary>
    /// 힘의 강도 단계
    /// </summary>
    public enum ForceLevel
    {
        Weak,    // 0~55%: 안전한 채굴
        Medium,  // 55~85%: 주의 필요  
        Strong   // 85~100%: 보석 손상 위험
    }

    void Start()
    {
        InitializeForceCalculator();
    }

    void InitializeForceCalculator()
    {
        handController = FindFirstObjectByType<HandController>();
        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return;
        }

        gripCalculator = handController.GetComponent<GripCalculator>();
        if (gripCalculator == null)
        {
            gripCalculator = FindFirstObjectByType<GripCalculator>();
        }

        uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogWarning("UIManager를 찾을 수 없습니다. UI 연동이 비활성화됩니다.");
        }

        Debug.Log($"ForceCalculator 초기화 완료 - 커스텀 쥐는 강도: {(useCustomGripCalculator && gripCalculator != null ? "활성" : "비활성")}");
    }

    void Update()
    {
        if (handController != null)
        {
            CalculateCurrentForce();
            UpdateForceLevel();

            if (enableDebugLogs && handController.IsStrikeDetected)
            {
                LogForceCalculation();
            }
        }
    }

    void CalculateCurrentForce()
    {
        // 1. 주먹 쥠 강도 정규화 (0~1)
        RawGripStrength = handController.RightHandGrabStrength;

        if (useCustomGripCalculator && gripCalculator != null)
        {
            AdjustedGripStrength = RawGripStrength * customGripMultiplier;
        }
        else
        {
            AdjustedGripStrength = RawGripStrength;
        }

        NormalizedGripStrength = Mathf.Clamp01(AdjustedGripStrength);

        // 2. 손 움직임 속도 정규화 (0~1)
        float rawVelocity = handController.RightHandVelocity.magnitude;
        NormalizedVelocity = NormalizeVelocity(rawVelocity);

        // 3. 기획서 공식 적용: 최종 채굴 힘 = 쥠 강도 + 속도
        CurrentForce = (NormalizedGripStrength * gripStrengthWeight) +
                      (NormalizedVelocity * velocityWeight);

        // 4. 0~1 범위로 클램프
        CurrentForce = Mathf.Clamp01(CurrentForce);
    }

    float NormalizeVelocity(float rawVelocity)
    {
        if (rawVelocity <= minVelocityThreshold)
            return 0f;

        if (rawVelocity >= maxVelocityThreshold)
            return 1f;

        return (rawVelocity - minVelocityThreshold) /
               (maxVelocityThreshold - minVelocityThreshold);
    }


    void UpdateForceLevel()
    {
        if (CurrentForce <= weakForceThreshold)
        {
            CurrentForceLevel = ForceLevel.Weak;
        }
        else if (CurrentForce <= strongForceThreshold)
        {
            CurrentForceLevel = ForceLevel.Medium;
        }
        else
        {
            CurrentForceLevel = ForceLevel.Strong;
        }
    }

    public float GetGemProtectionForce()
    {
        return CurrentForce * forceMultiplierForGems;
    }

    public bool IsSafeForce()
    {
        return CurrentForceLevel == ForceLevel.Weak;
    }

    public bool IsDangerousForGems()
    {
        return CurrentForceLevel == ForceLevel.Strong;
    }

    public void UpdateUIForce()
    {
        if (uiManager != null)
        {
            // UIManager의 힘 표시 시스템과 연동
        }
    }

    void LogForceCalculation()
    {
        string gripType = useCustomGripCalculator && gripCalculator != null ? "커스텀" : "기본";

        Debug.Log("=== 힘 계산 결과 ===");
        Debug.Log($"쥠 강도 ({gripType}): 원본={RawGripStrength:F2}, 조정={AdjustedGripStrength:F2}, 정규화={NormalizedGripStrength:F2}");
        Debug.Log($"속도: 원본={handController.RightHandVelocity.magnitude:F2}, 정규화={NormalizedVelocity:F2}");
        Debug.Log($"최종 힘: {CurrentForce:F2} ({CurrentForceLevel})");
        Debug.Log($"보석용 힘: {GetGemProtectionForce():F1}");
        Debug.Log($"안전 여부: {(IsSafeForce() ? "안전" : "위험")}");

        if (useCustomGripCalculator && gripCalculator != null)
        {
            Debug.Log($"커스텀 배율: {customGripMultiplier:F1}");
            Debug.Log($"GripCalculator 민감도: {gripCalculator.sensitivity:F1}");
        }
        Debug.Log("==================");
    }

    public Color GetForceColor()
    {
        switch (CurrentForceLevel)
        {
            case ForceLevel.Weak:
                return Color.green;
            case ForceLevel.Medium:
                return Color.yellow;
            case ForceLevel.Strong:
                return Color.red;
            default:
                return Color.white;
        }
    }

    public string GetForceDescription()
    {
        switch (CurrentForceLevel)
        {
            case ForceLevel.Weak:
                return "부드러운 채굴 (0-30%)";
            case ForceLevel.Medium:
                return "적당한 채굴 (30-70%)";
            case ForceLevel.Strong:
                return "강력한 채굴 (70-100%)";
            default:
                return "알 수 없음";
        }
    }

    public (float force, ForceLevel level, bool isSafe, string description) GetForceStatus()
    {
        return (CurrentForce, CurrentForceLevel, IsSafeForce(), GetForceDescription());
    }

    public void SetCustomGripEnabled(bool enabled)
    {
        useCustomGripCalculator = enabled;
        Debug.Log($"커스텀 쥐는 강도 계산기: {(enabled ? "활성화" : "비활성화")}");
    }

    public void SetCustomGripMultiplier(float multiplier)
    {
        customGripMultiplier = Mathf.Clamp(multiplier, 0.1f, 3.0f);
        Debug.Log($"커스텀 쥐는 강도 배율 설정: {customGripMultiplier:F1}");
    }

    [ContextMenu("약한 힘 테스트 (30%)")]
    public void TestWeakForce()
    {
        CurrentForce = 0.3f;
        UpdateForceLevel();
        Debug.Log($"테스트 힘 설정: {CurrentForce:F2} ({CurrentForceLevel})");
    }

    [ContextMenu("보통 힘 테스트 (70%)")]
    public void TestMediumForce()
    {
        CurrentForce = 0.7f;
        UpdateForceLevel();
        Debug.Log($"테스트 힘 설정: {CurrentForce:F2} ({CurrentForceLevel})");
    }

    [ContextMenu("강한 힘 테스트 (90%)")]
    public void TestStrongForce()
    {
        CurrentForce = 0.9f;
        UpdateForceLevel();
        Debug.Log($"테스트 힘 설정: {CurrentForce:F2} ({CurrentForceLevel})");
    }

    [ContextMenu("현재 힘 상태 출력")]
    public void PrintCurrentForceStatus()
    {
        var status = GetForceStatus();
        string gripSystem = useCustomGripCalculator && gripCalculator != null ? "커스텀" : "기본";

        Debug.Log("=== 현재 힘 상태 ===");
        Debug.Log($"쥐는 강도 시스템: {gripSystem}");
        Debug.Log($"힘: {status.force * 100f:F1}%");
        Debug.Log($"레벨: {status.level}");
        Debug.Log($"안전: {status.isSafe}");
        Debug.Log($"설명: {status.description}");
        Debug.Log($"원본 쥐는 강도: {RawGripStrength:F2}");
        Debug.Log($"조정된 쥐는 강도: {AdjustedGripStrength:F2}");
        Debug.Log($"속도 기여분: {NormalizedVelocity:F2}");
        Debug.Log("===================");
    }

    [ContextMenu("쥐는 강도 시스템 전환")]
    public void ToggleGripSystem()
    {
        SetCustomGripEnabled(!useCustomGripCalculator);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showForceVisualization) return;

        // 힘 레벨에 따른 색상으로 시각화
        Gizmos.color = GetForceColor();

        // 현재 힘을 구체 크기로 표시
        Vector3 visualPos = transform.position + Vector3.up * 2f;
        float sphereSize = 0.1f + (CurrentForce * 0.3f);
        Gizmos.DrawSphere(visualPos, sphereSize);

        // 힘 레벨 구간을 선으로 표시
        Gizmos.color = Color.white;
        Vector3 barStart = visualPos + Vector3.left * 0.5f;
        Vector3 barEnd = visualPos + Vector3.right * 0.5f;
        Gizmos.DrawLine(barStart, barEnd);

        // 현재 힘 위치 표시
        Vector3 forcePos = Vector3.Lerp(barStart, barEnd, CurrentForce);
        Gizmos.color = GetForceColor();
        Gizmos.DrawSphere(forcePos, 0.05f);

        // 약함/강함 경계선 표시
        Gizmos.color = Color.yellow;
        Vector3 weakBoundary = Vector3.Lerp(barStart, barEnd, weakForceThreshold);
        Vector3 strongBoundary = Vector3.Lerp(barStart, barEnd, strongForceThreshold);
        Gizmos.DrawLine(weakBoundary + Vector3.up * 0.1f, weakBoundary + Vector3.down * 0.1f);
        Gizmos.DrawLine(strongBoundary + Vector3.up * 0.1f, strongBoundary + Vector3.down * 0.1f);
    }
}