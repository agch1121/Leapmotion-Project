using UnityEngine;
using LibreFracture;

/// <summary>
/// 광물 블록 구조 및 힘 기반 채굴 로직 처리
/// 힘 강도에 따른 차별화된 채굴 효과와 더 관대한 채굴 시스템
/// </summary>
public class MineralBlock : MonoBehaviour
{
    [Header("채굴 설정")]
    public float miningRadius = 0.3f;
    public float gentleForce = 5f;
    public int chunksPerClick = 2;
    public LayerMask chunkLayer = -1;

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds;

    [Header("채굴 강도")]
    [Range(1f, 50f)]
    public float miningForceIntensity = 20f;

    [Header("스테이지별 설정")]
    public float hardness = 1.0f;
    public float gemQuality = 1.0f;

    // 시스템 참조들
    private ChunkGraphManager chunkGraphManager;
    private AudioSource audioSource;
    private ChunkNode[] allChunks;
    private GemProtectionSystem gemProtectionSystem;
    private ChunkCounter chunkCounter;
    private GameManager gameManager;
    private HandController handController;

    void Start()
    {
        InitializeMineralBlock();
    }

    void InitializeMineralBlock()
    {
        Debug.Log("=== MineralBlock 초기화 시작 ===");

        FindSystemReferences();
        SubscribeToEvents();
        SetupInitialSettings();

        Debug.Log("=== MineralBlock 초기화 완료 ===");
    }

    void FindSystemReferences()
    {
        // 로컬 컴포넌트들
        chunkGraphManager = GetComponent<ChunkGraphManager>();
        gemProtectionSystem = GetComponent<GemProtectionSystem>();
        chunkCounter = GetComponent<ChunkCounter>();

        if (chunkCounter == null)
        {
            chunkCounter = gameObject.AddComponent<ChunkCounter>();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 전역 매니저들
        gameManager = FindFirstObjectByType<GameManager>();
        handController = FindFirstObjectByType<HandController>();

        if (gameManager == null)
        {
            Debug.LogError("GameManager를 찾을 수 없습니다!");
        }

        if (handController == null)
        {
            Debug.LogError("HandController를 찾을 수 없습니다!");
        }
    }

    void SubscribeToEvents()
    {
        // ChunkCounter 이벤트를 GameManager로 전달
        if (chunkCounter != null && gameManager != null)
        {
            chunkCounter.OnChunkCountChanged += gameManager.OnMineralProgressChanged;
        }

        // HandController 망치 타격 이벤트 구독
        if (handController != null)
        {
            handController.OnHammerStrike += OnHammerStrike;
        }
    }

    void SetupInitialSettings()
    {
        RefreshChunkCache();
        ApplyStageSettings();
    }

    public void ApplyStageSettings()
    {
        miningForceIntensity = miningForceIntensity * hardness;

        if (chunkGraphManager != null)
        {
            chunkGraphManager.jointBreakForce = chunkGraphManager.jointBreakForce * hardness;
        }

        Debug.Log($"스테이지 설정 적용 - 경도: {hardness}, 품질: {gemQuality}");
    }

    void RefreshChunkCache()
    {
        var validChunks = new System.Collections.Generic.List<ChunkNode>();

        ChunkNode[] foundChunks = GetComponentsInChildren<ChunkNode>();
        foreach (ChunkNode chunk in foundChunks)
        {
            if (chunk != null && chunk.gameObject != null)
            {
                validChunks.Add(chunk);
            }
        }

        allChunks = validChunks.ToArray();
    }

    void Update()
    {
        // 게임이 시작되지 않았으면 입력 무시
        if (gameManager == null || !gameManager.IsGameStarted) return;

        // 마우스 클릭 처리 (테스트용)
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, chunkLayer))
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                MineAtPoint(hit.point, hit.normal);
            }
        }
    }

    /// <summary>
    /// HandController에서 발생하는 망치 타격 이벤트 처리
    /// </summary>
    public void OnHammerStrike(Vector3 position, Vector3 direction, float strikeForce)
    {
        Debug.Log($"망치 타격 감지! 위치: {position}, 방향: {direction}, 힘: {strikeForce:F2}");

        if (gameManager == null || !gameManager.IsGameStarted)
        {
            Debug.Log("게임이 아직 시작되지 않았습니다.");
            return;
        }

        Vector3 surfaceNormal = -direction;
        MineAtPointForce(position, surfaceNormal, strikeForce);
    }

    /// <summary>
    /// 힘 강도를 고려한 채굴 실행 - 유연한 채굴 + 차별화된 효과
    /// </summary>
    public void MineAtPointForce(Vector3 miningPoint, Vector3 surfaceNormal, float strikeForce)
    {
        Debug.Log($"힘 강도별 채굴 시작: {strikeForce * 100f:F0}%");

        // 1. 보석 보호 시스템에 충격 전달
        if (gemProtectionSystem != null)
        {
            float adjustedForce = CalcAdjustedForce(strikeForce);
            gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, adjustedForce);
        }

        // 2. 힘 강도별 차별화된 채굴 효과 생성
        CreateForceEffect(miningPoint, surfaceNormal, strikeForce);

        // 3. 채굴 사운드 재생 (힘에 따라 볼륨 조절)
        if (miningSound != null && audioSource != null)
        {
            float volume = Mathf.Lerp(0.3f, 1.0f, strikeForce);
            audioSource.PlayOneShot(miningSound, volume);
        }

        // 4. 실제 조각 제거
        RemoveChunksForce(miningPoint, strikeForce, surfaceNormal);
    }

    /// <summary>
    /// 기본 채굴 (마우스 클릭용)
    /// </summary>
    public void MineAtPoint(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        // 기본 힘으로 채굴 (중간 강도)
        MineAtPointForce(miningPoint, surfaceNormal, 0.5f);
    }

    /// <summary>
    /// 보석 보호용 힘 계산
    /// </summary>
    float CalcAdjustedForce(float strikeForce)
    {
        float baseForce = miningForceIntensity * 0.7f; // 30% 감소
        float forceMultiplier = Mathf.Lerp(0.5f, 1.5f, strikeForce);

        return baseForce * forceMultiplier * hardness;
    }

    /// <summary>
    /// 힘 강도별 차별화된 채굴 효과
    /// </summary>
    void CreateForceEffect(Vector3 position, Vector3 normal, float strikeForce)
    {
        int dustCount = CalcDustCount(strikeForce);
        int chipCount = CalcChipCount(strikeForce);

        Debug.Log($"채굴 효과: 먼지 {dustCount}개, 조각 {chipCount}개 (힘: {strikeForce * 100f:F0}%)");

        for (int i = 0; i < dustCount; i++)
        {
            CreateDust(position, normal, strikeForce);
        }

        for (int i = 0; i < chipCount; i++)
        {
            CreateChip(position, normal, strikeForce);
        }

        if (strikeForce > 0.8f)
        {
            CreateDangerEffect(position, normal);
        }
    }

    /// <summary>
    /// 힘 강도에 따른 먼지 개수 계산
    /// </summary>
    int CalcDustCount(float strikeForce)
    {
        if (strikeForce < 0.3f)
            return Random.Range(1, 3);
        else if (strikeForce < 0.7f)
            return Random.Range(3, 6);
        else
            return Random.Range(6, 11);
    }

    /// <summary>
    /// 힘 강도에 따른 조각 개수 계산
    /// </summary>
    int CalcChipCount(float strikeForce)
    {
        if (strikeForce < 0.3f)
            return Random.Range(0, 2);
        else if (strikeForce < 0.7f)
            return Random.Range(1, 4);
        else
            return Random.Range(3, 7);
    }

    /// <summary>
    /// 개별 먼지 파티클 생성
    /// </summary>
    void CreateDust(Vector3 position, Vector3 normal, float strikeForce)
    {
        GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dust.transform.position = position + Random.insideUnitSphere * 0.1f;

        float size = Random.Range(0.015f, 0.04f) * (0.5f + strikeForce);
        dust.transform.localScale = Vector3.one * size;

        Renderer dustRenderer = dust.GetComponent<Renderer>();

        float colorIntensity = 0.4f + (strikeForce * 0.3f);
        dustRenderer.material.color = new Color(0.7f * colorIntensity, 0.6f * colorIntensity, 0.4f * colorIntensity);

        Rigidbody dustRb = dust.AddComponent<Rigidbody>();

        float forceMultiplier = 0.5f + (strikeForce * 1.5f);
        Vector3 force = normal * Random.Range(1f, 3f) * forceMultiplier + Random.insideUnitSphere * 0.5f;
        dustRb.AddForce(force, ForceMode.Impulse);

        float lifetime = 1.0f + (strikeForce * 1.0f);
        Destroy(dust, lifetime);
    }

    /// <summary>
    /// 개별 조각 파티클 생성
    /// </summary>
    void CreateChip(Vector3 position, Vector3 normal, float strikeForce)
    {
        GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chip.transform.position = position + Random.insideUnitSphere * 0.05f;

        float size = Random.Range(0.02f, 0.06f) * (0.7f + strikeForce);
        chip.transform.localScale = Vector3.one * size;
        chip.transform.rotation = Random.rotation;

        Renderer chipRenderer = chip.GetComponent<Renderer>();

        float grayIntensity = 0.3f + (strikeForce * 0.2f);
        chipRenderer.material.color = new Color(0.5f + grayIntensity, 0.4f + grayIntensity, 0.3f + grayIntensity);

        Rigidbody chipRb = chip.AddComponent<Rigidbody>();

        float forceMultiplier = 1.0f + (strikeForce * 2.0f);
        Vector3 chipForce = normal * Random.Range(2f, 5f) * forceMultiplier + Random.insideUnitSphere * 1f;
        chipRb.AddForce(chipForce, ForceMode.Impulse);

        float lifetime = 1.5f + (strikeForce * 1.5f);
        Destroy(chip, lifetime);
    }

    /// <summary>
    /// 위험한 강도일 때 추가 효과
    /// </summary>
    void CreateDangerEffect(Vector3 position, Vector3 normal)
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject danger = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            danger.transform.position = position + Random.insideUnitSphere * 0.15f;
            danger.transform.localScale = Vector3.one * Random.Range(0.03f, 0.08f);

            Renderer dangerRenderer = danger.GetComponent<Renderer>();
            dangerRenderer.material.color = Color.red;
            dangerRenderer.material.SetFloat("_Metallic", 0.8f);

            Rigidbody dangerRb = danger.AddComponent<Rigidbody>();
            Vector3 dangerForce = normal * Random.Range(3f, 7f) + Random.insideUnitSphere * 2f;
            dangerRb.AddForce(dangerForce, ForceMode.Impulse);

            Destroy(danger, 0.8f);
        }

        Debug.Log("위험한 힘! 보석 손상 위험 증가");
    }

    /// <summary>
    /// 힘을 고려한 조각 제거
    /// </summary>
    void RemoveChunksForce(Vector3 miningPoint, float strikeForce, Vector3 surfaceNormal)
    {
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null &&
            chunk.gameObject != null &&
            chunk.gameObject.activeInHierarchy
        );

        if (activeChunks.Length == 0)
        {
            Debug.Log("더 이상 채굴할 조각이 없습니다.");
            RefreshChunkCache();
            return;
        }

        System.Array.Sort(activeChunks, (a, b) =>
        {
            float distA = Vector3.Distance(a.transform.position, miningPoint);
            float distB = Vector3.Distance(b.transform.position, miningPoint);
            return distA.CompareTo(distB);
        });

        int maxChunksToRemove = CalcChunksToRemove(strikeForce);
        int chunksRemoved = 0;

        foreach (ChunkNode chunk in activeChunks)
        {
            if (chunksRemoved >= maxChunksToRemove) break;

            float distance = Vector3.Distance(chunk.transform.position, miningPoint);

            float adjustedRadius = miningRadius * (0.7f + (strikeForce * 0.6f));
            if (distance <= adjustedRadius)
            {
                RemoveChunkForce(chunk, miningPoint, surfaceNormal, strikeForce);
                chunksRemoved++;
            }
        }

        Debug.Log($"채굴 완료: {chunksRemoved}개 조각 제거 (힘: {strikeForce * 100f:F0}%)");
    }

    /// <summary>
    /// 힘에 따른 채굴할 조각 개수 계산
    /// </summary>
    int CalcChunksToRemove(float strikeForce)
    {
        if (strikeForce < 0.3f)
            return 1;
        else if (strikeForce < 0.7f)
            return Random.Range(1, 3);
        else
            return Random.Range(2, 4);
    }

    /// <summary>
    /// 힘을 고려한 부드러운 조각 제거
    /// </summary>
    void RemoveChunkForce(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float strikeForce)
    {
        if (chunk == null || chunk.gameObject == null) return;

        BreakChunkConnections(chunk);
        ApplyForcePhysics(chunk, miningPoint, surfaceNormal, strikeForce);

        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            float delay = Random.Range(0.1f, 0.6f);
            StartCoroutine(PlayDelayedFallSound(delay));
        }
    }

    void BreakChunkConnections(ChunkNode chunk)
    {
        if (chunk == null) return;

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
    }

    /// <summary>
    /// 힘에 따른 물리력 적용
    /// </summary>
    void ApplyForcePhysics(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float strikeForce)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 forceDirection = (chunk.transform.position - miningPoint).normalized;
        forceDirection.y = Mathf.Max(forceDirection.y, 0.1f);

        float baseForce = gentleForce * hardness;
        float forceMultiplier = 0.8f + (strikeForce * 1.4f);
        float finalForce = baseForce * forceMultiplier;

        rb.AddForce(forceDirection * finalForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * finalForce * 0.3f, ForceMode.Impulse);

        Debug.Log($"조각 물리력 적용: {finalForce:F1} (기본: {baseForce:F1}, 배율: {forceMultiplier:F1})");
    }

    System.Collections.IEnumerator PlayDelayedFallSound(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (chunkFallSounds != null && chunkFallSounds.Length > 0 && audioSource != null)
        {
            AudioClip fallSound = chunkFallSounds[Random.Range(0, chunkFallSounds.Length)];
            audioSource.PlayOneShot(fallSound, 0.5f);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (chunkCounter != null && gameManager != null)
        {
            chunkCounter.OnChunkCountChanged -= gameManager.OnMineralProgressChanged;
        }

        if (handController != null)
        {
            handController.OnHammerStrike -= OnHammerStrike;
        }
    }
}