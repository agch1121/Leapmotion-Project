using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 게임의 모든 UI 요소(타이머, 진행률, 점수, 결과 창 등) 관리 및 업데이트
/// GameManager의 상태 변화나 다른 시스템의 이벤트에 따라 적절한 UI 표시/숨김
/// </summary>
public class UIManager : MonoBehaviour
{
    #region UI 변수 선언

    [Header("UI 패널들")]
    public GameObject gameplayPanel; // 게임 플레이 중 활성화될 UI 패널
    public GameObject resultPanel;   // 게임 종료 후 활성화될 결과 UI 패널

    [Header("게임플레이 UI 요소")]
    public TextMeshProUGUI timeTitle;       // 시간 표시 위의 제목 (예: "남은 시간", "채굴 성공!")
    public TextMeshProUGUI timeText;        // 실제 남은 시간 표시 텍스트 (00:00 형식)
    public Slider progressSlider;           // 채굴 진행률 표시 슬라이더
    public TextMeshProUGUI progressTitle;   // 진행률 표시 텍스트 (예: "진행률: 50.0%")
    public TextMeshProUGUI score;           // 현재 점수 표시 텍스트

    [Header("힘 표시 시스템")]
    public Slider forceSlider;          // 현재 플레이어의 힘 표시 슬라이더
    public Image safeImage;             // 힘 '안전' 단계 표시 이미지
    public Image warningImage;          // 힘 '주의' 또는 '위험' 단계 표시 이미지
    public Image successPointImage;     // 진행률 슬라이더 위 '성공' 기준점 표시 이미지

    [Header("정확도 표시 UI")]
    public TextMeshProUGUI lastAccuracyText;    // 가장 최근 타격의 정확도
    public TextMeshProUGUI averageAccuracyText; // 현재 스테이지의 평균 정확도

    [Header("보석 상태 UI")]
    public TextMeshProUGUI gemStatusText;   // 모든 보석의 이름과 상태 동시 표시 텍스트

    [Header("결과 패널 UI")]
    public TextMeshProUGUI resultTitleText;     // 결과 창 제목 (예: "채굴 성공", "채굴 실패")
    public TextMeshProUGUI resultMessageText;   // 결과 상세 메시지 (점수 등)
    public GameObject quitButton;
    public GameObject retryButton;
    public GameObject nextButton;
    public TextMeshProUGUI nextButtonText;      // '다음 스테이지' 버튼 텍스트

    [Header("게임 설정")]
    [Range(60f, 600f)]
    public float gameDuration = 180f; // 기본 게임 시간

    [Header("힘 상태 색상")]
    public Color safeForceColor = Color.green;
    public Color mediumForceColor = Color.yellow;
    public Color dangerForceColor = Color.red;

    [Header("타이머 설정")]
    public float defaultTimeLimit = 180f;       // 기본 제한 시간
    public Color normalTimeColor = Color.white;   // 타이머 일반 상태 색상
    public Color warningTimeColor = Color.yellow; // 타이머 경고 상태 색상
    public Color dangerTimeColor = Color.red;     // 타이머 위험 상태 색상

    #endregion

    #region 내부 변수

    // --- 시스템 참조 ---
    private GameManager gameManager;
    private ScoreSystem scoreSystem;
    private ForceCalculator forceCalculator;

    // --- 상태 변수 ---
    private float currentTimeLimit = 180f;  // 현재 스테이지의 실제 제한 시간
    private float gameStartTime = 0f;       // 게임 시작 시점의 시간(Time.time)
    private float remainingTime = 0f;       // 남은 시간 계산용 변수
    private bool isTimeWarning = false;     // 시간 경고 상태 플래그
    private bool isGameEnded = false;       // 게임 종료 여부 플래그

    #endregion

    #region 초기화

    void Start()
    {
        InitializeUIManager();
    }

    /// <summary>
    /// UIManager 초기화. 시스템 참조, 이벤트 구독, UI 상태 초기화 수행
    /// </summary>
    void InitializeUIManager()
    {
        // 주요 시스템 컴포넌트 참조 찾기
        gameManager = FindFirstObjectByType<GameManager>();
        forceCalculator = FindFirstObjectByType<ForceCalculator>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();

        // 이벤트 구독 설정
        SubscribeToEvents();

        // UI 초기 상태 설정
        SetInitialUIState();
    }

    /// <summary>
    /// 다른 시스템의 주요 이벤트 구독 설정
    /// 게임 상태, 점수 등 변경 시 관련 UI 업데이트 함수 자동 호출
    /// </summary>
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

    /// <summary>
    /// 게임 시작 시 UI 초기 상태 설정
    /// 게임플레이 패널 활성화, 결과 패널 비활성화, UI 값 리셋
    /// </summary>
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

    #endregion

    #region UI 업데이트

    /// <summary>
    /// 매 프레임 호출. 힘 게이지와 타이머 UI 지속적 업데이트
    /// </summary>
    void Update()
    {
        UpdateForceDisplay();
        UpdateTimeDisplay();
    }

    /// <summary>
    /// 게임 플레이 관련 UI 요소(진행률, 정확도 등) 초기 값으로 리셋
    /// </summary>
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

    /// <summary>
    /// 모든 보석의 이름과 상태(체력)를 UI 텍스트로 한 번에 업데이트
    /// </summary>
    /// <param name="gems">표시할 보석 데이터 배열</param>
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

        // 첫 줄: 보석 이름 (예: 다이아몬드 | 에메랄드)
        for (int i = 0; i < gems.Length; i++)
        {
            string simpleName = gems[i].gemName.Split(' ')[0];
            sb.Append(simpleName);
            if (i < gems.Length - 1) sb.Append(" | ");
        }
        sb.AppendLine();

        // 둘째 줄: 보석 상태 (예: 100% | 파괴)
        for (int i = 0; i < gems.Length; i++)
        {
            string status = gems[i].isDestroyed ? "<color=red>파괴</color>" : $"{gems[i].currentCondition:F0}%";
            sb.Append(status);
            if (i < gems.Length - 1) sb.Append(" | ");
        }
        gemStatusText.text = sb.ToString();
    }

    /// <summary>
    /// ForceCalculator에서 현재 힘 값을 받아 UI(슬라이더, 상태 이미지) 업데이트
    /// </summary>
    void UpdateForceDisplay()
    {
        if (forceCalculator == null) return;
        float currentForce = forceCalculator.CurrentForce;
        var forceLevel = forceCalculator.CurrentForceLevel;
        UpdateForceUI(currentForce, forceLevel);
    }

    /// <summary>
    /// 남은 시간 계산, 타이머 텍스트 및 색상 업데이트, 시간 초과 시 OnTimeUp 호출
    /// </summary>
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

    /// <summary>
    /// 남은 시간에 따라 타이머 텍스트 색상 변경 (일반/경고/위험)
    /// </summary>
    /// <param name="timeInSeconds">남은 시간(초)</param>
    void UpdateTimeColor(float timeInSeconds)
    {
        if (timeText == null) return;
        Color targetColor;
        if (timeInSeconds <= 10f)
        {
            targetColor = dangerTimeColor;
            if (timeInSeconds <= 5f)
            {
                // 5초 이하일 때 깜빡임 효과
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

    /// <summary>
    /// 초 단위 시간을 "mm:ss" 형식의 문자열로 변환하여 UI 텍스트 업데이트
    /// </summary>
    /// <param name="timeInSeconds">표시할 시간(초)</param>
    void UpdateTimeText(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        if (timeText != null)
        {
            // 숫자를 항상 두 자리로 표시 (예: 1:5 -> 01:05)
            timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    /// <summary>
    /// 힘 값과 단계를 받아 슬라이더와 상태 이미지 업데이트
    /// </summary>
    void UpdateForceUI(float force, ForceCalculator.ForceLevel level)
    {
        if (forceSlider != null) forceSlider.value = force;
        UpdateForceStatusImages(level);
    }

    /// <summary>
    /// 힘 단계에 따라 '안전' 또는 '주의/위험' 이미지 활성화/비활성화
    /// </summary>
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

    /// <summary>
    /// 진행률 슬라이더 위 '성공' 기준점(90%) 이미지 위치 설정
    /// </summary>
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

    /// <summary>
    /// 가장 최근 타격 정확도 UI 업데이트
    /// </summary>
    public void UpdateLastAccuracyUI(float accuracy)
    {
        if (lastAccuracyText == null) return;
        lastAccuracyText.text = accuracy > 0 ? $"정확도: {accuracy:P0}" : "정확도: -";
    }

    /// <summary>
    /// 현재 스테이지 평균 정확도 UI 업데이트
    /// </summary>
    public void UpdateAverageAccuracyUI(float accuracy)
    {
        if (averageAccuracyText == null) return;
        averageAccuracyText.text = accuracy > 0 ? $"평균 정확도: {accuracy:P0}" : "평균 정확도: -";
    }

    /// <summary>
    /// 진행률 슬라이더와 텍스트 UI 업데이트
    /// </summary>
    void UpdateProgressUI(float progress)
    {
        if (progressSlider != null) progressSlider.value = progress;
        if (progressTitle != null) progressTitle.text = $"진행률: {progress * 100f:F1}%";
    }

    /// <summary>
    /// 점수 텍스트 UI 업데이트
    /// </summary>
    void UpdateScoreUI(int scoreValue)
    {
        if (this.score != null) this.score.text = $"점수: {scoreValue}";
    }

    /// <summary>
    /// 시간 표시 위의 제목 텍스트 UI 업데이트
    /// </summary>
    void UpdateTimeUI(string status)
    {
        if (timeTitle != null) timeTitle.text = status;
    }

    #endregion

    #region 이벤트 핸들러

    /// <summary>
    /// 게임 상태 변경 시 호출. 현재 상태에 맞는 UI 표시
    /// </summary>
    /// <param name="newState">새로운 게임 상태</param>
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
                if (nextButton != null) nextButton.SetActive(true); // 성공 시 '다음' 버튼 활성화
                isGameEnded = true;
                break;
            case GameManager.GameState.Perfect:
                UpdateTimeUI("완벽한 채굴!");
                ShowResultPanelWithDetails("완벽한 채굴!", "", gameManager.GetScoreSystem());
                if (nextButton != null) nextButton.SetActive(true); // 완벽 성공 시 '다음' 버튼 활성화
                isGameEnded = true;
                break;
            case GameManager.GameState.Failed:
                string failMessage = remainingTime <= 0 ? "시간 초과" : "보석 파괴";
                UpdateTimeUI("채굴 실패..");
                ShowResultPanelWithDetails("채굴 실패", failMessage, null);
                if (nextButton != null) nextButton.SetActive(false); // 실패 시 '다음' 버튼 비활성화
                isGameEnded = true;
                break;
        }
    }

    /// <summary>
    /// 게임 종료 시 결과 패널 활성화, 제목/메시지/상세 점수 표시
    /// </summary>
    /// <param name="title">결과 패널 제목</param>
    /// <param name="message">결과 메시지 (예: 시간 초과)</param>
    /// <param name="scoreSystemRef">점수 정보를 가져올 ScoreSystem 참조</param>
    void ShowResultPanelWithDetails(string title, string message, ScoreSystem scoreSystemRef)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);
        if (resultTitleText != null) resultTitleText.text = title;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(message);

        // ScoreSystem 참조가 있으면 상세 점수 내역 추가
        if (scoreSystemRef != null)
        {
            sb.AppendLine();
            sb.AppendLine($"보석 점수: {scoreSystemRef.LastCalculatedGemScore}점");
            sb.AppendLine($"정확도 점수: {scoreSystemRef.LastCalculatedBonusScore}점");
            sb.AppendLine($"(평균 정확도: {scoreSystemRef.AverageAccuracy:P0})");
        }

        if (resultMessageText != null) resultMessageText.text = sb.ToString();
    }

    /// <summary>
    /// 스테이지 변경 시 호출. UI의 스테이지 텍스트 업데이트 및 타이머 재설정
    /// </summary>
    public void OnStageChanged(int newStage)
    {
        if (timeTitle != null) timeTitle.text = $"스테이지 {newStage}";
        SetupTimerForCurrentStage();
    }

    /// <summary>
    /// ScoreSystem에서 평균 정확도 변경 시 호출되는 이벤트 핸들러
    /// </summary>
    private void OnAverageAccuracyChanged(float newAverage)
    {
        UpdateAverageAccuracyUI(newAverage);
    }

    /// <summary>
    /// 제한 시간 초과 시 호출. GameManager에 시간 초과 알림
    /// </summary>
    void OnTimeUp()
    {
        if (gameManager != null)
        {
            // 진행률이 성공 기준을 넘으면 GameManager가 성공으로 처리하므로 여기선 제외
            if (gameManager.CurrentProgress >= 0.7f) { /* GameManager가 처리 */ }
            else { gameManager.OnTimeUp(); } // 70% 미만일 때만 시간 초과 실패 처리
        }
        isGameEnded = true;
    }

    /// <summary>
    /// 현재 스테이지에 맞는 제한 시간 설정
    /// </summary>
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

    /// <summary>
    /// 게임플레이 UI 표시, 결과 패널 숨김
    /// </summary>
    void ShowGameplayUI()
    {
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // --- 간단한 이벤트 핸들러 래퍼 ---
    public void OnProgressChanged(float progress) { UpdateProgressUI(progress); }
    public void OnScoreChanged(int newScore) { UpdateScoreUI(newScore); }

    #endregion

    #region 해제

    /// <summary>
    /// 오브젝트 파괴 시, 구독했던 모든 이벤트 해제 (메모리 누수 방지)
    /// </summary>
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

    #endregion
}