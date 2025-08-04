using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 사용자 인터페이스 관리 (기획서의 핵심 클래스)
/// 게임 상태에 따른 UI 표시, 힘 게이지, 점수 등을 관리
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("메인 UI 패널들")]
    public GameObject mainMenuPanel;
    public GameObject gameplayPanel;
    public GameObject pausePanel;
    public GameObject successPanel;
    public GameObject perfectPanel;
    public GameObject failurePanel;
    public GameObject gameCompletePanel;

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

    [Header("메시지 시스템")]
    public TextMeshProUGUI mainMessageText;
    public TextMeshProUGUI subMessageText;
    public GameObject messagePanel;

    [Header("버튼들")]
    public Button restartButton;
    public Button nextStageButton;
    public Button pauseButton;
    public Button resumeButton;
    public Button quitButton;

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
        if (restartButton != null)
            restartButton.onClick.AddListener(() => gameManager?.RestartCurrentStage());

        if (nextStageButton != null)
            nextStageButton.onClick.AddListener(() => gameManager?.ProceedToNextStage());

        if (pauseButton != null)
            pauseButton.onClick.AddListener(() => gameManager?.ChangeGameState(GameManager.GameState.Paused));

        if (resumeButton != null)
            resumeButton.onClick.AddListener(() => gameManager?.ChangeGameState(GameManager.GameState.Playing));

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    void SetInitialUIState()
    {
        // 모든 패널 비활성화
        SetAllPanelsActive(false);

        // 게임플레이 패널만 활성화
        if (gameplayPanel != null)
            gameplayPanel.SetActive(true);

        // 초기 값들 설정
        UpdateStageUI(1);
        UpdateProgressUI(0f);
        UpdateScoreUI(0);
        UpdateForceUI(0f, ForceCalculator.ForceLevel.Weak);

        isGameEnded = false;
    }

    void SetAllPanelsActive(bool active)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(active);
        if (gameplayPanel != null) gameplayPanel.SetActive(active);
        if (pausePanel != null) pausePanel.SetActive(active);
        if (successPanel != null) successPanel.SetActive(active);
        if (perfectPanel != null) perfectPanel.SetActive(active);
        if (failurePanel != null) failurePanel.SetActive(active);
        if (gameCompletePanel != null) gameCompletePanel.SetActive(active);
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

        // 새로운 보석 상태 UI 생성
        var gems = gemProtectionSystem.GetAllGems();
        foreach (var gem in gems)
        {
            CreateGemStatusUI(gem);
        }
    }

    void CreateGemStatusUI(GemProtectionSystem.GemData gem)
    {
        if (gemStatusPrefab == null) return;

        GameObject statusUI = Instantiate(gemStatusPrefab, gemStatusParent);

        // 보석 이름 설정
        var nameText = statusUI.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = gem.gemName;
        }

        // 상태 바 설정
        var statusSlider = statusUI.GetComponentInChildren<Slider>();
        if (statusSlider != null)
        {
            statusSlider.value = gem.currentCondition / 100f;

            // 상태에 따른 색상
            var fillImage = statusSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                if (gem.isDestroyed)
                    fillImage.color = Color.red;
                else if (gem.currentCondition > 70f)
                    fillImage.color = Color.green;
                else if (gem.currentCondition > 30f)
                    fillImage.color = Color.yellow;
                else
                    fillImage.color = Color.white;
            }
        }
    }

    /// <summary>
    /// 게임 상태 변경 처리
    /// </summary>
    public void OnGameStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"UI 상태 변경: {newState}");

        // 모든 패널 비활성화
        SetAllPanelsActive(false);

        switch (newState)
        {
            case GameManager.GameState.NotStarted:
            case GameManager.GameState.Initializing:
                if (gameplayPanel != null) gameplayPanel.SetActive(true);
                ShowMessage("게임 준비 중...", "");
                break;

            case GameManager.GameState.Playing:
                if (gameplayPanel != null) gameplayPanel.SetActive(true);
                HideMessage();
                isGameEnded = false;
                break;

            case GameManager.GameState.Success:
                ShowSuccessUI();
                break;

            case GameManager.GameState.Perfect:
                ShowPerfectUI();
                break;

            case GameManager.GameState.Failed:
                ShowFailureUI();
                break;

            case GameManager.GameState.Paused:
                if (pausePanel != null) pausePanel.SetActive(true);
                break;
        }
    }

    void ShowSuccessUI()
    {
        if (successPanel != null)
        {
            successPanel.SetActive(true);
        }
        else
        {
            ShowMessage("채굴 성공!", "70% 이상 채굴을 완료했습니다!");
        }

        isGameEnded = true;
    }

    void ShowPerfectUI()
    {
        if (perfectPanel != null)
        {
            perfectPanel.SetActive(true);
        }
        else
        {
            ShowMessage("완벽한 채굴!", "100% 완료로 보너스를 획득했습니다!");
        }

        isGameEnded = true;
    }

    void ShowFailureUI()
    {
        if (failurePanel != null)
        {
            failurePanel.SetActive(true);
        }
        else
        {
            ShowMessage("채굴 실패", "보석이 파괴되었습니다.");
        }

        isGameEnded = true;
    }

    public void ShowNextStageUI()
    {
        ShowMessage("준비 완료!", "N키: 다음 스테이지\nR키: 재시작");
    }

    public void ShowRestartUI()
    {
        ShowMessage("다시 도전하세요", "R키: 재시작\nQ키: 종료");
    }

    public void ShowGameCompleteUI(int finalScore)
    {
        if (gameCompletePanel != null)
        {
            gameCompletePanel.SetActive(true);

            // 최종 점수 표시
            var finalScoreText = gameCompletePanel.GetComponentInChildren<TextMeshProUGUI>();
            if (finalScoreText != null)
            {
                finalScoreText.text = $"최종 점수: {finalScore}";
            }
        }
        else
        {
            ShowMessage("게임 완료!", $"모든 스테이지 클리어!\n최종 점수: {finalScore}");
        }

        isGameEnded = true;
    }

    void ShowMessage(string main, string sub)
    {
        if (messagePanel != null)
        {
            messagePanel.SetActive(true);
        }

        if (mainMessageText != null)
        {
            mainMessageText.text = main;
        }

        if (subMessageText != null)
        {
            subMessageText.text = sub;
        }
    }

    void HideMessage()
    {
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }
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
        ShowMessage(message, "게임이 종료되었습니다.");
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
        if (gameplayPanel != null && gameplayPanel.activeInHierarchy) return "Gameplay";
        if (successPanel != null && successPanel.activeInHierarchy) return "Success";
        if (perfectPanel != null && perfectPanel.activeInHierarchy) return "Perfect";
        if (failurePanel != null && failurePanel.activeInHierarchy) return "Failure";
        if (pausePanel != null && pausePanel.activeInHierarchy) return "Pause";
        if (gameCompletePanel != null && gameCompletePanel.activeInHierarchy) return "GameComplete";
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