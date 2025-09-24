using LibreFracture;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection; // 리플렉션 사용을 위해 추가
using UnityEngine;

/// <summary>
/// 떨어져나온 조각들을 자동으로 정리하는 시스템 (비활성화 방식)
/// Detached 상태인 조각도 포함하여 정리합니다.
/// </summary>
public class ChunkCleaner : MonoBehaviour
{
    [Header("정리 설정")]
    public float cleanupInterval = 2f; // 정리 작업 간격 (초)
    public float maxDistanceFromOrigin = 10f; // 원점에서 이 거리 이상 떨어지면 삭제
    public float minGroundHeight = -5f; // 이 높이 아래로 떨어지면 삭제
    public float staticTimeThreshold = 3f; // 이 시간동안 움직이지 않으면 삭제
    public float minVelocityThreshold = 0.1f; // 이 속도 이하면 정적으로 간주

    [Header("성능 설정")]
    public int maxCleanupsPerFrame = 5; // 한 번에 최대 정리할 조각 수

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool enableDebugVisualization = false;

    private Dictionary<GameObject, float> staticChunks = new Dictionary<GameObject, float>();
    private Vector3 originPosition;
    private HashSet<GameObject> vanishingChunks = new HashSet<GameObject>();

    // ▼▼▼▼▼ [수정됨] ChunkNode의 내부 상태를 읽기 위한 변수 추가 ▼▼▼▼▼
    private FieldInfo stateFieldInfo;

    void Start()
    {
        InitializeReflection(); // 리플렉션 초기화 호출 추가
        originPosition = transform.position;
        InvokeRepeating(nameof(CleanupFallenChunks), cleanupInterval, cleanupInterval);

        if (enableDebugLogs)
            Debug.Log($"ChunkCleaner 시작 - {cleanupInterval}초마다 정리 작업 수행");
    }

    // ▼▼▼▼▼ [신규] ChunkNode의 private 필드에 접근하기 위한 리플렉션 초기화 메서드 ▼▼▼▼▼
    void InitializeReflection()
    {
        try
        {
            System.Type chunkNodeType = typeof(ChunkNode);
            // ChunkNode 스크립트의 내부 변수인 '_state' 필드 정보를 가져옵니다.
            stateFieldInfo = chunkNodeType.GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChunkCleaner] Reflection 초기화 실패: {e.Message}");
        }
    }

    // ▼▼▼▼▼ [신규] 리플렉션을 통해 ChunkNode의 현재 상태를 가져오는 메서드 ▼▼▼▼▼
    public ChunkNode.ChunkState GetChunkState(ChunkNode chunk)
    {
        if (stateFieldInfo == null || chunk == null)
        {
            // 리플렉션에 실패하면, 부모가 없는지를 기준으로 상태를 추정합니다.
            return chunk.transform.parent == null ? ChunkNode.ChunkState.Detached : ChunkNode.ChunkState.Connected;
        }

        try
        {
            object stateValue = stateFieldInfo.GetValue(chunk);
            return (ChunkNode.ChunkState)stateValue;
        }
        catch (System.Exception)
        {
            // GetValue 도중 오류 발생 시 안전한 기본값 반환
            return chunk.transform.parent == null ? ChunkNode.ChunkState.Detached : ChunkNode.ChunkState.Connected;
        }
    }

    /// <summary>
    /// 떨어진 조각들을 찾아서 정리
    /// </summary>
    void CleanupFallenChunks()
    {
        List<GameObject> allChunkObjects = FindAllChunkObjects();
        if (allChunkObjects.Count == 0) return;

        int cleanedUpThisFrame = 0;

        foreach (GameObject chunkObj in allChunkObjects)
        {
            if (chunkObj == null || !chunkObj.activeInHierarchy || vanishingChunks.Contains(chunkObj)) continue;

            if (ShouldCleanupChunk(chunkObj))
            {
                if (enableDebugLogs)
                    Debug.Log($"청크 비활성화 시작: {chunkObj.name}");

                vanishingChunks.Add(chunkObj);
                StartCoroutine(VanishChunk(chunkObj));
                cleanedUpThisFrame++;

                if (cleanedUpThisFrame >= maxCleanupsPerFrame) break;
            }
        }

        if (cleanedUpThisFrame > 0 && enableDebugLogs)
        {
            Debug.Log($"정리 작업 완료: {cleanedUpThisFrame}개 조각 비활성화 처리 시작");
        }
    }

    /// <summary>
    /// 청크를 몇 초 뒤에 비활성화시키는 코루틴
    /// </summary>
    IEnumerator VanishChunk(GameObject chunk)
    {
        float delay = Random.Range(3f, 5f);
        yield return new WaitForSeconds(delay);

        if (chunk != null)
        {
            if (enableDebugLogs)
                Debug.Log($"청크 비활성화: {chunk.name}");

            chunk.SetActive(false);
            vanishingChunks.Remove(chunk);
        }
    }

    // ▼▼▼▼▼ [수정됨] Detached 상태인 조각도 찾도록 로직 변경 ▼▼▼▼▼
    /// <summary>
    /// 모든 정리 대상 조각 오브젝트 찾기 (부모가 없거나, 상태가 Detached인 경우)
    /// </summary>
    List<GameObject> FindAllChunkObjects()
    {
        List<GameObject> detachedChunks = new List<GameObject>();
        ChunkNode[] allChunksInScene = FindObjectsByType<ChunkNode>(FindObjectsSortMode.None);

        foreach (ChunkNode chunk in allChunksInScene)
        {
            if (chunk == null || chunk.gameObject == null) continue;

            // 조건 1: 부모가 없는 경우 (기존 로직)
            // 조건 2: ChunkNode의 내부 상태가 Detached인 경우 (새로운 로직)
            if (chunk.transform.parent == null || GetChunkState(chunk) == ChunkNode.ChunkState.Detached)
            {
                detachedChunks.Add(chunk.gameObject);
            }
        }
        return detachedChunks;
    }

    /// <summary>
    /// 조각을 정리해야 하는지 판단
    /// </summary>
    bool ShouldCleanupChunk(GameObject chunk)
    {
        Vector3 chunkPosition = chunk.transform.position;

        if (chunkPosition.y < minGroundHeight)
        {
            if (enableDebugLogs)
                Debug.Log($"바닥 아래로 떨어져 정리: {chunk.name} (y: {chunkPosition.y:F1})");
            return true;
        }

        float distanceFromOrigin = Vector3.Distance(chunkPosition, originPosition);
        if (distanceFromOrigin > maxDistanceFromOrigin)
        {
            if (enableDebugLogs)
                Debug.Log($"거리 초과로 정리: {chunk.name} ({distanceFromOrigin:F1}m)");
            return true;
        }

        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (rb.linearVelocity.magnitude < minVelocityThreshold && rb.angularVelocity.magnitude < minVelocityThreshold)
            {
                if (!staticChunks.ContainsKey(chunk))
                {
                    staticChunks[chunk] = Time.time;
                }
                else if (Time.time - staticChunks[chunk] > staticTimeThreshold)
                {
                    if (enableDebugLogs)
                        Debug.Log($"정적 상태로 정리: {chunk.name}");
                    staticChunks.Remove(chunk);
                    return true;
                }
            }
            else
            {
                if (staticChunks.ContainsKey(chunk))
                    staticChunks.Remove(chunk);
            }
        }
        return false;
    }

    /// <summary>
    /// 모든 떨어진 조각 강제 비활성화
    /// </summary>
    [ContextMenu("모든 조각 강제 비활성화")]
    public void ForceDeactivateAllChunks()
    {
        List<GameObject> allChunks = FindAllChunkObjects();
        foreach (GameObject chunk in allChunks)
        {
            if (chunk != null && chunk.activeInHierarchy)
            {
                chunk.SetActive(false);
            }
        }
        staticChunks.Clear();
        vanishingChunks.Clear();
        StopAllCoroutines();
        Debug.Log($"강제 비활성화 완료: {allChunks.Count}개 조각 비활성화됨");
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(CleanupFallenChunks));
        StopAllCoroutines();
    }

    void OnDrawGizmosSelected()
    {
        if (!enableDebugVisualization) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDistanceFromOrigin);

        Gizmos.color = Color.red;
        Vector3 groundPlane = transform.position;
        groundPlane.y = minGroundHeight;
        Gizmos.DrawWireCube(groundPlane, new Vector3(maxDistanceFromOrigin * 2, 0.1f, maxDistanceFromOrigin * 2));
    }
}