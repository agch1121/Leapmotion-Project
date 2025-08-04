using UnityEngine;
using System.Collections;

/// <summary>
/// 전체 게임 흐름 및 상태 관리 (기획서의 핵심 클래스)
/// 스테이지 진행, 게임 상태, 승리 조건 등을 총괄 관리
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("게임 설정")]
    [SerializeField] public int totalStages = 3; // 총 스테이지 수 (UI에서 접근용)
    public float successThreshold = 0.7f; // 70% 성공 기준
    public bool allowPartialSuccess = true; // 70% 성공 허용 여부

    [Header("게임 시작 설정")]
    public bool showGemPreviewOnStart = true;
    public float gameStartDelay = 2f;

    // 현재 게임 상태
    public enum GameState
    {
        NotStarted,     // 게임 시작 전
        Initializing,   // 초기화 중
        Playing,        // 플레이 중
        Success,        // 70% 성공
        Perfect,        // 100% 완료
        Failed,         // 실패 (보석 파괴)
        Paused          // 일시정지
    }

    [Header("현재 상태")]
    [SerializeField] private GameState currentState = GameState.NotStarted;
    [SerializeField] private int currentStage = 1;
    [SerializeField] private float currentProgress = 0f;
    [SerializeField] private int currentScore = 0;

    // 시스템 참조들
    private StageManager stageManager;
    private UIManager uiManager;
    private ScoreSystem scoreSystem;
    private GemRevealSystem gemRevealSystem;
    private ChunkCounter chunkCounter;
    private GemProtectionSystem gemProtectionSystem;

    // 이벤트들
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

    void Start()
    {
        InitializeGameManager();
    }

    void InitializeGameManager()
    {
        Debug.Log("=== GameManager 초기화 시작 ===");

        // 시스템들 찾기
        FindSystemReferences();

        // 이벤트 구독
        SubscribeToEvents();

        // 게임 시작
        StartCoroutine(StartGameSequence());
    }

    void FindSystemReferences()
    {
        // 핵심 시스템들 찾기
        stageManager = FindFirstObjectByType<StageManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();
        gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
        chunkCounter = FindFirstObjectByType<ChunkCounter>();
        gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();

        // 필수 시스템 체크
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
        // ChunkCounter 이벤트 구독
        if (chunkCounter != null)
        {
            chunkCounter.OnChunkCountChanged += OnChunkCountChanged;
        }

        // 보석 파괴는 폴링 방식으로 체크 (Update에서 처리)
        Debug.Log("이벤트 구독 완료");
    }

    IEnumerator StartGameSequence()
    {
        ChangeGameState(GameState.Initializing);

        // 스테이지 초기화
        if (stageManager != null)
        {
            stageManager.InitializeStage(currentStage);
        }

        // 보석 미리보기 (옵션)
        if (showGemPreviewOnStart && gemRevealSystem != null)
        {
            yield return new WaitForSeconds(gameStartDelay);
            gemRevealSystem.StartGemPreview();

            // 미리보기 시간 대기
            float previewDuration = (gemRevealSystem.cameraTransitionTime * 2) +
                                   gemRevealSystem.gemDisplayTime + 1f;
            yield return new WaitForSeconds(previewDuration);
        }

        // 게임 시작
        ChangeGameState(GameState.Playing);
        Debug.Log($"스테이지 {currentStage} 게임 시작!");
    }

    /// <summary>
    /// 게임 상태 변경
    /// </summary>
    public void ChangeGameState(GameState newState)
    {
        if (currentState == newState) return;

        GameState previousState = currentState;
        currentState = newState;

        Debug.Log($"게임 상태 변경: {previousState} → {newState}");

        // 상태별 처리
        HandleStateChange(previousState, newState);

        // 이벤트 발생
        OnGameStateChanged?.Invoke(newState);

        // UI 업데이트
        if (uiManager != null)
        {
            uiManager.OnGameStateChanged(newState);
        }
    }

    void HandleStateChange(GameState from, GameState to)
    {
        switch (to)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;

            case GameState.Success:
                HandleGameSuccess();
                break;

            case GameState.Perfect:
                HandleGamePerfect();
                break;

            case GameState.Failed:
                HandleGameFailure();
                break;

            case GameState.Paused:
                Time.timeScale = 0f;
                break;
        }
    }

    /// <summary>
    /// ChunkCounter에서 오는 진행률 업데이트
    /// </summary>
    void OnChunkCountChanged(int activeChunks, int destroyedChunks, float progress)
    {
        // 게임이 진행 중이 아니면 무시
        if (currentState != GameState.Playing) return;

        UpdateProgress(progress);

        // 승리 조건 체크
        CheckWinConditions(progress, activeChunks);
    }

    void UpdateProgress(float newProgress)
    {
        currentProgress = newProgress;
        OnProgressChanged?.Invoke(currentProgress);

        Debug.Log($"채굴 진행률: {currentProgress * 100f:F1}%");
    }

    void CheckWinConditions(float progress, int activeChunks)
    {
        // 이미 성공/완료/실패 상태면 체크 안함
        if (currentState == GameState.Success ||
            currentState == GameState.Perfect ||
            currentState == GameState.Failed)
        {
            return;
        }

        // 보석 파괴 체크는 CheckGemDestructionStatus에서 별도 처리

        // 70% 성공 체크
        if (allowPartialSuccess && progress >= successThreshold && progress < 1.0f)
        {
            ChangeGameState(GameState.Success);
        }
        // 100% 완료 체크
        else if (activeChunks <= 0 && progress >= 1.0f)
        {
            ChangeGameState(GameState.Perfect);
        }
    }

    void HandleGameSuccess()
    {
        Debug.Log($"스테이지 {currentStage} - 70% 성공!");

        // 점수 계산
        int stageScore = CalculateStageScore(false);
        AddScore(stageScore);

        // 성공 연출
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemSuccessReveal(stageScore);
        }

        // 잠시 후 다음 단계 옵션 표시
        StartCoroutine(ShowNextStageOptions(3f));
    }

    void HandleGamePerfect()
    {
        Debug.Log($"스테이지 {currentStage} - 완벽한 채굴!");

        // 점수 계산 (보너스 포함)
        int stageScore = CalculateStageScore(true);
        AddScore(stageScore);

        // 완벽 연출
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemPerfectReveal(stageScore);
        }

        // 잠시 후 다음 단계 옵션 표시
        StartCoroutine(ShowNextStageOptions(4f));
    }

    void HandleGameFailure()
    {
        Debug.Log($"스테이지 {currentStage} - 게임 실패 (보석 파괴)");

        // 실패 연출
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemDestruction();
        }

        // 재시작 옵션 표시
        StartCoroutine(ShowRestartOptions(2f));
    }

    int CalculateStageScore(bool isPerfect)
    {
        int baseScore = 100;

        // 보석 상태에 따른 점수
        if (gemProtectionSystem != null)
        {
            var gems = gemProtectionSystem.GetAllGems();
            int gemScore = 0;

            foreach (var gem in gems)
            {
                gemScore += gemProtectionSystem.CalculateGemScore(gem);
            }

            baseScore = gemScore / gems.Length; // 평균 점수
        }

        // 완벽 완주시 보너스
        if (isPerfect)
        {
            baseScore += 50;
        }

        // 스테이지별 배율 적용
        return baseScore * currentStage;
    }

    void AddScore(int points)
    {
        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);

        if (scoreSystem != null)
        {
            scoreSystem.AddScore(points);
        }

        Debug.Log($"점수 획득: +{points} (총점: {currentScore})");
    }

    IEnumerator ShowNextStageOptions(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (currentStage < totalStages)
        {
            // 다음 스테이지 진행 가능
            if (uiManager != null)
            {
                uiManager.ShowNextStageUI();
            }

            Debug.Log("N키: 다음 스테이지, R키: 현재 스테이지 재시작");
        }
        else
        {
            // 모든 스테이지 완료
            HandleGameComplete();
        }
    }

    IEnumerator ShowRestartOptions(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (uiManager != null)
        {
            uiManager.ShowRestartUI();
        }

        Debug.Log("R키: 현재 스테이지 재시작, Q키: 게임 종료");
    }

    void HandleGameComplete()
    {
        Debug.Log("모든 스테이지 완료! 게임 클리어!");

        if (uiManager != null)
        {
            uiManager.ShowGameCompleteUI(currentScore);
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
    /// 보석 파괴 상태를 주기적으로 체크 (폴링 방식)
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

        if (Input.GetKeyDown(KeyCode.R))
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

        // 디버그 정보 출력
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PrintGameStatus();
        }
    }

    [ContextMenu("게임 상태 출력")]
    public void PrintGameStatus()
    {
        Debug.Log("=== GameManager 상태 ===");
        Debug.Log($"현재 상태: {currentState}");
        Debug.Log($"현재 스테이지: {currentStage}/{totalStages}");
        Debug.Log($"진행률: {currentProgress * 100f:F1}%");
        Debug.Log($"현재 점수: {currentScore}");
        Debug.Log($"성공 임계값: {successThreshold * 100f}%");
        Debug.Log("========================");
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