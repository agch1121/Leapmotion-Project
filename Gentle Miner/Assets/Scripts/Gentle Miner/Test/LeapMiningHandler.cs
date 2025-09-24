using UnityEngine;
using Leap;
using Unity.VisualScripting;

public class LeapMiningHandler : MonoBehaviour
{
    [Header("립모션 설정")]
    [Range(0.1f, 2f)]
    public float chiselReachDistance = 1f; // 끌 도달 거리

    [Range(0.3f, 1f)]
    public float hammerTriggerStrength = 0.5f; // 망치 발동 최소 힘

    [Range(0.5f, 3f)]
    public float miningCooldown = 1f; // 채굴 쿨타임

    [Header("채굴 설정")]
    public float miningRadius = 0.3f; // 채굴 반경
    public float gentleForce = 5f; // 부드러운 힘
    public int chunksPerClick = 2; // 클릭당 생성되는 조각 수
    public float miningForceIntensity = 20f; // 채굴 힘 강도
    public LayerMask chunkLayer = -1; // 채굴 대상 레이어

    [Header("시각화")]
    public GameObject chiselIndicator; // 끌 시각화 오브젝트
    public LineRenderer hammerTrajectory; // 망치 궤적 시각화
    public bool showDebugInfo = true;

    // 립모션 및 시스템 참조
    private Controller leapController;
    private GemProtectionSystem gemProtectionSystem;
    private ChunkCounter chunkCounter;
    private AudioSource audioSource;

    // 채굴 상태 변수
    private Vector3? currentChiselPosition = null;
    private float currentHammerForce = 0f;
    private float lastMiningTime = 0f;
    private bool isMining = false;

    [Header ("오디오 설정")]
    public AudioClip miningSound; // 채굴 사운드
    public AudioClip[] chunkFallSounds;

    private void Start()
    {
        InitializeLeapMining();
        SetupVisualIndicator();
    }

    void InitializeLeapMining()
    {
        // 립모션 컨트롤러 초기화
        leapController = new Controller();

        gemProtectionSystem = GetComponent<GemProtectionSystem>();
        chunkCounter = GetComponent<ChunkCounter>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 이벤트 구독
        if (chunkCounter != null)
        {
            chunkCounter.OnChunkCountChanged += OnChunkCountChanged;
        }
    }

    void SetupVisualIndicator()
    {
        // 끌 시각화 오브젝트 설정
        if (chiselIndicator == null)
        {
            chiselIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chiselIndicator.name = "ChiselIndicator";
            chiselIndicator.transform.localScale = new Vector3(0.02f, 0.05f, 0.02f);

            // 충돌 제거
            Destroy(chiselIndicator.GetComponent<Collider>());

            // 파란색으로 설정
            chiselIndicator.GetComponent<Renderer>().material.color = Color.blue;
            chiselIndicator.SetActive(false);
        }

        // 망치 궤적 시각화 설정
        if (hammerTrajectory == null)
        {
            GameObject lineObj = new GameObject("HammerTrajectory");
            lineObj.transform.SetParent(transform);
            hammerTrajectory = lineObj.AddComponent<LineRenderer>();

            hammerTrajectory.material = new Material(Shader.Find("Sprites/Default"));
            hammerTrajectory.startWidth = 0.01f;
            hammerTrajectory.endWidth = 0.01f;
            hammerTrajectory.positionCount = 2;
            hammerTrajectory.startColor = Color.yellow;
            hammerTrajectory.enabled = false;
        }
    }

    void Update()
    {
        UpdateHandTracking();
        ProcessMiningInput();
        UpdateVisualFeedback();
    }

    // 양손 추적 업데이트
    void UpdateHandTracking()
    {
        Frame frame = leapController.Frame();

        // 왼손 처리 (끌 위치)
        Hand leftHand = frame.Hands.Find(h => h.IsLeft);

        // 오른손 처리 (망치 힘)
        Hand rightHand = frame.Hands.Find(h => h.IsRight);

    }

    // 끌 위치 업데이트
    void UpdateChiselPosition(Hand leftHand)
    {
        if (leftHand == null)
        {
            currentChiselPosition = null;
            return;
        }

        // 립모션 좌표를 유니티 좌표로 변환
        Vector3 handPosition = new Vector3(
            leftHand.PalmPosition.x * 0.001f,
            leftHand.PalmPosition.y * 0.001f,
            leftHand.PalmPosition.z * 0.001f
        );

        // 월드 좌표로 변환
        Vector3 worldHandPos = transform.TransformPoint(handPosition);

        // 광물 블록 표면에서 가장 가까운 점 찾기
        Vector3 closestPoint = FindClosestPointOnRock(worldHandPos);

        // 도달 거리 체크
        float distance = Vector3.Distance(worldHandPos, closestPoint);
        if (distance <= chiselReachDistance)
        {
            currentChiselPosition = closestPoint;
        }
        else
        {
            currentChiselPosition = null;
        }
    }

    // 망치 힘 업데이트
    void UpdateHammerForce(Hand rightHand)
    {
        if (rightHand == null)
        {
            currentHammerForce = 0f;
            return;
        }

        float grabStrength = rightHand.GrabStrength;
        Vector3 velocity = rightHand.PalmVelocity;
        float handSpeed = velocity.magnitude * 0.001f;

        // 임시용 힘 계산
        currentHammerForce = grabStrength + Mathf.Clamp(handSpeed / 2f, 0f, 0.5f);
        currentHammerForce = Mathf.Clamp01(currentHammerForce);
    }

    // 채굴 입력 처리
    void ProcessMiningInput()
    {
        // 채굴조건 체크
        bool canMine = CanExecuteMining();

        if (canMine && ShouldTriggerMining())
        {
            ExecuteMining();
        }
    }

    bool CanExecuteMining()
    {
        // 1. 쿨타임 체크
        if (Time.time - lastMiningTime < miningCooldown)
        {
            return false;
        }

        // 2. 끌 위치 설정됨
        if (!currentChiselPosition.HasValue)
            return false;

        // 3. 망치 힘이 충분히 강함
        if (currentHammerForce < hammerTriggerStrength)
            return false;

        // 4. 채굴 중이 아님
        if (isMining)
            return false;

        return true;
    }

    // 망치 타격 트리거 조건
    bool ShouldTriggerMining()
    {
        // 망치 힘이 충분히 강할 경우
        return currentHammerForce >= hammerTriggerStrength;
    }

    // 채굴 실행
    void ExecuteMining()
    {
        if (!currentChiselPosition.HasValue) return;

        isMining = true;
        Vector3 miningPoint = currentChiselPosition.Value;
        Vector3 surfaceNormal = Vector3.up; // 임시로 위쪽을 표면으로 가정

        // 힘 강도에 따른 채굴 강도 조절
        float adjustedIntensity = miningForceIntensity * currentHammerForce;

        if (showDebugInfo)
        {
            Debug.Log($"립모션 채굴 실행: 위치({miningPoint}), 힘({currentHammerForce:F2}), 강도({adjustedIntensity:F1})");
        }

        PerformMining(miningPoint, surfaceNormal, adjustedIntensity);

        lastMiningTime = Time.time;
        isMining = false;
    }

    // 실제 채굴 수행
    void PerformMining(Vector3 miningPoint, Vector3 surfaceNormal, float intensity)
    {
        // 1. 보석 보호 시스템에 충격 전달
        if (gemProtectionSystem != null)
        {
            gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, intensity);
        }

        // 2. 채굴 효과 생성
        CreateMiningEffect(miningPoint, surfaceNormal);

        // 3. 채굴 사운드 재생
        PlayMiningSound();

        // 4. 조각 제거
        RemoveChunksAtPoint(miningPoint);
    }

    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 힘 강도에 따른 효과 스케일
        float effectScale = currentHammerForce;
        int dustCount = Mathf.RoundToInt(5 * (1f + effectScale));
        int chipCount = Mathf.RoundToInt(2 * (1f + effectScale));

        // 채굴 먼지
        for (int i = 0; i < dustCount; i++)
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

        // 돌 조각
        for (int i = 0; i< chipCount; i++)
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

    void RemoveChunksAtPoint(Vector3 miningPoint)
    {
        // 힘 강도에 따른 조각 수 조절
        int chunksToRemove = Mathf.RoundToInt(chunksPerClick * (0.5f + currentHammerForce));
        if (showDebugInfo)
        {
            Debug.Log($"조각 {chunksToRemove}개 제거 예정");
        }
    }

    /// <summary>
    /// 시각적 피드백 업데이트
    /// </summary>
    void UpdateVisualFeedback()
    {
        // 끌 위치 표시
        if (currentChiselPosition.HasValue)
        {
            chiselIndicator.SetActive(true);
            chiselIndicator.transform.position = currentChiselPosition.Value;

            // 힘 강도에 따른 색상 변경
            Color indicatorColor = Color.Lerp(Color.blue, Color.red, currentHammerForce);
            chiselIndicator.GetComponent<Renderer>().material.color = indicatorColor;
        }
        else
        {
            chiselIndicator.SetActive(false);
        }

        // 망치 궤적 표시 (오른손에서 끌 위치로)
        if (currentChiselPosition.HasValue && currentHammerForce > 0.1f)
        {
            // 간단히 끌 위치 위쪽에서 시작하는 라인
            hammerTrajectory.enabled = true;
            Vector3 hammerStart = currentChiselPosition.Value + Vector3.up * 0.2f;
            hammerTrajectory.SetPosition(0, hammerStart);
            hammerTrajectory.SetPosition(1, currentChiselPosition.Value);

            // 힘 강도에 따른 색상
            Color trajectoryColor = Color.Lerp(Color.yellow, Color.red, currentHammerForce);
            hammerTrajectory.startColor = trajectoryColor;
            hammerTrajectory.endColor = trajectoryColor;
        }
        else
        {
            hammerTrajectory.enabled = false;
        }
    }

    /// <summary>
    /// 광물 블록에서 가장 가까운 점 찾기 (Test.cs와 동일)
    /// </summary>
    Vector3 FindClosestPointOnRock(Vector3 handPosition)
    {
        Collider rockCollider = GetComponent<Collider>();
        if (rockCollider != null)
        {
            return rockCollider.ClosestPoint(handPosition);
        }
        return transform.position;
    }

    /// <summary>
    /// 채굴 사운드 재생 (Test.cs와 동일)
    /// </summary>
    void PlayMiningSound()
    {
        if (miningSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(miningSound);
        }
    }

    /// <summary>
    /// 조각 개수 변화 이벤트 핸들러
    /// </summary>
    void OnChunkCountChanged(int activeChunks, int destroyedChunks, float progress)
    {
        if (showDebugInfo)
        {
            Debug.Log($"립모션 채굴 진행: {destroyedChunks}개 파괴, {activeChunks}개 남음 ({progress * 100f:F1}%)");
        }

        // 보석 노출 체크
        if (progress >= 0.7f)
        {
            Debug.Log("?? 보석 노출! 조심스럽게 채굴하세요!");
        }

        // 완전 채굴 체크
        if (activeChunks <= 0)
        {
            Debug.Log("?? 립모션 채굴 완료!");
        }
    }

    /// <summary>
    /// 현재 상태 정보 반환
    /// </summary>
    public string GetStatusInfo()
    {
        string status = "";

        if (!currentChiselPosition.HasValue)
            status += "왼손으로 끌 위치를 지정하세요\n";
        else
            status += "끌 위치: 설정됨\n";

        status += $"망치 힘: {currentHammerForce * 100:F0}%\n";

        if (CanExecuteMining())
            status += "채굴 준비 완료!";
        else if (Time.time - lastMiningTime < miningCooldown)
            status += $"쿨다운: {(miningCooldown - (Time.time - lastMiningTime)):F1}초";
        else
            status += "준비 중...";

        return status;
    }

    /// <summary>
    /// 기즈모로 도달 범위 표시
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // 끌 도달 범위
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, chiselReachDistance);

        // 현재 끌 위치
        if (currentChiselPosition.HasValue)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(currentChiselPosition.Value, 0.05f);

            // 채굴 범위
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentChiselPosition.Value, miningRadius);
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
