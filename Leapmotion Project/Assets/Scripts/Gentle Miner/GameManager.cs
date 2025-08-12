using UnityEngine;
using System.Collections;

/// <summary>
/// 청크 기반 진행도 관리로 롤백된 GameManager
/// MineralBlock의 ChunkCounter 이벤트를 다시 받아서 처리
/// [수정] 정확도 이벤트 처리 및 시스템 연동
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [Header("게임 설정")]
    [SerializeField] public int totalStages = 3;
    public float successThreshold = 0.7f; // 70% 성공 기준
    public bool allowPartialSuccess = true;

    [Header("게임 시작 설정")]
    public bool showGemPreviewOnStart = true;
    public float gameStartDelay = 2f;

    // 현재 게임 상태
    public enum GameState
    {
        NotStarted,
        Initializing,
        Playing,
        Success,        // 70% 성공
        Perfect,        // 100% 완료
        Failed,         // 실패 (보석 파괴)
        Paused          // 일시정지 (일단 보류)
    }

    [Header("현재 상태")]
    [SerializeField] private GameState currentState = GameState.NotStarted;
    [SerializeField] private int currentStage = 1;
    [SerializeField] private float currentProgress = 0f;
    [SerializeField] private int currentScore = 0;

    // 게임 진행 상태 (기존 로직 복원)
    private bool gameStarted = false;
    private bool gameSucceeded = false;
    private bool gameCompleted = false;
    private bool gameInitialized = false;

    // 시스템 참조들
    private StageManager stageManager;
    private UIManager uiManager;
    private GemRevealSystem gemRevealSystem;
    private GemProtectionSystem gemProtectionSystem;
    private ScoreSystem scoreSystem; // [추가]
    private ToolSystem toolSystem; // [추가]

    // 이벤트들 (기존 복원)
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<int> OnStageChanged;
    public System.Action<float> OnProgressChanged;
    public System.Action<int> OnScoreChanged;

    // 프로퍼티들
    public GameState CurrentState => currentState;
    public int CurrentStage => currentStage;
    public float CurrentProgress => currentProgress;
    public int CurrentScore => currentScore;
    public bool IsGameActive => currentState == GameState.Playing;
    public bool IsGameStarted => gameStarted;

    void Start()
    {
        InitializeGameManager();
    }

    void InitializeGameManager()
    {
        Debug.Log("=== GameManager 초기화 시작 ===");

        FindSystemReferences();
        SubscribeToEvents();
        StartCoroutine(StartGameSequence());
    }

    void FindSystemReferences()
    {
        stageManager = FindFirstObjectByType<StageManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>(); // [추가]
        toolSystem = FindFirstObjectByType<ToolSystem>(); // [추가]

        if (stageManager == null)
        {
            Debug.LogError("StageManager가 필요합니다!");
        }

        if (uiManager == null)
        {
            Debug.LogWarning("UIManager가 없습니다. UI 기능이 제한됩니다.");
        }

        Debug.Log("시스템 참조 완료");
    }

    void SubscribeToEvents()
    {
        // MineralBlock에서 오는 진행률 이벤트 구독은 
        // MineralBlock이 생성될 때 동적으로 처리

        // [추가] ToolSystem의 정확도 이벤트 구독
        if (toolSystem != null)
        {
            toolSystem.OnStrikeAccuracyCalculated += OnStrikeAccuracyCalculated;
        }

        Debug.Log("이벤트 구독 완료");
    }

    // [추가] 정확도 이벤트 핸들러
    private void OnStrikeAccuracyCalculated(float accuracy)
    {
        if (scoreSystem != null)
        {
            scoreSystem.AddAccuracy(accuracy);
        }

        if (uiManager != null)
        {
            uiManager.UpdateLastAccuracyUI(accuracy);
        }
    }


    IEnumerator StartGameSequence()
    {
        ChangeGameState(GameState.Initializing);

        // 스테이지 초기화
        if (stageManager != null)
        {
            stageManager.InitializeStage(currentStage);
        }

        // [추가] 새 스테이지 시작 시 점수 시스템에 알림
        if (scoreSystem != null)
        {
            scoreSystem.StartNewStage();
        }

        // 현재 광물의 보석 보호 시스템 찾기
        yield return new WaitForSeconds(0.1f); // 광물 생성 대기
        FindCurrentGemProtectionSystem();

        // 보석 미리보기 (옵션)
        if (showGemPreviewOnStart && gemRevealSystem != null)
        {
            yield return new WaitForSeconds(gameStartDelay);
            gemRevealSystem.StartGemPreview();

            float previewDuration = (gemRevealSystem.cameraTransitionTime * 2) +
                                   gemRevealSystem.gemDisplayTime + 1f;
            yield return new WaitForSeconds(previewDuration);
        }

        // 게임 시작
        gameStarted = true;
        ChangeGameState(GameState.Playing);

        // 5초 후 초기화 완료 설정 (70% 성공 체크 버그 방지)
        yield return new WaitForSeconds(5f);
        gameInitialized = true;

        Debug.Log($"스테이지 {currentStage} 게임 시작!");
    }

    void FindCurrentGemProtectionSystem()
    {
        if (stageManager != null)
        {
            GameObject currentMineralBlock = stageManager.GetCurrentMineralBlock();
            if (currentMineralBlock != null)
            {
                gemProtectionSystem = currentMineralBlock.GetComponent<GemProtectionSystem>();
            }
        }
    }

    /// <summary>
    /// MineralBlock의 ChunkCounter에서 호출되는 진행률 업데이트 (기존 로직 복원)
    /// </summary>
    public void OnMineralProgressChanged(int activeChunks, int destroyedChunks, float progress)
    {
        // 게임이 시작되지 않았거나 초기화가 완료되지 않았으면 처리 안함
        if (!gameStarted || !gameInitialized)
        {
            Debug.Log($"게임 미시작 또는 초기화 미완료 - 진행률: {progress * 100f:F1}%");
            return;
        }

        // 이미 성공했으면 중복 처리 방지
        if (gameSucceeded || gameCompleted) return;

        currentProgress = progress;
        OnProgressChanged?.Invoke(progress);

        Debug.Log($"채굴 진행: {progress * 100f:F1}% ({activeChunks}개 남음)");

        // === 70% 성공 체크 ===
        if (!gameSucceeded && allowPartialSuccess &&
            progress >= successThreshold && progress < 1.0f)
        {
            OnMiningSuccess(progress);
        }
        // === 100% 완료 체크 ===
        else if (!gameCompleted && activeChunks <= 0 && progress >= 1.0f)
        {
            OnMiningComplete(progress);
        }
    }

    /// <summary>
    /// 70% 성공 처리 (기존 로직 복원)
    /// </summary>
    void OnMiningSuccess(float progress)
    {
        gameSucceeded = true;
        ChangeGameState(GameState.Success);

        Debug.Log($"채굴 성공! {progress * 100f:F1}% 달성 (목표: {successThreshold * 100f}%)");

        // 성공시 보석 점수 계산
        int finalGemScore = CalculateFinalGemScore();
        currentScore = finalGemScore;
        OnScoreChanged?.Invoke(currentScore);

        // UIManager에 성공 알림
        if (uiManager != null)
        {
            uiManager.ShowNextStageUI();
        }

        // 성공 연출 시작 (회전 + 보존)
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemSuccessReveal(finalGemScore);
        }
    }

    /// <summary>
    /// 100% 완료 처리 (기존 로직 복원)
    /// </summary>  
    void OnMiningComplete(float progress)
    {
        gameCompleted = true;
        ChangeGameState(GameState.Perfect);

        Debug.Log($"채굴 완료! {progress * 100f:F1}% 달성 - 완벽한 완주!");

        // [수정] ScoreSystem을 통해 점수 계산
        if (scoreSystem != null)
        {
            currentScore = scoreSystem.CalculateStageScore(currentStage, true);
        }
        else
        {
            currentScore = CalculateFinalGemScore() + 20;
        }
        OnScoreChanged?.Invoke(currentScore);

        // UIManager에 완료 알림
        if (uiManager != null)
        {
            uiManager.ShowGameCompleteUI(currentScore);
        }

        // 완벽 연출 시작 (특별 회전 + 보존)
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemPerfectReveal(currentScore);
        }
    }

    /// <summary>
    /// 시간 초과 처리 (UIManager에서 호출)
    /// </summary>
    public void OnTimeUp()
    {
        if (currentState != GameState.Playing) return;

        Debug.Log("시간 초과 발생!");

        // 현재 진행률 확인
        if (currentProgress >= successThreshold) // 70% 이상
        {
            Debug.Log("시간 초과지만 70% 이상 완료로 성공 처리");
            OnMiningSuccess(currentProgress);
        }
        else
        {
            Debug.Log($"시간 초과로 실패 - 진행률: {currentProgress * 100f:F1}%");
            ChangeGameState(GameState.Failed);

            // 실패 시 최종 점수는 현재까지의 진행률 기반으로 계산
            int timeoutScore = CalculateTimeoutScore();
            currentScore = timeoutScore;
            OnScoreChanged?.Invoke(currentScore);
        }
    }

    /// <summary>
    /// 시간 초과 시 점수 계산 (진행률 기반)
    /// </summary>
    int CalculateTimeoutScore()
    {
        if (gemProtectionSystem == null) return 0;

        // 기본 점수는 진행률에 비례
        int baseScore = Mathf.RoundToInt(currentProgress * 50f); // 최대 50점

        // 보석 상태도 고려
        var gems = gemProtectionSystem.GetAllGems();
        int gemBonus = 0;

        foreach (var gem in gems)
        {
            if (!gem.isDestroyed)
            {
                gemBonus += Mathf.RoundToInt(gem.currentCondition * 0.2f); // 보석 상태에 따른 보너스
            }
        }

        int totalScore = baseScore + gemBonus;
        Debug.Log($"시간 초과 점수: 기본 {baseScore} + 보석 보너스 {gemBonus} = {totalScore}");

        return totalScore;
    }

    /// <summary>
    /// 최종 보석 점수 계산 (기존 로직 복원)
    /// </summary>
    int CalculateFinalGemScore()
    {
        if (gemProtectionSystem == null) return 0;

        int totalScore = 0;
        int gemCount = 0;

        // 모든 보석의 점수 합산
        var allGems = gemProtectionSystem.GetAllGems();
        foreach (var gem in allGems)
        {
            int gemScore = CalculateIndividualGemScore(gem);
            totalScore += gemScore;
            gemCount++;

            Debug.Log($"보석 점수: {gemScore}점");
        }

        // 평균 점수 계산
        int averageScore = gemCount > 0 ? totalScore / gemCount : 0;

        Debug.Log($"최종 점수: {averageScore}점 (총 {gemCount}개 보석)");

        return averageScore;
    }

    /// <summary>
    /// 개별 보석 점수 계산 (안전한 방식)
    /// </summary>
    int CalculateIndividualGemScore(object gem)
    {
        try
        {
            // 리플렉션을 사용하여 보석 상태 확인
            var gemType = gem.GetType();

            var isDestroyedField = gemType.GetField("isDestroyed");
            bool isDestroyed = isDestroyedField != null && (bool)isDestroyedField.GetValue(gem);

            if (isDestroyed) return 0;

            var conditionField = gemType.GetField("currentCondition");
            float condition = conditionField != null ? (float)conditionField.GetValue(gem) : 100f;

            // 상태에 따른 점수 계산
            if (condition >= 90f) return 100; // 완벽한 보석
            if (condition >= 70f) return 70;  // 약간 손상
            if (condition >= 30f) return 30;  // 많이 손상
            return 10; // 거의 파괴 직전
        }
        catch
        {
            return 50; // 오류 시 기본 점수
        }
    }

    void ChangeGameState(GameState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"게임 상태 변경: {newState}");
        }
    }

    /// <summary>
    /// 다음 스테이지로 진행
    /// </summary>
    public void ProceedToNextStage()
    {
        if (currentStage >= totalStages)
        {
            Debug.Log("이미 마지막 스테이지입니다.");
            return;
        }

        currentStage++;
        OnStageChanged?.Invoke(currentStage);

        Debug.Log($"스테이지 {currentStage}로 진행");

        // 게임 재시작
        RestartCurrentStage();
    }

    /// <summary>
    /// 현재 스테이지 재시작
    /// </summary>
    public void RestartCurrentStage()
    {
        Debug.Log($"스테이지 {currentStage} 재시작");

        // 상태 초기화
        gameStarted = false;
        gameSucceeded = false;
        gameCompleted = false;
        gameInitialized = false;
        currentProgress = 0f;

        ChangeGameState(GameState.NotStarted);

        // 스테이지 재초기화
        if (stageManager != null)
        {
            stageManager.RestartStage(currentStage);
        }

        // 게임 재시작
        StartCoroutine(StartGameSequence());
    }

    void Update()
    {
        // 디버그 키 입력
        HandleDebugInput();

        // 게임 중일 때만 보석 상태 체크
        if (currentState == GameState.Playing)
        {
            CheckGemDestructionStatus();
        }
    }

    /// <summary>
    /// 보석 파괴 상태를 주기적으로 체크
    /// </summary>
    void CheckGemDestructionStatus()
    {
        if (gemProtectionSystem != null && gemProtectionSystem.HasAnyGemDestroyed())
        {
            // 보석이 파괴되었으면 게임 실패 처리
            if (currentState == GameState.Playing)
            {
                Debug.Log("보석 파괴 감지 - 게임 실패 처리");
                ChangeGameState(GameState.Failed);
            }
        }
    }

    void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (currentState == GameState.Success || currentState == GameState.Perfect)
            {
                ProceedToNextStage();
            }
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            RestartCurrentStage();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            // 일시정지/재개
            if (currentState == GameState.Playing)
            {
                ChangeGameState(GameState.Paused);
            }
            else if (currentState == GameState.Paused)
            {
                ChangeGameState(GameState.Playing);
            }
        }

        // 강제 테스트 키들
        if (Input.GetKeyDown(KeyCode.Alpha7)) // 7키: 70% 성공 강제 테스트
        {
            if (gameInitialized && !gameSucceeded && !gameCompleted)
            {
                Debug.Log("70% 성공 강제 테스트 실행");
                OnMiningSuccess(successThreshold);
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha0)) // 0키: 100% 완료 강제 테스트
        {
            if (gameInitialized && !gameCompleted)
            {
                Debug.Log("100% 완료 강제 테스트 실행");
                OnMiningComplete(1.0f);
            }
        }

        if (Input.GetKeyDown(KeyCode.F)) // F키: 실패 강제 테스트
        {
            if (gameInitialized && !gameSucceeded && !gameCompleted)
            {
                Debug.Log("게임 실패 강제 테스트 실행");
                gemRevealSystem.StartGemDestruction();
                ChangeGameState(GameState.Failed);
                currentScore = CalculateTimeoutScore();
                OnScoreChanged?.Invoke(currentScore);
            }
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제는 MineralBlock에서 처리
        // [추가] ToolSystem 이벤트 구독 해제
        if (toolSystem != null)
        {
            toolSystem.OnStrikeAccuracyCalculated -= OnStrikeAccuracyCalculated;
        }
    }
}