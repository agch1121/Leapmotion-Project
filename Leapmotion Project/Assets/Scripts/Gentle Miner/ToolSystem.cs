using UnityEngine;
using LibreFracture;
using System.Collections;

/// <summary>
/// 끌(Chisel) + 망치(Hammer) 상호작용 시스템
/// 양손에 도구 고정 배치
/// </summary>
public class ToolSystem : MonoBehaviour
{
    [Header("도구 시각적 표현")]
    public GameObject chiselPrefab;
    public GameObject hammerPrefab;
    public LineRenderer chiselGuideLine;

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

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds;
    public ParticleSystem miningParticleEffect;

    [Header("안전 시스템")]
    public bool enableSafetySystem = true;
    public float maxSafeDistance = 3f;

    // 시스템 참조들
    private HandController handController;
    private ForceCalculator forceCalculator;
    private AudioSource audioSource;

    // 도구 상태
    private GameObject chiselInstance;
    private GameObject hammerInstance;
    private Vector3 currentChiselTarget;
    private bool isChiselTargetValid = false;

    // 채굴 중 상태
    private bool isMining = false;
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
        forceCalculator = FindFirstObjectByType<ForceCalculator>();

        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return;
        }

        if (forceCalculator == null)
        {
            Debug.LogError("ForceCalculator를 찾을 수 없습니다!");
            return;
        }

        // 오디오 소스 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // HandController의 타격 이벤트 구독
        handController.OnHammerStrike += OnHammerStrike;

        // 도구 인스턴스 생성
        CreateToolInstances();

        // 미리보기 구체 설정
        SetupPreviewSphere();
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
        }

        // 망치 인스턴스 생성
        if (hammerPrefab != null)
        {
            hammerInstance = Instantiate(hammerPrefab);
            hammerInstance.name = "Hammer_Instance";

            // 물리 비활성화 (손에 고정되므로)
            Rigidbody hammerRb = hammerInstance.GetComponent<Rigidbody>();
            if (hammerRb != null)
            {
                hammerRb.isKinematic = true;
            }
        }

        // 가이드라인 설정
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
            dangerPreviewMaterial.SetFloat("_Metallic", 0.5f);
        }
    }

    void Update()
    {
        if (handController == null) return;

        UpdateToolPositions();
        UpdateChiselTarget();
        UpdateVisualGuides();
        HandleSafetySystem();
    }

    void UpdateToolPositions()
    {
        // 끌 위치 업데이트 (왼손)
        if (chiselInstance != null)
        {
            chiselInstance.transform.position = handController.LeftHandPosition;
            chiselInstance.transform.rotation = handController.LeftHandRotation;
        }

        // 망치 위치 업데이트 (오른손)
        if (hammerInstance != null)
        {
            hammerInstance.transform.position = handController.RightHandPosition;
            hammerInstance.transform.rotation = handController.RightHandRotation;

            // 잡기 강도에 따른 시각적 피드백
            float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
            hammerInstance.transform.localScale = Vector3.one * gripScale;
        }
    }

    void UpdateChiselTarget()
    {
        // 현재 활성 광물 블록 찾기
        GameObject currentMineralBlock = FindCurrentMineralBlock();

        if (currentMineralBlock == null)
        {
            isChiselTargetValid = false;
            currentChiselTarget = handController.LeftHandPosition +
                                (handController.LeftHandRotation * Vector3.forward * 0.5f);
            return;
        }

        Vector3 chiselPos = handController.LeftHandPosition;
        Vector3 chiselForward = handController.LeftHandRotation * Vector3.forward;

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
        // 끌 가이드라인 업데이트
        if (chiselGuideLine != null)
        {
            chiselGuideLine.SetPosition(0, handController.LeftHandPosition);
            chiselGuideLine.SetPosition(1, currentChiselTarget);

            // 유효한 타격점인지에 따라 색상 변경
            Color lineColor = isChiselTargetValid ? Color.green : Color.gray;
            chiselGuideLine.startColor = lineColor;
            chiselGuideLine.endColor = lineColor;
        }

        // 채굴 지점 미리보기 업데이트
        if (previewSphere != null && showChiselPreview)
        {
            previewSphere.SetActive(isChiselTargetValid);

            if (isChiselTargetValid)
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

    void HandleSafetySystem()
    {
        if (!enableSafetySystem) return;

        // 현재 광물 블록 위치 기준으로 안전 거리 체크
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null) return;

        Vector3 centerPos = currentMineralBlock.transform.position;

        if (Vector3.Distance(handController.LeftHandPosition, centerPos) > maxSafeDistance)
        {
            Debug.LogWarning("끌이 안전 거리를 벗어났습니다!");
        }

        if (Vector3.Distance(handController.RightHandPosition, centerPos) > maxSafeDistance)
        {
            Debug.LogWarning("망치가 안전 거리를 벗어났습니다!");
        }
    }

    void OnHammerStrike(Vector3 strikePosition, Vector3 strikeDirection, float gripStrength)
    {
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

        // 채굴 실행
        ExecuteMining(currentChiselTarget, strikeDirection);

        lastMiningTime = Time.time;
    }

    void ExecuteMining(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        isMining = true;

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

        isMining = false;
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

    [ContextMenu("도구 위치 리셋")]
    public void ResetToolPositions()
    {
        // 도구들을 손 위치로 즉시 이동
        if (chiselInstance != null && handController != null)
        {
            chiselInstance.transform.position = handController.LeftHandPosition;
            chiselInstance.transform.rotation = handController.LeftHandRotation;
        }

        if (hammerInstance != null && handController != null)
        {
            hammerInstance.transform.position = handController.RightHandPosition;
            hammerInstance.transform.rotation = handController.RightHandRotation;
        }

        Debug.Log("도구 위치 리셋 완료");
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 채굴 범위 시각화
        if (isChiselTargetValid)
        {
            Gizmos.color = forceCalculator?.IsSafeForce() == true ? Color.green : Color.red;
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
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (handController != null)
        {
            handController.OnHammerStrike -= OnHammerStrike;
        }
    }
}