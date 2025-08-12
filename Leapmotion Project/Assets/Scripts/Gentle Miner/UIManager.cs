using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// UI 관리 시스템
/// [수정] 누락된 타이머 색상 변수 선언 추가
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI 패널들")]
    public GameObject gameplayPanel;
    public GameObject resultPanel;

    [Header("게임플레이 UI 요소")]
    public TextMeshProUGUI timeTitle;
    public TextMeshProUGUI timeText;
    public Slider progressSlider;
    public TextMeshProUGUI progressTitle;
    public TextMeshProUGUI score;

    [Header("힘 표시 시스템")]
    public Slider forceSlider;
    public Image safeImage;
    public Image warningImage;
    public Image successPointImage;

    [Header("정확도 표시 UI")]
    public TextMeshProUGUI lastAccuracyText;
    public TextMeshProUGUI averageAccuracyText;

    [Header("보석 상태 UI")]
    public TextMeshProUGUI gemStatusText;

    [Header("결과 패널 UI")]
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultMessageText;
    public GameObject quitButton;
    public GameObject retryButton;
    public GameObject nextButton;
    public TextMeshProUGUI nextButtonText;

    [Header("게임 설정")]
    [Range(60f, 600f)]
    public float gameDuration = 180f;

    [Header("힘 상태 색상")]
    public Color safeForceColor = Color.green;
    public Color mediumForceColor = Color.yellow;
    public Color dangerForceColor = Color.red;

    [Header("타이머 설정")]
    public float defaultTimeLimit = 180f;
    public Color normalTimeColor = Color.white; // [수정] 변수 선언 추가
    public Color warningTimeColor = Color.yellow; // [수정] 변수 선언 추가
    public Color dangerTimeColor = Color.red; // [수정] 변수 선언 추가

    private GameManager gameManager;
    private ScoreSystem scoreSystem;
    private ForceCalculator forceCalculator;
    private float currentTimeLimit = 180f;
    private float gameStartTime = 0f;
    private float remainingTime = 0f;
    private bool isTimeWarning = false;
    private bool isGameEnded = false;

    void Start()
    {
        InitializeUIManager();
    }

    void InitializeUIManager()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();
        SubscribeToEvents();
        SetInitialUIState();
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

        if (scoreSystem != null)
        {
            scoreSystem.OnAverageAccuracyChanged += OnAverageAccuracyChanged;
        }
    }

    void SetInitialUIState()
    {
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
        ResetGameplayUI();
        SetupTimerForCurrentStage();
        UpdateTimeUI("채굴 준비 중");
        UpdateScoreUI(0);
        UpdateForceUI(0f, ForceCalculator.ForceLevel.Weak);
        UpdateSuccessPointPosition();
        isGameEnded = false;
    }

    public void ResetGameplayUI()
    {
        UpdateProgressUI(0f);
        UpdateLastAccuracyUI(0f);
        UpdateAverageAccuracyUI(0f);
        if (gemStatusText != null)
        {
            gemStatusText.text = "보석 정보 로딩 중...";
            gemStatusText.gameObject.SetActive(true);
        }
    }

    public void UpdateAllGemStatusText(GemProtectionSystem.GemData[] gems)
    {
        if (gemStatusText == null) return;
        if (gems == null || gems.Length == 0)
        {
            gemStatusText.gameObject.SetActive(false);
            return;
        }
        gemStatusText.gameObject.SetActive(true);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < gems.Length; i++)
        {
            // [수정] 긴 이름에서 첫 단어(보석 타입)만 추출
            string simpleName = gems[i].gemName.Split(' ')[0];
            sb.Append(simpleName);
            if (i < gems.Length - 1) sb.Append(" | ");
        }
        sb.AppendLine();
        for (int i = 0; i < gems.Length; i++)
        {
            string status = gems[i].isDestroyed ? "<color=red>파괴</color>" : $"{gems[i].currentCondition:F0}%";
            sb.Append(status);
            if (i < gems.Length - 1) sb.Append(" | ");
        }
        gemStatusText.text = sb.ToString();
    }

    void Update()
    {
        UpdateForceDisplay();
        UpdateTimeDisplay();
    }

    public void OnGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.NotStarted:
            case GameManager.GameState.Initializing:
                ShowGameplayUI();
                isGameEnded = false;
                SetupTimerForCurrentStage();
                UpdateTimeUI("게임 준비 중...");
                break;
            case GameManager.GameState.Playing:
                ShowGameplayUI();
                UpdateTimeUI("채굴 진행 중");
                gameStartTime = Time.time;
                isGameEnded = false;
                isTimeWarning = false;
                break;
            case GameManager.GameState.Success:
                UpdateTimeUI("채굴 성공!");
                ShowResultPanelWithDetails("채굴 성공!", "", gameManager.GetScoreSystem());
                isGameEnded = true;
                break;
            case GameManager.GameState.Perfect:
                UpdateTimeUI("완벽한 채굴!");
                ShowResultPanelWithDetails("완벽한 채굴!", "", gameManager.GetScoreSystem());
                isGameEnded = true;
                break;
            case GameManager.GameState.Failed:
                string failMessage = remainingTime <= 0 ? "시간 초과" : "보석 파괴";
                UpdateTimeUI("채굴 실패..");
                ShowResultPanelWithDetails("채굴 실패", failMessage, null);
                isGameEnded = true;
                break;
        }
    }

    void ShowResultPanelWithDetails(string title, string message, ScoreSystem scoreSystemRef)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);
        if (resultTitleText != null) resultTitleText.text = title;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(message);

        if (scoreSystemRef != null)
        {
            sb.AppendLine();
            sb.AppendLine($"보석 점수: {scoreSystemRef.LastCalculatedGemScore}점");
            sb.AppendLine($"정확도 점수: {scoreSystemRef.LastCalculatedBonusScore}점");
            sb.AppendLine($"(평균 정확도: {scoreSystemRef.AverageAccuracy:P0})");
        }

        if (resultMessageText != null) resultMessageText.text = sb.ToString();
    }

    public void OnStageChanged(int newStage)
    {
        if (timeTitle != null) timeTitle.text = $"스테이지 {newStage}";
        SetupTimerForCurrentStage();
    }

    public void UpdateLastAccuracyUI(float accuracy)
    {
        if (lastAccuracyText == null) return;
        lastAccuracyText.text = accuracy > 0 ? $"정확도: {accuracy:P0}" : "정확도: -";
    }

    private void OnAverageAccuracyChanged(float newAverage)
    {
        UpdateAverageAccuracyUI(newAverage);
    }

    public void UpdateAverageAccuracyUI(float accuracy)
    {
        if (averageAccuracyText == null) return;
        averageAccuracyText.text = accuracy > 0 ? $"평균 정확도: {accuracy:P0}" : "평균 정확도: -";
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
        if (gameManager == null || isGameEnded) return;

        if (!gameManager.IsGameStarted)
        {
            remainingTime = currentTimeLimit;
            UpdateTimeText(remainingTime);
            return;
        }

        float elapsedTime = Time.time - gameStartTime;
        remainingTime = Mathf.Max(0f, currentTimeLimit - elapsedTime);

        UpdateTimeText(remainingTime);
        UpdateTimeColor(remainingTime);

        if (remainingTime <= 0f && !isGameEnded)
        {
            OnTimeUp();
        }
    }

    void UpdateTimeColor(float timeInSeconds)
    {
        if (timeText == null) return;
        Color targetColor;
        if (timeInSeconds <= 10f)
        {
            targetColor = dangerTimeColor;
            if (timeInSeconds <= 5f)
            {
                float alpha = Mathf.PingPong(Time.time * 3f, 1f);
                targetColor.a = Mathf.Lerp(0.3f, 1f, alpha);
            }
        }
        else if (timeInSeconds <= 30f)
        {
            targetColor = warningTimeColor;
            if (!isTimeWarning) { isTimeWarning = true; }
        }
        else
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
        if (gameManager != null)
        {
            if (gameManager.CurrentProgress >= 0.7f) { /* GameManager가 처리 */ }
            else { gameManager.OnTimeUp(); }
        }
        isGameEnded = true;
    }

    void UpdateForceUI(float force, ForceCalculator.ForceLevel level)
    {
        if (forceSlider != null) forceSlider.value = force;
        UpdateForceStatusImages(level);
    }

    void SetupTimerForCurrentStage()
    {
        if (gameManager != null)
        {
            switch (gameManager.CurrentStage)
            {
                case 1: currentTimeLimit = 300f; break;
                case 2: currentTimeLimit = 240f; break;
                case 3: currentTimeLimit = 180f; break;
                default: currentTimeLimit = 180f; break;
            }
        }
    }

    void UpdateForceStatusImages(ForceCalculator.ForceLevel level)
    {
        if (safeImage != null) safeImage.gameObject.SetActive(false);
        if (warningImage != null) warningImage.gameObject.SetActive(false);
        switch (level)
        {
            case ForceCalculator.ForceLevel.Weak:
                if (safeImage != null) safeImage.gameObject.SetActive(true);
                break;
            case ForceCalculator.ForceLevel.Medium:
            case ForceCalculator.ForceLevel.Strong:
                if (warningImage != null) warningImage.gameObject.SetActive(true);
                break;
        }
    }

    void UpdateSuccessPointPosition()
    {
        if (successPointImage == null || progressSlider == null) return;
        RectTransform sliderRect = progressSlider.GetComponent<RectTransform>();
        RectTransform successRect = successPointImage.GetComponent<RectTransform>();
        if (sliderRect != null && successRect != null)
        {
            float successPosition = sliderRect.rect.width * 0.9f;
            successRect.anchoredPosition = new Vector2(successPosition, successRect.anchoredPosition.y);
        }
    }

    void ShowGameplayUI()
    {
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    public void OnProgressChanged(float progress) { UpdateProgressUI(progress); }
    public void OnScoreChanged(int newScore) { UpdateScoreUI(newScore); }
    void UpdateTimeUI(string status) { if (timeTitle != null) timeTitle.text = status; }
    void UpdateProgressUI(float progress)
    {
        if (progressSlider != null) progressSlider.value = progress;
        if (progressTitle != null) progressTitle.text = $"진행률: {progress * 100f:F1}%";
    }
    void UpdateScoreUI(int scoreValue) { if (this.score != null) this.score.text = $"점수: {scoreValue}"; }

    void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= OnGameStateChanged;
            gameManager.OnStageChanged -= OnStageChanged;
            gameManager.OnProgressChanged -= OnProgressChanged;
            gameManager.OnScoreChanged -= OnScoreChanged;
        }
        if (scoreSystem != null)
        {
            scoreSystem.OnAverageAccuracyChanged -= OnAverageAccuracyChanged;
        }
    }
}