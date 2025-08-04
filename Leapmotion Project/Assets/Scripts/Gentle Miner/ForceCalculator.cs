using UnityEngine;

/// <summary>
/// 기획서의 하이브리드 힘 계산 시스템
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

    [Header("속도 정규화 설정")]
    public float maxVelocityThreshold = 3f; // 최대 속도 기준값
    public float minVelocityThreshold = 0.1f; // 최소 속도 기준값

    [Header("힘 단계 구분")]
    [Range(0f, 1f)]
    public float weakForceThreshold = 0.55f; // 약함 상한선 (55%)
    [Range(0f, 1f)]
    public float strongForceThreshold = 0.85f; // 강함 하한선 (85%)

    [Header("보석 보호 시스템 연동")]
    public float forceMultiplierForGems = 30f; // GemProtectionSystem용 힘 배율

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool showForceVisualization = true;

    // 시스템 참조
    private HandController handController;
    private UIManager uiManager;

    // 계산된 힘 데이터
    public float CurrentForce { get; private set; }
    public ForceLevel CurrentForceLevel { get; private set; }
    public float NormalizedGripStrength { get; private set; }
    public float NormalizedVelocity { get; private set; }

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
        // HandController 참조
        handController = FindFirstObjectByType<HandController>();
        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return;
        }

        // UIManager 참조 (힘 표시용)
        uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogWarning("UIManager를 찾을 수 없습니다. UI 연동이 비활성화됩니다.");
        }

        Debug.Log("ForceCalculator 초기화 완료");
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

    /// <summary>
    /// 기획서의 하이브리드 힘 계산 공식 적용
    /// </summary>
    void CalculateCurrentForce()
    {
        // 1. 주먹 쥠 강도 정규화 (0~1)
        NormalizedGripStrength = Mathf.Clamp01(handController.RightHandGrabStrength);

        // 2. 손 움직임 속도 정규화 (0~1)
        float rawVelocity = handController.RightHandVelocity.magnitude;
        NormalizedVelocity = NormalizeVelocity(rawVelocity);

        // 3. 기획서 공식 적용: 최종 채굴 힘 = 쥠 강도 + 속도
        CurrentForce = (NormalizedGripStrength * gripStrengthWeight) +
                      (NormalizedVelocity * velocityWeight);

        // 4. 0~1 범위로 클램프
        CurrentForce = Mathf.Clamp01(CurrentForce);
    }

    /// <summary>
    /// 속도를 0~1 범위로 정규화
    /// </summary>
    float NormalizeVelocity(float rawVelocity)
    {
        // 최소값 이하는 0으로 처리
        if (rawVelocity <= minVelocityThreshold)
            return 0f;

        // 최대값 이상은 1로 처리  
        if (rawVelocity >= maxVelocityThreshold)
            return 1f;

        // 선형 보간으로 0~1 변환
        return (rawVelocity - minVelocityThreshold) /
               (maxVelocityThreshold - minVelocityThreshold);
    }

    /// <summary>
    /// 힘 레벨 업데이트 (약함/보통/강함)
    /// </summary>
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

    /// <summary>
    /// GemProtectionSystem에서 사용할 힘 값 계산
    /// </summary>
    public float GetGemProtectionForce()
    {
        return CurrentForce * forceMultiplierForGems;
    }

    /// <summary>
    /// 현재 힘이 안전한 수준인지 확인
    /// </summary>
    public bool IsSafeForce()
    {
        return CurrentForceLevel == ForceLevel.Weak;
    }

    /// <summary>
    /// 보석에 위험한 수준의 힘인지 확인  
    /// </summary>
    public bool IsDangerousForGems()
    {
        return CurrentForceLevel == ForceLevel.Strong;
    }

    /// <summary>
    /// UIManager와 연동하여 힘 정보 전달
    /// </summary>
    public void UpdateUIForce()
    {
        if (uiManager != null)
        {
            // UIManager의 힘 표시 시스템과 연동
            // (UIManager에 ForceCalculator 연동 메서드 필요)
        }
    }

    /// <summary>
    /// 힘 계산 상세 정보 로그 출력
    /// </summary>
    void LogForceCalculation()
    {
        Debug.Log("=== 힘 계산 결과 ===");
        Debug.Log($"쥠 강도: {NormalizedGripStrength:F2} ({handController.RightHandGrabStrength:F2})");
        Debug.Log($"속도: {NormalizedVelocity:F2} ({handController.RightHandVelocity.magnitude:F2})");
        Debug.Log($"최종 힘: {CurrentForce:F2} ({CurrentForceLevel})");
        Debug.Log($"보석용 힘: {GetGemProtectionForce():F1}");
        Debug.Log($"안전 여부: {(IsSafeForce() ? "안전" : "위험")}");
        Debug.Log("==================");
    }

    /// <summary>
    /// 힘 레벨에 따른 색상 반환 (UI용)
    /// </summary>
    public Color GetForceColor()
    {
        switch (CurrentForceLevel)
        {
            case ForceLevel.Weak:
                return Color.green; // 안전 - 초록색
            case ForceLevel.Medium:
                return Color.yellow; // 주의 - 노란색
            case ForceLevel.Strong:
                return Color.red; // 위험 - 빨간색
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// 힘 레벨 설명 텍스트 반환
    /// </summary>
    public string GetForceDescription()
    {
        switch (CurrentForceLevel)
        {
            case ForceLevel.Weak:
                return "안전한 채굴";
            case ForceLevel.Medium:
                return "주의 필요";
            case ForceLevel.Strong:
                return "보석 손상 위험!";
            default:
                return "알 수 없음";
        }
    }

    /// <summary>
    /// 현재 힘 상태의 상세 정보 반환
    /// </summary>
    public (float force, ForceLevel level, bool isSafe, string description) GetForceStatus()
    {
        return (CurrentForce, CurrentForceLevel, IsSafeForce(), GetForceDescription());
    }

    /// <summary>
    /// 테스트용: 특정 값으로 힘 강제 설정
    /// </summary>
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

        Debug.Log("=== 현재 힘 상태 ===");
        Debug.Log($"힘: {status.force * 100f:F1}%");
        Debug.Log($"레벨: {status.level}");
        Debug.Log($"안전: {status.isSafe}");
        Debug.Log($"설명: {status.description}");
        Debug.Log("===================");
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
    }
}