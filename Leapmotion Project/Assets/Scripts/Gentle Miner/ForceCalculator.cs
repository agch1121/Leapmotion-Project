using UnityEngine;

/// <summary>
/// 기획서의 하이브리드 힘 계산 시스템 - 커스텀 쥐는 강도 연동
/// 최종 채굴 힘 = 주먹 쥠 강도(정규화) * 가중치 + 손 움직임 속도(정규화) * 가중치
/// ForceLevel로 약/중/강 단계를 구분하여 UI/보석보호 시스템과 연동
/// </summary>
public class ForceCalculator : MonoBehaviour
{
    [Header("힘 계산 설정")]
    [Range(0f, 1f)]
    public float gripStrengthWeight = 0.6f; // 최종 힘에서 '쥠 강도' 비중 (예: 60%)
    [Range(0f, 1f)]
    public float velocityWeight = 0.4f;     // 최종 힘에서 '손 속도' 비중 (예: 40%)
    // ⚠️ 주의: 두 가중치의 합이 반드시 1일 필요는 없지만, 합이 1이 아닐 경우 체감 스케일이 변함.

    [Header("커스텀 쥐는 강도 연동")]
    public bool useCustomGripCalculator = true; // GripCalculator 사용 여부(없으면 HandController의 기본값 사용)
    public float customGripMultiplier = 1.2f;   // 커스텀 쥠 강도 보정 배율(민감도 미세 튜닝용)

    [Header("속도 정규화 설정")]
    public float maxVelocityThreshold = 2f;     // 이 이상 속도는 1로 클램프
    public float minVelocityThreshold = 0.03f;  // 이 이하 속도는 0으로 바닥 처리(미세 떨림 제거)

    [Header("힘 단계 구분")]
    [Range(0f, 1f)]
    public float weakForceThreshold = 0.3f;     // 약함 상한 경계(0~0.3 -> Weak)
    [Range(0f, 1f)]
    public float strongForceThreshold = 0.7f;   // 강함 하한 경계(0.7~1.0 -> Strong)
    // 💡 기획서 텍스트(55%/85%)와 현재 값(30%/70%)이 다름. 퍼센트 기준을 바꾸려면 여기 값을 0.55 / 0.85로 조정.

    [Header("보석 보호 시스템 연동")]
    public float forceMultiplierForGems = 30f;  // 보석 보호 시스템에서 사용하는 스케일 팩터(게임 밸런싱용)

    [Header("디버그")]
    public bool enableDebugLogs = true;         // 타격(frames) 시 계산 로그 출력
    public bool showForceVisualization = true;  // Gizmos로 힘 막대/구체 시각화

    // === 시스템 참조 ===
    private HandController handController; // ⚙️ 외부 스크립트 가정: RightHandGrabStrength(float 0~1), RightHandVelocity(Vector3), IsStrikeDetected(bool)
    private GripCalculator gripCalculator; // ⚙️ 커스텀 쥠 강도 계산기(없으면 기본 잡힘 강도 사용)
    private UIManager uiManager;           // ⚙️ 선택: UI 연동(게이지/텍스트 색상 등)

    // === 계산 결과(읽기 전용) ===
    public float CurrentForce { get; private set; }            // 최종 힘(0~1)
    public ForceLevel CurrentForceLevel { get; private set; }  // 약/중/강 단계
    public float NormalizedGripStrength { get; private set; }  // 정규화된 쥠 강도(0~1)
    public float NormalizedVelocity { get; private set; }      // 정규화된 속도(0~1)

    // === 내부 중간값(디버그용) ===
    public float RawGripStrength { get; private set; }         // HandController에서 읽은 원시 쥠 강도(0~1 가정)
    public float AdjustedGripStrength { get; private set; }    // 커스텀 배율/계산 반영 후 값

    /// <summary>
    /// 힘의 강도 단계(게임 로직 분기·이펙트·사운드/데미지 스케일 등에 활용)
    /// </summary>
    public enum ForceLevel
    {
        Weak,    // 안전(보석 손상 낮음)
        Medium,  // 주의 필요
        Strong   // 손상 위험(경고/진동/사운드 등 강하게)
    }

    void Start()
    {
        InitializeForceCalculator(); // 참조 캐싱 및 초기 상태 로그
    }

    /// <summary>
    /// 의존성(HandController, GripCalculator, UIManager) 찾아 캐싱
    /// </summary>
    void InitializeForceCalculator()
    {
        // 씬 내 첫 번째 HandController 탐색(런타임 1회만 수행 → 비용 저렴)
        handController = FindFirstObjectByType<HandController>();
        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return; // 필수 의존성 부재 → 업데이트 중 계산 중단
        }

        // 같은 오브젝트에 붙어 있으면 우선, 아니면 씬 전체에서 검색
        gripCalculator = handController.GetComponent<GripCalculator>();
        if (gripCalculator == null)
        {
            gripCalculator = FindFirstObjectByType<GripCalculator>();
        }

        // UI는 옵션. 없으면 단순 경고만 출력
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
            CalculateCurrentForce(); // 쥠 강도 + 속도 → 최종 힘(0~1)
            UpdateForceLevel();      // 최종 힘에 따른 레벨 결정

            // 특정 이벤트(예: 망치 타격 프레임)에서만 상세 로그 출력 → 스팸 방지
            if (enableDebugLogs && handController.IsStrikeDetected)
            {
                LogForceCalculation();
            }
        }
    }

    /// <summary>
    /// 최종 힘 계산 파이프라인
    /// 1) RawGripStrength→Adjusted→Normalized
    /// 2) 속도 정규화
    /// 3) 가중 평균으로 합성 후 [0,1] 클램프
    /// </summary>
    void CalculateCurrentForce()
    {
        // 1) 쥠 강도(0~1 가정) 읽기
        RawGripStrength = handController.RightHandGrabStrength;

        // 커스텀 계산기 사용 시 배율로 미세 조정(GripCalculator의 민감도와 별개)
        if (useCustomGripCalculator && gripCalculator != null)
        {
            AdjustedGripStrength = RawGripStrength * customGripMultiplier;
        }
        else
        {
            AdjustedGripStrength = RawGripStrength;
        }

        // 0~1로 클램프(배율로 1 초과 가능 → 안전 고정)
        NormalizedGripStrength = Mathf.Clamp01(AdjustedGripStrength);

        // 2) 손 속도 정규화(최소/최대 임계 기반 선형 매핑)
        float rawVelocity = handController.RightHandVelocity.magnitude;
        NormalizedVelocity = NormalizeVelocity(rawVelocity);

        // 3) 최종 힘 = 쥠 강도 기여 + 속도 기여
        CurrentForce = (NormalizedGripStrength * gripStrengthWeight) +
                       (NormalizedVelocity * velocityWeight);

        // 4) 안전 범위로 고정
        CurrentForce = Mathf.Clamp01(CurrentForce);
    }

    /// <summary>
    /// 속도를 [min, max] 구간으로 정규화하여 0~1로 반환
    /// </summary>
    float NormalizeVelocity(float rawVelocity)
    {
        if (rawVelocity <= minVelocityThreshold)
            return 0f; // 노이즈/정지 구간 제거

        if (rawVelocity >= maxVelocityThreshold)
            return 1f; // 상한 이상은 동일 취급

        // 구간 내 선형 스케일
        return (rawVelocity - minVelocityThreshold) /
               (maxVelocityThreshold - minVelocityThreshold);
    }

    /// <summary>
    /// 최종 힘(CurrentForce)에 따라 등급(Weak/Medium/Strong) 결정
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
    /// 보석 보호 시스템에서 쓰기 좋은 스케일 값(예: 데미지/내구도 계산 등에 바로 곱)
    /// </summary>
    public float GetGemProtectionForce()
    {
        return CurrentForce * forceMultiplierForGems;
    }

    /// <summary>
    /// 안전 구간 여부(약함 구간)
    /// </summary>
    public bool IsSafeForce()
    {
        return CurrentForceLevel == ForceLevel.Weak;
    }

    /// <summary>
    /// 보석에 위험한 구간 여부(강함 구간)
    /// </summary>
    public bool IsDangerousForGems()
    {
        return CurrentForceLevel == ForceLevel.Strong;
    }

    /// <summary>
    /// UI와 연동하여 게이지/텍스트/색상 등을 갱신(실제 구현은 UIManager 쪽에 맞춰 추가)
    /// </summary>
    public void UpdateUIForce()
    {
        if (uiManager != null)
        {
            // TODO: uiManager.SetForceGauge(CurrentForce, GetForceColor(), GetForceDescription()); 등으로 연결
        }
    }

    /// <summary>
    /// 타격 이벤트 시점에 상세 로그 출력(밸런싱/튜닝에 유용)
    /// </summary>
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

    /// <summary>
    /// 힘 레벨에 따른 대표 색상(녹/황/적)
    /// </summary>
    public Color GetForceColor()
    {
        switch (CurrentForceLevel)
        {
            case ForceLevel.Weak: return Color.green;
            case ForceLevel.Medium: return Color.yellow;
            case ForceLevel.Strong: return Color.red;
            default: return Color.white;
        }
    }

    /// <summary>
    /// UI 표기를 위한 설명 텍스트(퍼센트 범위는 현재 threshold 값과 일치하도록 수정 권장)
    /// </summary>
    public string GetForceDescription()
    {
        switch (CurrentForceLevel)
        {
            case ForceLevel.Weak: return "부드러운 채굴 (0-30%)";
            case ForceLevel.Medium: return "적당한 채굴 (30-70%)";
            case ForceLevel.Strong: return "강력한 채굴 (70-100%)";
            default: return "알 수 없음";
        }
    }

    /// <summary>
    /// 힘 관련 종합 상태 패킷(게이지/로그/UI 바인딩에 편리)
    /// </summary>
    public (float force, ForceLevel level, bool isSafe, string description) GetForceStatus()
    {
        return (CurrentForce, CurrentForceLevel, IsSafeForce(), GetForceDescription());
    }

    /// <summary>
    /// 런타임에서 커스텀 쥠 강도 시스템 on/off
    /// </summary>
    public void SetCustomGripEnabled(bool enabled)
    {
        useCustomGripCalculator = enabled;
        Debug.Log($"커스텀 쥐는 강도 계산기: {(enabled ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 커스텀 쥠 강도 배율(안전 범위 내 클램프)
    /// </summary>
    public void SetCustomGripMultiplier(float multiplier)
    {
        customGripMultiplier = Mathf.Clamp(multiplier, 0.1f, 3.0f);
        Debug.Log($"커스텀 쥐는 강도 배율 설정: {customGripMultiplier:F1}");
    }

    // === 인스펙터에서 바로 테스트 가능한 컨텍스트 메뉴 ===
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

    /// <summary>
    /// 씬 뷰에서 힘 값 시각화(Gizmos) — 디버깅/튜닝용
    /// </summary>
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showForceVisualization) return;

        // 현재 레벨 색으로 큰 구체(힘 크기에 비례한 반지름)
        Gizmos.color = GetForceColor();
        Vector3 visualPos = transform.position + Vector3.up * 2f;
        float sphereSize = 0.1f + (CurrentForce * 0.3f);
        Gizmos.DrawSphere(visualPos, sphereSize);

        // 하단 바(0~1 스케일)와 현재 위치 마커
        Gizmos.color = Color.white;
        Vector3 barStart = visualPos + Vector3.left * 0.5f;
        Vector3 barEnd = visualPos + Vector3.right * 0.5f;
        Gizmos.DrawLine(barStart, barEnd);

        // 현재 힘 위치
        Vector3 forcePos = Vector3.Lerp(barStart, barEnd, CurrentForce);
        Gizmos.color = GetForceColor();
        Gizmos.DrawSphere(forcePos, 0.05f);

        // 약함/강함 경계선 마커
        Gizmos.color = Color.yellow;
        Vector3 weakBoundary = Vector3.Lerp(barStart, barEnd, weakForceThreshold);
        Vector3 strongBoundary = Vector3.Lerp(barStart, barEnd, strongForceThreshold);
        Gizmos.DrawLine(weakBoundary + Vector3.up * 0.1f, weakBoundary + Vector3.down * 0.1f);
        Gizmos.DrawLine(strongBoundary + Vector3.up * 0.1f, strongBoundary + Vector3.down * 0.1f);
    }
}
