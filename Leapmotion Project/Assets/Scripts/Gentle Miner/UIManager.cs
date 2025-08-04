using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 간소화된 UI 관리 시스템
/// GameplayPanel (게임 중) + ResultPanel (결과/메시지) 2개 패널로 모든 UI 처리
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI 패널들")]
    public GameObject gameplayPanel; // 게임 중 표시되는 기본 UI
    public GameObject resultPanel;   // 범용 결과/메시지 패널

    [Header("게임플레이 UI")]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI scoreText;
    public Slider progressSlider;

    [Header("힘 표시 시스템")]
    public Slider forceSlider;
    public TextMeshProUGUI forceText;
    public Image forceBackground;
    public Color safeForceColor = Color.green;
    public Color mediumForceColor = Color.yellow;
    public Color dangerForceColor = Color.red;

    [Header("보석 상태 UI")]
    public Transform gemStatusParent;
    public GameObject gemStatusPrefab;

    [Header("범용 결과 패널")]
    public TextMeshProUGUI resultTitleText;  // 큰 제목 (성공/실패/일시정지 등)
    public TextMeshProUGUI resultMessageText; // 상세 메시지
    public Button quitButton;
    public Button retryButton;
    public Button nextButton;

    // 시스템 참조
    private GameManager gameManager;
    private ForceCalculator forceCalculator;
    private GemProtectionSystem gemProtectionSystem;
    private ScoreSystem scoreSystem;

    // UI 상태
    private bool isGameEnded = false;

    void Start()
    {
        InitializeUIManager();
    }

    void InitializeUIManager()
    {
        // 시스템 참조 찾기
        gameManager = FindFirstObjectByType<GameManager>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();
        gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();

        // 이벤트 구독
        SubscribeToEvents();

        // 버튼 이벤트 설정
        SetupButtonEvents();

        // 초기 UI 상태 설정
        SetInitialUIState();

        Debug.Log("UIManager 초기화 완료");
    }

    void SubscribeToEvents()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += OnGameStateChanged;
            gameManager.OnStageChanged += OnStageChanged;
            gameManager.OnProgressChanged += OnProgressChanged;
            gameManager.OnScoreChanged += OnScoreChanged;
        }
    }

    void SetupButtonEvents()
    {
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (retryButton != null)
            retryButton.onClick.AddListener(() => gameManager?.RestartCurrentStage());

        if (nextButton != null)
            nextButton.onClick.AddListener(() => gameManager?.ProceedToNextStage());
    }

    void SetInitialUIState()
    {
        // 게임플레이 패널 활성화
        if (gameplayPanel != null)
            gameplayPanel.SetActive(true);

        // 결과 패널 비활성화
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 초기 값들 설정
        UpdateStageUI(1);
        UpdateProgressUI(0f);
        UpdateScoreUI(0);
        UpdateForceUI(0f, ForceCalculator.ForceLevel.Weak);

        isGameEnded = false;
    }

    void Update()
    {
        // 힘 표시 업데이트
        UpdateForceDisplay();

        // 보석 상태 업데이트
        UpdateGemStatusDisplay();
    }

    void UpdateForceDisplay()
    {
        if (forceCalculator == null) return;

        float currentForce = forceCalculator.CurrentForce;
        var forceLevel = forceCalculator.CurrentForceLevel;

        UpdateForceUI(currentForce, forceLevel);
    }

    void UpdateForceUI(float force, ForceCalculator.ForceLevel level)
    {
        // 힘 슬라이더 업데이트
        if (forceSlider != null)
        {
            forceSlider.value = force;
        }

        // 힘 텍스트 업데이트
        if (forceText != null)
        {
            forceText.text = $"{force * 100f:F0}%";
        }

        // 색상 업데이트
        Color levelColor = GetForceColor(level);

        if (forceBackground != null)
        {
            forceBackground.color = levelColor;
        }

        if (forceSlider != null && forceSlider.fillRect != null)
        {
            var fillImage = forceSlider.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = levelColor;
            }
        }
    }

    Color GetForceColor(ForceCalculator.ForceLevel level)
    {
        switch (level)
        {
            case ForceCalculator.ForceLevel.Weak:
                return safeForceColor;
            case ForceCalculator.ForceLevel.Medium:
                return mediumForceColor;
            case ForceCalculator.ForceLevel.Strong:
                return dangerForceColor;
            default:
                return Color.white;
        }
    }

    void UpdateGemStatusDisplay()
    {
        if (gemProtectionSystem == null || gemStatusParent == null) return;

        // 기존 보석 상태 UI 정리
        foreach (Transform child in gemStatusParent)
        {
            Destroy(child.gameObject);
        }

        // GemProtectionSystem에서 보석 정보 가져오기
        try
        {
            var gems = gemProtectionSystem.GetAllGems();
            if (gems != null)
            {
                foreach (var gem in gems)
                {
                    CreateGemStatusUI(gem);
                }
            }
        }
        catch (System.Exception)
        {
            // GetAllGems 메서드가 없으면 무시
        }
    }

    void CreateGemStatusUI(object gemData)
    {
        if (gemStatusPrefab == null) return;

        GameObject statusUI = Instantiate(gemStatusPrefab, gemStatusParent);

        // 안전한 방식으로 보석 정보 처리
        try
        {
            // 보석 이름 설정
            var nameText = statusUI.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = "보석"; // 기본값
            }

            // 상태 바 설정  
            var statusSlider = statusUI.GetComponentInChildren<Slider>();
            if (statusSlider != null)
            {
                statusSlider.value = 1.0f; // 기본값 100%

                var fillImage = statusSlider.fillRect?.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = Color.green; // 기본 색상
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"보석 UI 생성 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 게임 상태 변경 처리
    /// </summary>
    public void OnGameStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"UI 상태 변경: {newState}");

        switch (newState)
        {
            case GameManager.GameState.NotStarted:
            case GameManager.GameState.Initializing:
                ShowGameplayUI();
                ShowResultPanel("게임 준비 중...", "잠시만 기다려주세요", false, false, false);
                break;

            case GameManager.GameState.Playing:
                ShowGameplayUI();
                HideResultPanel();
                isGameEnded = false;
                break;

            case GameManager.GameState.Success:
                ShowResultPanel("채굴 성공!", "70% 이상 채굴을 완료했습니다!", true, true, true);
                isGameEnded = true;
                break;

            case GameManager.GameState.Perfect:
                ShowResultPanel("완벽한 채굴!", "100% 완료로 보너스를 획득했습니다!", true, true, true);
                isGameEnded = true;
                break;

            case GameManager.GameState.Failed:
                ShowResultPanel("채굴 실패", "보석이 파괴되었습니다", true, true, false);
                isGameEnded = true;
                break;

            case GameManager.GameState.Paused:
                ShowResultPanel("일시정지", "게임이 일시정지되었습니다", false, true, false);
                break;
        }
    }

    /// <summary>
    /// 게임플레이 UI 표시
    /// </summary>
    void ShowGameplayUI()
    {
        if (gameplayPanel != null)
            gameplayPanel.SetActive(true);
    }

    /// <summary>
    /// 범용 결과 패널 표시
    /// </summary>
    /// <param name="title">제목</param>
    /// <param name="message">메시지</param>
    /// <param name="showQuit">Quit 버튼 표시 여부</param>
    /// <param name="showRetry">Retry 버튼 표시 여부</param>
    /// <param name="showNext">Next 버튼 표시 여부</param>
    void ShowResultPanel(string title, string message, bool showQuit, bool showRetry, bool showNext)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = title;
        }

        if (resultMessageText != null)
        {
            resultMessageText.text = message;
        }

        // 버튼 표시/숨김 설정
        if (quitButton != null)
            quitButton.gameObject.SetActive(showQuit);

        if (retryButton != null)
            retryButton.gameObject.SetActive(showRetry);

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(showNext);

            // Next 버튼은 마지막 스테이지가 아닐 때만 활성화
            if (showNext && gameManager != null)
            {
                bool isLastStage = gameManager.CurrentStage >= gameManager.totalStages;
                nextButton.interactable = !isLastStage;

                if (isLastStage)
                {
                    // 마지막 스테이지 완료시 버튼 텍스트 변경
                    var buttonText = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                        buttonText.text = "완료";
                }
                else
                {
                    var buttonText = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                        buttonText.text = "Next";
                }
            }
        }
    }

    /// <summary>
    /// 결과 패널 숨김
    /// </summary>
    void HideResultPanel()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void ShowNextStageUI()
    {
        ShowResultPanel("스테이지 완료!", "다음 스테이지로 진행하시겠습니까?", true, true, true);
    }

    public void ShowRestartUI()
    {
        ShowResultPanel("게임 재시작", "현재 스테이지를 다시 시작하시겠습니까?", true, true, false);
    }

    public void ShowGameCompleteUI(int finalScore)
    {
        ShowResultPanel("게임 완료!", $"모든 스테이지 클리어!\n최종 점수: {finalScore}", true, true, false);
        isGameEnded = true;
    }

    public void OnStageChanged(int newStage)
    {
        UpdateStageUI(newStage);
    }

    public void OnProgressChanged(float progress)
    {
        UpdateProgressUI(progress);
    }

    public void OnScoreChanged(int score)
    {
        UpdateScoreUI(score);
    }

    void UpdateStageUI(int stage)
    {
        if (stageText != null)
        {
            stageText.text = $"스테이지 {stage}";
        }
    }

    void UpdateProgressUI(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"진행률: {progress * 100f:F1}%";
        }
    }

    void UpdateScoreUI(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"점수: {score}";
        }
    }

    /// <summary>
    /// 게임 재시작 (외부 호출용)
    /// </summary>
    public void RestartGame()
    {
        isGameEnded = false;
        SetInitialUIState();
    }

    /// <summary>
    /// 강제 게임 종료 (Test.cs 호환용)
    /// </summary>
    public void ForceGameEnd(string message)
    {
        ShowResultPanel(message, "게임이 종료되었습니다.", true, true, false);
        isGameEnded = true;
    }

    /// <summary>
    /// 게임 종료 상태 반환 (Test.cs 호환용)
    /// </summary>
    public bool IsGameEnded()
    {
        return isGameEnded;
    }

    void QuitGame()
    {
        Debug.Log("게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    [ContextMenu("UI 상태 출력")]
    public void PrintUIStatus()
    {
        Debug.Log("=== UIManager 상태 ===");
        Debug.Log($"게임 종료: {isGameEnded}");
        Debug.Log($"활성 패널: {GetActivePanelName()}");
        Debug.Log("====================");
    }

    string GetActivePanelName()
    {
        if (gameplayPanel != null && gameplayPanel.activeInHierarchy &&
            (resultPanel == null || !resultPanel.activeInHierarchy))
            return "Gameplay";

        if (resultPanel != null && resultPanel.activeInHierarchy)
            return "Result";

        return "None";
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= OnGameStateChanged;
            gameManager.OnStageChanged -= OnStageChanged;
            gameManager.OnProgressChanged -= OnProgressChanged;
            gameManager.OnScoreChanged -= OnScoreChanged;
        }
    }
}