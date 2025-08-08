using UnityEngine;
using LibreFracture;
using System.Collections;

/// <summary>
/// 강화된 ToolSystem - 정확도 시스템 연동 및 도구 탈부착 지원
/// 망치 물리 제거, 실시간 가이드라인 색상 변경 추가
/// </summary>
public class ToolSystem : MonoBehaviour
{
    [Header("도구 시각적 표현")]
    public GameObject chiselPrefab;
    public GameObject hammerPrefab;
    public LineRenderer chiselGuideLine;

    [Header("References")]
    [SerializeField] private Transform chiselTransform; // 끌 본체(팁 없으면 이걸 사용)
    [SerializeField] private Transform chiselTip;       // 끌 팁(있으면 정확도 ↑)

    [Header("손 Visual 참조 (월드 좌표용)")]
    public Transform leftHandVisual;
    public Transform rightHandVisual;

    [Header("채굴 설정")]
    public LayerMask chunkLayer = -1;
    public float miningRadius = 0.1f;
    public int chunksPerStrike = 2;
    public float chiselRayDistance = 2f;

    [Header("시각적 가이드")]
    public bool showChiselPreview = true;
    public GameObject previewSphere;
    public Material safePreviewMaterial;
    public Material dangerPreviewMaterial;

    [Header("정확도 연동 시각적 피드백")]
    public bool enableAccuracyFeedback = true;
    public Material perfectGuideMaterial;    // 완벽 정확도 (초록)
    public Material goodGuideMaterial;       // 좋은 정확도 (노랑)
    public Material allowedGuideMaterial;    // 허용 정확도 (주황)
    public Material failGuideMaterial;       // 실패 정확도 (빨강)

    [Header("도구 탈부착 설정")]
    public bool enableToolDetachment = true;
    public Material detachedToolMaterial;    // 탈착된 도구 재질 (회색)
    public float detachedToolAlpha = 0.3f;   // 탈착된 도구 투명도

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds;
    public ParticleSystem miningParticleEffect;

    [Header("안전 시스템")]
    public bool enableSafetySystem = true;
    public float maxSafeDistance = 3f;

    // 시스템 참조들
    private HandController handController;
    private AccuracySystem accuracySystem;
    private ForceCalculator forceCalculator;
    private AudioSource audioSource;

    // 도구 상태
    private GameObject chiselInstance;
    private GameObject hammerInstance;
    private Vector3 currentChiselTarget;
    private bool isChiselTargetValid = false;
    private bool toolsEnabled = true;

    // 원본 재질 저장
    private Material originalChiselMaterial;
    private Material originalHammerMaterial;

    // 채굴 중 상태
    private float lastMiningTime = 0f;
    private float miningCooldown = 0.5f;

    void Start()
    {
        InitializeToolSystem();
    }

    void InitializeToolSystem()
    {
        // 시스템 참조 가져오기
        handController = FindFirstObjectByType<HandController>();
        accuracySystem = FindFirstObjectByType<AccuracySystem>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();

        if (handController == null)
        {
            Debug.LogError("HandController_Enhanced를 찾을 수 없습니다!");
            return;
        }

        // 손 Visual이 Inspector에서 할당되지 않았다면 HandController에서 가져오기
        if (leftHandVisual == null && handController.leftHandVisual != null)
        {
            leftHandVisual = handController.leftHandVisual;
        }

        if (rightHandVisual == null && handController.rightHandVisual != null)
        {
            rightHandVisual = handController.rightHandVisual;
        }

        // 오디오 소스 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // HandController 이벤트 구독
        handController.OnHammerStrike += OnHammerStrike;
        handController.OnToolStateChanged += OnToolStateChanged;

        // 도구 인스턴스 생성
        CreateToolInstances();

        // 미리보기 구체 설정
        SetupPreviewSphere();

        // 기본 재질 생성
        CreateDefaultMaterials();

        Debug.Log("강화된 ToolSystem 초기화 완료");
    }

    void CreateDefaultMaterials()
    {
        // 정확도 가이드라인 재질
        if (perfectGuideMaterial == null)
        {
            perfectGuideMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            perfectGuideMaterial.color = Color.green;
            perfectGuideMaterial.SetFloat("_Metallic", 0.8f);
        }

        if (goodGuideMaterial == null)
        {
            goodGuideMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            goodGuideMaterial.color = Color.yellow;
            goodGuideMaterial.SetFloat("_Metallic", 0.8f);
        }

        if (allowedGuideMaterial == null)
        {
            allowedGuideMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            allowedGuideMaterial.color = new Color(1f, 0.5f, 0f); // 주황색
            allowedGuideMaterial.SetFloat("_Metallic", 0.8f);
        }

        if (failGuideMaterial == null)
        {
            failGuideMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            failGuideMaterial.color = Color.red;
            failGuideMaterial.SetFloat("_Metallic", 0.8f);
        }

        // 탈착된 도구 재질
        if (detachedToolMaterial == null)
        {
            detachedToolMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            detachedToolMaterial.color = new Color(0.5f, 0.5f, 0.5f, detachedToolAlpha);
            detachedToolMaterial.SetFloat("_Metallic", 0.2f);
        }
    }

    void CreateToolInstances()
    {
        // 끌 인스턴스 생성
        if (chiselPrefab != null)
        {
            chiselInstance = Instantiate(chiselPrefab);
            chiselInstance.name = "Chisel_Instance";

            // 물리 비활성화 (손에 고정되므로)
            Rigidbody chiselRb = chiselInstance.GetComponent<Rigidbody>();
            if (chiselRb != null)
            {
                chiselRb.isKinematic = true;
            }

            // Collider도 트리거로 설정
            Collider chiselCol = chiselInstance.GetComponent<Collider>();
            if (chiselCol != null)
            {
                chiselCol.isTrigger = true;
            }

            // 원본 재질 저장
            Renderer chiselRenderer = chiselInstance.GetComponent<Renderer>();
            if (chiselRenderer != null)
            {
                originalChiselMaterial = chiselRenderer.material;
            }
        }

        // 망치 인스턴스 생성 (물리 완전 제거)
        if (hammerPrefab != null)
        {
            hammerInstance = Instantiate(hammerPrefab);
            hammerInstance.name = "Hammer_Instance";

            // 물리 완전 제거
            Rigidbody hammerRb = hammerInstance.GetComponent<Rigidbody>();
            if (hammerRb != null)
            {
                hammerRb.isKinematic = true;
            }

            // Collider를 트리거로 설정 (정확도 측정용)
            Collider hammerCol = hammerInstance.GetComponent<Collider>();
            if (hammerCol != null)
            {
                hammerCol.isTrigger = true;
            }

            // 원본 재질 저장
            Renderer hammerRenderer = hammerInstance.GetComponent<Renderer>();
            if (hammerRenderer != null)
            {
                originalHammerMaterial = hammerRenderer.material;
            }
        }

        // 가이드라인 설정
        if (chiselGuideLine == null)
        {
            chiselGuideLine = gameObject.AddComponent<LineRenderer>();
        }

        chiselGuideLine.material = perfectGuideMaterial; // 기본은 완벽 색상
        chiselGuideLine.startWidth = 0.01f;
        chiselGuideLine.endWidth = 0.01f;
        chiselGuideLine.positionCount = 2;
        chiselGuideLine.useWorldSpace = true;
    }

    void SetupPreviewSphere()
    {
        if (previewSphere == null)
        {
            previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            previewSphere.name = "ChiselPreview";
            previewSphere.transform.localScale = Vector3.one * 0.1f;
            Destroy(previewSphere.GetComponent<Collider>());
        }

        // 기본 재질 설정
        if (safePreviewMaterial == null)
        {
            safePreviewMaterial = new Material(Shader.Find("Standard"));
            safePreviewMaterial.color = Color.green;
            safePreviewMaterial.SetFloat("_Metallic", 0.5f);
        }

        if (dangerPreviewMaterial == null)
        {
            dangerPreviewMaterial = new Material(Shader.Find("Standard"));
            dangerPreviewMaterial.color = Color.red;
            safePreviewMaterial.SetFloat("_Metallic", 0.5f);
        }
    }

    void Update()
    {
        UpdateToolPositions();
        UpdateChiselTarget();
        UpdateVisualGuides();
        UpdateAccuracyFeedback(); // 새로운 기능
        HandleSafetySystem();

        // 게임이 시작되지 않았으면 도구 숨기기
        if (!GameManager.Instance.IsGameStarted)
            HideToolInstances();
        else
            ShowToolInstances();
    }

    void UpdateToolPositions()
    {
        // 도구가 비활성화되면 위치 업데이트 안함
        if (!toolsEnabled) return;

        // 손 Visual의 월드 좌표를 직접 사용
        if (leftHandVisual != null)
        {
            // 끌 위치 업데이트 (왼손 Visual의 월드 좌표 사용)
            if (chiselInstance != null)
            {
                chiselInstance.transform.position = leftHandVisual.position;
                chiselInstance.transform.rotation = leftHandVisual.rotation;

                // y축 위치 조정 (6배 높이)
                Vector3 adjustedPosition = chiselInstance.transform.position;
                adjustedPosition.y *= 6f;
                chiselInstance.transform.position = adjustedPosition;
            }
        }
        else if (handController != null)
        {
            // Visual이 없으면 HandController 값 사용 (fallback)
            if (chiselInstance != null)
            {
                chiselInstance.transform.position = handController.LeftHandPosition;
                chiselInstance.transform.rotation = handController.LeftHandRotation;
            }
        }

        if (rightHandVisual != null)
        {
            // 망치 위치 업데이트 (오른손 Visual의 월드 좌표 사용)
            if (hammerInstance != null)
            {
                hammerInstance.transform.position = rightHandVisual.position;
                hammerInstance.transform.rotation = rightHandVisual.rotation;

                // y축 위치 조정 (5배 높이)
                Vector3 adjustedPosition = hammerInstance.transform.position;
                adjustedPosition.y *= 5f;
                hammerInstance.transform.position = adjustedPosition;

                // 잡기 강도에 따른 시각적 피드백
                if (handController != null)
                {
                    float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
                    hammerInstance.transform.localScale = Vector3.one * gripScale;
                }
            }
        }
        else if (handController != null)
        {
            // Visual이 없으면 HandController 값 사용 (fallback)
            if (hammerInstance != null)
            {
                hammerInstance.transform.position = handController.RightHandPosition;
                hammerInstance.transform.rotation = handController.RightHandRotation;

                float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
                hammerInstance.transform.localScale = Vector3.one * gripScale;
            }
        }
    }

    void UpdateChiselTarget()
    {
        // 현재 활성 광물 블록 찾기
        GameObject currentMineralBlock = FindCurrentMineralBlock();

        if (currentMineralBlock == null)
        {
            isChiselTargetValid = false;

            // 끌의 월드 위치 사용
            Vector3 chiselWorldPos = chiselInstance != null ? chiselInstance.transform.position :
                               (leftHandVisual != null ? leftHandVisual.position : Vector3.zero);

            currentChiselTarget = chiselWorldPos + Vector3.forward * 0.5f;
            return;
        }

        // 끌의 실제 월드 위치와 방향 사용
        Vector3 chiselPos = chiselInstance != null ? chiselInstance.transform.position :
                           (leftHandVisual != null ? leftHandVisual.position : Vector3.zero);

        Vector3 chiselForward = chiselInstance != null ? chiselInstance.transform.forward :
                               (leftHandVisual != null ? leftHandVisual.forward : Vector3.forward);

        Ray chiselRay = new Ray(chiselPos, chiselForward);
        RaycastHit hit;

        // 채굴 대상(광물 블록)에 레이캐스트
        if (Physics.Raycast(chiselRay, out hit, chiselRayDistance, chunkLayer))
        {
            // 현재 활성 광물 블록의 자식인지 확인
            if (hit.collider.transform.IsChildOf(currentMineralBlock.transform))
            {
                currentChiselTarget = hit.point;
                isChiselTargetValid = true;
            }
            else
            {
                isChiselTargetValid = false;
            }
        }
        else
        {
            isChiselTargetValid = false;
            currentChiselTarget = chiselPos + chiselForward * 0.5f;
        }
    }

    GameObject FindCurrentMineralBlock()
    {
        // StageManager에서 현재 광물 블록 가져오기
        StageManager stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager != null)
        {
            return stageManager.GetCurrentMineralBlock();
        }

        // 백업: ChunkGraphManager가 있는 오브젝트 찾기
        ChunkGraphManager chunkManager = FindFirstObjectByType<ChunkGraphManager>();
        if (chunkManager != null)
        {
            return chunkManager.gameObject;
        }

        return null;
    }

    void UpdateVisualGuides()
    {
        // 도구가 비활성화되면 가이드도 숨김
        if (!toolsEnabled)
        {
            if (chiselGuideLine != null)
                chiselGuideLine.enabled = false;
            if (previewSphere != null)
                previewSphere.SetActive(false);
            return;
        }

        // 끌 가이드라인 업데이트
        if (chiselGuideLine != null && chiselInstance != null)
        {
            chiselGuideLine.enabled = true;
            chiselGuideLine.SetPosition(0, chiselInstance.transform.position);
            chiselGuideLine.SetPosition(1, currentChiselTarget);

            // 유효한 타격점인지에 따라 색상 변경
            Color lineColor = isChiselTargetValid ? Color.green : Color.gray;
            chiselGuideLine.startColor = lineColor;
            chiselGuideLine.endColor = lineColor;
        }

        // 채굴 지점 미리보기 업데이트
        if (previewSphere != null && showChiselPreview)
        {
            previewSphere.SetActive(isChiselTargetValid && toolsEnabled);

            if (isChiselTargetValid && toolsEnabled)
            {
                previewSphere.transform.position = currentChiselTarget;

                // 힘 레벨에 따른 색상 변경
                bool isSafeForce = forceCalculator?.IsSafeForce() ?? true;
                MeshRenderer renderer = previewSphere.GetComponent<MeshRenderer>();
                renderer.material = isSafeForce ? safePreviewMaterial : dangerPreviewMaterial;

                // 채굴 범위 시각화
                float previewScale = miningRadius * 2f;
                previewSphere.transform.localScale = Vector3.one * previewScale;
            }
        }
    }

    /// <summary>
    /// 정확도 시스템 연동 - 실시간 가이드라인 색상 업데이트 (새로운 기능)
    /// </summary>
    void UpdateAccuracyFeedback()
    {
        if (!enableAccuracyFeedback || !toolsEnabled || accuracySystem == null) return;

        if (hammerInstance == null || !isChiselTargetValid) return;

        // 현재 망치와 끌 타겟 사이의 정확도 예측
        Vector3 hammerPos = hammerInstance.transform.position;
        AccuracySystem.AccuracyLevel potentialAccuracy =
            accuracySystem.GetCurrentPotentialAccuracy(hammerPos, currentChiselTarget);

        // 가이드라인 색상을 정확도에 따라 변경
        if (chiselGuideLine != null)
        {
            Material targetMaterial = GetGuideMaterialForAccuracy(potentialAccuracy);
            chiselGuideLine.material = targetMaterial;

            // 색상도 동시에 업데이트
            Color guideColor = GetColorForAccuracy(potentialAccuracy);
            chiselGuideLine.startColor = guideColor;
            chiselGuideLine.endColor = guideColor;
        }

        // 미리보기 구체 색상도 정확도에 따라 변경
        if (previewSphere != null && previewSphere.activeInHierarchy)
        {
            MeshRenderer renderer = previewSphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = GetPreviewMaterialForAccuracy(potentialAccuracy);
            }
        }
    }

    /// <summary>
    /// 정확도 레벨에 따른 가이드 재질 반환
    /// </summary>
    Material GetGuideMaterialForAccuracy(AccuracySystem.AccuracyLevel accuracy)
    {
        return accuracy switch
        {
            AccuracySystem.AccuracyLevel.Perfect => perfectGuideMaterial,
            AccuracySystem.AccuracyLevel.Good => goodGuideMaterial,
            AccuracySystem.AccuracyLevel.Allowed => allowedGuideMaterial,
            AccuracySystem.AccuracyLevel.Failed => failGuideMaterial,
            _ => failGuideMaterial
        };
    }

    /// <summary>
    /// 정확도 레벨에 따른 색상 반환
    /// </summary>
    Color GetColorForAccuracy(AccuracySystem.AccuracyLevel accuracy)
    {
        return accuracy switch
        {
            AccuracySystem.AccuracyLevel.Perfect => Color.green,
            AccuracySystem.AccuracyLevel.Good => Color.yellow,
            AccuracySystem.AccuracyLevel.Allowed => new Color(1f, 0.5f, 0f), // 주황색
            AccuracySystem.AccuracyLevel.Failed => Color.red,
            _ => Color.red
        };
    }

    /// <summary>
    /// 정확도 레벨에 따른 미리보기 재질 반환
    /// </summary>
    Material GetPreviewMaterialForAccuracy(AccuracySystem.AccuracyLevel accuracy)
    {
        // 완벽/좋음은 안전 재질, 나머지는 위험 재질 사용
        return (accuracy == AccuracySystem.AccuracyLevel.Perfect ||
                accuracy == AccuracySystem.AccuracyLevel.Good)
                ? safePreviewMaterial : dangerPreviewMaterial;
    }

    /// <summary>
    /// 도구 상태 변경 이벤트 처리 (새로운 기능)
    /// </summary>
    void OnToolStateChanged(HandController.ToolState newState)
    {
        bool shouldEnable = (newState == HandController.ToolState.Attached);
        SetToolsEnabled(shouldEnable);

        Debug.Log($"도구 시각화: {(shouldEnable ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 도구 활성화/비활성화 설정
    /// </summary>
    void SetToolsEnabled(bool enabled)
    {
        toolsEnabled = enabled;

        if (chiselInstance != null)
        {
            UpdateToolAppearance(chiselInstance, originalChiselMaterial, enabled);
        }

        if (hammerInstance != null)
        {
            UpdateToolAppearance(hammerInstance, originalHammerMaterial, enabled);
        }

        // 정확도 시스템에도 상태 전달
        if (accuracySystem != null)
        {
            accuracySystem.SetSystemEnabled(enabled);
        }
    }

    /// <summary>
    /// 도구 외형 업데이트 (활성화/비활성화에 따라)
    /// </summary>
    void UpdateToolAppearance(GameObject tool, Material originalMaterial, bool enabled)
    {
        Renderer renderer = tool.GetComponent<Renderer>();
        if (renderer == null) return;

        if (enabled)
        {
            // 활성화: 원본 재질 복원
            renderer.material = originalMaterial;
            tool.SetActive(true);
        }
        else
        {
            // 비활성화: 회색/투명 재질 적용
            renderer.material = detachedToolMaterial;
            // 완전히 숨기지 않고 시각적으로만 비활성화 표시
        }
    }

    void HideToolInstances()
    {
        if (chiselInstance != null) chiselInstance.SetActive(false);
        if (hammerInstance != null) hammerInstance.SetActive(false);
        if (previewSphere != null) previewSphere.SetActive(false);
        if (chiselGuideLine != null) chiselGuideLine.enabled = false;
    }

    void ShowToolInstances()
    {
        if (chiselInstance != null) chiselInstance.SetActive(true);
        if (hammerInstance != null) hammerInstance.SetActive(true);
        if (previewSphere != null && toolsEnabled) previewSphere.SetActive(true);
        if (chiselGuideLine != null && toolsEnabled) chiselGuideLine.enabled = true;
    }

    void HandleSafetySystem()
    {
        if (!enableSafetySystem) return;

        // 현재 광물 블록 위치 기준으로 안전 거리 체크
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null) return;

        Vector3 centerPos = currentMineralBlock.transform.position;

        // 도구의 실제 월드 위치로 거리 체크
        if (chiselInstance != null)
        {
            float chiselDistance = Vector3.Distance(chiselInstance.transform.position, centerPos);
        }

        if (hammerInstance != null)
        {
            float hammerDistance = Vector3.Distance(hammerInstance.transform.position, centerPos);
        }
    }

    /// <summary>
    /// 현재 "끌 타겟" 좌표. 팁이 있으면 팁, 없으면 본체 위치를 반환.
    /// </summary>
    public Vector3 GetChiselTargetPosition()
    {
        if (chiselTip != null) return chiselTip.position;
        if (chiselTransform != null) return chiselTransform.position;
        return transform.position;   // 폴백
    }

    void OnHammerStrike(Vector3 hammerPosition, Vector3 chiselTarget, float gripStrength)
    {
        // 도구가 탈착된 상태면 채굴 안함
        if (!toolsEnabled)
        {
            Debug.Log("도구가 탈착된 상태로 채굴이 불가능합니다!");
            return;
        }

        // 채굴 쿨다운 확인
        if (Time.time - lastMiningTime < miningCooldown)
        {
            Debug.Log("채굴 쿨다운 중...");
            return;
        }

        // 유효한 채굴 대상이 있는지 확인
        if (!isChiselTargetValid)
        {
            Debug.Log("유효한 채굴 대상이 없습니다!");
            return;
        }

        // *** 수정: 끌 타겟 위치에서 채굴 실행 (망치 위치 아님) ***
        Vector3 surfaceNormal = (chiselTarget - hammerPosition).normalized;
        ExecuteMining(chiselTarget, surfaceNormal);

        lastMiningTime = Time.time;
    }

    void ExecuteMining(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        // ForceCalculator에서 계산된 힘 가져오기
        float calculatedForce = forceCalculator?.GetGemProtectionForce() ?? 20f;

        // 현재 광물의 보석 보호 시스템에 충격 전달
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock != null)
        {
            GemProtectionSystem gemProtectionSystem = currentMineralBlock.GetComponent<GemProtectionSystem>();
            if (gemProtectionSystem != null)
            {
                gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, calculatedForce);
            }
        }

        // 채굴 효과 생성
        CreateMiningEffect(miningPoint, surfaceNormal);

        // 채굴 사운드 재생
        if (miningSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(miningSound);
        }

        // 실제 조각 제거
        RemoveChunksAtPoint(miningPoint);

        Debug.Log($"채굴 실행! 위치: {miningPoint}, 힘: {calculatedForce:F1}");
    }

    void RemoveChunksAtPoint(Vector3 miningPoint)
    {
        // 현재 광물 블록의 조각들 찾기
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null) return;

        ChunkNode[] allChunks = currentMineralBlock.GetComponentsInChildren<ChunkNode>();
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null &&
            chunk.gameObject != null &&
            chunk.gameObject.activeInHierarchy
        );

        if (activeChunks.Length == 0) return;

        // 거리순으로 정렬
        System.Array.Sort(activeChunks, (a, b) =>
        {
            float distA = Vector3.Distance(a.transform.position, miningPoint);
            float distB = Vector3.Distance(b.transform.position, miningPoint);
            return distA.CompareTo(distB);
        });

        // 가까운 조각들만 제거
        int removedCount = 0;
        foreach (ChunkNode chunk in activeChunks)
        {
            if (removedCount >= chunksPerStrike) break;

            float distance = Vector3.Distance(chunk.transform.position, miningPoint);
            if (distance <= miningRadius)
            {
                RemoveChunkGently(chunk, miningPoint);
                removedCount++;
            }
        }
    }

    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint)
    {
        if (chunk == null) return;

        // Joint 연결 끊기
        Joint[] joints = chunk.GetComponents<Joint>();
        foreach (Joint joint in joints)
        {
            if (joint != null) Destroy(joint);
        }

        FixedJoint[] fixedJoints = chunk.GetComponents<FixedJoint>();
        foreach (FixedJoint fixedJoint in fixedJoints)
        {
            if (fixedJoint != null) Destroy(fixedJoint);
        }

        // 부드러운 물리 힘 적용
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 direction = (chunk.transform.position - miningPoint).normalized;
            direction.y = Mathf.Max(direction.y, 0.1f);

            float gentleForce = 5f;
            rb.AddForce(direction * gentleForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
        }

        // 떨어지는 소리 (지연 재생)
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 파티클 시스템이 있으면 사용
        if (miningParticleEffect != null)
        {
            miningParticleEffect.transform.position = position;
            miningParticleEffect.Play();
        }

        // 돌가루 효과
        for (int i = 0; i < 3; i++)
        {
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.transform.position = position + Random.insideUnitSphere * 0.1f;
            dust.transform.localScale = Vector3.one * Random.Range(0.02f, 0.05f);

            Renderer dustRenderer = dust.GetComponent<Renderer>();
            dustRenderer.material.color = new Color(0.7f, 0.6f, 0.4f);

            Rigidbody dustRb = dust.AddComponent<Rigidbody>();
            Vector3 force = normal * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.5f;
            dustRb.AddForce(force, ForceMode.Impulse);

            Destroy(dust, 1.5f);
        }

        // 돌조각 효과
        for (int i = 0; i < 2; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.transform.position = position + Random.insideUnitSphere * 0.05f;
            chip.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
            chip.transform.rotation = Random.rotation;

            Renderer chipRenderer = chip.GetComponent<Renderer>();
            chipRenderer.material.color = new Color(0.5f, 0.4f, 0.3f);

            Rigidbody chipRb = chip.AddComponent<Rigidbody>();
            Vector3 chipForce = normal * Random.Range(2f, 5f) + Random.insideUnitSphere * 1f;
            chipRb.AddForce(chipForce, ForceMode.Impulse);

            Destroy(chip, 2f);
        }
    }

    IEnumerator PlayDelayedFallSound(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (chunkFallSounds != null && chunkFallSounds.Length > 0 && audioSource != null)
        {
            AudioClip fallSound = chunkFallSounds[Random.Range(0, chunkFallSounds.Length)];
            audioSource.PlayOneShot(fallSound, 0.5f);
        }
    }

    /// <summary>
    /// 현재 끌 타겟 위치 반환 (AccuracySystem에서 사용)
    /// </summary>
    public Vector3 GetCurrentChiselTarget()
    {
        return currentChiselTarget;
    }

    /// <summary>
    /// 도구 활성화 상태 반환
    /// </summary>
    public bool AreToolsEnabled()
    {
        return toolsEnabled;
    }

    [ContextMenu("도구 위치 리셋")]
    public void ResetToolPositions()
    {
        // 도구들을 손 Visual 위치로 즉시 이동
        if (chiselInstance != null && leftHandVisual != null)
        {
            chiselInstance.transform.position = leftHandVisual.position;
            chiselInstance.transform.rotation = leftHandVisual.rotation;
        }

        if (hammerInstance != null && rightHandVisual != null)
        {
            hammerInstance.transform.position = rightHandVisual.position;
            hammerInstance.transform.rotation = rightHandVisual.rotation;
        }

        Debug.Log("도구 위치 리셋 완료");
    }

    [ContextMenu("도구 강제 활성화")]
    public void ForceEnableTools()
    {
        SetToolsEnabled(true);
    }

    [ContextMenu("도구 강제 비활성화")]
    public void ForceDisableTools()
    {
        SetToolsEnabled(false);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 채굴 범위 시각화
        if (isChiselTargetValid)
        {
            // 정확도 시스템이 있으면 정확도에 따른 색상, 없으면 힘에 따른 색상
            if (accuracySystem != null && hammerInstance != null)
            {
                AccuracySystem.AccuracyLevel accuracy =
                    accuracySystem.GetCurrentPotentialAccuracy(hammerInstance.transform.position, currentChiselTarget);
                Gizmos.color = GetColorForAccuracy(accuracy);
            }
            else
            {
                Gizmos.color = forceCalculator?.IsSafeForce() == true ? Color.green : Color.red;
            }

            Gizmos.DrawWireSphere(currentChiselTarget, miningRadius);
        }

        // 안전 거리 시각화
        if (enableSafetySystem)
        {
            GameObject currentMineralBlock = FindCurrentMineralBlock();
            if (currentMineralBlock != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentMineralBlock.transform.position, maxSafeDistance);
            }
        }

        // 도구 상태 표시
        if (!toolsEnabled)
        {
            Gizmos.color = Color.gray;
            if (chiselInstance != null)
                Gizmos.DrawWireCube(chiselInstance.transform.position + Vector3.up * 0.1f, Vector3.one * 0.05f);
            if (hammerInstance != null)
                Gizmos.DrawWireCube(hammerInstance.transform.position + Vector3.up * 0.1f, Vector3.one * 0.05f);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (handController != null)
        {
            handController.OnHammerStrike -= OnHammerStrike;
            handController.OnToolStateChanged -= OnToolStateChanged;
        }
    }
}