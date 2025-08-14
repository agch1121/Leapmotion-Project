using UnityEngine;
using LibreFracture;
using System.Collections;
using Unity.VisualScripting;

/// <summary>
/// 사용자의 손 움직임에 따른 끌(Chisel)과 망치(Hammer) 제어 시스템.
/// 끌의 목표 지점 계산, 망치 타격 이벤트 수신, 실제 채굴 로직 실행, 시각적 가이드 및 효과 관리.
/// </summary>
public class ToolSystem : MonoBehaviour
{
    #region 변수 선언

    [Header("도구 시각적 표현")]
    public GameObject chiselPrefab;         // 끌 프리팹
    public GameObject hammerPrefab;         // 망치 프리팹
    public LineRenderer chiselGuideLine;    // 끌 조준 가이드 라인

    [Header("Hammer Length Control")]
    [Tooltip("해머의 가상 길이를 조절하여 조준을 보정. 1.0은 원래 길이, 1.5는 50% 더 길어짐.")]
    [Range(1.0f, 3.0f)]
    public float hammerLengthMultiplier = 2.2f; // 망치 길이 보정 배율

    [Header("손 Visual 참조 (월드 좌표용)")]
    public Transform leftHandVisual;  // 왼손 시각적 모델 Transform
    public Transform rightHandVisual; // 오른손 시각적 모델 Transform

    [Header("채굴 설정")]
    public LayerMask chunkLayer = -1;       // 채굴 대상 레이어 마스크
    public float miningRadius = 0.1f;       // 1회 타격 시 채굴 반경
    public int chunksPerStrike = 2;         // 1회 타격 시 제거되는 최대 조각 수
    public float chiselRayDistance = 2f;    // 끌 조준 광선 최대 거리

    [Header("시각적 가이드")]
    public bool showChiselPreview = true;       // 끌 조준 지점 미리보기 활성화
    public GameObject previewSphere;            // 조준 지점 표시용 구체
    public Material safePreviewMaterial;        // 안전한 힘일 때의 미리보기 재질
    public Material dangerPreviewMaterial;      // 위험한 힘일 때의 미리보기 재질

    [Header("채굴 효과")]
    public AudioClip miningSound;               // 채굴 사운드
    public AudioClip[] chunkFallSounds;         // 조각 떨어지는 사운드 (배열)
    public ParticleSystem miningParticleEffect; // 채굴 파티클 효과

    [Header("안전 시스템")]
    public bool enableSafetySystem = true;      // 도구가 광물에서 너무 멀어지는 것을 방지하는 시스템 활성화
    public float maxSafeDistance = 3f;          // 안전 거리

    // --- 시스템 참조 ---
    private HandController handController;
    private ForceCalculator forceCalculator;
    private AudioSource audioSource;
    private AimSystem aimSystem;

    // --- 도구 상태 ---
    private GameObject chiselInstance;
    private GameObject hammerInstance;
    private Transform hammerTip;                // 망치 타격점 Transform
    private Vector3 currentChiselTarget;        // 현재 끌이 조준하는 목표 지점
    private bool isChiselTargetValid = false;   // 조준 지점이 유효한지 여부
    private bool areToolsCreated = false;       // 도구 프리팹이 생성되었는지 여부

    // --- 채굴 상태 ---
    private float lastMiningTime = 0f;          // 마지막 채굴 시간 (쿨다운용)
    private float miningCooldown = 0.5f;        // 채굴 쿨다운 시간
    private float lastAccuracy = 1.0f;          // 마지막 타격의 정확도

    // --- 이벤트 ---
    public event System.Action<float> OnStrikeAccuracyCalculated; // 정확도 계산 완료 시 발생

    #endregion

    #region 초기화

    void Start()
    {
        InitializeToolSystem();
    }

    /// <summary>
    /// ToolSystem 초기화. 시스템 참조, 이벤트 구독, 시각적 요소 설정.
    /// </summary>
    void InitializeToolSystem()
    {
        // 시스템 참조 가져오기
        handController = FindFirstObjectByType<HandController>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();
        aimSystem = FindFirstObjectByType<AimSystem>();

        // AimSystem의 정확도 계산 완료 이벤트 구독
        if (aimSystem != null)
        {
            aimSystem.OnAccuracyCalculated += (accuracy) =>
            {
                lastAccuracy = accuracy;
                Debug.Log($"[ToolSystem] 정확도 업데이트: {accuracy:P0}");
            };
        }

        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return;
        }

        // 손 Visual 참조가 비어있으면 HandController에서 가져오기
        if (leftHandVisual == null && handController.leftHandVisual != null)
        {
            leftHandVisual = handController.leftHandVisual;
        }
        if (rightHandVisual == null && handController.rightHandVisual != null)
        {
            rightHandVisual = handController.rightHandVisual;
        }

        // 오디오 소스 컴포넌트 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // HandController의 망치 타격 이벤트 구독
        handController.OnHammerStrike += OnHammerStrike;

        // 미리보기 구체 설정
        SetupPreviewSphere();

        // 초기에는 가이드라인 및 미리보기 비활성화
        if (chiselGuideLine != null) chiselGuideLine.gameObject.SetActive(false);
        if (previewSphere != null) previewSphere.SetActive(false);
    }

    /// <summary>
    /// 끌과 망치 프리팹을 게임 월드에 생성.
    /// </summary>
    void CreateToolInstances()
    {
        // 끌 생성 및 물리/충돌 설정
        if (chiselPrefab != null)
        {
            chiselInstance = Instantiate(chiselPrefab);
            chiselInstance.name = "Chisel_Instance";
            var rb = chiselInstance.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            var col = chiselInstance.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // 망치 생성 및 물리/충돌 설정
        if (hammerPrefab != null)
        {
            hammerInstance = Instantiate(hammerPrefab);
            hammerInstance.name = "Hammer_Instance";
            hammerTip = hammerInstance.transform.Find("HammerTip");
            if (hammerTip == null)
            {
                Debug.LogWarning("망치 프리팹에 'HammerTip' 자식이 없어 피봇을 기준으로 사용.");
                hammerTip = hammerInstance.transform;
            }
            var rb = hammerInstance.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            var col = hammerInstance.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // 가이드 라인 렌더러 설정
        if (chiselGuideLine == null)
        {
            chiselGuideLine = gameObject.AddComponent<LineRenderer>();
        }
        chiselGuideLine.material = new Material(Shader.Find("Sprites/Default"));
        chiselGuideLine.startWidth = 0.01f;
        chiselGuideLine.endWidth = 0.01f;
        chiselGuideLine.positionCount = 2;
        chiselGuideLine.useWorldSpace = true;
    }

    /// <summary>
    /// 조준점 미리보기용 구체 오브젝트 생성 및 재질 설정.
    /// </summary>
    void SetupPreviewSphere()
    {
        if (previewSphere == null)
        {
            previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            previewSphere.name = "ChiselPreview";
            previewSphere.transform.localScale = Vector3.one * 0.1f;
            Destroy(previewSphere.GetComponent<Collider>());
        }
        if (safePreviewMaterial == null)
        {
            safePreviewMaterial = new Material(Shader.Find("Standard")) { color = Color.green };
            safePreviewMaterial.SetFloat("_Metallic", 0.5f);
        }
        if (dangerPreviewMaterial == null)
        {
            dangerPreviewMaterial = new Material(Shader.Find("Standard")) { color = Color.red };
            dangerPreviewMaterial.SetFloat("_Metallic", 0.5f);
        }
    }

    #endregion

    #region 업데이트 루프

    /// <summary>
    /// 매 프레임 실행. 도구 생성, 위치 업데이트, 조준, 가이드 업데이트 처리.
    /// </summary>
    void Update()
    {
        // HandController에서 첫 데이터를 받을 때까지 도구 생성 대기
        if (!areToolsCreated)
        {
            if (handController != null && handController.HasReceivedValidData)
            {
                CreateToolInstances();
                areToolsCreated = true;
                if (chiselGuideLine != null) chiselGuideLine.gameObject.SetActive(true);
                if (previewSphere != null) previewSphere.SetActive(true);
            }
            else
            {
                return;
            }
        }

        UpdateToolPositions();
        UpdateChiselTarget();
        UpdateVisualGuides();
        HandleSafetySystem();

        // 게임 상태에 따라 도구 표시/숨김 처리
        if (!GameManager.Instance.IsGameStarted || GameManager.Instance.IsGameSucceeded || GameManager.Instance.IsGameCompleted)
        {
            HideToolInstances();
        }
        else
        {
            if (chiselInstance != null) chiselInstance.SetActive(true);
            if (hammerInstance != null) hammerInstance.SetActive(true);
            if (previewSphere != null) previewSphere.SetActive(true);
        }
    }

    /// <summary>
    /// 도구 인스턴스 숨기기.
    /// </summary>
    void HideToolInstances()
    {
        if (chiselInstance != null) chiselInstance.SetActive(false);
        if (hammerInstance != null) hammerInstance.SetActive(false);
        if (previewSphere != null) previewSphere.SetActive(false);
    }

    /// <summary>
    /// HandController에서 받은 손 위치/회전 값으로 도구의 Transform 업데이트.
    /// </summary>
    void UpdateToolPositions()
    {
        // 왼손(끌) 위치/회전 업데이트
        if (leftHandVisual != null && chiselInstance != null)
        {
            chiselInstance.transform.position = leftHandVisual.position;
            chiselInstance.transform.rotation = leftHandVisual.rotation;
            Vector3 adjustedPosition = chiselInstance.transform.position;
            adjustedPosition.y *= 6f; // Y좌표 보정
            chiselInstance.transform.position = adjustedPosition;
        }
        else if (handController != null && chiselInstance != null)
        {
            chiselInstance.transform.position = handController.LeftHandPosition;
            chiselInstance.transform.rotation = handController.LeftHandRotation;
        }

        // 오른손(망치) 위치/회전 업데이트
        if (rightHandVisual != null && hammerInstance != null)
        {
            hammerInstance.transform.position = rightHandVisual.position;
            hammerInstance.transform.rotation = rightHandVisual.rotation;
            Vector3 adjustedPosition = hammerInstance.transform.position;
            adjustedPosition.y *= 5f; // Y좌표 보정
            hammerInstance.transform.position = adjustedPosition;
            if (handController != null)
            {
                float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
                hammerInstance.transform.localScale = Vector3.one * gripScale;
            }
        }
        else if (handController != null && hammerInstance != null)
        {
            hammerInstance.transform.position = handController.RightHandPosition;
            hammerInstance.transform.rotation = handController.RightHandRotation;
            float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
            hammerInstance.transform.localScale = Vector3.one * gripScale;
        }
    }

    /// <summary>
    /// 끌의 위치/방향에서 광선을 발사(Raycast)하여 광물 표면의 타격 목표 지점 계산.
    /// </summary>
    void UpdateChiselTarget()
    {
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null)
        {
            isChiselTargetValid = false;
            return;
        }

        Vector3 chiselPos = chiselInstance != null ? chiselInstance.transform.position : (leftHandVisual != null ? leftHandVisual.position : Vector3.zero);
        Vector3 chiselForward = chiselInstance != null ? chiselInstance.transform.forward : (leftHandVisual != null ? leftHandVisual.forward : Vector3.forward);

        Ray chiselRay = new Ray(chiselPos, chiselForward);

        // 광선 발사로 충돌 지점 찾기
        if (Physics.Raycast(chiselRay, out RaycastHit hit, chiselRayDistance, chunkLayer))
        {
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
            currentChiselTarget = chiselPos + chiselForward * 0.5f; // 충돌 없으면 가상 지점 설정
        }
    }

    /// <summary>
    /// 조준 가이드 라인과 미리보기 구체의 위치 및 색상 업데이트.
    /// </summary>
    void UpdateVisualGuides()
    {
        // 가이드 라인 업데이트
        if (chiselGuideLine != null && chiselInstance != null)
        {
            chiselGuideLine.SetPosition(0, chiselInstance.transform.position);
            chiselGuideLine.SetPosition(1, currentChiselTarget);
            Color lineColor = isChiselTargetValid ? Color.green : Color.gray;
            chiselGuideLine.startColor = lineColor;
            chiselGuideLine.endColor = lineColor;
        }

        // 미리보기 구체 업데이트
        if (previewSphere != null && showChiselPreview)
        {
            previewSphere.SetActive(isChiselTargetValid);
            if (isChiselTargetValid)
            {
                previewSphere.transform.position = currentChiselTarget;
                bool isSafeForce = forceCalculator?.IsSafeForce() ?? true;
                previewSphere.GetComponent<MeshRenderer>().material = isSafeForce ? safePreviewMaterial : dangerPreviewMaterial;
                previewSphere.transform.localScale = Vector3.one * (miningRadius * 2f);
            }
        }
    }

    /// <summary>
    /// 안전 시스템 처리 (현재는 기능 비어있음).
    /// </summary>
    void HandleSafetySystem()
    {
        if (!enableSafetySystem) return;

        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null) return;

        Vector3 centerPos = currentMineralBlock.transform.position;

        if (chiselInstance != null)
        {
            float chiselDistance = Vector3.Distance(chiselInstance.transform.position, centerPos);
        }

        if (hammerInstance != null)
        {
            float hammerDistance = Vector3.Distance(hammerInstance.transform.position, centerPos);
        }
    }

    #endregion

    #region 채굴 로직

    /// <summary>
    /// HandController에서 망치 타격 이벤트 발생 시 호출되는 핸들러.
    /// 채굴 쿨다운, 유효 타겟 확인 후 실제 채굴 로직 실행.
    /// </summary>
    void OnHammerStrike(Vector3 strikePosition, Vector3 strikeDirection, float gripStrength)
    {
        if (Time.time - lastMiningTime < miningCooldown)
        {
            Debug.Log("채굴 쿨다운 중...");
            return;
        }
        if (!isChiselTargetValid)
        {
            Debug.Log("유효한 채굴 대상이 없습니다!");
            return;
        }

        // AimSystem에서 계산된 마지막 정확도를 함께 전달하여 채굴 실행
        ExecuteMining(currentChiselTarget, strikeDirection, lastAccuracy);
        lastAccuracy = 1.0f; // 정확도 사용 후 초기화
        lastMiningTime = Time.time;
    }

    /// <summary>
    /// 실제 채굴 실행. 정확도에 따라 최종 힘 조절, 보석에 충격 전달, 효과 재생, 조각 제거.
    /// </summary>
    /// <param name="miningPoint">채굴 지점</param>
    /// <param name="surfaceNormal">표면 방향</param>
    /// <param name="accuracy">타격 정확도 (0.0 ~ 1.0)</param>
    void ExecuteMining(Vector3 miningPoint, Vector3 surfaceNormal, float accuracy)
    {
        // 정확도 계산 완료 이벤트 발생
        OnStrikeAccuracyCalculated?.Invoke(accuracy);

        // 정확도에 따라 최종 힘 보정 (정확할수록 강해짐)
        float baseForce = forceCalculator?.GetGemProtectionForce() ?? 20f;
        float accuracyModifier = Mathf.Lerp(0.5f, 1.2f, accuracy);
        float finalForce = baseForce * accuracyModifier;

        // 보석 보호 시스템에 최종 힘 전달
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock != null)
        {
            var gemProtectionSystem = currentMineralBlock.GetComponent<GemProtectionSystem>();
            gemProtectionSystem?.CheckMiningImpactOnGems(miningPoint, finalForce);
        }

        CreateMiningEffect(miningPoint, surfaceNormal);
        if (miningSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(miningSound);
        }
        RemoveChunksAtPoint(miningPoint);

        Debug.Log($"채굴 실행! 위치: {miningPoint}, 힘: {finalForce:F1}, 정확도: {accuracy:P0}");
    }

    /// <summary>
    /// 채굴 지점 주변의 조각들을 찾아 제거.
    /// </summary>
    void RemoveChunksAtPoint(Vector3 miningPoint)
    {
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null) return;

        // 활성화된 모든 조각을 찾아 타격 지점과 가까운 순으로 정렬
        var activeChunks = System.Array.FindAll(currentMineralBlock.GetComponentsInChildren<ChunkNode>(), c => c != null && c.gameObject.activeInHierarchy);
        if (activeChunks.Length == 0) return;
        System.Array.Sort(activeChunks, (a, b) => Vector3.Distance(a.transform.position, miningPoint).CompareTo(Vector3.Distance(b.transform.position, miningPoint)));

        // miningRadius 내에 있는 조각들을 chunksPerStrike 개수만큼 제거
        int removedCount = 0;
        foreach (ChunkNode chunk in activeChunks)
        {
            if (removedCount >= chunksPerStrike) break;
            if (Vector3.Distance(chunk.transform.position, miningPoint) <= miningRadius)
            {
                RemoveChunkGently(chunk, miningPoint);
                removedCount++;
            }
        }
    }

    /// <summary>
    /// 개별 조각의 연결(Joint)을 끊고 물리력을 적용하여 떨어져 나가게 만듬.
    /// </summary>
    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint)
    {
        if (chunk == null) return;

        // 부모로부터 분리 (ChunkCleaner가 인식하도록)
        chunk.transform.parent = null;

        // 모든 Joint 파괴
        foreach (Joint joint in chunk.GetComponents<Joint>())
        {
            if (joint != null) Destroy(joint);
        }
        foreach (FixedJoint fixedJoint in chunk.GetComponents<FixedJoint>())
        {
            if (fixedJoint != null) Destroy(fixedJoint);
        }


        // Rigidbody에 힘과 회전력 추가
        var rb = chunk.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 direction = (chunk.transform.position - miningPoint).normalized;
            direction.y = Mathf.Max(direction.y, 0.1f); // 약간 위로 튀도록
            rb.AddForce(direction * 5f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 1f, ForceMode.Impulse);
        }

        // 조각 떨어지는 소리 재생 (지연)
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }
    }

    /// <summary>
    /// 채굴 시 발생하는 파티클과 사운드 효과 생성.
    /// </summary>
    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        if (miningParticleEffect != null)
        {
            miningParticleEffect.transform.position = position;
            miningParticleEffect.Play();
        }

        // 먼지 효과
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

    /// <summary>
    /// 조각 떨어지는 소리를 약간의 지연 후 재생하는 코루틴.
    /// </summary>
    IEnumerator PlayDelayedFallSound(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (chunkFallSounds != null && chunkFallSounds.Length > 0 && audioSource != null)
        {
            AudioClip fallSound = chunkFallSounds[Random.Range(0, chunkFallSounds.Length)];
            audioSource.PlayOneShot(fallSound, 0.5f);
        }
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 현재 스테이지의 광물 블록 오브젝트 찾기.
    /// </summary>
    GameObject FindCurrentMineralBlock()
    {
        var stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager != null) return stageManager.GetCurrentMineralBlock();
        var chunkManager = FindFirstObjectByType<ChunkGraphManager>();
        if (chunkManager != null) return chunkManager.gameObject;
        return null;
    }

    /// <summary>
    /// AimSystem에서 사용할 망치 끝(HammerTip)의 보정된 위치 반환.
    /// </summary>
    public Vector3 GetHammerTipPosition()
    {
        if (hammerInstance != null && hammerTip != null)
        {
            Vector3 hammerPivot = hammerInstance.transform.position;
            Vector3 originalTipVector = hammerTip.position - hammerPivot;
            // hammerLengthMultiplier를 적용하여 가상 길이 조절
            return hammerPivot + (originalTipVector * hammerLengthMultiplier);
        }
        return handController != null ? handController.RightHandPosition : Vector3.zero;
    }

    /// <summary>
    /// AimSystem에서 사용할 현재 끌의 목표 지점 반환.
    /// </summary>
    public Vector3 GetCurrentChiselTarget()
    {
        return isChiselTargetValid ? currentChiselTarget : (chiselInstance != null ? chiselInstance.transform.position : Vector3.zero);
    }

    /// <summary>
    /// 디버그용 기즈모(Gizmos) 렌더링.
    /// </summary>
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 망치 길이 보정 시각화
        if (hammerInstance != null && hammerTip != null)
        {
            Vector3 hammerPivot = hammerInstance.transform.position;
            Vector3 originalTipPos = hammerTip.position;
            Vector3 correctedTipPos = GetHammerTipPosition();
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(hammerPivot, originalTipPos);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originalTipPos, correctedTipPos);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(correctedTipPos, 0.02f);
        }

        // 채굴 반경 시각화
        if (isChiselTargetValid)
        {
            Gizmos.color = forceCalculator?.IsSafeForce() == true ? Color.green : Color.red;
            Gizmos.DrawWireSphere(currentChiselTarget, miningRadius);
        }

        // 안전 시스템 반경 시각화
        if (enableSafetySystem)
        {
            GameObject currentMineralBlock = FindCurrentMineralBlock();
            if (currentMineralBlock != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentMineralBlock.transform.position, maxSafeDistance);
            }
        }
    }

    /// <summary>
    /// 오브젝트 파괴 시 이벤트 구독 해제 (메모리 누수 방지).
    /// </summary>
    void OnDestroy()
    {
        if (handController != null)
        {
            handController.OnHammerStrike -= OnHammerStrike;
        }
    }

    #endregion
}