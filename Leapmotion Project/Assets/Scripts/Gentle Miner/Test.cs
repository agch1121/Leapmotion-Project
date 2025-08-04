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

    [Header("성공/실패 처리")]
    public bool allow70PercentSuccess = true; // 70% 성공 허용 여부
    [Range(0.7f, 1.0f)]
    public float successThreshold = 0.7f; // 70% 성공 기준

    // 시스템 참조들
    private ChunkGraphManager chunkGraphManager;
    private AudioSource audioSource;
    private ChunkNode[] allChunks; // 모든 조각 캐시
    private GemProtectionSystem gemProtectionSystem; // 보석 보호 시스템 참조
    private ChunkCounter chunkCounter; // 조각 카운터 시스템 참조

    // 게임 상태
    private bool gameStarted = false;
    private bool gameSucceeded = false; // 70% 성공 달성
    private bool gameCompleted = false; // 100% 완료 달성
    private bool gameInitialized = false; // 게임 초기화 완료 플래그 (버그 방지용)

    /// <summary>
    /// 게임이 시작되었는지 확인하는 프로퍼티 (다른 스크립트에서 참조용)
    /// </summary>
    public bool IsGameStarted => gameStarted;

    void Start()
    {
        Debug.Log("=== Test.cs 초기화 시작 ===");

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

        // 조각 카운터 시스템 찾기 (기존 방식 그대로)
        chunkCounter = GetComponent<ChunkCounter>();
        if (chunkCounter == null)
        {
            chunkCounter = gameObject.AddComponent<ChunkCounter>();
        }

        // 기존 이벤트만 구독 (새로운 이벤트는 구독하지 않음)
        chunkCounter.OnChunkCountChanged += OnChunkCountChanged;

        // 오디오 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // GemRevealSystem 참조
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
            gameStarted = true;
        }

        // 게임 초기화 완료를 5초 후로 설정 (70% 성공 체크 버그 방지)
        Invoke(nameof(SetGameInitialized), 5f);

        Debug.Log("=== Test.cs 초기화 완료 - 5초 후 70% 성공 체크 활성화 ===");
    }

    /// <summary>
    /// 게임 초기화 완료 설정 (버그 방지용)
    /// </summary>
    void SetGameInitialized()
    {
        gameInitialized = true;
        Debug.Log("게임 초기화 완료 - 70% 성공 체크 활성화됨");
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

        // === 기존 테스트 키들 ===
        if (Input.GetKeyDown(KeyCode.R))
        {
            RefreshChunkCache();
            Debug.Log("조각 캐시 수동 갱신");
        }

        if (Input.GetKeyDown(KeyCode.G) && gemProtectionSystem != null)
        {
            gemProtectionSystem.PrintGemStatus();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            CleanupAllFallenChunks();
        }

        if (Input.GetKeyDown(KeyCode.P) && gemRevealSystem != null)
        {
            gemRevealSystem.StartGemPreview();
        }

        // === 새로운 보석 연출 테스트 키들 ===
        if (Input.GetKeyDown(KeyCode.U) && gemRevealSystem != null) // U키: 성공 연출 테스트
        {
            int testScore = CalculateFinalGemScore();
            gemRevealSystem.StartGemSuccessReveal(testScore);
            Debug.Log("성공 연출 테스트 (회전 + 보존)");
        }

        if (Input.GetKeyDown(KeyCode.I) && gemRevealSystem != null) // I키: 완벽 연출 테스트
        {
            int testScore = CalculateFinalGemScore() + 20;
            gemRevealSystem.StartGemPerfectReveal(testScore);
            Debug.Log("완벽 연출 테스트 (특별 회전 + 보존)");
        }

        if (Input.GetKeyDown(KeyCode.O) && gemRevealSystem != null) // O키: 파괴 연출 테스트
        {
            int testScore = CalculateFinalGemScore();
            gemRevealSystem.StartGemReveal(testScore);
            Debug.Log("파괴 연출 테스트 (기존)");
        }

        // === 새로운 70% 성공 테스트 키들 ===
        if (Input.GetKeyDown(KeyCode.Alpha7)) // 7키: 70% 성공 강제 테스트
        {
            if (gameInitialized && !gameSucceeded && !gameCompleted)
            {
                Debug.Log("70% 성공 강제 테스트 실행");
                OnMiningSuccess(successThreshold);
            }
            else
            {
                Debug.Log("테스트 불가: 초기화 미완료 또는 이미 성공/완료함");
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha0)) // 0키: 100% 완료 강제 테스트
        {
            if (gameInitialized && !gameCompleted)
            {
                Debug.Log("100% 완료 강제 테스트 실행");
                OnMiningComplete(1.0f);
            }
            else
            {
                Debug.Log("테스트 불가: 초기화 미완료 또는 이미 완료함");
            }
        }

        if (Input.GetKeyDown(KeyCode.N)) // N키: 다음 단계
        {
            if (gameSucceeded || gameCompleted)
            {
                ProceedToNextStage();
            }
            else
            {
                Debug.Log($"아직 성공하지 못했습니다. {successThreshold * 100f}% 이상 채굴하세요!");
            }
        }

        if (Input.GetKeyDown(KeyCode.T)) // T키: 재시작
        {
            RestartCurrentStage();
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

    /// <summary>
    /// ChunkCounter 이벤트 처리 - 70% 성공 로직 추가
    /// </summary>
    void OnChunkCountChanged(int activeChunks, int destroyedChunks, float progress)
    {
        // 게임이 시작되지 않았거나 초기화가 완료되지 않았으면 처리 안함
        if (!gameStarted || !gameInitialized)
        {
            Debug.Log($"게임 미시작 또는 초기화 미완료 - 진행률: {progress * 100f:F1}%");
            return;
        }

        // 이미 성공했으면 중복 처리 방지
        if (gameSucceeded || gameCompleted) return;

        Debug.Log($"채굴 진행: {progress * 100f:F1}% ({activeChunks}개 남음)");

        // === 70% 성공 체크 (Test.cs에서 직접 처리) ===
        if (!gameSucceeded && allow70PercentSuccess &&
            progress >= successThreshold && progress < 1.0f)
        {
            OnMiningSuccess(progress);
        }

        // === 100% 완료 체크 (기존 로직 유지하되 조건 강화) ===
        else if (!gameCompleted && activeChunks <= 0 && progress >= 1.0f)
        {
            OnMiningComplete(progress);
        }
    }

    /// <summary>
    /// 70% 성공 처리
    /// </summary>
    void OnMiningSuccess(float progress)
    {
        gameSucceeded = true;
        Debug.Log($"🎉 채굴 성공! {progress * 100f:F1}% 달성 (목표: {successThreshold * 100f}%)");

        // UIManager에 성공 알림
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ForceGameEnd("채굴 성공!");
        }

        // 성공시 보석 점수 계산
        int finalGemScore = CalculateFinalGemScore();

        // 🎯 성공 연출 시작 (회전 + 보존)
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemSuccessReveal(finalGemScore);
        }

        ShowSuccessUI();
    }

    /// <summary>
    /// 100% 완료 처리
    /// </summary>  
    void OnMiningComplete(float progress)
    {
        gameCompleted = true;
        Debug.Log($"🏆 채굴 완료! {progress * 100f:F1}% 달성 - 완벽한 완주!");

        // UIManager에 완료 알림
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ForceGameEnd("완벽한 채굴!");
        }

        // 최종 보석 점수 계산 + 보너스
        int finalGemScore = CalculateFinalGemScore() + 20; // 완벽 완주 보너스

        // 🏆 완벽 연출 시작 (특별 회전 + 보존)
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemPerfectReveal(finalGemScore);
        }

        ShowCompleteUI();
    }

    /// <summary>
    /// 성공 UI 표시
    /// </summary>
    void ShowSuccessUI()
    {
        Debug.Log("=== 채굴 성공! ===");
        Debug.Log($"{successThreshold * 100f}% 이상 채굴 완료!");
        Debug.Log("N키: 다음 단계로 진행");
        Debug.Log("T키: 현재 단계 재시작");
        Debug.Log("==================");

        // TODO: 실제 UI 패널 구현
        // - 성공 축하 메시지 ("채굴 성공!")
        // - 달성 진행률 표시 (예: "75% 달성")
        // - 획득 점수 표시
        // - "다음 단계로" 버튼 (ProceedToNextStage 호출)
        // - "재시작" 버튼 (RestartCurrentStage 호출)
    }

    /// <summary>
    /// 완료 UI 표시
    /// </summary>
    void ShowCompleteUI()
    {
        Debug.Log("=== 완벽한 채굴! ===");
        Debug.Log("100% 완료로 보너스 획득!");
        Debug.Log("N키: 다음 단계로 진행 (보너스 적용)");
        Debug.Log("T키: 현재 단계 재시작");
        Debug.Log("====================");

        // TODO: 실제 UI 패널 구현
        // - 완벽 달성 축하 메시지 ("완벽한 채굴!")
        // - 보너스 점수 강조 표시 ("+20 보너스!")
        // - "다음 단계로" 버튼 (골드/특별 스타일)
        // - "재시작" 버튼
    }

    int CalculateFinalGemScore()
    {
        if (gemProtectionSystem == null) return 0;

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
    /// 게임 재시작
    /// </summary>
    public void RestartCurrentStage()
    {
        Debug.Log("=== 현재 단계 재시작 ===");

        // 상태 초기화
        gameStarted = false;
        gameSucceeded = false;
        gameCompleted = false;
        gameInitialized = false; // 초기화 플래그도 리셋

        // UIManager 재시작
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.RestartGame();
        }

        // 기존 재시작 로직
        RefreshChunkCache();

        // 게임 재시작 (기존 로직)
        if (showGemPreviewOnStart && gemRevealSystem != null)
        {
            Invoke(nameof(StartGemPreview), gameStartDelay);
        }
        else
        {
            gameStarted = true;
        }

        // 5초 후 다시 초기화 완료로 설정
        Invoke(nameof(SetGameInitialized), 5f);

        Debug.Log("현재 단계 재시작 완료 - 5초 후 70% 체크 활성화");
    }

    /// <summary>
    /// 다음 단계로 진행
    /// </summary>
    public void ProceedToNextStage()
    {
        Debug.Log("=== 다음 단계로 진행 ===");
        Debug.Log("새로운 광물 생성 예정...");

        // TODO: 실제 구현 필요
        // - 새로운 광물 프리팹 생성
        // - 난이도 증가 (보석 개수, 보호막 강도 등)
        // - 스테이지 번호 증가
        // - UI 업데이트

        // 임시로 현재 단계 재시작
        RestartCurrentStage();
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

        // 1. ChunkNode 컴포넌트가 있는 것들만 정리
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

        // 2. 특정 태그가 있는 것들만 정리 (안전함)
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("StoneChunk");
        foreach (GameObject obj in taggedObjects)
        {
            if (obj != null && !obj.transform.IsChildOf(transform))
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

    // === 디버그 및 테스트 메서드들 ===

    /// <summary>
    /// 현재 게임 상태와 UI 상태 종합 정보
    /// </summary>
    [ContextMenu("게임 상태 확인")]
    public void PrintGameStatus()
    {
        Debug.Log("=== 전체 게임 상태 ===");
        Debug.Log($"게임 시작: {gameStarted}");
        Debug.Log($"게임 초기화 완료: {gameInitialized}");
        Debug.Log($"70% 성공: {gameSucceeded}");
        Debug.Log($"100% 완료: {gameCompleted}");
        Debug.Log($"성공 임계값: {successThreshold * 100f}%");

        // UIManager 상태 확인
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            Debug.Log($"UI 게임 종료: {uiManager.IsGameEnded()}");
        }

        Debug.Log("=======================");
    }

    /// <summary>
    /// 70% 성공 허용 토글
    /// </summary>
    [ContextMenu("70% 성공 허용 토글")]
    public void Toggle70PercentSuccess()
    {
        allow70PercentSuccess = !allow70PercentSuccess;
        Debug.Log($"70% 성공 허용: {allow70PercentSuccess}");
    }

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
            chunk.gameObject.activeInHierarchy
            );
        Debug.Log($"활성 조각 수: {activeChunks.Length} / {allChunks.Length}");
    }

    /// <summary>
    /// 게임 상태 반환 (외부 참조용)
    /// </summary>
    public (bool started, bool succeeded, bool completed, bool initialized) GetGameState()
    {
        return (gameStarted, gameSucceeded, gameCompleted, gameInitialized);
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