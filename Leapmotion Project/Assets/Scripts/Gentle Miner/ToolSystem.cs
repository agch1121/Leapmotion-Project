using UnityEngine;
using LibreFracture;
using System.Collections;
using Unity.VisualScripting;

/// <summary>
/// 끌(Chisel) + 망치(Hammer) 상호작용 시스템
/// 시각적 타겟과 감지 영역을 '박스' 형태로 변경하고, '자식 오브젝트'의 망치 콜라이더를 시각화합니다.
/// </summary>
public class ToolSystem : MonoBehaviour
{
    [Header("도구 시각적 표현")]
    public GameObject chiselPrefab;
    public GameObject hammerPrefab;
    public LineRenderer chiselGuideLine;
    public LineRenderer hammerColliderVisual;

    [Header("손 Visual 참조 (월드 좌표용)")]
    public Transform leftHandVisual;
    public Transform rightHandVisual;

    [Header("도구 활성/비활성 제어")]
    [Range(0f, 1f)]
    public float openThreshold = 0.15f;
    [Range(0f, 1f)]
    public float closeThreshold = 0.25f;
    public float reactivateDelay = 0.6f;

    private bool toolsTemporarilyDisabled = false;
    private float reenableTime = 0f;

    [Header("채굴 설정")]
    public LayerMask chunkLayer = -1;
    public float miningRadius = 0.1f;
    public int chunksPerStrike = 2;
    public float chiselRayDistance = 2f;

    [Header("시각적 가이드")]
    public bool showChiselPreview = true;
    public GameObject previewTarget;
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
    private AccuracyDetector accuracyDetector;

    // 도구 상태
    private GameObject chiselInstance;
    private GameObject hammerInstance;
    private Vector3 currentChiselTarget;
    private bool isChiselTargetValid = false;

    // 채굴 중 상태
    private float lastMiningTime = 0f;
    private float miningCooldown = 0.5f;

    void Start()
    {
        InitializeToolSystem();
    }

    void InitializeToolSystem()
    {
        handController = FindFirstObjectByType<HandController>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();
        audioSource = GetComponent<AudioSource>();

        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
            return;
        }

        if (leftHandVisual == null && handController.leftHandVisual != null) leftHandVisual = handController.leftHandVisual;
        if (rightHandVisual == null && handController.rightHandVisual != null) rightHandVisual = handController.rightHandVisual;
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        CreateToolInstances();
        SetupPreviewTarget(FindObjectOfType<AccuracyDetector>());
    }

    void CreateToolInstances()
    {
        if (chiselPrefab != null)
        {
            chiselInstance = Instantiate(chiselPrefab);
            chiselInstance.name = "Chisel_Instance";
            var rb = chiselInstance.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            var col = chiselInstance.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }


        if (hammerPrefab != null)
        {
            hammerInstance = Instantiate(hammerPrefab);
            hammerInstance.name = "Hammer_Instance";

            // [수정됨] 자식에서 콜라이더를 먼저 찾고, 없을 경우에만 부모에 추가합니다.
            if (hammerInstance.GetComponentInChildren<BoxCollider>() == null)
            {
                var col = hammerInstance.AddComponent<BoxCollider>();
                col.isTrigger = true;
                Debug.LogWarning("망치 자식 오브젝트에 BoxCollider가 없어 부모에 추가합니다.");
            }

            if (hammerInstance.GetComponentInChildren<Rigidbody>() == null)
            {
                var rb = hammerInstance.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }
            hammerInstance.tag = "Hammer";
        }

        // 망치 콜라이더 시각화용 LineRenderer 설정
        if (hammerColliderVisual == null)
        {
            GameObject visualObj = new GameObject("HammerCollider_Visual");
            hammerColliderVisual = visualObj.AddComponent<LineRenderer>();
            hammerColliderVisual.material = new Material(Shader.Find("Sprites/Default"));
            hammerColliderVisual.startColor = Color.cyan;
            hammerColliderVisual.endColor = Color.cyan;
            hammerColliderVisual.startWidth = 0.005f;
            hammerColliderVisual.endWidth = 0.005f;
            hammerColliderVisual.positionCount = 24;
            hammerColliderVisual.loop = false;
            hammerColliderVisual.useWorldSpace = true;
        }

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

    void SetupPreviewTarget(AccuracyDetector detectorTemplate)
    {
        if (detectorTemplate == null)
        {
            Debug.LogError("씬에 AccuracyDetector가 하나 이상 존재해야 합니다!");
            return;
        }

        previewTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewTarget.name = "ChiselPreview_VisualBox";
        Destroy(previewTarget.GetComponent<Collider>());

        float visualScale = detectorTemplate.perfectHitRadius * 2f;
        previewTarget.transform.localScale = Vector3.one * visualScale;

        GameObject detectionZone = new GameObject("ChiselPreview_DetectionZone");
        detectionZone.transform.SetParent(previewTarget.transform, false);
        detectionZone.tag = "ChiselTarget";

        BoxCollider detectionCollider = detectionZone.AddComponent<BoxCollider>();
        detectionCollider.isTrigger = true;
        detectionCollider.size = Vector3.one * detectorTemplate.maxDetectionRadius * 2f;

        accuracyDetector = detectionZone.AddComponent<AccuracyDetector>();
        accuracyDetector.hammerTag = detectorTemplate.hammerTag;
        accuracyDetector.perfectHitRadius = detectorTemplate.perfectHitRadius;
        accuracyDetector.maxDetectionRadius = detectorTemplate.maxDetectionRadius;

        Destroy(detectorTemplate.gameObject);

        if (safePreviewMaterial == null)
        {
            safePreviewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            safePreviewMaterial.color = Color.green;
            safePreviewMaterial.SetFloat("_Metallic", 0.5f);
        }
        if (dangerPreviewMaterial == null)
        {
            dangerPreviewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            dangerPreviewMaterial.color = Color.red;
            dangerPreviewMaterial.SetFloat("_Metallic", 0.5f);
        }
        Debug.Log($"<color=cyan>프리뷰 타겟 설정 완료:</color> 시각적 크기({visualScale:F2}), 감지 범위({detectionCollider.size.x:F2})");
    }

    void Update()
    {
        UpdateToolPositions();
        UpdateChiselTarget();
        UpdateVisualGuides();
        HandleSafetySystem();

        if (GameManager.Instance == null || !GameManager.Instance.IsGameStarted)
        {
            HideToolInstances();
            return;
        }

        bool hasHand = handController != null;
        if (!hasHand)
        {
            SetToolsActive(true);
            return;
        }

        bool leftOpen = handController.LeftHandGrabStrength < openThreshold;
        bool rightOpen = handController.RightHandGrabStrength < openThreshold;
        bool bothOpen = leftOpen && rightOpen;

        if (bothOpen)
        {
            if (!toolsTemporarilyDisabled)
            {
                SetToolsActive(false);
                toolsTemporarilyDisabled = true;
                reenableTime = Time.time + reactivateDelay;
            }
        }
        else
        {
            bool anyClosed = (handController.LeftHandGrabStrength > closeThreshold) ||
                             (handController.RightHandGrabStrength > closeThreshold);

            if (toolsTemporarilyDisabled && anyClosed && Time.time >= reenableTime)
            {
                SetToolsActive(true);
                toolsTemporarilyDisabled = false;
            }
        }

        SetToolsActive(!toolsTemporarilyDisabled);
    }

    // 원래의 끌 타겟 위치 계산 로직을 유지합니다.
    void UpdateChiselTarget()
    {
        GameObject currentMineralBlock = FindCurrentMineralBlock();

        Vector3 chiselPos = chiselInstance != null ? chiselInstance.transform.position : (leftHandVisual != null ? leftHandVisual.position : Vector3.zero);
        Vector3 chiselForward = chiselInstance != null ? chiselInstance.transform.forward : (leftHandVisual != null ? leftHandVisual.forward : Vector3.forward);
        Ray chiselRay = new Ray(chiselPos, chiselForward);
        RaycastHit hit;

        isChiselTargetValid = false;

        if (Physics.Raycast(chiselRay, out hit, chiselRayDistance, chunkLayer))
        {
            if (currentMineralBlock != null && hit.collider.transform.IsChildOf(currentMineralBlock.transform))
            {
                currentChiselTarget = hit.point;
                isChiselTargetValid = true;
            }
        }

        if (!isChiselTargetValid)
        {
            currentChiselTarget = chiselPos + chiselForward * chiselRayDistance;
        }
    }

    void UpdateVisualGuides()
    {
        // 1. 끌 가이드 라인 업데이트
        if (chiselGuideLine != null && chiselInstance != null && previewTarget != null)
        {
            if (isChiselTargetValid)
            {
                chiselGuideLine.SetPosition(0, chiselInstance.transform.position);
                chiselGuideLine.SetPosition(1, previewTarget.transform.position);
            }
            else
            {
                chiselGuideLine.SetPosition(0, chiselInstance.transform.position);
                chiselGuideLine.SetPosition(1, chiselInstance.transform.position + chiselInstance.transform.forward * chiselRayDistance);
            }
            Color lineColor = isChiselTargetValid ? Color.green : Color.gray;
            chiselGuideLine.startColor = lineColor;
            chiselGuideLine.endColor = lineColor;
        }

        // 2. 시각적 타겟(박스) 업데이트
        if (previewTarget != null && showChiselPreview)
        {
            if (isChiselTargetValid)
            {
                previewTarget.transform.position = currentChiselTarget;
                previewTarget.SetActive(true);
                bool isSafeForce = forceCalculator?.IsSafeForce() ?? true;
                MeshRenderer renderer = previewTarget.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.material = isSafeForce ? safePreviewMaterial : dangerPreviewMaterial;
            }
            else
            {
                previewTarget.SetActive(false);
            }
        }

        // 3. **[핵심 수정]** 망치 콜라이더 시각화 (자식 오브젝트 검색)
        if (hammerColliderVisual != null && hammerInstance != null && hammerInstance.activeSelf)
        {
            hammerColliderVisual.enabled = true;
            // 부모가 아닌 자식에서 BoxCollider를 찾습니다.
            BoxCollider col = hammerInstance.GetComponentInChildren<BoxCollider>();

            if (col != null)
            {
                // 콜라이더가 있는 자식 오브젝트의 Transform을 기준으로 꼭짓점 계산
                Transform colliderTransform = col.transform;
                Vector3 center = col.center;
                Vector3 size = col.size / 2;

                Vector3[] vertices = new Vector3[8];
                vertices[0] = colliderTransform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z));
                vertices[1] = colliderTransform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z));
                vertices[2] = colliderTransform.TransformPoint(center + new Vector3(size.x, -size.y, size.z));
                vertices[3] = colliderTransform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z));
                vertices[4] = colliderTransform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z));
                vertices[5] = colliderTransform.TransformPoint(center + new Vector3(size.x, size.y, -size.z));
                vertices[6] = colliderTransform.TransformPoint(center + new Vector3(size.x, size.y, size.z));
                vertices[7] = colliderTransform.TransformPoint(center + new Vector3(-size.x, size.y, size.z));

                // 24개의 점으로 박스 와이어프레임을 그리는 순서
                Vector3[] wireframePoints = {
                    vertices[0], vertices[1],
                    vertices[1], vertices[2],
                    vertices[2], vertices[3],
                    vertices[3], vertices[0], // 아래쪽 면
                    vertices[4], vertices[5],
                    vertices[5], vertices[6],
                    vertices[6], vertices[7],
                    vertices[7], vertices[4], // 위쪽 면
                    vertices[0], vertices[4],
                    vertices[1], vertices[5],
                    vertices[2], vertices[6],
                    vertices[3], vertices[7]  // 옆면 기둥들
                };
                hammerColliderVisual.SetPositions(wireframePoints);
            }
        }
        else if (hammerColliderVisual != null)
        {
            hammerColliderVisual.enabled = false;
        }
    }

    // ... 이하 나머지 코드는 변경 사항 없습니다 ...
    #region Unchanged Methods
    void HideToolInstances()
    {
        if (chiselInstance != null) chiselInstance.SetActive(false);
        if (hammerInstance != null) hammerInstance.SetActive(false);
        if (previewTarget != null) previewTarget.SetActive(false);
        if (hammerColliderVisual != null) hammerColliderVisual.enabled = false;
    }

    void SetToolsActive(bool active)
    {
        if (chiselInstance != null) chiselInstance.SetActive(active);
        if (hammerInstance != null) hammerInstance.SetActive(active);
        if (previewTarget != null) previewTarget.SetActive(active && showChiselPreview);
        if (hammerColliderVisual != null) hammerColliderVisual.enabled = (active && hammerInstance != null && hammerInstance.activeSelf);
    }

    void UpdateToolPositions()
    {
        if (leftHandVisual != null)
        {
            if (chiselInstance != null)
            {
                chiselInstance.transform.position = leftHandVisual.position;
                chiselInstance.transform.rotation = leftHandVisual.rotation;
            }
        }
        else if (handController != null)
        {
            if (chiselInstance != null)
            {
                chiselInstance.transform.position = handController.LeftHandPosition;
                chiselInstance.transform.rotation = handController.LeftHandRotation;
            }
        }

        if (rightHandVisual != null)
        {
            if (hammerInstance != null)
            {
                hammerInstance.transform.position = rightHandVisual.position;
                hammerInstance.transform.rotation = rightHandVisual.rotation;
                if (handController != null)
                {
                    float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
                    hammerInstance.transform.localScale = Vector3.one * gripScale;
                }
            }
        }
        else if (handController != null)
        {
            if (hammerInstance != null)
            {
                hammerInstance.transform.position = handController.RightHandPosition;
                hammerInstance.transform.rotation = handController.RightHandRotation;
                float gripScale = 1f + handController.RightHandGrabStrength * 0.1f;
                hammerInstance.transform.localScale = Vector3.one * gripScale;
            }
        }
    }

    GameObject FindCurrentMineralBlock()
    {
        StageManager stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager != null) return stageManager.GetCurrentMineralBlock();
        ChunkGraphManager chunkManager = FindFirstObjectByType<ChunkGraphManager>();
        if (chunkManager != null) return chunkManager.gameObject;
        return null;
    }

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

    public void ExecuteMining(Vector3 miningPoint)
    {
        if (Time.time - lastMiningTime < miningCooldown) return;
        if (!isChiselTargetValid) return;

        float calculatedForce = forceCalculator?.GetGemProtectionForce() ?? 20f;
        GameObject currentMineralBlock = FindCurrentMineralBlock();

        if (currentMineralBlock != null)
        {
            GemProtectionSystem gemProtectionSystem = currentMineralBlock.GetComponent<GemProtectionSystem>();
            if (gemProtectionSystem != null)
            {
                gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, calculatedForce);
            }
        }

        CreateMiningEffect(miningPoint, Vector3.up);

        if (miningSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(miningSound);
        }
        RemoveChunksAtPoint(miningPoint);
        lastMiningTime = Time.time;
    }

    void RemoveChunksAtPoint(Vector3 miningPoint)
    {
        GameObject currentMineralBlock = FindCurrentMineralBlock();
        if (currentMineralBlock == null) return;
        ChunkNode[] allChunks = currentMineralBlock.GetComponentsInChildren<ChunkNode>();
        var activeChunks = System.Array.FindAll(allChunks, chunk => chunk != null && chunk.gameObject != null && chunk.gameObject.activeInHierarchy);
        if (activeChunks.Length == 0) return;
        System.Array.Sort(activeChunks, (a, b) => Vector3.Distance(a.transform.position, miningPoint).CompareTo(Vector3.Distance(b.transform.position, miningPoint)));
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

    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint)
    {
        if (chunk == null) return;
        foreach (Joint joint in chunk.GetComponents<Joint>()) if (joint != null) Destroy(joint);
        foreach (FixedJoint joint in chunk.GetComponents<FixedJoint>()) if (joint != null) Destroy(joint);
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 direction = (chunk.transform.position - miningPoint).normalized;
            direction.y = Mathf.Max(direction.y, 0.1f);
            float gentleForce = 5f;
            rb.AddForce(direction * gentleForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
        }
        if (chunkFallSounds != null && chunkFallSounds.Length > 0) StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        if (miningParticleEffect != null)
        {
            miningParticleEffect.transform.position = position;
            miningParticleEffect.Play();
        }
        for (int i = 0; i < 3; i++)
        {
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.transform.position = position + Random.insideUnitSphere * 0.1f;
            dust.transform.localScale = Vector3.one * Random.Range(0.02f, 0.05f);
            dust.GetComponent<Renderer>().material.color = new Color(0.7f, 0.6f, 0.4f);
            Rigidbody dustRb = dust.AddComponent<Rigidbody>();
            dustRb.AddForce((normal * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.5f), ForceMode.Impulse);
            Destroy(dust, 1.5f);
        }
        for (int i = 0; i < 2; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.transform.position = position + Random.insideUnitSphere * 0.05f;
            chip.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
            chip.transform.rotation = Random.rotation;
            chip.GetComponent<Renderer>().material.color = new Color(0.5f, 0.4f, 0.3f);
            Rigidbody chipRb = chip.AddComponent<Rigidbody>();
            chipRb.AddForce((normal * Random.Range(2f, 5f) + Random.insideUnitSphere * 1f), ForceMode.Impulse);
            Destroy(chip, 2f);
        }
    }

    IEnumerator PlayDelayedFallSound(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (chunkFallSounds != null && chunkFallSounds.Length > 0 && audioSource != null)
        {
            audioSource.PlayOneShot(chunkFallSounds[Random.Range(0, chunkFallSounds.Length)], 0.5f);
        }
    }

    [ContextMenu("도구 위치 리셋")]
    public void ResetToolPositions()
    {
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
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (isChiselTargetValid)
        {
            Gizmos.color = forceCalculator?.IsSafeForce() == true ? Color.green : Color.red;
            Gizmos.DrawWireSphere(currentChiselTarget, miningRadius);
        }
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
    #endregion
}