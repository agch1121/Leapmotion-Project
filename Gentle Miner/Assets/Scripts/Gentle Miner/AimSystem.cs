using UnityEngine;
using System.Collections;

/// <summary>
/// 사용자가 제안한 아이디어를 기반으로 한 조준 정확도 측정 시스템.
/// 목표 지점(끌)을 중심으로 한 영역에 망치(오른손)가 진입하고 이탈할 때의
/// 최소 거리를 추적하여 타격 정확도를 계산합니다.
/// </summary>
public class AimSystem : MonoBehaviour
{
    [Header("시스템 참조")]
    public HandController handController; // 손의 위치와 속도 정보
    public ToolSystem toolSystem;         // 끌의 목표 지점 정보

    [Header("판정 영역 설정")]
    [Tooltip("정확도 판정을 시작할 영역의 반지름 (미터)")]
    public float enterRadius = 0.3f;
    [Tooltip("100% 정확도로 인정될 최대 거리 (미터)")]
    public float perfectRadius = 0.05f;
    [Tooltip("0% 정확도로 처리될 최대 거리 (미터)")]
    public float maxEvaluationRadius = 0.3f;

    [Header("안정화 조건")]
    [Tooltip("정확도 판정을 위한 최소 속도")]
    public float minSpeedForTracking = 0.1f;
    [Tooltip("영역 내 최소 체류 시간 (스쳐 지나가는 것 방지)")]
    public float minDurationForTracking = 0.1f;

    // --- 내부 상태 변수 ---
    private bool isTracking = false;      // 현재 거리 추적 중인지 여부
    private float minDistance;            // 추적 중 기록된 최소 거리
    private float entryTime;              // 영역에 진입한 시간
    private Vector3 lastTrackedChiselPosition; // 추적이 시작된 끌의 위치

    /// <summary>
    /// 정확도 계산 완료 시 호출되는 이벤트 (정확도: 0.0 ~ 1.0)
    /// </summary>
    public event System.Action<float> OnAccuracyCalculated;

    void Update()
    {
        if (handController == null || toolSystem == null) return;

        // --- 1. 필요한 정보 가져오기 ---
        // [수정] HandController 대신 ToolSystem에서 '망치'의 위치를 가져옵니다.
        Vector3 hammerPosition = toolSystem.GetHammerTipPosition();
        // 속도는 여전히 손의 움직임을 기준으로 합니다.
        float rightHandSpeed = handController.RightHandVelocity.magnitude;
        // 목표 지점은 이전과 동일하게 끌의 위치입니다.
        Vector3 chiselTargetPosition = toolSystem.GetCurrentChiselTarget();


        // --- 2. 망치와 목표 지점 사이의 거리 계산 ---
        float distance = Vector3.Distance(hammerPosition, chiselTargetPosition);


        // --- 3. 정확도 추적 로직 (망치 위치 기준) ---
        if (!isTracking)
        {
            // [진입 조건]
            if (distance <= enterRadius && rightHandSpeed > minSpeedForTracking)
            {
                // [수정] 추적 시작 시 '망치'의 위치를 전달합니다.
                StartTracking(hammerPosition, chiselTargetPosition);
            }
        }
        else // isTracking == true
        {
            // [추적 중]
            // [수정] 현재 '망치'와 추적 시작점 사이의 거리를 계산하여 최소 거리를 갱신합니다.
            float currentDistanceToStartPoint = Vector3.Distance(hammerPosition, lastTrackedChiselPosition);
            if (currentDistanceToStartPoint < minDistance)
            {
                minDistance = currentDistanceToStartPoint;
            }

            // [이탈 및 평가 조건]
            if (distance > enterRadius)
            {
                if (Time.time - entryTime >= minDurationForTracking)
                {
                    EvaluateAndFireEvent();
                }
                StopTracking();
            }
        }
    }

    /// <summary>
    /// 정확도 추적을 시작합니다.
    /// </summary>
    // [수정] 첫 번째 매개변수 이름을 handPos에서 toolPos로 변경하여 명확화
    private void StartTracking(Vector3 toolPos, Vector3 chiselPos)
    {
        isTracking = true;
        entryTime = Time.time;
        lastTrackedChiselPosition = chiselPos;
        // [수정] 초기 최소 거리를 '망치'와 끌의 거리로 설정
        minDistance = Vector3.Distance(toolPos, lastTrackedChiselPosition);
        Debug.Log($"[AimSystem] 정확도 추적 시작. (망치 기준) 초기 거리: {minDistance:F3}m");
    }


    /// <summary>
    /// 정확도 추적을 중지합니다.
    /// </summary>
    private void StopTracking()
    {
        isTracking = false;
    }

    /// <summary>
    /// 기록된 최소 거리를 바탕으로 정확도를 계산하고 이벤트를 발생시킵니다.
    /// </summary>
    private void EvaluateAndFireEvent()
    {
        // 선형 보간을 사용하여 정확도 계산
        // perfectRadius에 가까울수록 1.0, maxEvaluationRadius에 가까울수록 0.0
        float accuracy = 1.0f - Mathf.InverseLerp(perfectRadius, maxEvaluationRadius, minDistance);
        accuracy = Mathf.Clamp01(accuracy); // 0~1 사이 값으로 제한

        Debug.Log($"[AimSystem] 평가 완료! 최소 거리: {minDistance:F3}m -> 정확도: {accuracy:P0}");

        // 이벤트 호출
        OnAccuracyCalculated?.Invoke(accuracy);
    }

    /// <summary>
    /// 타격 이벤트 발생 시 정확도를 강제로 평가하는 외부 호출용 함수 (대안)
    /// </summary>
    public void EvaluateOnStrike()
    {
        if (!isTracking) return;

        Debug.Log("[AimSystem] OnHammerStrike 시점 강제 평가 실행!");
        EvaluateAndFireEvent();
        StopTracking();
    }


    // 디버그용 시각화
    void OnDrawGizmos()
    {
        if (toolSystem == null) return;

        Vector3 chiselTarget = toolSystem.GetCurrentChiselTarget();

        // 판정 영역 (반투명 파란색)
        Gizmos.color = new Color(0, 0, 1, 0.1f);
        Gizmos.DrawSphere(chiselTarget, enterRadius);

        // 100% 정확도 영역 (녹색)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(chiselTarget, perfectRadius);

        // 0% 정확도 영역 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(chiselTarget, maxEvaluationRadius);

        // 추적 중일 때 최소 거리 시각화
        if (isTracking)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastTrackedChiselPosition, minDistance);
        }
    }
}