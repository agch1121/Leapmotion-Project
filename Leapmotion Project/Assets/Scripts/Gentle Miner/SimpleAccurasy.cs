using UnityEngine;

/// <summary>
/// 간단한 트리거 기반 정확도 시스템
/// 끌 타겟 구체와 망치 끝 구체의 겹침으로 정확도 측정
/// </summary>
public class SimpleAccuracy : MonoBehaviour
{
    [Header("정확도 영역 설정")]
    [Range(0.03f, 0.1f)]
    public float perfectRadius = 0.05f;    // 완벽 구역 반지름
    [Range(0.08f, 0.15f)]
    public float goodRadius = 0.1f;        // 좋음 구역 반지름  
    [Range(0.12f, 0.2f)]
    public float allowedRadius = 0.15f;    // 허용 구역 반지름

    [Header("망치 설정")]
    [Range(0.02f, 0.08f)]
    public float hammerTipRadius = 0.04f;  // 망치 끝 구체 반지름

    [Header("시각적 피드백")]
    public Material perfectMaterial;       // 완벽 구역 재질 (초록)
    public Material goodMaterial;          // 좋음 구역 재질 (노랑)
    public Material allowedMaterial;       // 허용 구역 재질 (주황)
    public Material hammerMaterial;        // 망치 끝 재질

    [Header("점수 설정")]
    public int perfectScore = 100;
    public int goodScore = 70;
    public int allowedScore = 40;
    public int failScore = 0;

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool showGizmos = true;

    // 컴포넌트 참조
    private HandController handController;
    private ToolSystem toolSystem;

    // 타겟 구체들 (끌이 가리키는 지점)
    private GameObject perfectSphere;
    private GameObject goodSphere;
    private GameObject allowedSphere;

    // 망치 끝 구체
    private GameObject hammerTipSphere;

    // 현재 상태
    public enum AccuracyLevel
    {
        Perfect,
        Good,
        Allowed,
        Failed
    }

    private AccuracyLevel currentAccuracy = AccuracyLevel.Failed;
    private Vector3 currentTargetPosition;
    private bool isTargetValid = false;

    // 이벤트
    public System.Action<AccuracyLevel, int> OnAccuracyChanged;
    public System.Action<AccuracyLevel, int> OnAccuracyMeasured;

    // 프로퍼티
    public AccuracyLevel CurrentAccuracy => currentAccuracy;
    public bool IsTargetValid => isTargetValid;

    void Start()
    {
        InitializeSimpleAccuracy();
    }

    void InitializeSimpleAccuracy()
    {
        // 시스템 참조
        handController = FindFirstObjectByType<HandController>();
        toolSystem = FindFirstObjectByType<ToolSystem>();

        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return;
        }

        // 기본 재질 생성
        CreateDefaultMaterials();

        // 타겟 구체들 생성
        CreateTargetSpheres();

        // 망치 끝 구체 생성
        CreateHammerTipSphere();

        // HandController 이벤트 구독
        if (handController != null)
        {
            handController.OnHammerStrike += OnHammerStrike;
        }

        Debug.Log("SimpleAccuracy 시스템 초기화 완료");
    }

    void CreateDefaultMaterials()
    {
        if (perfectMaterial == null)
        {
            perfectMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            perfectMaterial.color = new Color(0f, 1f, 0f, 0.3f); // 반투명 초록
            SetMaterialTransparent(perfectMaterial);
        }

        if (goodMaterial == null)
        {
            goodMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            goodMaterial.color = new Color(1f, 1f, 0f, 0.3f); // 반투명 노랑
            SetMaterialTransparent(goodMaterial);
        }

        if (allowedMaterial == null)
        {
            allowedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            allowedMaterial.color = new Color(1f, 0.5f, 0f, 0.3f); // 반투명 주황
            SetMaterialTransparent(allowedMaterial);
        }

        if (hammerMaterial == null)
        {
            hammerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            hammerMaterial.color = new Color(1f, 0f, 0f, 0.5f); // 반투명 빨강
            SetMaterialTransparent(hammerMaterial);
        }
    }

    void SetMaterialTransparent(Material material)
    {
        // URP Lit 셰이더를 투명 모드로 설정
        material.SetFloat("_Surface", 1); // Transparent
        material.SetFloat("_Blend", 0);   // Alpha
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    void CreateTargetSpheres()
    {
        // 허용 구역 (가장 큰 구체, 뒤쪽)
        allowedSphere = CreateTargetSphere("AllowedTarget", allowedRadius, allowedMaterial);

        // 좋음 구역 (중간 구체)
        goodSphere = CreateTargetSphere("GoodTarget", goodRadius, goodMaterial);

        // 완벽 구역 (가장 작은 구체, 앞쪽)
        perfectSphere = CreateTargetSphere("PerfectTarget", perfectRadius, perfectMaterial);

        // 초기에는 모두 비활성화
        SetTargetSpheresActive(false);
    }

    GameObject CreateTargetSphere(string name, float radius, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.localScale = Vector3.one * (radius * 2f); // 반지름 → 지름

        // 트리거로 설정
        SphereCollider collider = sphere.GetComponent<SphereCollider>();
        collider.isTrigger = true;

        // 재질 적용
        Renderer renderer = sphere.GetComponent<Renderer>();
        renderer.material = material;

        // 태그 설정 (구분용)
        sphere.tag = name;

        return sphere;
    }

    void CreateHammerTipSphere()
    {
        hammerTipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hammerTipSphere.name = "HammerTip";
        hammerTipSphere.transform.localScale = Vector3.one * (hammerTipRadius * 2f);

        // 트리거로 설정
        SphereCollider collider = hammerTipSphere.GetComponent<SphereCollider>();
        collider.isTrigger = true;

        // 재질 적용
        Renderer renderer = hammerTipSphere.GetComponent<Renderer>();
        renderer.material = hammerMaterial;

        // 태그 설정
        hammerTipSphere.tag = "HammerTip";

        // 트리거 감지 스크립트 추가
        HammerTipTrigger triggerScript = hammerTipSphere.AddComponent<HammerTipTrigger>();
        triggerScript.Initialize(this);
    }

    void Update()
    {
        UpdateTargetPosition();
        UpdateHammerTipPosition();
        UpdateAccuracyFeedback();
    }

    void UpdateTargetPosition()
    {
        // ToolSystem에서 끌 타겟 위치 가져오기
        if (toolSystem != null)
        {
            Vector3 chiselTarget = toolSystem.GetCurrentChiselTarget();

            // 유효한 타겟인지 확인 (광물 블록 범위 내)
            isTargetValid = IsValidTarget(chiselTarget);

            if (isTargetValid)
            {
                currentTargetPosition = chiselTarget;
                UpdateTargetSpherePositions(currentTargetPosition);
                SetTargetSpheresActive(true);
            }
            else
            {
                SetTargetSpheresActive(false);
            }
        }
        else
        {
            // ToolSystem이 없으면 HandController 사용 (fallback)
            if (handController != null)
            {
                currentTargetPosition = handController.GetChiselTargetPoint();
                UpdateTargetSpherePositions(currentTargetPosition);
                SetTargetSpheresActive(true);
            }
        }
    }

    bool IsValidTarget(Vector3 targetPosition)
    {
        // 현재 광물 블록 찾기
        StageManager stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager != null)
        {
            GameObject mineralBlock = stageManager.GetCurrentMineralBlock();
            if (mineralBlock != null)
            {
                float distance = Vector3.Distance(targetPosition, mineralBlock.transform.position);
                return distance <= 3f; // 적당한 범위 내
            }
        }
        return true; // 기본적으로는 유효
    }

    void UpdateTargetSpherePositions(Vector3 position)
    {
        if (perfectSphere != null) perfectSphere.transform.position = position;
        if (goodSphere != null) goodSphere.transform.position = position;
        if (allowedSphere != null) allowedSphere.transform.position = position;
    }

    void SetTargetSpheresActive(bool active)
    {
        if (perfectSphere != null) perfectSphere.SetActive(active);
        if (goodSphere != null) goodSphere.SetActive(active);
        if (allowedSphere != null) allowedSphere.SetActive(active);
    }

    void UpdateHammerTipPosition()
    {
        if (hammerTipSphere != null && handController != null)
        {
            // 망치 끝을 오른손 위치로 이동
            Vector3 hammerPos = handController.RightHandPosition;

            // 약간의 오프셋 추가 (망치 끝이 손끝보다 약간 앞)
            Vector3 hammerForward = handController.RightHandRotation * Vector3.forward;
            hammerPos += hammerForward * 1.2f;

            hammerTipSphere.transform.position = hammerPos;
        }
    }

    void UpdateAccuracyFeedback()
    {
        if (!isTargetValid || hammerTipSphere == null) return;

        // 현재 망치와 타겟 사이 거리 계산
        float distance = Vector3.Distance(hammerTipSphere.transform.position, currentTargetPosition);

        AccuracyLevel newAccuracy = CalculateAccuracy(distance);

        if (newAccuracy != currentAccuracy)
        {
            currentAccuracy = newAccuracy;
            OnAccuracyChanged?.Invoke(currentAccuracy, GetScoreForAccuracy(currentAccuracy));

            if (enableDebugLogs)
            {
                Debug.Log($"정확도 변경: {currentAccuracy} (거리: {distance:F3}m)");
            }
        }
    }

    AccuracyLevel CalculateAccuracy(float distance)
    {
        // 망치 끝 반지름도 고려해서 계산
        float effectiveDistance = distance - hammerTipRadius;

        if (effectiveDistance <= perfectRadius) return AccuracyLevel.Perfect;
        if (effectiveDistance <= goodRadius) return AccuracyLevel.Good;
        if (effectiveDistance <= allowedRadius) return AccuracyLevel.Allowed;
        return AccuracyLevel.Failed;
    }

    int GetScoreForAccuracy(AccuracyLevel accuracy)
    {
        return accuracy switch
        {
            AccuracyLevel.Perfect => perfectScore,
            AccuracyLevel.Good => goodScore,
            AccuracyLevel.Allowed => allowedScore,
            AccuracyLevel.Failed => failScore,
            _ => 0
        };
    }

    /// <summary>
    /// 망치 타격 시 호출되는 이벤트 핸들러
    /// </summary>
    void OnHammerStrike(Vector3 hammerPosition, Vector3 chiselTarget, float strikeForce)
    {
        if (!isTargetValid) return;

        // 타격 시점의 정확도 측정
        float distance = Vector3.Distance(hammerPosition, currentTargetPosition);
        AccuracyLevel strikeAccuracy = CalculateAccuracy(distance);
        int score = GetScoreForAccuracy(strikeAccuracy);

        // 정확도 측정 이벤트 발생
        OnAccuracyMeasured?.Invoke(strikeAccuracy, score);

        if (enableDebugLogs)
        {
            Debug.Log($"타격 정확도: {strikeAccuracy} | 거리: {distance:F3}m | 점수: {score}");
        }

        // 시각적 피드백 생성
        CreateAccuracyFeedback(hammerPosition, strikeAccuracy);
    }

    void CreateAccuracyFeedback(Vector3 position, AccuracyLevel accuracy)
    {
        // 간단한 시각적 피드백
        GameObject feedback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        feedback.name = $"AccuracyFeedback_{accuracy}";
        feedback.transform.position = position;
        feedback.transform.localScale = Vector3.one * 0.1f;

        // 콜라이더 제거
        Destroy(feedback.GetComponent<Collider>());

        // 정확도에 따른 색상
        Color feedbackColor = GetColorForAccuracy(accuracy);
        Renderer renderer = feedback.GetComponent<Renderer>();
        renderer.material.color = feedbackColor;

        // 2초 후 삭제
        Destroy(feedback, 2f);
    }

    Color GetColorForAccuracy(AccuracyLevel accuracy)
    {
        return accuracy switch
        {
            AccuracyLevel.Perfect => Color.green,
            AccuracyLevel.Good => Color.yellow,
            AccuracyLevel.Allowed => new Color(1f, 0.5f, 0f), // 주황
            AccuracyLevel.Failed => Color.red,
            _ => Color.white
        };
    }

    /// <summary>
    /// 시스템 활성화/비활성화
    /// </summary>
    public void SetSystemEnabled(bool enabled)
    {
        this.enabled = enabled;
        SetTargetSpheresActive(enabled && isTargetValid);

        if (hammerTipSphere != null)
            hammerTipSphere.SetActive(enabled);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying) return;

        if (isTargetValid)
        {
            // 타겟 위치에 정확도 영역 그리기
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(currentTargetPosition, perfectRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(currentTargetPosition, goodRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawSphere(currentTargetPosition, allowedRadius);

            // 현재 정확도 표시
            Gizmos.color = GetColorForAccuracy(currentAccuracy);
            if (hammerTipSphere != null)
            {
                Gizmos.DrawWireSphere(hammerTipSphere.transform.position, hammerTipRadius);
            }
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (handController != null)
        {
            handController.OnHammerStrike -= OnHammerStrike;
        }

        // 생성한 구체들 정리
        if (perfectSphere != null) Destroy(perfectSphere);
        if (goodSphere != null) Destroy(goodSphere);
        if (allowedSphere != null) Destroy(allowedSphere);
        if (hammerTipSphere != null) Destroy(hammerTipSphere);
    }
}

/// <summary>
/// 망치 끝 트리거 감지용 헬퍼 클래스
/// </summary>
public class HammerTipTrigger : MonoBehaviour
{
    private SimpleAccuracy accuracySystem;

    public void Initialize(SimpleAccuracy system)
    {
        accuracySystem = system;
    }

    void OnTriggerEnter(Collider other)
    {
        if (accuracySystem != null && accuracySystem.enableDebugLogs)
        {
            Debug.Log($"망치가 {other.name}에 진입");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (accuracySystem != null && accuracySystem.enableDebugLogs)
        {
            Debug.Log($"망치가 {other.name}에서 벗어남");
        }
    }
}