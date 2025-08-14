using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 게임 점수 계산 및 관리 시스템.
/// 보석 상태, 클리어 시간, 타격 정확도를 종합하여 스테이지 점수 산출 및 최고 점수 기록.
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    #region 점수 설정 및 상태

    /// <summary>
    /// 점수 계산에 사용될 설정 값들의 구조체
    /// </summary>
    [System.Serializable]
    public class ScoreConfig
    {
        [Header("기본 점수")]
        public int perfectGemScore = 100;      // 완벽 상태 보석 점수
        public int damagedGemScore = 70;       // 손상 상태 보석 점수
        public int heavyDamagedScore = 30;     // 심한 손상 상태 보석 점수
        public int destroyedGemScore = 0;        // 파괴된 보석 점수

        [Header("보너스 점수")]
        public int perfectStageBonus = 50;     // 100% 클리어 시 보너스
        public int speedBonusMax = 25;         // 클리어 시간 보너스 (최대)
        public int accuracyBonusMax = 30;      // 타격 정확도 보너스 (최대)
        public int stageMultiplier = 1;        // 스테이지별 점수 배율

        [Header("페널티")]
        public int gemDestructionPenalty = -50; // 보석 파괴 시 페널티
    }

    [Header("점수 설정")]
    public ScoreConfig scoreConfig = new ScoreConfig(); // 인스펙터에서 설정할 점수 구성

    [Header("현재 점수 상태")]
    [SerializeField] private int currentScore = 0; // 현재 총 점수 (여러 스테이지 누적)
    [SerializeField] private int stageScore = 0;   // 현재 스테이지에서 획득한 점수
    [SerializeField] private int totalScore = 0;   // `currentScore`와 동일한 역할 (정리 필요)
    [SerializeField] private int highScore = 0;    // 최고 점수 기록

    #endregion

    #region 내부 변수 및 이벤트

    // --- 시스템 참조 ---
    private GemProtectionSystem gemProtectionSystem;

    // --- 점수 기록용 변수 ---
    private float stageStartTime = 0f;                   // 스테이지 시작 시간 기록
    private List<float> stageAccuracies = new List<float>(); // 스테이지 내 모든 타격 정확도 기록 리스트
    public float AverageAccuracy { get; private set; } = 0f; // 현재 스테이지의 평균 정확도

    // --- 외부 공개용 점수 정보 ---
    public int LastCalculatedGemScore { get; private set; }   // 마지막으로 계산된 순수 보석 점수
    public int LastCalculatedBonusScore { get; private set; } // 마지막으로 계산된 보너스 점수

    // --- 이벤트 선언 ---
    public event System.Action<int> OnScoreChanged;           // 총 점수 변경 시 발생
    public event System.Action<int> OnNewHighScore;           // 최고 점수 갱신 시 발생
    public event System.Action<int> OnStageScoreCalculated;   // 스테이지 점수 계산 완료 시 발생
    public event System.Action<float> OnAverageAccuracyChanged; // 평균 정확도 변경 시 발생

    #endregion

    #region 초기화

    void Start()
    {
        // 저장된 최고 점수 불러오기
        LoadHighScore();
        // 보석 보호 시스템 참조 찾기
        gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();
    }

    /// <summary>
    /// 새 스테이지 시작 시 호출. 점수 관련 변수 초기화
    /// </summary>
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

    #endregion

    #region 점수 계산

    /// <summary>
    /// 타격 정확도 데이터를 리스트에 추가하고, 평균 정확도 다시 계산
    /// </summary>
    /// <param name="accuracy">AimSystem에서 계산된 타격 정확도 (0.0 ~ 1.0)</param>
    public void AddAccuracy(float accuracy)
    {
        stageAccuracies.Add(accuracy);
        if (stageAccuracies.Count > 0)
        {
            AverageAccuracy = stageAccuracies.Average();
        }
        OnAverageAccuracyChanged?.Invoke(AverageAccuracy);
    }

    /// <summary>
    /// 스테이지 클리어 시 최종 점수 계산.
    /// 보석/보너스/페널티 점수를 합산 후 스테이지 배율 적용.
    /// </summary>
    /// <param name="stageNumber">현재 스테이지 번호</param>
    /// <param name="isPerfectStage">100% 완벽 클리어 여부</param>
    /// <returns>최종 계산된 스테이지 점수</returns>
    public int CalculateStageScore(int stageNumber, bool isPerfectStage)
    {
        if (gemProtectionSystem == null) gemProtectionSystem = FindFirstObjectByType<GemProtectionSystem>();
        if (gemProtectionSystem == null) return 0;

        // 개별 점수 계산 후 프로퍼티에 저장 (UI 등에서 참조)
        LastCalculatedGemScore = CalculateGemScore();
        LastCalculatedBonusScore = CalculateBonusScore(isPerfectStage);
        int penaltyScore = CalculatePenaltyScore();

        // 최종 스테이지 점수 계산
        stageScore = (LastCalculatedGemScore + LastCalculatedBonusScore + penaltyScore) * scoreConfig.stageMultiplier;
        stageScore = Mathf.Max(0, stageScore); // 점수가 0점 미만으로 내려가지 않도록 함

        // 계산 결과 로그 출력
        LogDetailedScore(LastCalculatedGemScore, LastCalculatedBonusScore, penaltyScore);

        // 총점에 현재 스테이지 점수 누적
        totalScore += stageScore;
        OnStageScoreCalculated?.Invoke(stageScore);

        return stageScore;
    }

    /// <summary>
    /// 모든 보석의 최종 상태를 점수로 환산하여 평균 계산
    /// </summary>
    /// <returns>평균 보석 점수</returns>
    int CalculateGemScore()
    {
        if (gemProtectionSystem == null || gemProtectionSystem.GetAllGems().Length == 0) return 0;

        int totalGemScore = 0;
        foreach (var gem in gemProtectionSystem.GetAllGems())
        {
            // 보석 상태에 따라 차등 점수 부여
            if (gem.isDestroyed) totalGemScore += scoreConfig.destroyedGemScore;
            else if (gem.currentCondition >= 90f) totalGemScore += scoreConfig.perfectGemScore;
            else if (gem.currentCondition >= 70f) totalGemScore += scoreConfig.damagedGemScore;
            else totalGemScore += scoreConfig.heavyDamagedScore;
        }
        // 모든 보석 점수의 평균 반환
        return totalGemScore / gemProtectionSystem.GetAllGems().Length;
    }

    /// <summary>
    /// 완벽 클리어, 클리어 시간, 평균 정확도에 따른 보너스 점수 계산
    /// </summary>
    /// <param name="isPerfectStage">100% 완벽 클리어 여부</param>
    /// <returns>계산된 총 보너스 점수</returns>
    int CalculateBonusScore(bool isPerfectStage)
    {
        int bonus = 0;

        // 100% 클리어 보너스
        if (isPerfectStage)
        {
            bonus += scoreConfig.perfectStageBonus;
        }

        // 시간 보너스 (빠를수록 높음)
        float stageTime = Time.time - stageStartTime;
        if (stageTime < 180f)
        {
            float speedRatio = (180f - stageTime) / 180f; // 남은 시간에 비례
            bonus += Mathf.RoundToInt(scoreConfig.speedBonusMax * speedRatio);
        }

        // 정확도 보너스 (평균 정확도에 비례)
        if (stageAccuracies.Count > 0)
        {
            int accuracyBonus = Mathf.Max(0, Mathf.RoundToInt(AverageAccuracy * scoreConfig.accuracyBonusMax));
            bonus += accuracyBonus;
        }

        return bonus;
    }

    /// <summary>
    /// 보석 파괴에 따른 페널티 점수 계산
    /// </summary>
    /// <returns>총 페널티 점수</returns>
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

    #endregion

    #region 유틸리티

    /// <summary>
    /// 점수 계산 상세 내역을 콘솔에 출력
    /// </summary>
    void LogDetailedScore(int gemScore, int bonusScore, int penaltyScore)
    {
        Debug.Log("--- 스테이지 점수 계산 상세 ---");
        Debug.Log($"보석 점수: {gemScore}");
        Debug.Log($"보너스 점수: {bonusScore}");
        Debug.Log($"페널티: {penaltyScore}");
        Debug.Log($"총 스테이지 점수: {stageScore}");
        Debug.Log("--------------------------");
    }

    /// <summary>
    /// PlayerPrefs에서 최고 점수 불러오기
    /// </summary>
    void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }

    #endregion
}