using LibreFracture;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Progress 계산 문제 해결된 ChunkCounter
/// 비활성화된 조각도 제거된 것으로 카운트
/// </summary>
public class ChunkCounter : MonoBehaviour
{
    [Header("카운터 상태")]
    [SerializeField] private int totalChunksAtStart = 0;
    [SerializeField] private int currentActiveChunks = 0;
    [SerializeField] private int currentRemovedChunks = 0;
    [SerializeField] private float miningProgress = 0f;

    [Header("목표 설정")]
    [Range(10, 500)]
    public int targetChunkCount = 100;  // 100% 달성을 위한 목표 청크 수
    public bool useCustomTarget = true; // 커스텀 목표 사용 여부

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool enableProgressDebug = true;
    public bool autoRefreshCount = true;
    public float refreshInterval = 0.5f;

    private HashSet<ChunkNode> trackedChunks = new HashSet<ChunkNode>();
    private FieldInfo stateFieldInfo;

    // 이벤트 - 매개변수 순서: (활성조각수, 제거된조각수, 진행률)
    public System.Action<int, int, float> OnChunkCountChanged;

    // 프로퍼티
    public int TotalChunksAtStart => totalChunksAtStart;
    public int CurrentActiveChunks => currentActiveChunks;
    public int CurrentRemovedChunks => currentRemovedChunks;
    public float MiningProgress => miningProgress;

    // 기존 호환성을 위한 프로퍼티들
    public int CurrentConnectedChunks => currentActiveChunks;
    public int CurrentDetachedChunks => currentRemovedChunks;
    public int DestroyedChunks => currentRemovedChunks;
    public bool IsFullyMined => currentActiveChunks <= 0;

    void Start()
    {
        InitializeReflection();
        InitializeCounter();

        if (autoRefreshCount)
        {
            InvokeRepeating(nameof(RefreshChunkCount), refreshInterval, refreshInterval);
        }
    }

    void InitializeReflection()
    {
        try
        {
            System.Type chunkNodeType = typeof(ChunkNode);
            stateFieldInfo = chunkNodeType.GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);

            if (stateFieldInfo == null)
            {
                Debug.LogError("ChunkNode의 _state 필드를 찾을 수 없습니다!");
            }
            else if (enableDebugLogs)
            {
                Debug.Log("ChunkNode._state 필드 접근 성공!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Reflection 초기화 실패: {e.Message}");
        }
    }

    public ChunkNode.ChunkState GetChunkState(ChunkNode chunk)
    {
        if (stateFieldInfo == null || chunk == null)
        {
            return ChunkNode.ChunkState.Connected;
        }

        try
        {
            object stateValue = stateFieldInfo.GetValue(chunk);
            return (ChunkNode.ChunkState)stateValue;
        }
        catch (System.Exception e)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"상태 조회 실패 {chunk.name}: {e.Message}");
            return ChunkNode.ChunkState.Connected;
        }
    }

    void InitializeCounter()
    {
        RefreshChunkCount();
        totalChunksAtStart = currentActiveChunks + currentRemovedChunks;

        // 초기화 시점에서는 제거된 조각이 없으므로 progress = 0
        miningProgress = 0f;

        if (enableProgressDebug)
        {
            Debug.Log($"=== ChunkCounter 초기화 ===");
            Debug.Log($"시작 시 총 조각: {totalChunksAtStart}개");
            Debug.Log($"활성 조각: {currentActiveChunks}개");
            Debug.Log($"제거된 조각: {currentRemovedChunks}개");
            Debug.Log($"초기 Progress: {miningProgress * 100f:F1}%");
            Debug.Log("===============================");
        }
    }

    public void RefreshChunkCount()
    {
        int previousActive = currentActiveChunks;
        int previousRemoved = currentRemovedChunks;
        float previousProgress = miningProgress;

        currentActiveChunks = 0;
        currentRemovedChunks = 0;

        // 모든 ChunkNode 찾기 (자식 포함)
        ChunkNode[] allChunks = GetComponentsInChildren<ChunkNode>(true); // includeInactive = true

        foreach (ChunkNode chunk in allChunks)
        {
            if (chunk != null && chunk.gameObject != null)
            {
                // 활성화되어 있고 실제로 연결된 상태인 조각만 활성으로 카운트
                if (chunk.gameObject.activeInHierarchy && chunk.enabled)
                {
                    ChunkNode.ChunkState state = GetChunkState(chunk);

                    // Detached가 아닌 상태는 모두 활성으로 카운트
                    if (state != ChunkNode.ChunkState.Detached)
                    {
                        currentActiveChunks++;
                    }
                    else
                    {
                        currentRemovedChunks++;
                    }
                }
                else
                {
                    // 비활성화되었거나 enabled가 false인 조각은 제거된 것으로 카운트
                    currentRemovedChunks++;
                }
            }
        }

        // Progress 계산
        if (totalChunksAtStart > 0)
        {
            miningProgress = (float)currentRemovedChunks / targetChunkCount;
        }
        else
        {
            miningProgress = 0f;
        }

        // 변화가 있을 때만 로그 출력 및 이벤트 발생
        if (previousActive != currentActiveChunks || previousRemoved != currentRemovedChunks)
        {
            if (enableProgressDebug)
            {
                Debug.Log($"=== Progress 업데이트 ===");
                Debug.Log($"활성 조각: {currentActiveChunks}개 (이전: {previousActive}개)");
                Debug.Log($"제거된 조각: {currentRemovedChunks}개 (이전: {previousRemoved}개)");
                Debug.Log($"Progress: {miningProgress * 100f:F1}% (이전: {previousProgress * 100f:F1}%)");
                Debug.Log("========================");
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"조각 상태: 활성 {currentActiveChunks}개, 제거 {currentRemovedChunks}개 ({miningProgress * 100f:F1}% 진행)");
            }

            // 이벤트 발생 (활성조각수, 제거된조각수, 진행률)
            OnChunkCountChanged?.Invoke(currentActiveChunks, currentRemovedChunks, miningProgress);
        }
    }

    /// <summary>
    /// 상태별 조각 개수 반환
    /// </summary>
    public (int active, int removed, int total) GetChunkCounts()
    {
        return (currentActiveChunks, currentRemovedChunks, totalChunksAtStart);
    }

    /// <summary>
    /// 상세 상태별 조각 개수 (디버그용)
    /// </summary>
    public (int connected, int anchored, int broken, int detached, int disabled) GetDetailedChunkCount()
    {
        int connected = 0, anchored = 0, broken = 0, detached = 0, disabled = 0;

        ChunkNode[] allChunks = GetComponentsInChildren<ChunkNode>(true);

        foreach (ChunkNode chunk in allChunks)
        {
            if (chunk != null && chunk.gameObject != null)
            {
                if (!chunk.gameObject.activeInHierarchy || !chunk.enabled)
                {
                    disabled++;
                }
                else
                {
                    ChunkNode.ChunkState state = GetChunkState(chunk);
                    switch (state)
                    {
                        case ChunkNode.ChunkState.Connected: connected++; break;
                        case ChunkNode.ChunkState.Anchored: anchored++; break;
                        case ChunkNode.ChunkState.Broken: broken++; break;
                        case ChunkNode.ChunkState.Detached: detached++; break;
                    }
                }
            }
        }

        return (connected, anchored, broken, detached, disabled);
    }

    public float GetMiningProgressPercent() => miningProgress * 100f;
    public bool IsMiningProgressOver(float percentage) => GetMiningProgressPercent() >= percentage;
    public bool IsGemExposed() => miningProgress >= 0.7f;

    /// <summary>
    /// 즉시 Progress 강제 업데이트 (테스트용)
    /// </summary>
    [ContextMenu("즉시 Progress 업데이트")]
    public void ForceRefresh()
    {
        RefreshChunkCount();
        Debug.Log($"강제 업데이트 완료! Progress: {GetMiningProgressPercent():F1}%");
    }

    [ContextMenu("카운터 리셋")]
    public void ResetCounter()
    {
        trackedChunks.Clear();
        InitializeCounter();
        Debug.Log("ChunkCounter 리셋 완료!");
    }

    [ContextMenu("상세 상태 출력")]
    public void PrintDetailedStatus()
    {
        var (connected, anchored, broken, detached, disabled) = GetDetailedChunkCount();

        Debug.Log("=== ChunkCounter 상세 상태 ===");
        Debug.Log($"시작 시 총 조각: {totalChunksAtStart}개");
        Debug.Log($"현재 활성 조각: {currentActiveChunks}개");
        Debug.Log($"제거된 조각: {currentRemovedChunks}개");
        Debug.Log($"--- 상세 분류 ---");
        Debug.Log($"연결된 조각 (Connected): {connected}개");
        Debug.Log($"고정된 조각 (Anchored): {anchored}개");
        Debug.Log($"손상된 조각 (Broken): {broken}개");
        Debug.Log($"분리된 조각 (Detached): {detached}개");
        Debug.Log($"비활성화된 조각 (Disabled): {disabled}개");
        Debug.Log($"채굴 진행률: {GetMiningProgressPercent():F1}%");
        Debug.Log($"보석 노출 여부: {(IsGemExposed() ? "예" : "아니오")}");
        Debug.Log($"완전 채굴 여부: {(IsFullyMined ? "예" : "아니오")}");
        Debug.Log("==============================");
    }

    // 기존 호환성을 위한 함수
    public void NotifyChunkWillBeDestroyed(ChunkNode chunk)
    {
        // 이제는 자동으로 감지되므로 바로 갱신만 함
        if (enableDebugLogs)
        {
            Debug.Log($"조각 제거 예정: {chunk?.name} - 자동 감지로 Progress 업데이트됨");
        }
    }

    void OnDestroy()
    {
        if (autoRefreshCount)
        {
            CancelInvoke(nameof(RefreshChunkCount));
        }
    }
}