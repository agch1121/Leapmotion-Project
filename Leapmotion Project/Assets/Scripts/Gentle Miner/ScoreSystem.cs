using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 점수 계산 및 저장 시스템 (기획서의 핵심 클래스)
/// 보석 상태에 따른 점수 계산, 하이스코어 관리, 성취도 추적
/// [수정] 정확도 기반 보너스 점수 시스템 추가
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    [System.Serializable]
    public class ScoreConfig
    {
        [Header("기본 점수")]
        public int perfectGemScore = 100;    // 완벽한 보석 점수
        public int damagedGemScore = 70;     // 손상된 보석 점수
        public int heavyDamagedScore = 30;   // 심하게 손상된 보석 점수
        public int destroyedGemScore = 0;    // 파괴된 보석 점수

        [Header("보너스 점수")]
        public int perfectStageBonus = 50;   // 100% 완료 보너스
        public int speedBonusMax = 25;       // 빠른 완료 보너스 최대값
        public int accuracyBonusMax = 20;    // 정확도 보너스 최대값
        public int stageMultiplier = 1;      // 스테이지별 배율

        [Header("페널티")]
        public int gemDestructionPenalty = -50; // 보석 파괴 페널티
    }

    [Header("점수 설정")]
    public ScoreConfig scoreConfig = new ScoreConfig();

    [Header("현재 점수 상태")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int stageScore = 0;
    [SerializeField] private int totalScore = 0;
    [SerializeField] private int highScore = 0;

    [Header("통계")]
    [SerializeField] private int totalGemsFound = 0;
    [SerializeField] private int perfectGems = 0;
    [SerializeField] private int damagedGems = 0;
    [SerializeField] private int destroyedGems = 0;
    [SerializeField] private float totalPlayTime = 0f;

    // 시스템 참조
    private GemProtectionSystem gemProtectionSystem;

    // 점수 기록
    private List<int> stageScores = new List<int>();
    private float stageStartTime = 0f;

    // [추가] 정확도 기록
    private List<float> stageAccuracies = new List<float>();
    public float AverageAccuracy { get; private set; } = 0f;


    // 이벤트
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnNewHighScore;
    public System.Action<int> OnStageScoreCalculated;
    public System.Action<float> OnAverageAccuracyChanged; // [추가]

    // 프로퍼티
    public int CurrentScore => currentScore;
    public int TotalScore => totalScore;
    public int HighScore => highScore;
    public float TotalPlayTime => totalPlayTime;

    void Start()
    {
        InitializeScoreSystem();
    }

    void InitializeScoreSystem()
    {
        // 하이스코어 로드
        LoadHighScore();

        // 시스템 참조
        gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();

        // 스테이지 시작 시간 기록
        stageStartTime = Time.time;

        Debug.Log("ScoreSystem 초기화 완료");
    }

    void Update()
    {
        // 플레이 시간 추적
        totalPlayTime += Time.deltaTime;
    }

    /// <summary>
    /// [추가] 타격 정확도를 기록하고 평균을 업데이트
    /// </summary>
    public void AddAccuracy(float accuracy)
    {
        stageAccuracies.Add(accuracy);
        AverageAccuracy = stageAccuracies.Average();
        OnAverageAccuracyChanged?.Invoke(AverageAccuracy);
        Debug.Log($"정확도 추가: {accuracy:P0}. 현재 평균 정확도: {AverageAccuracy:P0}");
    }

    /// <summary>
    /// 점수 추가
    /// </summary>
    public void AddScore(int points)
    {
        currentScore += points;
        totalScore += points;

        OnScoreChanged?.Invoke(currentScore);

        // 하이스코어 체크
        if (totalScore > highScore)
        {
            highScore = totalScore;
            SaveHighScore();
            OnNewHighScore?.Invoke(highScore);
            Debug.Log($"새로운 최고 점수: {highScore}");
        }

        Debug.Log($"점수 추가: +{points} (현재: {currentScore}, 총점: {totalScore})");
    }

    /// <summary>
    /// 스테이지 점수 계산
    /// </summary>
    public int CalculateStageScore(int stageNumber, bool isPerfectStage = false)
    {
        if (gemProtectionSystem == null)
        {
            Debug.LogWarning("GemProtectionSystem이 없어 기본 점수를 사용합니다.");
            return scoreConfig.perfectGemScore;
        }

        int gemScore = CalculateGemScore();
        int bonusScore = CalculateBonusScore(stageNumber, isPerfectStage);
        int penaltyScore = CalculatePenaltyScore();

        stageScore = (gemScore + bonusScore + penaltyScore) * scoreConfig.stageMultiplier * stageNumber;
        stageScore = Mathf.Max(0, stageScore); // 음수 방지

        // 스테이지 점수 기록
        while (stageScores.Count < stageNumber)
        {
            stageScores.Add(0);
        }
        stageScores[stageNumber - 1] = stageScore;

        OnStageScoreCalculated?.Invoke(stageScore);

        Debug.Log($"스테이지 {stageNumber} 점수 계산 완료: {stageScore}점");
        LogDetailedScore(gemScore, bonusScore, penaltyScore);

        return stageScore;
    }

    int CalculateGemScore()
    {
        var gems = gemProtectionSystem.GetAllGems();
        int totalGemScore = 0;

        // 통계 초기화
        int perfectCount = 0, damagedCount = 0, heavyDamagedCount = 0, destroyedCount = 0;

        foreach (var gem in gems)
        {
            int gemScore = 0;

            if (gem.isDestroyed)
            {
                gemScore = scoreConfig.destroyedGemScore;
                destroyedCount++;
            }
            else if (gem.currentCondition >= 90f)
            {
                gemScore = scoreConfig.perfectGemScore;
                perfectCount++;
            }
            else if (gem.currentCondition >= 70f)
            {
                gemScore = scoreConfig.damagedGemScore;
                damagedCount++;
            }
            else if (gem.currentCondition >= 30f)
            {
                gemScore = scoreConfig.heavyDamagedScore;
                heavyDamagedCount++;
            }
            else
            {
                gemScore = scoreConfig.destroyedGemScore;
                destroyedCount++;
            }

            totalGemScore += gemScore;
        }

        // 통계 업데이트
        totalGemsFound += gems.Length;
        perfectGems += perfectCount;
        damagedGems += damagedCount;
        destroyedGems += destroyedCount;

        Debug.Log($"보석 점수: 완벽 {perfectCount}개, 손상 {damagedCount}개, 심각 {heavyDamagedCount}개, 파괴 {destroyedCount}개");

        return totalGemScore;
    }

    int CalculateBonusScore(int stageNumber, bool isPerfectStage)
    {
        int bonus = 0;

        // 완벽 완주 보너스
        if (isPerfectStage)
        {
            bonus += scoreConfig.perfectStageBonus;
            Debug.Log($"완벽 완주 보너스: +{scoreConfig.perfectStageBonus}");
        }

        // 빠른 완료 보너스
        float stageTime = Time.time - stageStartTime;
        int speedBonus = CalculateSpeedBonus(stageTime);
        bonus += speedBonus;

        // [추가] 정확도 보너스
        int accuracyBonus = 0;
        if (stageAccuracies.Count > 0)
        {
            // 평균 정확도가 50%일때 0점, 100%일때 최대 보너스, 0%일때 최대 페널티
            accuracyBonus = Mathf.RoundToInt((AverageAccuracy - 0.5f) * 2 * scoreConfig.accuracyBonusMax);
            Debug.Log($"정확도 보너스: +{accuracyBonus} (평균: {AverageAccuracy:P0})");
        }
        bonus += accuracyBonus;

        return bonus;
    }

    int CalculateSpeedBonus(float completionTime)
    {
        // 기준 시간: 3분 (180초)
        float baseTime = 180f;

        if (completionTime < baseTime)
        {
            float speedRatio = (baseTime - completionTime) / baseTime;
            int speedBonus = Mathf.RoundToInt(scoreConfig.speedBonusMax * speedRatio);
            Debug.Log($"빠른 완료 보너스: +{speedBonus} (완료 시간: {completionTime:F1}초)");
            return speedBonus;
        }

        return 0;
    }

    int CalculatePenaltyScore()
    {
        if (gemProtectionSystem == null) return 0;

        int penalty = 0;
        var gems = gemProtectionSystem.GetAllGems();

        foreach (var gem in gems)
        {
            if (gem.isDestroyed)
            {
                penalty += scoreConfig.gemDestructionPenalty;
            }
        }

        if (penalty < 0)
        {
            Debug.Log($"보석 파괴 페널티: {penalty}");
        }

        return penalty;
    }

    void LogDetailedScore(int gemScore, int bonusScore, int penaltyScore)
    {
        Debug.Log("=== 점수 계산 상세 ===");
        Debug.Log($"보석 점수: {gemScore}");
        Debug.Log($"보너스 점수: {bonusScore}");
        Debug.Log($"페널티 점수: {penaltyScore}");
        Debug.Log($"최종 스테이지 점수: {stageScore}");
        Debug.Log("====================");
    }

    /// <summary>
    /// 새 스테이지 시작
    /// </summary>
    public void StartNewStage()
    {
        stageStartTime = Time.time;
        stageScore = 0;

        // [추가] 정확도 기록 리셋
        stageAccuracies.Clear();
        AverageAccuracy = 0f;
        OnAverageAccuracyChanged?.Invoke(AverageAccuracy);

        Debug.Log("새 스테이지 점수 추적 시작");
    }

    /// <summary>
    /// 게임 리셋
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
        stageScore = 0;
        stageScores.Clear();
        stageStartTime = Time.time;

        OnScoreChanged?.Invoke(currentScore);

        Debug.Log("점수 시스템 리셋");
    }

    /// <summary>
    /// 전체 게임 리셋 (총점까지 리셋)
    /// </summary>
    public void ResetAllScores()
    {
        ResetScore();
        totalScore = 0;
        totalGemsFound = 0;
        perfectGems = 0;
        damagedGems = 0;
        destroyedGems = 0;
        totalPlayTime = 0f;

        Debug.Log("모든 점수 및 통계 리셋");
    }

    /// <summary>
    /// 하이스코어 저장
    /// </summary>
    void SaveHighScore()
    {
        PlayerPrefs.SetInt("HighScore", highScore);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 하이스코어 로드
    /// </summary>
    void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        Debug.Log($"최고 점수 로드: {highScore}");
    }

    /// <summary>
    /// 게임 통계 반환
    /// </summary>
    public (int total, int perfect, int damaged, int destroyed, float playTime) GetGameStats()
    {
        return (totalGemsFound, perfectGems, damagedGems, destroyedGems, totalPlayTime);
    }

    /// <summary>
    /// 스테이지별 점수 반환
    /// </summary>
    public List<int> GetStageScores()
    {
        return new List<int>(stageScores);
    }

    /// <summary>
    /// 평균 스테이지 점수 계산
    /// </summary>
    public float GetAverageStageScore()
    {
        if (stageScores.Count == 0) return 0f;

        int total = 0;
        foreach (int score in stageScores)
        {
            total += score;
        }

        return (float)total / stageScores.Count;
    }

    /// <summary>
    /// 성취도 계산 (퍼센트)
    /// </summary>
    public float GetAchievementPercentage()
    {
        if (totalGemsFound == 0) return 0f;

        // 완벽한 보석 비율을 성취도로 사용
        return ((float)perfectGems / totalGemsFound) * 100f;
    }

    [ContextMenu("현재 점수 상태 출력")]
    public void PrintScoreStatus()
    {
        Debug.Log("=== 점수 시스템 상태 ===");
        Debug.Log($"현재 점수: {currentScore}");
        Debug.Log($"스테이지 점수: {stageScore}");
        Debug.Log($"총 점수: {totalScore}");
        Debug.Log($"최고 점수: {highScore}");
        Debug.Log($"발견한 보석: {totalGemsFound}개");
        Debug.Log($"완벽한 보석: {perfectGems}개");
        Debug.Log($"손상된 보석: {damagedGems}개");
        Debug.Log($"파괴된 보석: {destroyedGems}개");
        Debug.Log($"플레이 시간: {totalPlayTime:F1}초");
        Debug.Log($"성취도: {GetAchievementPercentage():F1}%");
        Debug.Log($"평균 정확도: {AverageAccuracy:P0}");
        Debug.Log("========================");
    }

    [ContextMenu("점수 설정 리셋")]
    public void ResetScoreConfig()
    {
        scoreConfig = new ScoreConfig();
        Debug.Log("점수 설정이 기본값으로 리셋되었습니다.");
    }

    [ContextMenu("하이스코어 삭제")]
    public void DeleteHighScore()
    {
        PlayerPrefs.DeleteKey("HighScore");
        highScore = 0;
        Debug.Log("하이스코어가 삭제되었습니다.");
    }
}