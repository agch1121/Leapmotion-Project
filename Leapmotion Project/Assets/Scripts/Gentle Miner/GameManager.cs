using UnityEngine;
using System.Collections;

/// <summary>
/// GameManager: 게임의 전체적인 흐름, 상태, 스테이지 전환을 관리하는 중앙 컨트롤 타워
/// [수정] UIManager 참조 전달 및 상태 변경 로직 간소화
/// </summary>
public class GameManager : MonoBehaviour
{
    // 싱글턴 인스턴스: 다른 스크립트에서 GameManager.Instance로 쉽게 접근 가능
    public static GameManager Instance { get; private set; }

    private void Awake() { Instance = this; }

    [Header("게임 설정")]
    [SerializeField] public int totalStages = 3; // 전체 스테이지 수
    public float successThreshold = 0.7f; // 이 진행률(70%)을 넘으면 '성공'으로 간주
    public bool allowPartialSuccess = true; // 부분 성공(70% 채굴)을 허용할지 여부

    [Header("게임 시작 설정")]
    public bool showGemPreviewOnStart = true; // 게임 시작 시 보석 미리보기 연출을 보여줄지 여부
    public float gameStartDelay = 2f; // 게임 시작 전 딜레이

    // 게임의 현재 상태를 나타내는 열거형
    public enum GameState { NotStarted, Initializing, Playing, Success, Perfect, Failed, Paused }

    [Header("현재 상태")]
    [SerializeField] private GameState currentState = GameState.NotStarted; // 현재 게임 상태
    [SerializeField] private int currentStage = 1; // 현재 진행 중인 스테이지 번호
    [SerializeField] private float currentProgress = 0f; // 현재 채굴 진행률
    [SerializeField] private int currentScore = 0; // 현재 점수

    // 게임 상태를 나타내는 내부 플래그 변수들
    private bool gameStarted = false; // 게임이 실제로 시작되었는지 (미리보기 연출 후)
    private bool gameSucceeded = false; // 70% 채굴에 성공했는지
    private bool gameCompleted = false; // 100% 채굴에 성공했는지
    private bool gameInitialized = false; // 초기화가 완료되었는지

    // 다른 주요 시스템들에 대한 참조
    private StageManager stageManager;
    private UIManager uiManager;
    private GemRevealSystem gemRevealSystem;
    private GemProtectionSystem gemProtectionSystem;
    private ScoreSystem scoreSystem;
    private ToolSystem toolSystem;

    // 외부에 상태 변경을 알리기 위한 이벤트
    public event System.Action<GameState> OnGameStateChanged;
    public event System.Action<int> OnStageChanged;
    public event System.Action<float> OnProgressChanged;
    public event System.Action<int> OnScoreChanged;

    // 외부에서 현재 상태를 읽기 위한 프로퍼티
    public GameState CurrentState => currentState;
    public int CurrentStage => currentStage;
    public float CurrentProgress => currentProgress;
    public int CurrentScore => currentScore;
    public bool IsGameActive => currentState == GameState.Playing;
    public bool IsGameStarted => gameStarted;
    public bool IsGameSucceeded => gameSucceeded;
    public bool IsGameCompleted => gameCompleted;

    // ScoreSystem 참조를 외부에 제공하는 함수
    public ScoreSystem GetScoreSystem() { return scoreSystem; }

    void Start() { InitializeGameManager(); }

    /// <summary>
    /// 게임 매니저를 초기화하고, 필요한 시스템 참조를 찾고, 이벤트를 구독합니다.
    /// </summary>
    void InitializeGameManager()
    {
        FindSystemReferences();
        SubscribeToEvents();
        StartCoroutine(StartGameSequence()); // 게임 시작 시퀀스 코루틴 실행
    }

    /// <summary>
    /// 씬에 있는 다른 주요 매니저 스크립트들을 찾아서 참조를 할당합니다.
    /// </summary>
    void FindSystemReferences()
    {
        stageManager = FindFirstObjectByType<StageManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();
        toolSystem = FindFirstObjectByType<ToolSystem>();
    }

    /// <summary>
    /// 다른 시스템에서 발생하는 이벤트를 구독하여 특정 동작을 수행하도록 연결합니다.
    /// </summary>
    void SubscribeToEvents()
    {
        if (toolSystem != null)
        {
            // ToolSystem에서 정확도 계산이 완료될 때마다 OnStrikeAccuracyCalculated 함수를 호출하도록 연결
            toolSystem.OnStrikeAccuracyCalculated += OnStrikeAccuracyCalculated;
        }
    }

    /// <summary>
    /// ToolSystem에서 정확도 이벤트가 발생했을 때 호출되는 함수
    /// </summary>
    /// <param name="accuracy">계산된 정확도 값 (0.0 ~ 1.0)</param>
    private void OnStrikeAccuracyCalculated(float accuracy)
    {
        if (scoreSystem != null) scoreSystem.AddAccuracy(accuracy); // ScoreSystem에 정확도 값을 추가
        if (uiManager != null) uiManager.UpdateLastAccuracyUI(accuracy); // UIManager에 마지막 정확도 UI 업데이트 요청
    }

    /// <summary>
    /// 게임 시작 시의 전체적인 흐름을 관리하는 코루틴 (스테이지 초기화, 미리보기 등)
    /// </summary>
    IEnumerator StartGameSequence()
    {
        ChangeGameState(GameState.Initializing); // 상태를 '초기화 중'으로 변경
        if (stageManager != null) stageManager.InitializeStage(currentStage); // StageManager에게 현재 스테이지 초기화 요청
        if (scoreSystem != null) scoreSystem.StartNewStage(); // ScoreSystem에게 새 스테이지 시작을 알림
        if (uiManager != null) uiManager.ResetGameplayUI(); // UIManager에게 게임 플레이 UI 리셋 요청
        yield return new WaitForSeconds(0.2f); // 잠시 대기
        FindCurrentGemProtectionSystem(); // 현재 스테이지의 GemProtectionSystem을 찾아 이벤트를 연결

        // 보석 미리보기 옵션이 켜져 있으면 연출을 실행
        if (showGemPreviewOnStart && gemRevealSystem != null)
        {
            yield return new WaitForSeconds(gameStartDelay);
            gemRevealSystem.StartGemPreview();
            // 미리보기 연출이 끝날 때까지 대기
            float previewDuration = (gemRevealSystem.cameraTransitionTime * 2) + gemRevealSystem.gemDisplayTime + 1f;
            yield return new WaitForSeconds(previewDuration);
        }

        gameStarted = true; // 실제 게임 시작 플래그를 true로 설정
        ChangeGameState(GameState.Playing); // 상태를 '플레이 중'으로 변경
        yield return new WaitForSeconds(5f); // 초기 불안정한 값들을 무시하기 위해 잠시 대기
        gameInitialized = true; // 초기화 완료 플래그를 true로 설정
    }

    /// <summary>
    /// 현재 스테이지의 GemProtectionSystem을 찾아 보석 상태 변경 이벤트를 구독합니다.
    /// </summary>
    void FindCurrentGemProtectionSystem()
    {
        if (stageManager == null) return;
        GameObject mineralBlock = stageManager.GetCurrentMineralBlock();
        if (mineralBlock == null) return;

        // 이전에 연결된 이벤트가 있다면 해제
        if (gemProtectionSystem != null) { gemProtectionSystem.OnGemConditionChanged -= OnGemConditionChanged; }
        gemProtectionSystem = mineralBlock.GetComponent<GemProtectionSystem>();
        if (gemProtectionSystem != null)
        {
            // 새로 찾은 GemProtectionSystem의 이벤트에 OnGemConditionChanged 함수를 연결
            gemProtectionSystem.OnGemConditionChanged += OnGemConditionChanged;
            // UI에 현재 보석 상태를 업데이트하도록 요청
            if (uiManager != null) { uiManager.UpdateAllGemStatusText(gemProtectionSystem.GetAllGems()); }
        }
    }

    /// <summary>
    /// 보석의 상태(체력)가 변경될 때마다 호출되는 함수
    /// </summary>
    /// <param name="gemData">상태가 변경된 보석의 데이터</param>
    private void OnGemConditionChanged(GemProtectionSystem.GemData gemData)
    {
        if (uiManager != null && gemProtectionSystem != null)
        {
            // UIManager에게 보석 상태 UI 업데이트를 요청
            uiManager.UpdateAllGemStatusText(gemProtectionSystem.GetAllGems());
        }
    }

    /// <summary>
    /// ChunkCounter에서 채굴 진행률이 변경될 때마다 호출되는 함수
    /// </summary>
    public void OnMineralProgressChanged(int activeChunks, int destroyedChunks, float progress)
    {
        if (!gameStarted || !gameInitialized || gameSucceeded || gameCompleted) return;
        currentProgress = progress;
        OnProgressChanged?.Invoke(progress); // 진행률 변경 이벤트를 외부에 알림

        // 70%를 넘겼고, 아직 성공 처리가 안됐으며, 부분 성공이 허용된 경우 -> '성공' 처리
        if (!gameSucceeded && allowPartialSuccess && progress >= successThreshold) { OnMiningSuccess(); }
        // 모든 조각을 다 캤고, 아직 완료 처리가 안된 경우 -> '완벽' 처리
        else if (!gameCompleted && activeChunks <= 0) { OnMiningComplete(); }
    }

    /// <summary>
    /// 70% 채굴 성공 시 호출되는 함수
    /// </summary>
    void OnMiningSuccess()
    {
        gameSucceeded = true;
        if (scoreSystem != null)
        {
            // 점수 계산 (isPerfectStage = false)
            currentScore = scoreSystem.CalculateStageScore(currentStage, false);
            OnScoreChanged?.Invoke(currentScore);
        }
        ChangeGameState(GameState.Success);
        // GemRevealSystem에 성공 연출 시작을 요청
        if (gemRevealSystem != null) gemRevealSystem.StartGemSuccessReveal(currentScore);
    }

    /// <summary>
    /// 100% 채굴 완료 시 호출되는 함수
    /// </summary>
    void OnMiningComplete()
    {
        gameCompleted = true;
        if (scoreSystem != null)
        {
            // 점수 계산 (isPerfectStage = true)
            currentScore = scoreSystem.CalculateStageScore(currentStage, true);
            OnScoreChanged?.Invoke(currentScore);
        }
        ChangeGameState(GameState.Perfect);
        // GemRevealSystem에 완벽 성공 연출 시작을 요청
        if (gemRevealSystem != null) gemRevealSystem.StartGemPerfectReveal(currentScore);
    }

    /// <summary>
    /// UIManager에서 제한 시간이 다 되었을 때 호출하는 함수
    /// </summary>
    public void OnTimeUp()
    {
        if (currentState != GameState.Playing) return;
        // 시간이 다 됐을 때 진행률이 성공 기준을 넘었으면 성공 처리, 아니면 실패 처리
        if (currentProgress >= successThreshold) { OnMiningSuccess(); }
        else { ChangeGameState(GameState.Failed); }
    }

    /// <summary>
    /// 게임의 상태를 변경하고, OnGameStateChanged 이벤트를 발생시키는 함수
    /// </summary>
    /// <param name="newState">변경할 새로운 게임 상태</param>
    void ChangeGameState(GameState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }

    /// <summary>
    /// 다음 스테이지로 진행합니다. (UI 버튼에서 호출)
    /// </summary>
    public void ProceedToNextStage()
    {
        if (currentStage >= totalStages) return; // 마지막 스테이지면 진행하지 않음
        currentStage++;
        OnStageChanged?.Invoke(currentStage); // 스테이지 변경 이벤트를 알림
        RestartCurrentStage(); // 현재 스테이지(변경된)를 재시작하는 방식으로 다음 스테이지 로드
    }

    /// <summary>
    /// 현재 스테이지를 처음부터 다시 시작합니다.
    /// </summary>
    public void RestartCurrentStage()
    {
        // 모든 상태 플래그를 리셋
        gameStarted = false;
        gameSucceeded = false;
        gameCompleted = false;
        gameInitialized = false;
        currentProgress = 0f;
        StartCoroutine(StartGameSequence()); // 게임 시작 시퀀스를 다시 실행
    }

    void Update()
    {
        // 게임 플레이 중에
        if (currentState == GameState.Playing)
        {
            // 보석이 하나라도 파괴되었다면 즉시 '실패' 상태로 변경
            if (gemProtectionSystem != null && gemProtectionSystem.HasAnyGemDestroyed())
            {
                ChangeGameState(GameState.Failed);
            }
        }
    }

    // 스크립트가 파괴될 때 구독했던 이벤트를 모두 해제합니다. (메모리 누수 방지)
    void OnDestroy()
    {
        if (toolSystem != null) { toolSystem.OnStrikeAccuracyCalculated -= OnStrikeAccuracyCalculated; }
        if (gemProtectionSystem != null) { gemProtectionSystem.OnGemConditionChanged -= OnGemConditionChanged; }
    }
}