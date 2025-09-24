using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 점수 계산 시스템
/// [수정] 보너스/보석 점수 외부 공개 프로퍼티 추가
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    [System.Serializable]
    public class ScoreConfig
    {
        [Header("기본 점수")]
        public int perfectGemScore = 100;
        public int damagedGemScore = 70;
        public int heavyDamagedScore = 30;
        public int destroyedGemScore = 0;

        [Header("정확도 점수")]
        public int perfectStageBonus = 50;
        public int speedBonusMax = 25;
        public int accuracyBonusMax = 30;
        public int stageMultiplier = 1;

        [Header("페널티")]
        public int gemDestructionPenalty = -50;
    }

    [Header("점수 설정")]
    public ScoreConfig scoreConfig = new ScoreConfig();

    [Header("현재 점수 상태")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int stageScore = 0;
    [SerializeField] private int totalScore = 0;
    [SerializeField] private int highScore = 0;

    // 시스템 참조
    private GemProtectionSystem gemProtectionSystem;

    // 점수 기록
    private List<int> stageScores = new List<int>();
    private float stageStartTime = 0f;
    private List<float> stageAccuracies = new List<float>();
    public float AverageAccuracy { get; private set; } = 0f;

    // [추가] 점수 상세 정보를 외부에 제공하기 위한 프로퍼티
    public int LastCalculatedGemScore { get; private set; }
    public int LastCalculatedBonusScore { get; private set; }

    // 이벤트
    public event System.Action<int> OnScoreChanged;
    public event System.Action<int> OnNewHighScore;
    public event System.Action<int> OnStageScoreCalculated;
    public event System.Action<float> OnAverageAccuracyChanged;

    void Start()
    {
        LoadHighScore();
        gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();
    }

    public void AddAccuracy(float accuracy)
    {
        stageAccuracies.Add(accuracy);
        if (stageAccuracies.Count > 0)
        {
            AverageAccuracy = stageAccuracies.Average();
        }
        OnAverageAccuracyChanged?.Invoke(AverageAccuracy);
    }

    public int CalculateStageScore(int stageNumber, bool isPerfectStage)
    {
        if (gemProtectionSystem == null) gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();
        if (gemProtectionSystem == null) return 0;

        // [수정] 계산된 값을 프로퍼티에 저장
        LastCalculatedGemScore = CalculateGemScore();
        LastCalculatedBonusScore = CalculateBonusScore(isPerfectStage);
        int penaltyScore = CalculatePenaltyScore();

        stageScore = (LastCalculatedGemScore + LastCalculatedBonusScore + penaltyScore) * scoreConfig.stageMultiplier;
        stageScore = Mathf.Max(0, stageScore);

        LogDetailedScore(LastCalculatedGemScore, LastCalculatedBonusScore, penaltyScore);

        totalScore += stageScore;
        OnStageScoreCalculated?.Invoke(stageScore);

        return stageScore;
    }

    int CalculateGemScore()
    {
        if (gemProtectionSystem == null || gemProtectionSystem.GetAllGems().Length == 0) return 0;

        int totalGemScore = 0;
        foreach (var gem in gemProtectionSystem.GetAllGems())
        {
            if (gem.isDestroyed) totalGemScore += scoreConfig.destroyedGemScore;
            else if (gem.currentCondition >= 90f) totalGemScore += scoreConfig.perfectGemScore;
            else if (gem.currentCondition >= 70f) totalGemScore += scoreConfig.damagedGemScore;
            else totalGemScore += scoreConfig.heavyDamagedScore;
        }
        return totalGemScore / gemProtectionSystem.GetAllGems().Length;
    }

    int CalculateBonusScore(bool isPerfectStage)
    {
        int bonus = 0;

        if (isPerfectStage)
        {
            bonus += scoreConfig.perfectStageBonus;
        }

        float stageTime = Time.time - stageStartTime;
        if (stageTime < 180f)
        {
            float speedRatio = (180f - stageTime) / 180f;
            bonus += Mathf.RoundToInt(scoreConfig.speedBonusMax * speedRatio);
        }

        if (stageAccuracies.Count > 0)
        {
            int accuracyBonus = Mathf.Max(0, Mathf.RoundToInt(AverageAccuracy * scoreConfig.accuracyBonusMax));
            bonus += accuracyBonus;
        }

        return bonus;
    }

    int CalculatePenaltyScore()
    {
        if (gemProtectionSystem == null) return 0;

        int penalty = 0;
        foreach (var gem in gemProtectionSystem.GetAllGems())
        {
            if (gem.isDestroyed)
            {
                penalty += scoreConfig.gemDestructionPenalty;
            }
        }
        return penalty;
    }

    void LogDetailedScore(int gemScore, int bonusScore, int penaltyScore)
    {
        Debug.Log("--- 스테이지 점수 계산 상세 ---");
        Debug.Log($"보석 점수: {gemScore}");
        Debug.Log($"정확도 점수: {bonusScore}");
        Debug.Log($"페널티: {penaltyScore}");
        Debug.Log($"총 스테이지 점수: {stageScore}");
        Debug.Log("--------------------------");
    }

    public void StartNewStage()
    {
        stageStartTime = Time.time;
        stageScore = 0;
        stageAccuracies.Clear();
        AverageAccuracy = 0f;
        LastCalculatedGemScore = 0;
        LastCalculatedBonusScore = 0;
        OnAverageAccuracyChanged?.Invoke(AverageAccuracy);
    }

    void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }
}