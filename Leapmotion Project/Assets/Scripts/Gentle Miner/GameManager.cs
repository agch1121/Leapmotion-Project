using UnityEngine;
using System.Collections;

/// <summary>
/// GameManager
/// [수정] UIManager 참조 전달 및 상태 변경 로직 간소화
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake() { Instance = this; }

    [Header("게임 설정")]
    [SerializeField] public int totalStages = 3;
    public float successThreshold = 0.7f;
    public bool allowPartialSuccess = true;

    [Header("게임 시작 설정")]
    public bool showGemPreviewOnStart = true;
    public float gameStartDelay = 2f;

    public enum GameState { NotStarted, Initializing, Playing, Success, Perfect, Failed, Paused }

    [Header("현재 상태")]
    [SerializeField] private GameState currentState = GameState.NotStarted;
    [SerializeField] private int currentStage = 1;
    [SerializeField] private float currentProgress = 0f;
    [SerializeField] private int currentScore = 0;

    private bool gameStarted = false;
    private bool gameSucceeded = false;
    private bool gameCompleted = false;
    private bool gameInitialized = false;

    private StageManager stageManager;
    private UIManager uiManager;
    private GemRevealSystem gemRevealSystem;
    private GemProtectionSystem gemProtectionSystem;
    private ScoreSystem scoreSystem;
    private ToolSystem toolSystem;

    public event System.Action<GameState> OnGameStateChanged;
    public event System.Action<int> OnStageChanged;
    public event System.Action<float> OnProgressChanged;
    public event System.Action<int> OnScoreChanged;

    public GameState CurrentState => currentState;
    public int CurrentStage => currentStage;
    public float CurrentProgress => currentProgress;
    public int CurrentScore => currentScore;
    public bool IsGameActive => currentState == GameState.Playing;
    public bool IsGameStarted => gameStarted;
    public bool IsGameSucceeded => gameSucceeded;
    public bool IsGameCompleted => gameCompleted;

    public ScoreSystem GetScoreSystem() { return scoreSystem; }

    void Start() { InitializeGameManager(); }

    void InitializeGameManager()
    {
        FindSystemReferences();
        SubscribeToEvents();
        StartCoroutine(StartGameSequence());
    }

    void FindSystemReferences()
    {
        stageManager = FindFirstObjectByType<StageManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();
        toolSystem = FindFirstObjectByType<ToolSystem>();
    }

    void SubscribeToEvents()
    {
        if (toolSystem != null)
        {
            toolSystem.OnStrikeAccuracyCalculated += OnStrikeAccuracyCalculated;
        }
    }

    private void OnStrikeAccuracyCalculated(float accuracy)
    {
        if (scoreSystem != null) scoreSystem.AddAccuracy(accuracy);
        if (uiManager != null) uiManager.UpdateLastAccuracyUI(accuracy);
    }

    IEnumerator StartGameSequence()
    {
        ChangeGameState(GameState.Initializing);
        if (stageManager != null) stageManager.InitializeStage(currentStage);
        if (scoreSystem != null) scoreSystem.StartNewStage();
        if (uiManager != null) uiManager.ResetGameplayUI();
        yield return new WaitForSeconds(0.2f);
        FindCurrentGemProtectionSystem();
        if (showGemPreviewOnStart && gemRevealSystem != null)
        {
            yield return new WaitForSeconds(gameStartDelay);
            gemRevealSystem.StartGemPreview();
            float previewDuration = (gemRevealSystem.cameraTransitionTime * 2) + gemRevealSystem.gemDisplayTime + 1f;
            yield return new WaitForSeconds(previewDuration);
        }
        gameStarted = true;
        ChangeGameState(GameState.Playing);
        yield return new WaitForSeconds(5f);
        gameInitialized = true;
    }

    void FindCurrentGemProtectionSystem()
    {
        if (stageManager == null) return;
        GameObject mineralBlock = stageManager.GetCurrentMineralBlock();
        if (mineralBlock == null) return;
        if (gemProtectionSystem != null) { gemProtectionSystem.OnGemConditionChanged -= OnGemConditionChanged; }
        gemProtectionSystem = mineralBlock.GetComponent<GemProtectionSystem>();
        if (gemProtectionSystem != null)
        {
            gemProtectionSystem.OnGemConditionChanged += OnGemConditionChanged;
            if (uiManager != null) { uiManager.UpdateAllGemStatusText(gemProtectionSystem.GetAllGems()); }
        }
    }

    private void OnGemConditionChanged(GemProtectionSystem.GemData gemData)
    {
        if (uiManager != null && gemProtectionSystem != null)
        {
            uiManager.UpdateAllGemStatusText(gemProtectionSystem.GetAllGems());
        }
    }

    public void OnMineralProgressChanged(int activeChunks, int destroyedChunks, float progress)
    {
        if (!gameStarted || !gameInitialized || gameSucceeded || gameCompleted) return;
        currentProgress = progress;
        OnProgressChanged?.Invoke(progress);
        if (!gameSucceeded && allowPartialSuccess && progress >= successThreshold) { OnMiningSuccess(); }
        else if (!gameCompleted && activeChunks <= 0) { OnMiningComplete(); }
    }

    void OnMiningSuccess()
    {
        gameSucceeded = true;
        if (scoreSystem != null)
        {
            currentScore = scoreSystem.CalculateStageScore(currentStage, false);
            OnScoreChanged?.Invoke(currentScore);
        }
        ChangeGameState(GameState.Success);
        if (gemRevealSystem != null) gemRevealSystem.StartGemSuccessReveal(currentScore);
    }

    void OnMiningComplete()
    {
        gameCompleted = true;
        if (scoreSystem != null)
        {
            currentScore = scoreSystem.CalculateStageScore(currentStage, true);
            OnScoreChanged?.Invoke(currentScore);
        }
        ChangeGameState(GameState.Perfect);
        if (gemRevealSystem != null) gemRevealSystem.StartGemPerfectReveal(currentScore);
    }

    public void OnTimeUp()
    {
        if (currentState != GameState.Playing) return;
        if (currentProgress >= successThreshold) { OnMiningSuccess(); }
        else { ChangeGameState(GameState.Failed); }
    }

    void ChangeGameState(GameState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }

    public void ProceedToNextStage()
    {
        if (currentStage >= totalStages) return;
        currentStage++;
        OnStageChanged?.Invoke(currentStage);
        RestartCurrentStage();
    }

    public void RestartCurrentStage()
    {
        gameStarted = false;
        gameSucceeded = false;
        gameCompleted = false;
        gameInitialized = false;
        currentProgress = 0f;
        StartCoroutine(StartGameSequence());
    }

    void Update()
    {
        if (currentState == GameState.Playing)
        {
            if (gemProtectionSystem != null && gemProtectionSystem.HasAnyGemDestroyed())
            {
                ChangeGameState(GameState.Failed);
            }
        }
    }

    void OnDestroy()
    {
        if (toolSystem != null) { toolSystem.OnStrikeAccuracyCalculated -= OnStrikeAccuracyCalculated; }
        if (gemProtectionSystem != null) { gemProtectionSystem.OnGemConditionChanged -= OnGemConditionChanged; }
    }
}