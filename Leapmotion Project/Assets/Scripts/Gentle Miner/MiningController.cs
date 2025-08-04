using LibreFracture;
using UnityEngine;
using System.Collections;

/// <summary>
/// 채굴 작업 총괄 제어 (Test.cs의 핵심 기능들을 정리)
/// 마우스 입력, 채굴 실행, 조각 관리 등을 담당
/// </summary>
public class MiningController : MonoBehaviour
{
    [Header("채굴 설정")]
    public float miningRadius = 0.3f;
    public float gentleForce = 5f;
    public int chunksPerClick = 2;
    public LayerMask chunkLayer = -1;

    [Header("채굴 강도")]
    [Range(1f, 50f)]
    public float miningForceIntensity = 20f;

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds;

    // 시스템 참조들
    private GameManager gameManager;
    private GemProtectionSystem gemProtectionSystem;
    private ForceCalculator forceCalculator;
    private AudioSource audioSource;
    private ChunkNode[] allChunks;

    void Start()
    {
        InitializeMiningController();
    }

    void InitializeMiningController()
    {
        // 시스템 참조
        gameManager = FindFirstObjectByType<GameManager>();
        gemProtectionSystem = GetComponent<GemProtectionSystem>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();

        // 오디오 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 조각 캐시
        RefreshChunkCache();

        Debug.Log("MiningController 초기화 완료");
    }

    void Update()
    {
        // 게임이 활성 상태일 때만 입력 처리
        if (gameManager != null && !gameManager.IsGameActive)
            return;

        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }

        // 디버그 키들
        if (Input.GetKeyDown(KeyCode.R))
        {
            RefreshChunkCache();
            Debug.Log("조각 캐시 갱신");
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
                ExecuteMining(hit.point, hit.normal);
            }
        }
    }

    /// <summary>
    /// 채굴 실행
    /// </summary>
    public void ExecuteMining(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        // 1. 힘 계산 (ForceCalculator가 있으면 사용)
        float actualForce = miningForceIntensity;
        if (forceCalculator != null)
        {
            actualForce = forceCalculator.GetGemProtectionForce();
        }

        // 2. 보석 보호 시스템에 충격 전달
        if (gemProtectionSystem != null)
        {
            gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, actualForce);
        }

        // 3. 채굴 효과 생성
        CreateMiningEffect(miningPoint, surfaceNormal);

        // 4. 채굴 사운드 재생
        PlayMiningSound();

        // 5. 조각 제거
        RemoveChunksAtPoint(miningPoint, surfaceNormal);

        Debug.Log($"채굴 실행: 위치 {miningPoint}, 힘 {actualForce:F1}");
    }

    void RemoveChunksAtPoint(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        // 활성화된 조각들만 찾기
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null &&
            chunk.gameObject != null &&
            chunk.gameObject.activeInHierarchy
        );

        if (activeChunks.Length == 0)
        {
            RefreshChunkCache();
            return;
        }

        // 거리순으로 정렬
        System.Array.Sort(activeChunks, (a, b) =>
        {
            float distA = Vector3.Distance(a.transform.position, miningPoint);
            float distB = Vector3.Distance(b.transform.position, miningPoint);
            return distA.CompareTo(distB);
        });

        // 가까운 조각들 제거
        int chunksRemoved = 0;
        foreach (ChunkNode chunk in activeChunks)
        {
            if (chunksRemoved >= chunksPerClick) break;

            float distance = Vector3.Distance(chunk.transform.position, miningPoint);
            if (distance <= miningRadius)
            {
                RemoveChunkGently(chunk, miningPoint, surfaceNormal);
                chunksRemoved++;
            }
        }

        Debug.Log($"조각 {chunksRemoved}개 제거");
    }

    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal)
    {
        if (chunk == null) return;

        // 연결 끊기
        BreakChunkConnections(chunk);

        // 부드러운 힘 적용
        ApplyGentleForce(chunk, surfaceNormal);

        // 떨어지는 소리 (지연 재생)
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }
    }

    void BreakChunkConnections(ChunkNode chunk)
    {
        // 모든 Joint 제거
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

    void ApplyGentleForce(ChunkNode chunk, Vector3 surfaceNormal)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 gentleDirection = surfaceNormal + Random.insideUnitSphere * 0.3f;
        gentleDirection.y = Mathf.Max(gentleDirection.y, 0.1f);

        rb.AddForce(gentleDirection * gentleForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 채굴 먼지
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

        // 작은 돌조각
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

    void PlayMiningSound()
    {
        if (miningSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(miningSound);
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
        Debug.Log($"조각 캐시 갱신: {allChunks.Length}개");
    }

    /// <summary>
    /// 떨어진 조각들 정리
    /// </summary>
    public void CleanupFallenChunks()
    {
        // ChunkCleaner가 있으면 사용
        ChunkCleaner cleaner = GetComponent<ChunkCleaner>();
        if (cleaner != null)
        {
            cleaner.CleanupNow();
            return;
        }

        // 수동 정리
        int cleanedCount = 0;
        ChunkNode[] allChunkNodes = FindObjectsByType<ChunkNode>(FindObjectsSortMode.None);

        foreach (ChunkNode chunk in allChunkNodes)
        {
            if (chunk != null && chunk.gameObject != null &&
                !chunk.transform.IsChildOf(transform))
            {
                Destroy(chunk.gameObject);
                cleanedCount++;
            }
        }

        Debug.Log($"떨어진 조각 정리: {cleanedCount}개 삭제");
    }

    void OnDrawGizmosSelected()
    {
        // 마우스 위치에 채굴 범위 표시
        if (Application.isPlaying)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(hit.point, miningRadius);
            }
        }
    }

    [ContextMenu("조각 캐시 갱신")]
    public void RefreshChunkCacheMenu() => RefreshChunkCache();

    [ContextMenu("떨어진 조각 정리")]
    public void CleanupFallenChunksMenu() => CleanupFallenChunks();

    [ContextMenu("활성 조각 수 확인")]
    public void CountActiveChunks()
    {
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null && chunk.gameObject != null && chunk.gameObject.activeInHierarchy);
        Debug.Log($"활성 조각 수: {activeChunks.Length} / {allChunks.Length}");
    }
}