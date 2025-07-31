using LibreFracture;
using UnityEngine;
using System.Collections;

/// <summary>
/// ChunkNode의 안전한 제거를 담당하는 유틸리티 클래스
/// LibreFracture의 MissingReferenceException 오류 방지
/// </summary>
public class SafeChunkRemoval : MonoBehaviour
{
    [Header("제거 설정")]
    public float removalDelay = 0.1f; // ChunkNode 컴포넌트 제거 지연 시간
    public float destroyDelay = 2f; // 오브젝트 완전 파괴 지연 시간 (5초 → 2초로 단축)
    public float fallDistanceThreshold = 5f; // 이 거리만큼 떨어지면 자동 삭제
    public float groundCheckRadius = 0.5f; // 바닥 체크 반경

    /// <summary>
    /// ChunkNode를 안전하게 제거합니다
    /// </summary>
    public static void RemoveChunkSafely(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float gentleForce = 5f)
    {
        if (chunk == null || chunk.gameObject == null) return;

        // SafeChunkRemoval 컴포넌트가 없으면 추가
        SafeChunkRemoval remover = chunk.GetComponent<SafeChunkRemoval>();
        if (remover == null)
        {
            remover = chunk.gameObject.AddComponent<SafeChunkRemoval>();
        }

        // 안전한 제거 프로세스 시작
        remover.StartCoroutine(remover.SafeRemovalProcess(chunk, miningPoint, surfaceNormal, gentleForce));
    }

    /// <summary>
    /// 안전한 제거 프로세스 코루틴
    /// </summary>
    private IEnumerator SafeRemovalProcess(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float gentleForce)
    {
        if (chunk == null) yield break;

        GameObject chunkObject = chunk.gameObject;
        Vector3 startPosition = chunkObject.transform.position;

        // 1. 먼저 모든 조인트 연결 끊기
        BreakAllConnections(chunk);

        // 2. 물리적 힘 적용 (ChunkNode가 아직 활성화된 상태에서)
        ApplyGentleForce(chunk, miningPoint, surfaceNormal, gentleForce);

        // 3. 짧은 지연 후 ChunkNode 컴포넌트 비활성화
        yield return new WaitForSeconds(removalDelay);

        // 4. ChunkNode 컴포넌트를 안전하게 비활성화
        if (chunk != null)
        {
            chunk.enabled = false; // OnDrawGizmos 호출 방지
            SafelyDisableChunkNode(chunk);
        }

        // 5. 추가 지연 후 ChunkNode 컴포넌트 완전 제거
        yield return new WaitForSeconds(0.1f);

        if (chunk != null && chunkObject != null)
        {
            Destroy(chunk); // ChunkNode 컴포넌트 제거
        }

        // 6. 동적 삭제 체크 시작 (거리 기반 + 시간 기반)
        float elapsedTime = 0f;
        float maxWaitTime = destroyDelay - removalDelay - 0.1f;

        while (elapsedTime < maxWaitTime && chunkObject != null)
        {
            // 원래 위치에서 너무 멀리 떨어졌으면 즉시 삭제
            float distanceFromStart = Vector3.Distance(chunkObject.transform.position, startPosition);
            if (distanceFromStart > fallDistanceThreshold)
            {
                Debug.Log($"조각이 {distanceFromStart:F1}m 떨어져 자동 삭제: {chunkObject.name}");
                break;
            }

            // 바닥 아래로 떨어졌으면 즉시 삭제
            if (chunkObject.transform.position.y < startPosition.y - fallDistanceThreshold)
            {
                Debug.Log($"조각이 바닥 아래로 떨어져 삭제: {chunkObject.name}");
                break;
            }

            // 움직임이 거의 없으면 (정적 상태) 빨리 삭제
            Rigidbody rb = chunkObject.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude < 0.1f && elapsedTime > 1f)
            {
                Debug.Log($"조각이 정적 상태가 되어 삭제: {chunkObject.name}");
                break;
            }

            elapsedTime += 0.2f; // 0.2초마다 체크
            yield return new WaitForSeconds(0.2f);
        }

        // 7. 최종 삭제
        if (chunkObject != null)
        {
            Debug.Log($"조각 최종 삭제: {chunkObject.name} (경과시간: {elapsedTime:F1}초)");
            Destroy(chunkObject);
        }
    }

    /// <summary>
    /// ChunkNode의 모든 연결을 안전하게 끊습니다
    /// </summary>
    private void BreakAllConnections(ChunkNode chunk)
    {
        if (chunk == null) return;

        // 모든 조인트 찾아서 제거
        Joint[] joints = chunk.GetComponents<Joint>();
        foreach (Joint joint in joints)
        {
            if (joint != null)
            {
                Destroy(joint);
            }
        }

        // FixedJoint도 제거
        FixedJoint[] fixedJoints = chunk.GetComponents<FixedJoint>();
        foreach (FixedJoint fixedJoint in fixedJoints)
        {
            if (fixedJoint != null)
            {
                Destroy(fixedJoint);
            }
        }

        // ConfigurableJoint도 제거 (혹시 있다면)
        ConfigurableJoint[] configurableJoints = chunk.GetComponents<ConfigurableJoint>();
        foreach (ConfigurableJoint configurableJoint in configurableJoints)
        {
            if (configurableJoint != null)
            {
                Destroy(configurableJoint);
            }
        }
    }

    /// <summary>
    /// ChunkNode를 안전하게 비활성화합니다
    /// </summary>
    private void SafelyDisableChunkNode(ChunkNode chunk)
    {
        if (chunk == null) return;

        try
        {
            // Rigidbody가 여전히 유효한지 확인
            Rigidbody rb = chunk.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Rigidbody는 유지하되 ChunkNode만 비활성화
                // 이렇게 하면 물리 효과는 계속 작동하지만 OnDrawGizmos는 호출되지 않음
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ChunkNode 비활성화 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 부드러운 물리적 힘을 적용합니다
    /// </summary>
    private void ApplyGentleForce(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float gentleForce)
    {
        if (chunk == null) return;

        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        try
        {
            // 표면 법선 방향으로 부드럽게 밀어내기
            Vector3 gentleDirection = surfaceNormal + Random.insideUnitSphere * 0.3f;
            gentleDirection.y = Mathf.Max(gentleDirection.y, 0.1f); // 최소한 위쪽으로

            // 부드러운 힘 적용
            rb.AddForce(gentleDirection * gentleForce, ForceMode.Impulse);

            // 약간의 회전 (자연스러운 효과)
            rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"물리적 힘 적용 중 오류: {e.Message}");
        }
    }
}

/// <summary>
/// ChunkNode 확장 메서드 - 안전한 제거를 위한 유틸리티
/// </summary>
public static class ChunkNodeExtensions
{
    /// <summary>
    /// ChunkNode가 안전하게 제거 가능한 상태인지 확인
    /// </summary>
    public static bool IsSafeToRemove(this ChunkNode chunk)
    {
        if (chunk == null || chunk.gameObject == null) return false;

        try
        {
            // Rigidbody가 여전히 유효한지 확인
            Rigidbody rb = chunk.GetComponent<Rigidbody>();
            return rb != null && rb.gameObject != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ChunkNode의 연결 상태 확인
    /// </summary>
    public static bool HasActiveConnections(this ChunkNode chunk)
    {
        if (chunk == null) return false;

        Joint[] joints = chunk.GetComponents<Joint>();
        foreach (Joint joint in joints)
        {
            if (joint != null && joint.connectedBody != null)
            {
                return true;
            }
        }

        return false;
    }
}