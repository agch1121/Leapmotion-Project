using UnityEngine;

public class AccuracyDetector : MonoBehaviour
{
    [Header("설정")]
    public string hammerTag = "Hammer"; // 망치 콜라이더에 부여할 태그

    // ⭐ perfectHitRadius를 0.1f로 설정하고, Range도 1.0f까지 유지합니다.
    [Range(0.01f, 1.0f)]
    public float perfectHitRadius = 0.1f;

    // ⭐ maxDetectionRadius를 5.0f로 설정하고, Range도 5.0f까지 늘립니다.
    [Range(0.1f, 5.0f)]
    public float maxDetectionRadius = 5.0f;

    private float lastCollisionTime = 0f;
    private const float cooldown = 0.5f;
    private ToolSystem toolSystem;

    void Start()
    {
        toolSystem = FindObjectOfType<ToolSystem>();
        if (toolSystem == null)
        {
            Debug.LogError("AccuracyDetector: ToolSystem을 찾을 수 없습니다!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastCollisionTime < cooldown)
        {
            return;
        }

        if (other.CompareTag(hammerTag))
        {
            Vector3 collisionPoint = other.ClosestPoint(transform.position);
            float distance = Vector3.Distance(transform.position, collisionPoint);

            int finalScore = 0;

            if (distance <= perfectHitRadius)
            {
                finalScore = 100;
            }
            else if (distance <= maxDetectionRadius)
            {
                float normalizedDistance = (distance - perfectHitRadius) / (maxDetectionRadius - perfectHitRadius);
                float score = 1 - normalizedDistance;
                finalScore = Mathf.RoundToInt(Mathf.Clamp01(score) * 100);
            }

            Debug.Log($"<color=green>정확도 충돌 감지!</color> 점수: <b>{finalScore}점</b> (거리: {distance:F2}m)");
            lastCollisionTime = Time.time;

            if (toolSystem != null)
            {
                toolSystem.ExecuteMining(collisionPoint);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDetectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, perfectHitRadius);
    }
}