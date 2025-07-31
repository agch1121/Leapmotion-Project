using UnityEngine;
using LibreFracture;
using Leap;

public class ChunkClickDestroyer : MonoBehaviour
{
    [Header("립모션 채굴 설정")]
    public float miningRadius = 0.3f; // 한 번에 채굴할 반경
    public float minPunchForce = 0.6f; // 최소 펀치 강도 (60% 이상)
    public float gentleForce = 5f; // 부드러운 힘
    public int chunksPerPunch = 2; // 펀치당 제거할 조각 수
    public float punchCooldown = 0.5f; // 펀치 쿨다운 (연속 펀치 방지)

    [Header("립모션 연동")]
    public LayerMask chunkLayer = -1; // 조각 레이어

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds; // 조각이 떨어지는 소리들

    private Controller leapController;
    private ForceDetector forceDetector;
    private ChunkGraphManager chunkGraphManager;
    private AudioSource audioSource;
    private ChunkNode[] allChunks; // 모든 조각 캐시

    private float lastPunchTime = 0f;
    private bool wasPunching = false;

    void Start()
    {
        // 립모션 초기화
        leapController = new Controller();

        // ForceDetector 찾기
        forceDetector = FindObjectOfType<ForceDetector>();
        if (forceDetector == null)
        {
            Debug.LogError("ForceDetector가 필요합니다!");
        }

        // ChunkGraphManager 찾기
        chunkGraphManager = GetComponent<ChunkGraphManager>();
        if (chunkGraphManager == null)
        {
            Debug.LogError("ChunkGraphManager가 필요합니다!");
        }

        // 오디오 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 모든 조각들 캐시
        RefreshChunkCache();

        Debug.Log($"립모션 채굴 시스템 초기화 완료 - 총 {allChunks?.Length ?? 0}개 조각");
    }

    void RefreshChunkCache()
    {
        allChunks = GetComponentsInChildren<ChunkNode>();
        Debug.Log($"조각 캐시 갱신: {allChunks.Length}개");
    }

    void Update()
    {
        DetectPunch();

        // R키로 조각 캐시 갱신
        if (Input.GetKeyDown(KeyCode.R))
        {
            RefreshChunkCache();
            Debug.Log("조각 캐시 수동 갱신");
        }
    }

    void DetectPunch()
    {
        if (forceDetector == null) return;

        float currentForce = forceDetector.GetCurrentForce();
        bool isPunching = currentForce >= minPunchForce;

        // 펀치 감지: 이전에 펀치 안했다가 지금 펀치하는 순간
        if (isPunching && !wasPunching && Time.time - lastPunchTime >= punchCooldown)
        {
            // 오른손 위치 가져오기
            Frame frame = leapController.Frame();
            Hand rightHand = frame.Hands.Find(h => h.IsRight);

            if (rightHand != null)
            {
                // 립모션 좌표를 유니티 좌표로 변환
                Vector3 handPosition = new Vector3(
                    rightHand.PalmPosition.x * 0.001f,  // mm to m
                    rightHand.PalmPosition.y * 0.001f,
                    rightHand.PalmPosition.z * 0.001f
                );

                // 손 속도도 가져오기
                Vector3 handVelocity = new Vector3(
                    rightHand.PalmVelocity.x * 0.001f,
                    rightHand.PalmVelocity.y * 0.001f,
                    rightHand.PalmVelocity.z * 0.001f
                );

                Debug.Log($"펀치 감지! 강도: {currentForce:F2}, 위치: {handPosition}, 속도: {handVelocity.magnitude:F2}");

                // 해당 위치에서 채굴 실행
                MineAtHandPosition(handPosition, handVelocity.normalized, currentForce);

                lastPunchTime = Time.time;
            }
        }

        wasPunching = isPunching;
    }

    void MineAtHandPosition(Vector3 handPosition, Vector3 punchDirection, float punchForce)
    {
        // 손 위치에서 가장 가까운 돌 찾기
        Vector3 closestPoint = FindClosestPointOnRock(handPosition);

        // 채굴 효과 생성
        CreateMiningEffect(closestPoint, punchDirection);

        // 채굴 사운드 재생
        if (miningSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f); // 약간의 피치 변화
            audioSource.PlayOneShot(miningSound);
        }

        // 활성화된 조각들만 찾기
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null && chunk.gameObject.activeInHierarchy);

        if (activeChunks.Length == 0)
        {
            Debug.Log("더 이상 채굴할 조각이 없습니다.");
            return;
        }

        // 채굴 지점에 가까운 조각들을 거리순으로 정렬
        System.Array.Sort(activeChunks, (a, b) =>
        {
            float distA = Vector3.Distance(a.transform.position, closestPoint);
            float distB = Vector3.Distance(b.transform.position, closestPoint);
            return distA.CompareTo(distB);
        });

        // 펀치 강도에 따라 제거할 조각 수 조절
        int chunksToRemove = Mathf.RoundToInt(chunksPerPunch * punchForce);
        chunksToRemove = Mathf.Clamp(chunksToRemove, 1, chunksPerPunch + 2);

        int chunksRemoved = 0;

        // 가까운 조각부터 차례로 제거
        foreach (ChunkNode chunk in activeChunks)
        {
            if (chunksRemoved >= chunksToRemove) break;

            float distance = Vector3.Distance(chunk.transform.position, closestPoint);

            // 채굴 반경 내에 있는 조각들만 처리
            if (distance <= miningRadius)
            {
                RemoveChunkGently(chunk, closestPoint, punchDirection, punchForce);
                chunksRemoved++;
            }
        }

        Debug.Log($"조각 {chunksRemoved}개 채굴됨 (강도: {punchForce:F2}). 남은 조각: {activeChunks.Length - chunksRemoved}개");
    }

    Vector3 FindClosestPointOnRock(Vector3 handPosition)
    {
        // 돌의 콜라이더에서 가장 가까운 점 찾기
        Collider rockCollider = GetComponent<Collider>();
        if (rockCollider != null)
        {
            return rockCollider.ClosestPoint(handPosition);
        }

        // 콜라이더가 없으면 중심점 반환
        return transform.position;
    }

    void RemoveChunkGently(ChunkNode chunk, Vector3 miningPoint, Vector3 punchDirection, float punchForce)
    {
        if (chunk == null) return;

        // 조각의 모든 조인트 끊기
        BreakChunkConnections(chunk);

        // 펀치 강도에 따라 힘 조절
        ApplyPunchForce(chunk, miningPoint, punchDirection, punchForce);

        // 조각 떨어지는 소리 (지연)
        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
        {
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.2f, 0.8f)));
        }

        // 일정 시간 후 조각 삭제
        StartCoroutine(DestroyChunkAfterDelay(chunk.gameObject, Random.Range(5f, 8f)));

        Debug.Log($"조각 제거: {chunk.name} (펀치 강도: {punchForce:F2})");
    }

    void BreakChunkConnections(ChunkNode chunk)
    {
        // ChunkNode의 모든 조인트 찾아서 끊기
        Joint[] joints = chunk.GetComponents<Joint>();

        foreach (Joint joint in joints)
        {
            if (joint != null)
            {
                Destroy(joint);
            }
        }

        // FixedJoint들도 찾아서 끊기
        FixedJoint[] fixedJoints = chunk.GetComponents<FixedJoint>();
        foreach (FixedJoint fixedJoint in fixedJoints)
        {
            if (fixedJoint != null)
            {
                Destroy(fixedJoint);
            }
        }
    }

    void ApplyPunchForce(ChunkNode chunk, Vector3 miningPoint, Vector3 punchDirection, float punchForce)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        // 펀치 방향 + 약간의 랜덤성
        Vector3 forceDirection = punchDirection + Random.insideUnitSphere * 0.2f;
        forceDirection.y = Mathf.Max(forceDirection.y, 0.1f); // 최소한 위쪽으로

        // 펀치 강도에 비례한 힘 적용
        float appliedForce = gentleForce * (1f + punchForce * 2f); // 강한 펀치일수록 더 센 힘
        rb.AddForce(forceDirection * appliedForce, ForceMode.Impulse);

        // 회전 (자연스러운 효과)
        rb.AddTorque(Random.insideUnitSphere * appliedForce * 0.2f, ForceMode.Impulse);
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 채굴 먼지 (적은 양)
        for (int i = 0; i < 8; i++)
        {
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.transform.position = position + Random.insideUnitSphere * 0.15f;
            dust.transform.localScale = Vector3.one * Random.Range(0.02f, 0.06f);

            Renderer dustRenderer = dust.GetComponent<Renderer>();
            dustRenderer.material.color = new Color(0.7f, 0.6f, 0.4f, 0.8f);

            Rigidbody dustRb = dust.AddComponent<Rigidbody>();
            Vector3 force = normal * Random.Range(2f, 5f) + Random.insideUnitSphere * 1f;
            dustRb.AddForce(force, ForceMode.Impulse);

            Destroy(dust, 1.5f);
        }

        // 돌조각 (2-3개)
        int chipCount = Random.Range(2, 4);
        for (int i = 0; i < chipCount; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.transform.position = position + Random.insideUnitSphere * 0.08f;
            chip.transform.localScale = Vector3.one * Random.Range(0.04f, 0.09f);
            chip.transform.rotation = Random.rotation;

            Renderer chipRenderer = chip.GetComponent<Renderer>();
            chipRenderer.material.color = new Color(0.5f, 0.4f, 0.3f);

            Rigidbody chipRb = chip.AddComponent<Rigidbody>();
            Vector3 chipForce = normal * Random.Range(3f, 7f) + Random.insideUnitSphere * 2f;
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
            audioSource.PlayOneShot(fallSound, 0.5f); // 볼륨 낮춤
        }
    }

    System.Collections.IEnumerator DestroyChunkAfterDelay(GameObject chunk, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (chunk != null)
        {
            Destroy(chunk);
        }
    }

    // 기즈모로 채굴 반경 표시
    void OnDrawGizmosSelected()
    {
        // 오른손 위치와 채굴 반경 표시
        if (Application.isPlaying && leapController != null)
        {
            Frame frame = leapController.Frame();
            Hand rightHand = frame.Hands.Find(h => h.IsRight);

            if (rightHand != null)
            {
                Vector3 handPosition = new Vector3(
                    rightHand.PalmPosition.x * 0.001f,
                    rightHand.PalmPosition.y * 0.001f,
                    rightHand.PalmPosition.z * 0.001f
                );

                Vector3 closestPoint = FindClosestPointOnRock(handPosition);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(handPosition, 0.05f); // 손 위치

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(closestPoint, miningRadius); // 채굴 범위

                Gizmos.color = Color.red;
                Gizmos.DrawLine(handPosition, closestPoint); // 연결선
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
            chunk != null && chunk.gameObject.activeInHierarchy);
        Debug.Log($"활성 조각 수: {activeChunks.Length} / {allChunks.Length}");
    }

    [ContextMenu("Test Punch")]
    public void TestPunch()
    {
        Debug.Log("펀치 테스트!");
        MineAtHandPosition(transform.position, Vector3.forward, 1f);
    }
}