using LibreFracture;
using UnityEngine;

public class Test : MonoBehaviour
{
    [Header("채굴 설정")]
    public float miningRadius = 0.3f; // 한 번에 채굴할 범위 (작게)
    public float gentleForce = 5f; // 부드러운 힘 (파편 대신)
    public int chunksPerClick = 2; // 클릭당 제거할 조각 수
    public LayerMask chunkLayer = -1; // 조각 레이어

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds; // 조각이 떨어지는 소리들

    [Header("채굴 강도 (보석 보호용)")]
    [Range(1f, 50f)]
    public float miningForceIntensity = 20f; // 채굴 강도 (보석 보호 시스템에서 사용)

    [Header("보석 연출 시스템")]
    public GemRevealSystem gemRevealSystem; // GemRevealSystem 참조

    [Header("게임 시작 설정")]
    public bool showGemPreviewOnStart = true; // 게임 시작 시 보석 미리보기 여부
    public float gameStartDelay = 2f; // 게임 시작 후 미리보기까지 대기 시간

    private ChunkGraphManager chunkGraphManager;
    private AudioSource audioSource;
    private ChunkNode[] allChunks; // 모든 조각 캐시

    // 보석 보호 시스템 참조
    private GemProtectionSystem gemProtectionSystem;

    // 조각 카운터 시스템 참조
    private ChunkCounter chunkCounter;

    // 게임 시작 상태
    private bool gameStarted = false;

    /// <summary>
    /// 게임이 시작되었는지 확인하는 프로퍼티 (다른 스크립트에서 참조용)
    /// </summary>
    public bool IsGameStarted => gameStarted;

    void Start()
    {
        // ChunkGraphManager 찾기
        chunkGraphManager = GetComponent<ChunkGraphManager>();
        if (chunkGraphManager == null)
        {
            Debug.LogError("ChunkGraphManager가 필요합니다!");
        }

        // 보석 보호 시스템 찾기
        gemProtectionSystem = GetComponent<GemProtectionSystem>();
        if (gemProtectionSystem == null)
        {
            Debug.LogWarning("GemProtectionSystem이 없습니다. 보석 보호 기능이 비활성화됩니다.");
        }

        // 조각 카운터 시스템 찾기
        chunkCounter = GetComponent<ChunkCounter>();
        if (chunkCounter == null)
        {
            chunkCounter = gameObject.AddComponent<ChunkCounter>();
        }

        // 카운터 이벤트 구독
        chunkCounter.OnChunkCountChanged += OnChunkCountChanged;

        // 오디오 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (gemRevealSystem == null)
        {
            gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
            if (gemRevealSystem == null)
            {
                Debug.LogWarning("GemRevealSystem이 없습니다. 보석 연출이 비활성화됩니다.");
            }
        }

        // 모든 조각들 캐시 (성능 향상)
        RefreshChunkCache();

        Debug.Log($"채굴 시스템 초기화 완료 - 총 {allChunks?.Length ?? 0}개 조각");

        // 게임 시작 시 보석 미리보기 실행
        if (showGemPreviewOnStart && gemRevealSystem != null)
        {
            Invoke(nameof(StartGemPreview), gameStartDelay);
        }
        else
        {
            gameStarted = true; // 미리보기 없으면 바로 게임 시작
        }
    }

    /// <summary>
    /// 게임 시작 시 보석 미리보기 실행
    /// </summary>
    void StartGemPreview()
    {
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemPreview(); // 미리보기만!

            // 미리보기 연출 시간 후 게임 시작 (카메라 이동 시간 * 2 + 보석 표시 시간)
            float previewDuration = (gemRevealSystem.cameraTransitionTime * 2) + gemRevealSystem.gemDisplayTime;
            Invoke(nameof(EnableGameplay), previewDuration + 1f); // 여유시간 1초 추가
        }
        else
        {
            gameStarted = true;
        }
    }

    /// <summary>
    /// 게임플레이 활성화
    /// </summary>
    void EnableGameplay()
    {
        gameStarted = true;
    }

    void RefreshChunkCache()
    {
        // 안전한 청크 캐시 갱신 - null이나 파괴된 청크 제외
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
        if (!gameStarted) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }

        // R키로 조각 캐시 갱신
        if (Input.GetKeyDown(KeyCode.R))
        {
            RefreshChunkCache();
            Debug.Log("조각 캐시 수동 갱신");
        }

        // G키로 보석 상태 확인
        if (Input.GetKeyDown(KeyCode.G) && gemProtectionSystem != null)
        {
            gemProtectionSystem.PrintGemStatus();
        }

        // X키로 즉시 조각 정리
        if (Input.GetKeyDown(KeyCode.X))
        {
            CleanupAllFallenChunks();
        }

        // P키로 보석 미리보기 수동 실행 (테스트용)
        if (Input.GetKeyDown(KeyCode.P) && gemRevealSystem != null)
        {
            gemRevealSystem.StartGemPreview();
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

    void MineAtPoint(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        // 1. 채굴 전에 보석 보호 시스템에 충격 전달
        if (gemProtectionSystem != null)
        {
            gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, miningForceIntensity);
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

            // 캐시 갱신 시도
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

        // 간단하게 연결만 끊기 - 삭제하지 않음
        BreakChunkConnections(chunk);

        // 부드러운 물리 힘 적용
        ApplyGentleForce(chunk, surfaceNormal);

        // 조각 떨어지는 소리
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }
    }

    /// <summary>
    /// 연결 끊기만 하고 삭제하지 않음
    /// </summary>
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

    /// <summary>
    /// 부드러운 물리 힘 적용
    /// </summary>
    void ApplyGentleForce(ChunkNode chunk, Vector3 surfaceNormal)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        // 자연스러운 방향으로 부드럽게
        Vector3 gentleDirection = surfaceNormal + Random.insideUnitSphere * 0.3f;
        gentleDirection.y = Mathf.Max(gentleDirection.y, 0.1f);

        rb.AddForce(gentleDirection * gentleForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 채굴 먼지 (작은 양)
        for (int i = 0; i < 5; i++)
        {
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.transform.position = position + Random.insideUnitSphere * 0.1f;
            dust.transform.localScale = Vector3.one * Random.Range(0.02f, 0.05f);

            Renderer dustRenderer = dust.GetComponent<Renderer>();
            dustRenderer.material.color = new Color(0.7f, 0.6f, 0.4f, 0.8f);

            Rigidbody dustRb = dust.AddComponent<Rigidbody>();
            Vector3 force = normal * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.5f;
            dustRb.AddForce(force, ForceMode.Impulse);

            Destroy(dust, 1.5f);
        }

        // 작은 돌조각 (1-2개만)
        int chipCount = Random.Range(1, 3);
        for (int i = 0; i < chipCount; i++)
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

    void OnChunkCountChanged(int activeChunks, int destroyedChunks, float progress)
    {
        // 게임이 시작되지 않았으면 진행도 처리 안함
        if (!gameStarted) return;

        // 완전 채굴 체크
        if (activeChunks <= 0)
        {
            // 최종 보석 점수 계산
            int finalGemScore = CalculateFinalGemScore();

            // 보석 연출 시작 (게임이 완전히 시작된 후에만!)
            if (gemRevealSystem != null)
            {
                gemRevealSystem.StartGemReveal(finalGemScore);
            }
        }
    }

    int CalculateFinalGemScore()
    {
        if (gemProtectionSystem == null) return 0; // 기본 점수

        int totalScore = 0;
        int gemCount = 0;

        // 모든 보석의 점수 합산
        var allGems = gemProtectionSystem.GetAllGems();
        foreach (var gem in allGems)
        {
            int gemScore = gemProtectionSystem.CalculateGemScore(gem);
            totalScore += gemScore;
            gemCount++;

            Debug.Log($"보석 '{gem.gemName}': {gemScore}점 ({gem.currentCondition:F1}% 상태)");
        }

        // 평균 점수 계산
        int averageScore = gemCount > 0 ? totalScore / gemCount : 0;

        Debug.Log($"최종 점수: {averageScore}점 (총 {gemCount}개 보석)");

        return averageScore;
    }

    /// <summary>
    /// 떨어진 모든 조각들을 즉시 정리 (안전한 버전)
    /// </summary>
    void CleanupAllFallenChunks()
    {
        // ChunkCleaner가 있으면 사용
        ChunkCleaner cleaner = GetComponent<ChunkCleaner>();
        if (cleaner != null)
        {
            cleaner.CleanupNow();
            return;
        }

        // ChunkCleaner가 없으면 직접 정리 (안전한 방식)
        int cleanedCount = 0;

        // 1. ChunkNode나 SafeChunkRemoval 컴포넌트가 있는 것들만 정리
        ChunkNode[] allChunkNodes = FindObjectsByType<ChunkNode>(FindObjectsSortMode.None);
        foreach (ChunkNode chunk in allChunkNodes)
        {
            if (chunk != null && chunk.gameObject != null &&
                !chunk.transform.IsChildOf(transform)) // 부모가 광물 블록이 아닌 것들만
            {
                Destroy(chunk.gameObject);
                cleanedCount++;
            }
        }

        // 3. 특정 태그가 있는 것들만 정리 (안전함)
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("StoneChunk");
        foreach (GameObject obj in taggedObjects)
        {
            if (obj != null && !obj.transform.IsChildOf(transform))
            {
                Destroy(obj);
                cleanedCount++;
            }
        }

        // 4. 매우 작고 이름이 특정 패턴인 것들만 (추가 안전 검사)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj == null) continue;

            // 매우 엄격한 조건: 작은 크기 + 특정 이름 패턴 + 부모 없음 + Rigidbody 있음
            bool isVerySmall = obj.transform.localScale.magnitude < 1f;
            bool hasSpecificName = obj.name.ToLower().Contains("chunk") &&
                                  (obj.name.ToLower().Contains("librefracture") ||
                                   obj.name.ToLower().Contains("fractured"));
            bool hasNoParent = obj.transform.parent == null;
            bool hasRigidbody = obj.GetComponent<Rigidbody>() != null;
            bool isNotImportantObject = !obj.CompareTag("Player") &&
                                       !obj.CompareTag("MainCamera") &&
                                       !obj.CompareTag("UI") &&
                                       obj != gameObject;

            if (isVerySmall && hasSpecificName && hasNoParent && hasRigidbody && isNotImportantObject)
            {
                Destroy(obj);
                cleanedCount++;
            }
        }

        Debug.Log($"안전한 정리 완료: {cleanedCount}개 오브젝트 삭제");
    }

    System.Collections.IEnumerator PlayDelayedFallSound(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (chunkFallSounds != null && chunkFallSounds.Length > 0 && audioSource != null)
        {
            AudioClip fallSound = chunkFallSounds[Random.Range(0, chunkFallSounds.Length)];
            audioSource.PlayOneShot(fallSound, 0.5f); // 볼륨 낮춤
        }
    }

    // 기즈모로 채굴 범위 표시
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        // 마우스 위치에 채굴 범위 표시
        if (Application.isPlaying)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Gizmos.DrawWireSphere(hit.point, miningRadius);
            }
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (chunkCounter != null)
        {
            chunkCounter.OnChunkCountChanged -= OnChunkCountChanged;
        }
    }
}