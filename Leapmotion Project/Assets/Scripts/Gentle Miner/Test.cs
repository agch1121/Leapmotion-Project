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

    private ChunkGraphManager chunkGraphManager;
    private AudioSource audioSource;
    private ChunkNode[] allChunks; // 모든 조각 캐시

    // 보석 보호 시스템 참조
    private GemProtectionSystem gemProtectionSystem;

    // 조각 카운터 시스템 참조
    private ChunkCounter chunkCounter;

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
            Debug.Log("ChunkCounter 자동 추가됨");
        }

        // 카운터 이벤트 구독
        chunkCounter.OnChunkCountChanged += OnChunkCountChanged;

        // 오디오 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 모든 조각들 캐시 (성능 향상)
        RefreshChunkCache();

        Debug.Log($"채굴 시스템 초기화 완료 - 총 {allChunks?.Length ?? 0}개 조각");
    }

    void RefreshChunkCache()
    {
        // 안전한 청크 캐시 갱신 - null이나 파괴된 청크 제외
        var validChunks = new System.Collections.Generic.List<ChunkNode>();

        ChunkNode[] foundChunks = GetComponentsInChildren<ChunkNode>();
        foreach (ChunkNode chunk in foundChunks)
        {
            if (chunk != null && chunk.gameObject != null && chunk.IsSafeToRemove())
            {
                validChunks.Add(chunk);
            }
        }

        allChunks = validChunks.ToArray();
        Debug.Log($"조각 캐시 갱신: {allChunks.Length}개 (유효한 조각만)");
    }

    void Update()
    {
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

        // C키로 정확한 조각 개수 확인
        if (Input.GetKeyDown(KeyCode.C) && chunkCounter != null)
        {
            chunkCounter.PrintDetailedStatus();
        }

        // X키로 즉시 조각 정리
        if (Input.GetKeyDown(KeyCode.X))
        {
            CleanupAllFallenChunks();
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
                Debug.Log($"채굴 지점: {hit.collider.name}, 위치: {hit.point}");
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
            chunk.gameObject.activeInHierarchy &&
            chunk.IsSafeToRemove());

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

        Debug.Log($"조각 {chunksRemoved}개 채굴됨. 채굴 상태는 ChunkCounter에서 자동 추적됩니다.");

        // ChunkCounter가 자동으로 진행률을 추적하므로 별도 체크 불필요
    }

    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal)
    {
        if (chunk == null) return;

        // 안전한 제거를 위한 상태 확인
        if (!chunk.IsSafeToRemove())
        {
            Debug.LogWarning($"ChunkNode {chunk.name}이 안전하지 않은 상태입니다. 제거를 건너뜁니다.");
            return;
        }

        // 카운터에 제거 예고 (정확한 카운팅을 위해)
        if (chunkCounter != null)
        {
            chunkCounter.NotifyChunkWillBeDestroyed(chunk);
        }

        // 조각 떨어지는 소리 (지연)
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }

        // SafeChunkRemoval을 사용한 안전한 제거
        SafeChunkRemoval.RemoveChunkSafely(chunk, miningPoint, surfaceNormal, gentleForce);

        Debug.Log($"조각 안전 제거 시작: {chunk.name}");
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

    /// <summary>
    /// 조각 개수 변화 이벤트 핸들러
    /// </summary>
    void OnChunkCountChanged(int activeChunks, int destroyedChunks, float progress)
    {
        Debug.Log($"채굴 진행 상황: {destroyedChunks}개 파괴됨, {activeChunks}개 남음 ({progress * 100f:F1}%)");

        // 보석 노출 상태 체크 (정확한 진행률 기반)
        if (progress >= 0.7f && gemProtectionSystem != null)
        {
            Debug.Log("보석이 노출되었습니다!");
            gemProtectionSystem.PrintGemStatus();
        }

        // 완전 채굴 체크
        if (activeChunks <= 0)
        {
            Debug.Log("=== 채굴 완료! 최종 보석 상태 ===");
            if (gemProtectionSystem != null)
            {
                gemProtectionSystem.PrintGemStatus();
            }
        }
    }

    /// <summary>
    /// 떨어진 모든 조각들을 즉시 정리 (안전한 버전)
    /// </summary>
    void CleanupAllFallenChunks()
    {
        Debug.Log("수동 조각 정리 시작...");

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

        // 2. SafeChunkRemoval 컴포넌트가 있는 것들 정리
        SafeChunkRemoval[] safeRemovals = FindObjectsByType<SafeChunkRemoval>(FindObjectsSortMode.None);
        foreach (SafeChunkRemoval removal in safeRemovals)
        {
            if (removal != null && removal.gameObject != null &&
                !removal.transform.IsChildOf(transform))
            {
                Destroy(removal.gameObject);
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

    // 인스펙터에서 호출 가능한 함수들
    [ContextMenu("Refresh Chunk Cache")]
    public void RefreshChunkCacheMenu()
    {
        RefreshChunkCache();
    }

    [ContextMenu("Count Active Chunks")]
    public void CountActiveChunks()
    {
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null &&
            chunk.gameObject != null &&
            chunk.gameObject.activeInHierarchy &&
            chunk.IsSafeToRemove());
        Debug.Log($"활성 조각 수: {activeChunks.Length} / {allChunks.Length}");
    }

    [ContextMenu("Test Gem Protection")]
    public void TestGemProtection()
    {
        if (gemProtectionSystem != null)
        {
            // 임의의 위치에서 강한 충격 테스트
            Vector3 testPoint = transform.position;
            float testForce = 25f; // 강한 충격

            Debug.Log($"보석 보호 테스트: 충격 강도 {testForce} 적용");
            gemProtectionSystem.CheckMiningImpactOnGems(testPoint, testForce);
        }
        else
        {
            Debug.LogWarning("GemProtectionSystem이 없습니다!");
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