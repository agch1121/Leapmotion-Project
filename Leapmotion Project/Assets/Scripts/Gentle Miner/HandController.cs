using UnityEngine;
using Leap; // 립모션 SDK (나중에 설치)

/// <summary>
/// 립모션 기반 양손 추적 시스템
/// 왼손: 끌 위치/각도 제어, 오른손: 망치 타격 감지
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

    [Header("손 위치 설정")]
    public Transform leftHandVisual; // 왼손 시각적 표현 (끌)
    public Transform rightHandVisual; // 오른손 시각적 표현 (망치)
    public float handMoveSpeed = 2f; // 테스트 모드 손 이동 속도

    [Header("채굴 설정")]
    public float strikeDetectionThreshold = 0.7f; // 타격 감지 임계값
    public float maxStrikeDistance = 0.5f; // 최대 타격 거리

    // 립모션 컨트롤러 (나중에 SDK 설치시 활성화)
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
    private Vector3 testLeftHandPos = new Vector3(0, 1, 0);
    private Vector3 testRightHandPos = new Vector3(0.3f, 1, 0);
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

            // 왼손 데이터 (끌 제어용)
            if (leftHand != null)
            {
                LeftHandPosition = new Vector3(
                    leftHand.PalmPosition.x * 0.001f,  // mm to m
                    leftHand.PalmPosition.y * 0.001f,
                    leftHand.PalmPosition.z * 0.001f
                );

                LeftHandRotation = new Quaternion(
                    leftHand.PalmNormal.x,
                    leftHand.PalmNormal.y,
                    leftHand.PalmNormal.z,
                    1f
                );
            }

            // 오른손 데이터 (망치 제어용)
            if (rightHand != null)
            {
                RightHandPosition = new Vector3(
                    rightHand.PalmPosition.x * 0.001f,
                    rightHand.PalmPosition.y * 0.001f,
                    rightHand.PalmPosition.z * 0.001f
                );

                RightHandRotation = new Quaternion(
                    rightHand.PalmNormal.x,
                    rightHand.PalmNormal.y,
                    rightHand.PalmNormal.z,
                    1f
                );

                RightHandGrabStrength = rightHand.GrabStrength;

                RightHandVelocity = new Vector3(
                    rightHand.PalmVelocity.x * 0.001f,
                    rightHand.PalmVelocity.y * 0.001f,
                    rightHand.PalmVelocity.z * 0.001f
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

        // 오른손은 왼손 옆에 고정
        testRightHandPos = testLeftHandPos + Vector3.right * 0.3f;
        RightHandPosition = testRightHandPos;

        // 테스트 타격 (스페이스바)
        if (Input.GetKeyDown(hammerStrikeKey))
        {
            testModeStrike = true;
            RightHandGrabStrength = 1.0f; // 최대 쥠 강도
            RightHandVelocity = Vector3.down * 2f; // 아래로 빠른 움직임
        }
        else if (Input.GetKeyUp(hammerStrikeKey))
        {
            RightHandGrabStrength = 0f;
            RightHandVelocity = Vector3.zero;
            testModeStrike = false;
        }
    }

    /// <summary>
    /// 망치 타격 감지 (기획서의 핵심 로직)
    /// </summary>
    void DetectHammerStrike()
    {
        IsStrikeDetected = false;

        // 타격 조건: 쥠 강도가 높고 + 빠른 움직임
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

            // 쥠 강도에 따른 시각적 피드백
            float scale = 1f + RightHandGrabStrength * 0.2f;
            rightHandVisual.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// 현재 끌이 가리키는 채굴 지점 계산
    /// </summary>
    public Vector3 GetChiselTargetPoint()
    {
        // 왼손(끌)에서 앞쪽으로 레이캐스트
        Vector3 chiselForward = LeftHandRotation * Vector3.forward;
        Ray chiselRay = new Ray(LeftHandPosition, chiselForward);

        RaycastHit hit;
        if (Physics.Raycast(chiselRay, out hit, 2f))
        {
            return hit.point;
        }

        // 히트가 없으면 왼손 앞쪽 기본 지점
        return LeftHandPosition + chiselForward * 0.5f;
    }

    /// <summary>
    /// 현재 손 상태 디버그 출력
    /// </summary>
    [ContextMenu("손 상태 출력")]
    public void PrintHandStatus()
    {
        Debug.Log("=== 손 상태 ===");
        Debug.Log($"립모션 사용: {useLeapMotion}");
        Debug.Log($"왼손 (끌): {LeftHandPosition}");
        Debug.Log($"오른손 (망치): {RightHandPosition}");
        Debug.Log($"쥠 강도: {RightHandGrabStrength:F2}");
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

        // 채굴 타겟 지점 - 자홍색
        Gizmos.color = Color.magenta;
        Vector3 targetPoint = GetChiselTargetPoint();
        Gizmos.DrawWireSphere(targetPoint, 0.03f);
    }
}