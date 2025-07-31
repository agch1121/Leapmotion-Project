using LibreFracture;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 떨어져나온 조각들을 자동으로 정리하는 시스템
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

    void Start()
    {
        originPosition = transform.position;
        InvokeRepeating(nameof(CleanupFallenChunks), cleanupInterval, cleanupInterval);

        if (enableDebugLogs)
            Debug.Log($"ChunkCleaner 시작 - {cleanupInterval}초마다 정리 작업 수행");
    }

    /// <summary>
    /// 떨어진 조각들을 찾아서 정리
    /// </summary>
    void CleanupFallenChunks()
    {
        // 씬에서 모든 조각 관련 오브젝트 찾기
        List<GameObject> allChunkObjects = FindAllChunkObjects();

        if (allChunkObjects.Count == 0) return;

        List<GameObject> chunksToDelete = new List<GameObject>();

        foreach (GameObject chunkObj in allChunkObjects)
        {
            if (chunkObj == null) continue;

            // 삭제 조건 체크
            if (ShouldDeleteChunk(chunkObj))
            {
                chunksToDelete.Add(chunkObj);

                // 한 번에 너무 많이 삭제하지 않도록 제한
                if (chunksToDelete.Count >= maxCleanupsPerFrame)
                    break;
            }
        }

        // 실제 삭제 수행
        foreach (GameObject chunk in chunksToDelete)
        {
            if (enableDebugLogs)
                Debug.Log($"조각 정리: {chunk.name}");

            // 정적 추적에서 제거
            if (staticChunks.ContainsKey(chunk))
                staticChunks.Remove(chunk);

            Destroy(chunk);
        }

        if (chunksToDelete.Count > 0 && enableDebugLogs)
        {
            Debug.Log($"정리 완료: {chunksToDelete.Count}개 조각 삭제됨 (남은 조각: {allChunkObjects.Count - chunksToDelete.Count}개)");
        }
    }

    /// <summary>
    /// 모든 조각 오브젝트 찾기 (안전한 방식)
    /// </summary>
    List<GameObject> FindAllChunkObjects()
    {
        List<GameObject> chunkObjects = new List<GameObject>();

        // 1. ChunkNode 컴포넌트가 있는 것들만 (가장 안전함)
        ChunkNode[] allChunkNodes = FindObjectsByType<ChunkNode>(FindObjectsSortMode.None);
        foreach (ChunkNode chunk in allChunkNodes)
        {
            if (chunk != null && chunk.gameObject != null &&
                !chunk.transform.IsChildOf(transform)) // 부모가 현재 광물 블록이 아닌 것들
            {
                chunkObjects.Add(chunk.gameObject);
            }
        }

        // 2. SafeChunkRemoval 컴포넌트가 있는 것들
        SafeChunkRemoval[] safeRemovals = FindObjectsByType<SafeChunkRemoval>(FindObjectsSortMode.None);
        foreach (SafeChunkRemoval removal in safeRemovals)
        {
            if (removal != null && removal.gameObject != null &&
                !removal.transform.IsChildOf(transform) &&
                !chunkObjects.Contains(removal.gameObject))
            {
                chunkObjects.Add(removal.gameObject);
            }
        }

        // 3. 특정 태그가 있는 것들 (안전함)
        GameObject[] stoneChunks = GameObject.FindGameObjectsWithTag("StoneChunk");
        foreach (GameObject obj in stoneChunks)
        {
            if (obj != null && !obj.transform.IsChildOf(transform) &&
                !chunkObjects.Contains(obj))
            {
                chunkObjects.Add(obj);
            }
        }

        // 4. 매우 엄격한 조건의 이름 기반 검색 (LibreFracture 생성 오브젝트만)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj == null || chunkObjects.Contains(obj)) continue;

            string nameToCheck = obj.name.ToLower();

            // LibreFracture가 생성한 조각들의 특징적인 이름 패턴만 매치
            bool isLibreFractureChunk = (nameToCheck.Contains("chunk") && nameToCheck.Contains("librefracture")) ||
                                       (nameToCheck.Contains("fractured") && nameToCheck.Contains("piece")) ||
                                       (nameToCheck.StartsWith("chunk_") && obj.GetComponent<Rigidbody>() != null);

            bool isSmallObject = obj.transform.localScale.magnitude < 1.5f;
            bool hasNoParent = obj.transform.parent == null;
            bool isNotImportantObject = !obj.CompareTag("Player") &&
                                       !obj.CompareTag("MainCamera") &&
                                       !obj.CompareTag("UI") &&
                                       obj != gameObject;

            if (isLibreFractureChunk && isSmallObject && hasNoParent && isNotImportantObject)
            {
                chunkObjects.Add(obj);
            }
        }

        return chunkObjects;
    }

    /// <summary>
    /// 조각을 삭제해야 하는지 판단
    /// </summary>
    bool ShouldDeleteChunk(GameObject chunk)
    {
        if (chunk == null) return true;

        Vector3 chunkPosition = chunk.transform.position;

        // 1. 거리 체크 - 원점에서 너무 멀리 떨어짐
        float distanceFromOrigin = Vector3.Distance(chunkPosition, originPosition);
        if (distanceFromOrigin > maxDistanceFromOrigin)
        {
            if (enableDebugLogs)
                Debug.Log($"거리 초과로 삭제: {chunk.name} ({distanceFromOrigin:F1}m)");
            return true;
        }

        // 2. 높이 체크 - 바닥 아래로 떨어짐
        if (chunkPosition.y < minGroundHeight)
        {
            if (enableDebugLogs)
                Debug.Log($"바닥 아래로 떨어져 삭제: {chunk.name} (y: {chunkPosition.y:F1})");
            return true;
        }

        // 3. 정적 상태 체크 - 오랫동안 움직이지 않음
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb != null)
        {
            bool isStatic = rb.linearVelocity.magnitude < minVelocityThreshold;

            if (isStatic)
            {
                // 정적 상태 시간 추적
                if (!staticChunks.ContainsKey(chunk))
                {
                    staticChunks[chunk] = Time.time;
                }
                else
                {
                    float staticDuration = Time.time - staticChunks[chunk];
                    if (staticDuration > staticTimeThreshold)
                    {
                        if (enableDebugLogs)
                            Debug.Log($"정적 상태로 삭제: {chunk.name} ({staticDuration:F1}초 동안 정지)");
                        return true;
                    }
                }
            }
            else
            {
                // 다시 움직이기 시작하면 정적 추적에서 제거
                if (staticChunks.ContainsKey(chunk))
                    staticChunks.Remove(chunk);
            }
        }

        return false;
    }

    /// <summary>
    /// 수동으로 즉시 정리 수행
    /// </summary>
    [ContextMenu("즉시 조각 정리")]
    public void CleanupNow()
    {
        Debug.Log("수동 정리 시작...");
        CleanupFallenChunks();
    }

    /// <summary>
    /// 모든 떨어진 조각 강제 삭제
    /// </summary>
    [ContextMenu("모든 조각 강제 삭제")]
    public void ForceDeleteAllChunks()
    {
        List<GameObject> allChunks = FindAllChunkObjects();

        foreach (GameObject chunk in allChunks)
        {
            if (chunk != null)
            {
                Destroy(chunk);
            }
        }

        staticChunks.Clear();
        Debug.Log($"강제 삭제 완료: {allChunks.Count}개 조각 삭제됨");
    }

    /// <summary>
    /// 현재 떨어진 조각 개수 확인
    /// </summary>
    [ContextMenu("떨어진 조각 개수 확인")]
    public void CountFallenChunks()
    {
        List<GameObject> fallenChunks = FindAllChunkObjects();
        Debug.Log($"현재 떨어진 조각: {fallenChunks.Count}개");
        Debug.Log($"정적 추적 중인 조각: {staticChunks.Count}개");
    }

    /// <summary>
    /// 디버그 시각화
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!enableDebugVisualization) return;

        // 정리 범위 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDistanceFromOrigin);

        // 바닥 높이 표시
        Gizmos.color = Color.red;
        Vector3 groundPlane = transform.position;
        groundPlane.y = minGroundHeight;
        Gizmos.DrawWireCube(groundPlane, new Vector3(maxDistanceFromOrigin * 2, 0.1f, maxDistanceFromOrigin * 2));
    }

    void OnDestroy()
    {
        // 자동 정리 중지
        CancelInvoke(nameof(CleanupFallenChunks));
    }
}