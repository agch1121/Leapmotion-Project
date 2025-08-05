using LibreFracture;
using UnityEngine;

/// <summary>
/// 광물 블록 구조 및 기본 채굴 로직 처리 (Test.cs 간소화 버전)
/// 주요 게임 로직은 GameManager로 이동됨
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
    public float hardness = 1.0f;      // StageManager에서 설정하는 광물 경도
    public float gemQuality = 1.0f;    // StageManager에서 설정하는 보석 품질

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
        // 모든 조각들 캐시
        RefreshChunkCache();

        // 스테이지별 설정 적용
        ApplyStageSettings();
    }

    /// <summary>
    /// 스테이지별 설정을 실제 시스템에 적용 (StageManager에서 호출 가능)
    /// </summary>
    public void ApplyStageSettings()
    {
        // 채굴 강도를 경도에 따라 조정
        miningForceIntensity = miningForceIntensity * hardness;

        // 조각 연결 강도를 경도에 따라 조정
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

        // 마우스 클릭 처리
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
            // 클릭된 조각이 이 오브젝트의 일부인지 확인
            if (hit.collider.transform.IsChildOf(transform))
            {
                MineAtPoint(hit.point, hit.normal);
            }
        }
    }

    /// <summary>
    /// HandController에서 발생하는 망치 타격 이벤트 처리
    /// </summary>
    public void OnHammerStrike(Vector3 position, Vector3 direction, float force)
    {
        Debug.Log($"망치 타격 감지! 위치: {position}, 힘: {force:F2}");

        // 게임이 시작되지 않았으면 무시
        if (gameManager == null || !gameManager.IsGameStarted)
        {
            Debug.Log("게임이 아직 시작되지 않았습니다.");
            return;
        }

        // 채굴 실행
        Vector3 surfaceNormal = -direction;
        MineAtPoint(position, surfaceNormal);
    }

    /// <summary>
    /// 지정된 위치에서 채굴 실행
    /// </summary>
    public void MineAtPoint(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        // 1. 채굴 전에 보석 보호 시스템에 충격 전달
        if (gemProtectionSystem != null)
        {
            // 경도에 따라 충격 강도 조정
            float adjustedForce = miningForceIntensity * hardness;
            gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, adjustedForce);
        }

        // 2. 채굴 효과 생성
        CreateMiningEffect(miningPoint, surfaceNormal);

        // 3. 채굴 사운드 재생
        if (miningSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(miningSound);
        }

        // 4. 활성화되고 안전한 조각들만 찾기
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null &&
            chunk.gameObject != null &&
            chunk.gameObject.activeInHierarchy
        );

        if (activeChunks.Length == 0)
        {
            Debug.Log("더 이상 채굴할 조각이 없습니다. (캐시 갱신 시도 중...)");
            RefreshChunkCache();
            return;
        }

        // 5. 채굴 지점에 가까운 조각들을 거리순으로 정렬
        System.Array.Sort(activeChunks, (a, b) =>
        {
            float distA = Vector3.Distance(a.transform.position, miningPoint);
            float distB = Vector3.Distance(b.transform.position, miningPoint);
            return distA.CompareTo(distB);
        });

        int chunksRemoved = 0;

        // 6. 가까운 조각부터 차례로 제거
        foreach (ChunkNode chunk in activeChunks)
        {
            if (chunksRemoved >= chunksPerClick) break;

            float distance = Vector3.Distance(chunk.transform.position, miningPoint);

            // 채굴 범위 내에 있는 조각들만 처리
            if (distance <= miningRadius)
            {
                RemoveChunkGently(chunk, miningPoint, surfaceNormal);
                chunksRemoved++;
            }
        }
    }

    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal)
    {
        if (chunk == null || chunk.gameObject == null) return;

        // 간단하게 연결만 끊기
        BreakChunkConnections(chunk);

        // 부드러운 물리 힘 적용 (경도에 따라 조정)
        ApplyGentleForce(chunk, surfaceNormal);

        // 조각 떨어지는 소리
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }
    }

    void BreakChunkConnections(ChunkNode chunk)
    {
        if (chunk == null) return;

        // 모든 Joint 제거
        Joint[] joints = chunk.GetComponents<Joint>();
        foreach (Joint joint in joints)
        {
            if (joint != null)
                Destroy(joint);
        }

        FixedJoint[] fixedJoints = chunk.GetComponents<FixedJoint>();
        foreach (FixedJoint fixedJoint in fixedJoints)
        {
            if (fixedJoint != null)
                Destroy(fixedJoint);
        }
    }

    void ApplyGentleForce(ChunkNode chunk, Vector3 surfaceNormal)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 forceDirection = (chunk.transform.position - transform.position).normalized;
        forceDirection.y = Mathf.Max(forceDirection.y, 0.1f);

        // 경도에 따라 힘 조정
        float adjustedForce = gentleForce * hardness;
        rb.AddForce(forceDirection * adjustedForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * adjustedForce * 0.2f, ForceMode.Impulse);
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
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