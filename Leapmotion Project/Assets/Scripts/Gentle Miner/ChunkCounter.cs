using LibreFracture;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 정확한 조각 개수 추적 시스템
/// </summary>
public class ChunkCounter : MonoBehaviour
{
    [Header("카운터 상태")]
    [SerializeField] private int totalChunksAtStart = 0;
    [SerializeField] private int currentActiveChunks = 0;
    [SerializeField] private int destroyedChunks = 0;
    [SerializeField] private float miningProgress = 0f;

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool autoRefreshCount = true; // 자동으로 개수 갱신
    public float refreshInterval = 1f; // 갱신 간격 (초)

    private HashSet<ChunkNode> trackedChunks = new HashSet<ChunkNode>();
    private HashSet<ChunkNode> destroyedChunksList = new HashSet<ChunkNode>();

    // 이벤트
    public System.Action<int, int, float> OnChunkCountChanged; // (현재개수, 파괴개수, 진행률)

    void Start()
    {
        InitializeCounter();

        if (autoRefreshCount)
        {
            InvokeRepeating(nameof(RefreshChunkCount), refreshInterval, refreshInterval);
        }
    }

    /// <summary>
    /// 카운터 초기화
    /// </summary>
    void InitializeCounter()
    {
        RefreshChunkCount();
        totalChunksAtStart = currentActiveChunks;
        destroyedChunks = 0;
        miningProgress = 0f;

        if (enableDebugLogs)
            Debug.Log($"ChunkCounter 초기화: 총 {totalChunksAtStart}개 조각 발견");
    }

    /// <summary>
    /// 실시간으로 조각 개수 갱신
    /// </summary>
    public void RefreshChunkCount()
    {
        // 이전 추적 상태 저장
        int previousActiveCount = currentActiveChunks;

        // 현재 활성 조각들 찾기
        ChunkNode[] allChunks = GetComponentsInChildren<ChunkNode>();
        HashSet<ChunkNode> currentValidChunks = new HashSet<ChunkNode>();

        foreach (ChunkNode chunk in allChunks)
        {
            if (IsChunkValid(chunk))
            {
                currentValidChunks.Add(chunk);
            }
        }

        // 새롭게 파괴된 조각들 감지
        foreach (ChunkNode trackedChunk in trackedChunks)
        {
            if (!currentValidChunks.Contains(trackedChunk) && !destroyedChunksList.Contains(trackedChunk))
            {
                // 이 조각이 파괴됨
                destroyedChunksList.Add(trackedChunk);
                OnChunkDestroyed(trackedChunk);
            }
        }

        // 추적 리스트 업데이트
        trackedChunks = currentValidChunks;
        currentActiveChunks = trackedChunks.Count;

        // 진행률 계산
        if (totalChunksAtStart > 0)
        {
            destroyedChunks = totalChunksAtStart - currentActiveChunks;
            miningProgress = (float)destroyedChunks / totalChunksAtStart;
        }

        // 변화가 있을 때만 로그 출력
        if (previousActiveCount != currentActiveChunks && enableDebugLogs)
        {
            Debug.Log($"조각 상태 갱신: {currentActiveChunks}개 남음 ({destroyedChunks}개 파괴됨, {miningProgress * 100f:F1}% 진행)");
        }

        // 이벤트 발생
        OnChunkCountChanged?.Invoke(currentActiveChunks, destroyedChunks, miningProgress);
    }

    /// <summary>
    /// 조각이 유효한지 확인
    /// </summary>
    bool IsChunkValid(ChunkNode chunk)
    {
        if (chunk == null) return false;
        if (chunk.gameObject == null) return false;
        if (!chunk.gameObject.activeInHierarchy) return false;
        if (!chunk.enabled) return false;

        // Rigidbody 상태 확인
        try
        {
            Rigidbody rb = chunk.GetComponent<Rigidbody>();
            if (rb == null) return false;

            // Rigidbody가 실제로 접근 가능한지 테스트
            _ = rb.mass; // 간단한 접근 테스트
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 조각이 파괴되었을 때 호출
    /// </summary>
    void OnChunkDestroyed(ChunkNode chunk)
    {
        if (enableDebugLogs)
            Debug.Log($"조각 파괴 감지: {(chunk != null ? chunk.name : "Unknown")}");

        // 보석 보호 시스템과 연동 가능
        GemProtectionSystem gemSystem = GetComponent<GemProtectionSystem>();
        if (gemSystem != null && chunk != null)
        {
            // 파괴된 조각 위치에서 보석까지의 거리 체크
            // (필요에 따라 추가 로직 구현 가능)
        }
    }

    /// <summary>
    /// 수동으로 조각이 제거될 것을 미리 알림
    /// </summary>
    public void NotifyChunkWillBeDestroyed(ChunkNode chunk)
    {
        if (chunk != null && trackedChunks.Contains(chunk))
        {
            destroyedChunksList.Add(chunk);
            trackedChunks.Remove(chunk);

            currentActiveChunks = trackedChunks.Count;
            destroyedChunks = totalChunksAtStart - currentActiveChunks;
            miningProgress = (float)destroyedChunks / totalChunksAtStart;

            if (enableDebugLogs)
                Debug.Log($"조각 제거 예고: {chunk.name} (남은 조각: {currentActiveChunks}개)");

            OnChunkCountChanged?.Invoke(currentActiveChunks, destroyedChunks, miningProgress);
        }
    }

    /// <summary>
    /// Getter 프로퍼티들
    /// </summary>
    public int TotalChunksAtStart => totalChunksAtStart;
    public int CurrentActiveChunks => currentActiveChunks;
    public int DestroyedChunks => destroyedChunks;
    public float MiningProgress => miningProgress;
    public bool IsFullyMined => currentActiveChunks <= 0;

    /// <summary>
    /// 진행률을 퍼센트로 반환
    /// </summary>
    public float GetMiningProgressPercent()
    {
        return miningProgress * 100f;
    }

    /// <summary>
    /// 특정 퍼센트 이상 채굴되었는지 확인
    /// </summary>
    public bool IsMiningProgressOver(float percentage)
    {
        return GetMiningProgressPercent() >= percentage;
    }

    /// <summary>
    /// 디버그용: 상세 상태 출력
    /// </summary>
    [ContextMenu("상세 상태 출력")]
    public void PrintDetailedStatus()
    {
        Debug.Log("=== ChunkCounter 상태 ===");
        Debug.Log($"시작 시 총 조각: {totalChunksAtStart}개");
        Debug.Log($"현재 활성 조각: {currentActiveChunks}개");
        Debug.Log($"파괴된 조각: {destroyedChunks}개");
        Debug.Log($"채굴 진행률: {GetMiningProgressPercent():F1}%");
        Debug.Log($"완전 채굴 여부: {(IsFullyMined ? "예" : "아니오")}");
        Debug.Log($"추적 중인 조각: {trackedChunks.Count}개");
        Debug.Log($"파괴 목록 크기: {destroyedChunksList.Count}개");
        Debug.Log("========================");
    }

    /// <summary>
    /// 강제로 카운터 리셋
    /// </summary>
    [ContextMenu("카운터 리셋")]
    public void ResetCounter()
    {
        trackedChunks.Clear();
        destroyedChunksList.Clear();
        InitializeCounter();
        Debug.Log("ChunkCounter 리셋 완료!");
    }

    void OnDestroy()
    {
        // 자동 갱신 중지
        if (autoRefreshCount)
        {
            CancelInvoke(nameof(RefreshChunkCount));
        }
    }
}