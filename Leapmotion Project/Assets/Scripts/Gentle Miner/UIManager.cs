using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 현재 UI 구조에 맞게 수정된 UI 관리 시스템
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI 패널들")]
    public GameObject gameplayPanel; // Panel - Info가 게임플레이 UI 역할
    public GameObject resultPanel;   // 아직 생성 안됨 - 추후 생성 예정

    [Header("현재 게임플레이 UI 요소들")]
    public TextMeshProUGUI timeTitle;        // Text (TMP) - Time Title
    public TextMeshProUGUI timeText;         // Text (TMP) - Time  
    public Slider progressSlider;            // Slider - Progress Bar
    public TextMeshProUGUI progressTitle;    // Text (TMP) - Progress Title
    public TextMeshProUGUI score;       // Text (TMP) - Score (Canvas 직속)

    [Header("힘 표시 시스템")]
    public Slider forceSlider;              // Slider - Force Strength
    public Image safeImage;                 // Image - Safe
    public Image warningImage;              // Image - Warning
    public Image successPointImage;         // Image - Success Point

    [Header("게임 설정")]
    [Range(60f, 600f)]
    public float gameDuration = 180f;       // 게임 제한 시간 (초) - 기본 3분

    [Header("힘 상태 색상")]
    public Color safeForceColor = Color.green;
    public Color mediumForceColor = Color.yellow;
    public Color dangerForceColor = Color.red;

    [Header("타이머 설정")]
    public float defaultTimeLimit = 180f;    // 기본 제한시간 (3분)
    public Color normalTimeColor = Color.white;
    public Color warningTimeColor = Color.yellow;  // 30초 남았을 때
    public Color dangerTimeColor = Color.red;      // 10초 남았을 때


    [Header("결과 패널 UI (추후 생성)")]
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultMessageText;
    public Button quitButton;
    public Button retryButton;
    public Button nextButton;

    // 시스템 참조
    private GameManager gameManager;
    private ForceCalculator forceCalculator;
    private GemProtectionSystem gemProtectionSystem;
    private ScoreSystem scoreSystem;

    // 시간 관련 변수들
    private float currentTimeLimit = 180f;
    private float gameStartTime = 0f;
    private float remainingTime = 0f;
    private bool isTimeWarning = false;

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

        // 버튼 이벤트 설정 (결과 패널이 있는 경우에만)
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

        // 결과 패널 비활성화 (있는 경우에만)
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 초기 값들 설정
        SetupTimerForCurrentStage();
        UpdateTimeUI("채굴 준비 중");
        UpdateProgressUI(0f);
        UpdateScoreUI(0);
        UpdateForceUI(0f, ForceCalculator.ForceLevel.Weak);

        // 성공 포인트 이미지 70% 위치에 표시
        UpdateSuccessPointPosition();

        isGameEnded = false;
    }

    void Update()
    {
        // 힘 표시 업데이트
        UpdateForceDisplay();

        // 시간 업데이트
        UpdateTimeDisplay();

        // 보석 상태 업데이트 (향후 확장용)
        UpdateGemStatusDisplay();
    }

    void UpdateForceDisplay()
    {
        if (forceCalculator == null) return;

        float currentForce = forceCalculator.CurrentForce;
        var forceLevel = forceCalculator.CurrentForceLevel;

        UpdateForceUI(currentForce, forceLevel);
    }

    void UpdateTimeDisplay()
    {
        if (gameManager == null) return;

        // 게임 시작 전에는 제한시간 표시
        if (!gameManager.IsGameStarted)
        {
            remainingTime = currentTimeLimit;
            UpdateTimeText(remainingTime);
            return;
        }

        // 게임 진행 중에는 카운트다운
        float elapsedTime = Time.time - gameStartTime;
        remainingTime = Mathf.Max(0f, currentTimeLimit - elapsedTime);

        UpdateTimeText(remainingTime);
        UpdateTimeColor(remainingTime);

        // 시간 종료 체크
        if (remainingTime <= 0f && !isGameEnded)
        {
            OnTimeUp();
        }
    }

    void UpdateTimeColor(float timeInSeconds)
    {
        if (timeText == null) return;

        Color targetColor;

        if (timeInSeconds <= 10f) // 10초 이하 - 위험 (빨간색)
        {
            targetColor = dangerTimeColor;

            // 깜박임 효과 (5초 이하일 때)
            if (timeInSeconds <= 5f)
            {
                float alpha = Mathf.PingPong(Time.time * 3f, 1f); // 빠른 깜박임
                targetColor.a = Mathf.Lerp(0.3f, 1f, alpha);
            }
        }
        else if (timeInSeconds <= 30f) // 30초 이하 - 경고 (노란색)
        {
            targetColor = warningTimeColor;

            if (!isTimeWarning)
            {
                isTimeWarning = true;
                Debug.Log("시간 경고! 30초 남음");
            }
        }
        else // 30초 초과 - 정상 (흰색)
        {
            targetColor = normalTimeColor;
        }

        timeText.color = targetColor;
    }

    void UpdateTimeText(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        if (timeText != null)
        {
            timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    void OnTimeUp()
    {
        Debug.Log("제한시간 종료!");

        if (gameManager != null)
        {
            // 현재 진행률에 따라 결과 결정
            float currentProgress = gameManager.CurrentProgress;

            if (currentProgress >= 0.7f) // 70% 이상이면 성공으로 처리
            {
                Debug.Log("시간 종료되었지만 70% 이상 완료로 성공 처리");
                // GameManager에서 자동으로 성공 처리될 것임
            }
            else
            {
                // 시간 초과로 실패 처리
                gameManager.OnTimeUp(); // GameManager에 시간 초과 알림
            }
        }

        isGameEnded = true;
    }

    void UpdateForceUI(float force, ForceCalculator.ForceLevel level)
    {
        // 힘 슬라이더 업데이트
        if (forceSlider != null)
        {
            forceSlider.value = force;
        }

        // 힘 상태에 따른 이미지 표시
        UpdateForceStatusImages(level);
    }

    /// <summary>
    /// 현재 스테이지에 맞는 제한시간 설정
    /// </summary>
    void SetupTimerForCurrentStage()
    {
        if (gameManager != null)
        {
            // 스테이지별 제한시간 설정
            switch (gameManager.CurrentStage)
            {
                case 1:
                    currentTimeLimit = 300f; // 5분 (초보자용)
                    break;
                case 2:
                    currentTimeLimit = 240f; // 4분 (중급자용)
                    break;
                case 3:
                    currentTimeLimit = 180f; // 3분 (고급자용)
                    break;
                default:
                    currentTimeLimit = defaultTimeLimit;
                    break;
            }
        }
        else
        {
            currentTimeLimit = defaultTimeLimit;
        }

        remainingTime = currentTimeLimit;
        Debug.Log($"스테이지 {gameManager?.CurrentStage ?? 1} 제한시간: {currentTimeLimit}초");
    }

    void UpdateForceStatusImages(ForceCalculator.ForceLevel level)
    {
        // 모든 이미지 비활성화
        if (safeImage != null) safeImage.gameObject.SetActive(false);
        if (warningImage != null) warningImage.gameObject.SetActive(false);

        // 현재 상태에 맞는 이미지만 활성화
        switch (level)
        {
            case ForceCalculator.ForceLevel.Weak:
                if (safeImage != null) safeImage.gameObject.SetActive(true);
                break;
            case ForceCalculator.ForceLevel.Medium:
                if (warningImage != null) warningImage.gameObject.SetActive(true);
                break;
            case ForceCalculator.ForceLevel.Strong:
                if (warningImage != null) warningImage.gameObject.SetActive(true);
                break;
        }
    }

    void UpdateSuccessPointPosition()
    {
        if (successPointImage == null || progressSlider == null) return;

        // 70% 지점에 성공 포인트 이미지 배치
        RectTransform sliderRect = progressSlider.GetComponent<RectTransform>();
        RectTransform successRect = successPointImage.GetComponent<RectTransform>();

        if (sliderRect != null && successRect != null)
        {
            float sliderWidth = sliderRect.rect.width;
            float successPosition = sliderWidth * 0.7f; // 70% 위치

            successRect.anchoredPosition = new Vector2(successPosition, successRect.anchoredPosition.y);
        }
    }

    void UpdateGemStatusDisplay()
    {
        // 향후 보석 상태 UI 확장용
        // 현재는 비어있음
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
                SetupTimerForCurrentStage();
                UpdateTimeUI("게임 준비 중...");
                break;

            case GameManager.GameState.Playing:
                ShowGameplayUI();
                UpdateTimeUI("채굴 진행 중");
                gameStartTime = Time.time; // 게임 시작 시간 기록
                isGameEnded = false;
                isTimeWarning = false;
                break;

            case GameManager.GameState.Success:
                ShowResultMessage("채굴 성공!", $"70% 이상 채굴 완료!\n남은 시간: {Mathf.FloorToInt(remainingTime)}초");
                isGameEnded = true;
                break;

            case GameManager.GameState.Perfect:
                ShowResultMessage("완벽한 채굴!", $"100% 완료로 보너스 획득!\n남은 시간: {Mathf.FloorToInt(remainingTime)}초");
                isGameEnded = true;
                break;

            case GameManager.GameState.Failed:
                string failMessage = remainingTime <= 0 ?
                    "시간 초과로 실패했습니다" :
                    "보석이 파괴되었습니다";
                ShowResultMessage("채굴 실패", failMessage);
                isGameEnded = true;
                break;

            case GameManager.GameState.Paused:
                ShowResultMessage("일시정지", "게임이 일시정지되었습니다");
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

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    /// <summary>
    /// 결과 메시지 표시 (결과 패널이 없으면 콘솔에만 출력)
    /// </summary>
    void ShowResultMessage(string title, string message)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);

            if (resultTitleText != null)
                resultTitleText.text = title;

            if (resultMessageText != null)
                resultMessageText.text = message;
        }
        else
        {
            // 결과 패널이 없으면 콘솔에 출력
            Debug.Log($"=== {title} ===");
            Debug.Log(message);
        }
    }

    public void OnStageChanged(int newStage)
    {
        if (timeTitle != null)
        {
            timeTitle.text = $"스테이지 {newStage}";
        }

        // 새 스테이지의 제한시간 설정
        SetupTimerForCurrentStage();
    }

    public void OnProgressChanged(float progress)
    {
        UpdateProgressUI(progress);
    }

    public void OnScoreChanged(int score)
    {
        UpdateScoreUI(score);
    }

    void UpdateTimeUI(string status)
    {
        if (timeTitle != null)
        {
            timeTitle.text = status;
        }
    }

    void UpdateProgressUI(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        if (progressTitle != null)
        {
            progressTitle.text = $"진행률: {progress * 100f:F1}%";
        }
    }

    void UpdateScoreUI(int score)
    {
        if (this.score != null)
        {
            this.score.text = $"점수: {score}";
        }
    }

    /// <summary>
    /// 다음 스테이지 UI 표시
    /// </summary>
    public void ShowNextStageUI()
    {
        ShowResultMessage("스테이지 완료!", "다음 스테이지로 진행하시겠습니까?");
    }

    /// <summary>
    /// 게임 완료 UI 표시
    /// </summary>
    public void ShowGameCompleteUI(int finalScore)
    {
        ShowResultMessage("게임 완료!", $"모든 스테이지 클리어!\n최종 점수: {finalScore}");
        isGameEnded = true;
    }

    /// <summary>
    /// 게임 종료
    /// </summary>
    void QuitGame()
    {
        Debug.Log("게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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