using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 8일차: 기존 UI와 시스템들을 연결하는 UIManager
/// 힘 강도, 진행률, 점수, 타이머를 실시간으로 업데이트
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("힘 강도 표시")]
    public GameObject greenIndicator; // 초록색 표시등 (Safe)
    public GameObject redIndicator;   // 빨간색 표시등 (Warning)
    public Slider forceSlider;        // 힘 강도 슬라이더

    [Header("게임 정보")]
    public TextMeshProUGUI timeText;     // 남은 시간 표시
    public Slider progressSlider;        // 채굴 진행률 슬라이더
    public TextMeshProUGUI scoreText;    // 점수 표시

    [Header("힘 강도 설정")]
    [Range(0f, 1f)]
    public float safeThreshold = 0.55f; // 안전 임계값 (55%)

    [Header("게임 모드 설정")]
    public float gameTimeLimit = 300f; // 5분 (도전 모드용)
    public bool enableTimer = false;   // 타이머 활성화 여부

    // 시스템 참조
    private ForceDetector forceDetector;
    private ChunkCounter chunkCounter;
    private GemProtectionSystem gemProtectionSystem;

    // 게임 상태
    private float gameStartTime;
    private float currentScore = 0f;
    private bool gameActive = false;

    // 테스트용 힘 강도 오버라이드
    [Header("테스트 설정")]
    public bool enableTestMode = true; // 테스트 모드 활성화
    private float testForceValue = 0.1f; // 테스트용 힘 값 (10%)
    private bool isTestModeActive = false;

    void Start()
    {
        InitializeUIManager();
        SetupInitialValues();
    }

    void InitializeUIManager()
    {
        // 기존 시스템들 찾기
        forceDetector = FindFirstObjectByType<ForceDetector>();
        chunkCounter = FindFirstObjectByType<ChunkCounter>();
        gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();

        if (forceDetector == null)
        {
            Debug.LogWarning("ForceDetector를 찾을 수 없습니다!");
        }

        if (chunkCounter == null)
        {
            Debug.LogWarning("ChunkCounter를 찾을 수 없습니다!");
        }
        else
        {
            // 채굴 진행률 변화 이벤트 구독
            chunkCounter.OnChunkCountChanged += OnMiningProgressChanged;
            Debug.Log("ChunkCounter 이벤트 구독 완료");
        }

        if (gemProtectionSystem == null)
        {
            Debug.LogWarning("GemProtectionSystem을 찾을 수 없습니다!");
        }

        // 게임 시작 시간 설정
        gameStartTime = Time.time;
        gameActive = true;

        Debug.Log("UIManager 초기화 완료");
    }

    void SetupInitialValues()
    {
        // 초기 UI 값 설정
        if (forceSlider != null)
        {
            forceSlider.minValue = 0f;
            forceSlider.maxValue = 1f;
            forceSlider.value = 0f;
        }

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
        }

        // 초기 표시등 상태 (안전)
        if (greenIndicator != null) greenIndicator.SetActive(true);
        if (redIndicator != null) redIndicator.SetActive(false);

        // 초기 점수
        UpdateScoreDisplay(0f);

        // 초기 시간
        if (enableTimer)
        {
            UpdateTimeDisplay(gameTimeLimit);
        }
        else
        {
            UpdateTimeDisplay(0f); // 타이머 비활성화 시 00:00 표시
        }
    }

    void Update()
    {
        HandleTestInput(); // 테스트 입력 처리
        UpdateForceDisplay();
        UpdateTimeDisplay();
        CalculateAndUpdateScore();
    }

    /// <summary>
    /// 테스트용 키보드 입력 처리
    /// </summary>
    void HandleTestInput()
    {
        if (!enableTestMode) return;

        // Tab 키로 힘 강도 변경
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            CycleTestForce();
        }

        // T 키로 테스트 모드 토글
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleTestMode();
        }
    }

    /// <summary>
    /// 테스트 힘 강도 순환 (10% → 40% → 70% → 100% → 10%)
    /// </summary>
    void CycleTestForce()
    {
        if (testForceValue <= 0.1f)
        {
            testForceValue = 0.4f; // 10% → 40%
        }
        else if (testForceValue <= 0.4f)
        {
            testForceValue = 0.7f; // 40% → 70%
        }
        else if (testForceValue <= 0.7f)
        {
            testForceValue = 1.0f; // 70% → 100%
        }
        else
        {
            testForceValue = 0.1f; // 100% → 10%
        }

        isTestModeActive = true;

        Debug.Log($"테스트 힘 강도: {testForceValue * 100:F0}%");
    }

    /// <summary>
    /// 테스트 모드 토글
    /// </summary>
    void ToggleTestMode()
    {
        isTestModeActive = !isTestModeActive;

        if (isTestModeActive)
        {
            Debug.Log("테스트 모드 활성화 - Tab키로 힘 강도 조절");
        }
        else
        {
            Debug.Log("테스트 모드 비활성화 - 실제 ForceDetector 사용");
        }
    }

    /// <summary>
    /// 힘 강도 표시 업데이트
    /// </summary>
    void UpdateForceDisplay()
    {
        float currentForce;

        // 테스트 모드 활성화 시 테스트 값 사용
        if (enableTestMode && isTestModeActive)
        {
            currentForce = testForceValue;
        }
        else if (forceDetector != null)
        {
            currentForce = forceDetector.GetCurrentForce();
        }
        else
        {
            currentForce = 0f;
        }

        // 슬라이더 값 업데이트
        if (forceSlider != null)
        {
            forceSlider.value = currentForce;
        }

        // 안전/위험 표시등 업데이트 (55% 기준)
        bool isSafe = currentForce < safeThreshold;

        if (greenIndicator != null)
        {
            greenIndicator.SetActive(isSafe);
        }

        if (redIndicator != null)
        {
            redIndicator.SetActive(!isSafe);
        }

        // 테스트 모드 활성화 시 UI에 표시
        if (enableTestMode && isTestModeActive)
        {
            // 슬라이더 색상을 노란색으로 변경하여 테스트 모드임을 표시
            if (forceSlider != null)
            {
                var fillImage = forceSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
                if (fillImage != null)
                {
                    fillImage.color = Color.yellow;
                }
            }
        }
        else
        {
            // 일반 모드 시 원래 색상 복원
            if (forceSlider != null)
            {
                var fillImage = forceSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
                if (fillImage != null)
                {
                    fillImage.color = Color.white;
                }
            }
        }
    }

    /// <summary>
    /// 시간 표시 업데이트
    /// </summary>
    void UpdateTimeDisplay()
    {
        if (timeText == null) return;

        float displayTime;

        if (enableTimer && gameActive)
        {
            // 도전 모드: 남은 시간 표시
            float elapsedTime = Time.time - gameStartTime;
            float remainingTime = Mathf.Max(0f, gameTimeLimit - elapsedTime);
            displayTime = remainingTime;

            // 시간 초과 체크
            if (remainingTime <= 0f)
            {
                OnTimeUp();
            }
        }
        else
        {
            // 연습 모드: 경과 시간 표시
            displayTime = Time.time - gameStartTime;
        }

        UpdateTimeDisplay(displayTime);
    }

    /// <summary>
    /// 시간 텍스트 업데이트
    /// </summary>
    void UpdateTimeDisplay(float timeInSeconds)
    {
        if (timeText == null) return;

        int minutes = (int)(timeInSeconds / 60);
        int seconds = (int)(timeInSeconds % 60);

        // 시간 초과 시 빨간색으로 표시
        if (enableTimer && timeInSeconds <= 30f)
        {
            timeText.color = Color.red;
        }
        else
        {
            timeText.color = Color.white;
        }

        timeText.text = $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// 점수 계산 및 업데이트
    /// </summary>
    void CalculateAndUpdateScore()
    {
        if (gemProtectionSystem == null) return;

        float newScore = CalculateCurrentScore();

        // 점수가 변경되었을 때만 UI 업데이트 (성능 최적화)
        if (Mathf.Abs(newScore - currentScore) > 0.1f)
        {
            currentScore = newScore;
            UpdateScoreDisplay(currentScore);
        }
    }

    /// <summary>
    /// 현재 점수 계산
    /// </summary>
    float CalculateCurrentScore()
    {
        float totalScore = 0f;

        // 보석 상태에 따른 점수 계산
        if (gemProtectionSystem != null)
        {
            var gems = gemProtectionSystem.GetAllGems();

            foreach (var gem in gems)
            {
                if (gem.gemObject == null) continue;

                // 보석 상태별 점수
                if (gem.isDestroyed)
                {
                    totalScore += 0f; // 파괴된 보석은 0점
                }
                else
                {
                    // 상태에 따른 점수 (100점 만점)
                    float gemScore = gem.currentCondition; // 0~100
                    totalScore += gemScore;
                }
            }
        }
        return totalScore;
    }

    /// <summary>
    /// 점수 표시 업데이트
    /// </summary>
    void UpdateScoreDisplay(float score)
    {
        if (scoreText == null) return;

        scoreText.text = Mathf.RoundToInt(score).ToString();

        // 점수에 따른 색상 변경
        if (score >= 90f)
        {
            scoreText.color = Color.green; // 고득점: 초록색
        }
        else if (score >= 50f)
        {
            scoreText.color = Color.yellow; // 중간점수: 노란색
        }
        else if (score >= 10f)
        {
            scoreText.color = Color.white; // 기본: 흰색
        }
        else
        {
            scoreText.color = Color.red; // 저득점: 빨간색
        }
    }

    /// <summary>
    /// 채굴 진행률 변화 이벤트 핸들러
    /// </summary>
    void OnMiningProgressChanged(int activeChunks, int destroyedChunks, float progress)
    {
        // 진행률 슬라이더 업데이트
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        // 진행률에 따른 시각적 피드백
        if (progress >= 0.7f) // 70% 이상일 때 위험 구간
        {
            ShowDangerZoneWarning();
        }

        Debug.Log($"UI 업데이트: 진행률 {progress * 100f:F1}%, 점수 {currentScore:F0}");
    }

    /// <summary>
    /// 위험 구간 경고 표시
    /// </summary>
    void ShowDangerZoneWarning()
    {
        // 현재 힘 강도 가져오기 (테스트 모드 고려)
        float currentForce;
        if (enableTestMode && isTestModeActive)
        {
            currentForce = testForceValue;
        }
        else if (forceDetector != null)
        {
            currentForce = forceDetector.GetCurrentForce();
        }
        else
        {
            currentForce = 0f;
        }

        // 강한 힘을 사용 중인지 확인
        if (currentForce >= safeThreshold)
        {
            // 빨간 표시등 깜빡임 효과
            StartCoroutine(BlinkRedIndicator());
        }
    }

    /// <summary>
    /// 빨간 표시등 깜빡임 효과
    /// </summary>
    System.Collections.IEnumerator BlinkRedIndicator()
    {
        if (redIndicator == null) yield break;

        Color originalColor = redIndicator.GetComponent<Image>()?.color ?? Color.red;
        Image redImage = redIndicator.GetComponent<Image>();

        if (redImage == null) yield break;

        // 3번 깜빡임
        for (int i = 0; i < 3; i++)
        {
            redImage.color = Color.red;
            yield return new WaitForSeconds(0.2f);
            redImage.color = new Color(1f, 0f, 0f, 0.3f); // 반투명
            yield return new WaitForSeconds(0.2f);
        }

        redImage.color = originalColor; // 원래 색상 복원
    }

    /// <summary>
    /// 시간 초과 시 호출
    /// </summary>
    void OnTimeUp()
    {
        if (!gameActive) return;

        gameActive = false;
        Debug.Log("시간 초과! 게임 종료");

        // 게임 종료 처리 (필요시 추가)
        // 예: 결과 화면 표시, 최종 점수 저장 등
    }

    /// <summary>
    /// 게임 모드 설정
    /// </summary>
    public void SetGameMode(bool timerEnabled, float timeLimit = 300f)
    {
        enableTimer = timerEnabled;
        gameTimeLimit = timeLimit;
        gameStartTime = Time.time;
        gameActive = true;

        Debug.Log($"게임 모드 설정: 타이머 {(enableTimer ? "활성화" : "비활성화")}, 제한시간 {timeLimit}초");
    }

    /// <summary>
    /// 게임 재시작
    /// </summary>
    public void RestartGame()
    {
        gameStartTime = Time.time;
        currentScore = 0f;
        gameActive = true;

        // UI 초기화
        SetupInitialValues();

        Debug.Log("게임 재시작됨");
    }

    /// <summary>
    /// 현재 게임 상태 정보 반환
    /// </summary>
    public (float score, float time, float progress, bool isSafe) GetGameStatus()
    {
        float currentTime = enableTimer ?
            Mathf.Max(0f, gameTimeLimit - (Time.time - gameStartTime)) :
            Time.time - gameStartTime;

        float progress = chunkCounter?.MiningProgress ?? 0f;

        // 테스트 모드 고려하여 안전 상태 확인
        float currentForce;
        if (enableTestMode && isTestModeActive)
        {
            currentForce = testForceValue;
        }
        else
        {
            currentForce = forceDetector != null ? forceDetector.GetCurrentForce() : 0f;
        }

        bool isSafe = currentForce < safeThreshold;

        return (currentScore, currentTime, progress, isSafe);
    }

    /// <summary>
    /// 테스트용 힘 강도 값 반환 (외부에서 사용할 수 있도록)
    /// </summary>
    public float GetTestForceValue()
    {
        if (enableTestMode && isTestModeActive)
        {
            return testForceValue;
        }
        return forceDetector != null ? forceDetector.GetCurrentForce() : 0f;
    }

    /// <summary>
    /// 디버그: 현재 UI 상태 출력
    /// </summary>
    [ContextMenu("UI 상태 출력")]
    public void PrintUIStatus()
    {
        var status = GetGameStatus();

        Debug.Log("=== UIManager 상태 ===");
        Debug.Log($"점수: {status.score:F0}");
        Debug.Log($"시간: {status.time:F1}초");
        Debug.Log($"진행률: {status.progress * 100f:F1}%");
        Debug.Log($"안전 상태: {(status.isSafe ? "안전" : "위험")}");
        Debug.Log($"게임 활성: {gameActive}");
        Debug.Log("==================");
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (chunkCounter != null)
        {
            chunkCounter.OnChunkCountChanged -= OnMiningProgressChanged;
        }
    }
}