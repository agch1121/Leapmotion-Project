using UnityEngine;
using Leap;

/// <summary>
/// 립모션 기반 양손 추적 시스템
/// 왼손: 끌 고정, 오른손: 망치 고정
/// </summary>
public class HandController : MonoBehaviour
{
    [Header("립모션 설정")]
    public bool useLeapMotion = true; // 립모션 사용 여부

    [Header("테스트 모드 (립모션 없을 때)")]
    public bool enableTestMode = true;
    public KeyCode leftHandUpKey = KeyCode.W;
    public KeyCode leftHandDownKey = KeyCode.S;
    public KeyCode leftHandLeftKey = KeyCode.A;
    public KeyCode leftHandRightKey = KeyCode.D;
    public KeyCode hammerStrikeKey = KeyCode.Space;

    [Header("립모션 좌표 보정")]
    public Vector3 leapMotionOffset = new Vector3(0f, 1.5f, 0f); // 높이 보정
    public float leapMotionScale = 0.001f; // mm to m 변환 스케일
    public bool invertZ = true; // Z축 반전 여부

    [Header("손 위치 설정")]
    public Transform leftHandVisual; // 왼손 시각적 표현 (끌)
    public Transform rightHandVisual; // 오른손 시각적 표현 (망치)
    public float handMoveSpeed = 2f; // 테스트 모드 손 이동 속도

    [Header("채굴 설정")]
    public float strikeDetectionThreshold = 0.7f; // 타격 감지 임계값
    public float maxStrikeDistance = 0.8f; // 최대 타격 거리 (늘림)

    // 립모션 컨트롤러
    private Controller leapController;

    // 손 상태 데이터
    public Vector3 LeftHandPosition { get; private set; }
    public Vector3 RightHandPosition { get; private set; }
    public Quaternion LeftHandRotation { get; private set; }
    public Quaternion RightHandRotation { get; private set; }
    public float RightHandGrabStrength { get; private set; }
    public Vector3 RightHandVelocity { get; private set; }

    // 타격 감지
    public bool IsStrikeDetected { get; private set; }
    private bool wasGripping = false;

    // 이벤트
    public System.Action<Vector3, Vector3, float> OnHammerStrike; // (위치, 방향, 힘)

    // 테스트 모드 변수들
    private Vector3 testLeftHandPos = new Vector3(-0.5f, 1.2f, 0f);  // 더 멀리 배치
    private Vector3 testRightHandPos = new Vector3(0.5f, 1.2f, 0f);   // 더 멀리 배치
    private bool testModeStrike = false;

    void Start()
    {
        InitializeHandController();
    }

    void InitializeHandController()
    {
        Debug.Log("HandController 시작");

        // 립모션 초기화 시도 (SDK 없으면 에러 무시)
        try
        {
            if (useLeapMotion)
            {
                leapController = new Controller();
                Debug.Log("립모션 컨트롤러 초기화 성공");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"립모션 SDK 없음 - 테스트 모드로 전환: {e.Message}");
            useLeapMotion = false;
            enableTestMode = true;
        }

        // 초기 위치 설정
        LeftHandPosition = testLeftHandPos;
        RightHandPosition = testRightHandPos;
        LeftHandRotation = Quaternion.identity;
        RightHandRotation = Quaternion.identity;

        UpdateHandVisuals();
    }

    void Update()
    {
        if (useLeapMotion && leapController != null)
        {
            UpdateLeapMotionHands();
        }
        else if (enableTestMode)
        {
            UpdateTestModeHands();
        }

        DetectHammerStrike();
        UpdateHandVisuals();
    }

    /// <summary>
    /// 립모션 손 데이터 업데이트 (SDK 설치 후 동작)
    /// </summary>
    void UpdateLeapMotionHands()
    {
        try
        {
            Frame frame = leapController.Frame();

            Hand leftHand = null;
            Hand rightHand = null;

            // 양손 찾기
            foreach (Hand hand in frame.Hands)
            {
                if (hand.IsLeft) leftHand = hand;
                if (hand.IsRight) rightHand = hand;
            }

            // 왼손 데이터 (끌 제어용) - 오프셋 추가
            if (leftHand != null)
            {
                LeftHandPosition = new Vector3(
                    leftHand.PalmPosition.x * leapMotionScale,
                    leftHand.PalmPosition.y * leapMotionScale,
                    (invertZ ? -leftHand.PalmPosition.z : leftHand.PalmPosition.z) * leapMotionScale
                ) + leapMotionOffset;

                Vector3 palmNormal = new Vector3(leftHand.PalmNormal.x, leftHand.PalmNormal.y,
                    invertZ ? -leftHand.PalmNormal.z : leftHand.PalmNormal.z);
                Vector3 direction = new Vector3(leftHand.Direction.x, leftHand.Direction.y,
                    invertZ ? -leftHand.Direction.z : leftHand.Direction.z);
                LeftHandRotation = Quaternion.LookRotation(direction, palmNormal);
            }

            // 오른손 데이터 (망치 제어용) - 오프셋 추가
            if (rightHand != null)
            {
                RightHandPosition = new Vector3(
                    rightHand.PalmPosition.x * leapMotionScale,
                    rightHand.PalmPosition.y * leapMotionScale,
                    (invertZ ? -rightHand.PalmPosition.z : rightHand.PalmPosition.z) * leapMotionScale
                ) + leapMotionOffset;

                Vector3 palmNormal = new Vector3(rightHand.PalmNormal.x, rightHand.PalmNormal.y,
                    invertZ ? -rightHand.PalmNormal.z : rightHand.PalmNormal.z);
                Vector3 direction = new Vector3(rightHand.Direction.x, rightHand.Direction.y,
                    invertZ ? -rightHand.Direction.z : rightHand.Direction.z);
                RightHandRotation = Quaternion.LookRotation(direction, palmNormal);

                RightHandGrabStrength = rightHand.GrabStrength;

                RightHandVelocity = new Vector3(
                    rightHand.PalmVelocity.x * leapMotionScale,
                    rightHand.PalmVelocity.y * leapMotionScale,
                    (invertZ ? -rightHand.PalmVelocity.z : rightHand.PalmVelocity.z) * leapMotionScale
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"립모션 데이터 읽기 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 테스트 모드 손 조작 (키보드)
    /// </summary>
    void UpdateTestModeHands()
    {
        // 왼손 위치 조작 (WASD)
        Vector3 leftMove = Vector3.zero;
        if (Input.GetKey(leftHandUpKey)) leftMove += Vector3.up;
        if (Input.GetKey(leftHandDownKey)) leftMove += Vector3.down;
        if (Input.GetKey(leftHandLeftKey)) leftMove += Vector3.left;
        if (Input.GetKey(leftHandRightKey)) leftMove += Vector3.right;

        testLeftHandPos += leftMove * handMoveSpeed * Time.deltaTime;
        LeftHandPosition = testLeftHandPos;

        // 오른손은 왼손 옆에 고정 (거리 유지)
        testRightHandPos = testLeftHandPos + Vector3.right * 0.6f; // 더 멀리 배치
        RightHandPosition = testRightHandPos;

        // 테스트 타격 (스페이스바) - 개선된 버전
        if (Input.GetKeyDown(hammerStrikeKey))
        {
            // 타격 시작
            testModeStrike = true;
            RightHandGrabStrength = 1.0f; // 최대 잡기 강도
            RightHandVelocity = Vector3.down * 3f; // 아래로 빠른 움직임 (더 빠르게)
            Debug.Log("테스트 타격 시작!");
        }
        else if (Input.GetKeyUp(hammerStrikeKey))
        {
            // 타격 끝
            RightHandGrabStrength = 0f;
            RightHandVelocity = Vector3.zero;
            testModeStrike = false;
            Debug.Log("테스트 타격 끝!");
        }
        else if (testModeStrike)
        {
            // 타격 중에는 계속 강한 값 유지
            RightHandGrabStrength = 1.0f;
            RightHandVelocity = Vector3.down * 3f;
        }
        else
        {
            // 평상시
            RightHandGrabStrength = 0f;
            RightHandVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 망치 타격 감지 (기획서의 핸들 로직) - 개선된 버전
    /// </summary>
    void DetectHammerStrike()
    {
        IsStrikeDetected = false;

        // 타격 조건: 잡기 강도가 높고 + 빠른 움직임
        bool isGripping = RightHandGrabStrength > strikeDetectionThreshold;
        bool hasVelocity = RightHandVelocity.magnitude > 1.0f;

        // 타격 거리 확인 (끌과 망치가 가까이 있어야 함)
        float distance = Vector3.Distance(LeftHandPosition, RightHandPosition);
        bool isInRange = distance <= maxStrikeDistance;

        // 타격 감지: 이전엔 안 쥐고 있다가 지금 쥐기 시작 + 조건 만족
        if (!wasGripping && isGripping && hasVelocity && isInRange)
        {
            IsStrikeDetected = true;

            // 타격 방향 계산 (망치에서 끌로)
            Vector3 strikeDirection = (LeftHandPosition - RightHandPosition).normalized;

            // 타격 이벤트 발생
            OnHammerStrike?.Invoke(LeftHandPosition, strikeDirection, RightHandGrabStrength);

            Debug.Log($"망치 타격 감지! 위치: {LeftHandPosition}, 힘: {RightHandGrabStrength:F2}");
            Debug.Log($"조건 확인 - 잡기: {isGripping}, 속도: {hasVelocity}({RightHandVelocity.magnitude:F2}), 거리: {isInRange}({distance:F2})");
        }

        // 디버그: 조건들 상세 출력
        if (Input.GetKey(hammerStrikeKey))
        {
            Debug.Log($"타격 시도 중 - 잡기강도: {RightHandGrabStrength:F2}, 속도: {RightHandVelocity.magnitude:F2}, 거리: {distance:F2}, wasGripping: {wasGripping}");
        }

        wasGripping = isGripping;
    }

    /// <summary>
    /// 손 시각적 표현 업데이트
    /// </summary>
    void UpdateHandVisuals()
    {
        if (leftHandVisual != null)
        {
            leftHandVisual.position = LeftHandPosition;
            leftHandVisual.rotation = LeftHandRotation;
        }

        if (rightHandVisual != null)
        {
            rightHandVisual.position = RightHandPosition;
            rightHandVisual.rotation = RightHandRotation;

            // 잡기 강도에 따른 시각적 피드백
            float scale = 1f + RightHandGrabStrength * 0.2f;
            rightHandVisual.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// 현재 끌이 가리키는 채굴 지점 계산
    /// </summary>
    public Vector3 GetChiselTargetPoint()
    {
        // 왼손(끌)에서 앞방향으로 레이캐스트
        Vector3 chiselForward = LeftHandRotation * Vector3.forward;
        Ray chiselRay = new Ray(LeftHandPosition, chiselForward);

        RaycastHit hit;
        if (Physics.Raycast(chiselRay, out hit, 2f))
        {
            return hit.point;
        }

        // 히트가 없으면 왼손 앞방향 기본 지점
        return LeftHandPosition + chiselForward * 0.5f;
    }

    [ContextMenu("현재 립모션 상태 확인")]
    public void DebugLeapMotionStatus()
    {
        if (leapController == null)
        {
            Debug.Log("립모션 컨트롤러가 null입니다.");
            return;
        }

        Frame frame = leapController.Frame();
        Debug.Log($"감지된 손 개수: {frame.Hands.Count}");

        if (frame.Hands.Count > 0)
        {
            Hand hand = frame.Hands[0];
            Vector3 rawPos = new Vector3(hand.PalmPosition.x, hand.PalmPosition.y, hand.PalmPosition.z);
            Vector3 convertedPos = new Vector3(
                hand.PalmPosition.x * leapMotionScale,
                hand.PalmPosition.y * leapMotionScale,
                (invertZ ? -hand.PalmPosition.z : hand.PalmPosition.z) * leapMotionScale
            ) + leapMotionOffset;

            Debug.Log($"원본 립모션 위치: {rawPos}");
            Debug.Log($"변환된 Unity 위치: {convertedPos}");
        }
    }

    [ContextMenu("타격 조건 상세 확인")]
    public void DebugStrikeConditions()
    {
        bool isGripping = RightHandGrabStrength > strikeDetectionThreshold;
        bool hasVelocity = RightHandVelocity.magnitude > 1.0f;
        float distance = Vector3.Distance(LeftHandPosition, RightHandPosition);
        bool isInRange = distance <= maxStrikeDistance;

        Debug.Log("=== 타격 조건 상세 확인 ===");
        Debug.Log($"잡기 강도: {RightHandGrabStrength:F2} (임계값: {strikeDetectionThreshold}) → {(isGripping ? "OK" : "FAIL")}");
        Debug.Log($"손 속도: {RightHandVelocity.magnitude:F2} (최소: 1.0) → {(hasVelocity ? "OK" : "FAIL")}");
        Debug.Log($"손 거리: {distance:F2} (최대: {maxStrikeDistance}) → {(isInRange ? "OK" : "FAIL")}");
        Debug.Log($"이전 잡기 상태: {wasGripping} (false여야 함)");
        Debug.Log($"최종 타격 가능: {(!wasGripping && isGripping && hasVelocity && isInRange)}");
        Debug.Log("===========================");
    }

    [ContextMenu("손 상태 출력")]
    public void PrintHandStatus()
    {
        Debug.Log("=== 손 상태 ===");
        Debug.Log($"립모션 사용: {useLeapMotion}");
        Debug.Log($"왼손 (끌): {LeftHandPosition}");
        Debug.Log($"오른손 (망치): {RightHandPosition}");
        Debug.Log($"잡기 강도: {RightHandGrabStrength:F2}");
        Debug.Log($"손 속도: {RightHandVelocity.magnitude:F2}");
        Debug.Log($"타격 범위 내: {Vector3.Distance(LeftHandPosition, RightHandPosition) <= maxStrikeDistance}");
        Debug.Log("===============");
    }
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 왼손 (끌) - 파란색
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(LeftHandPosition, 0.05f);

        // 오른손 (망치) - 빨간색  
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(RightHandPosition, 0.05f);

        // 타격 범위 - 노란색
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(LeftHandPosition, maxStrikeDistance);

        // 끌이 가리키는 방향 - 초록색
        Gizmos.color = Color.green;
        Vector3 chiselForward = LeftHandRotation * Vector3.forward;
        Gizmos.DrawRay(LeftHandPosition, chiselForward * 0.5f);

        // 채굴 타격 지점 - 자홍색
        Gizmos.color = Color.magenta;
        Vector3 targetPoint = GetChiselTargetPoint();
        Gizmos.DrawWireSphere(targetPoint, 0.03f);
    }
}