using UnityEngine;
using Leap;

/// <summary>
/// 립모션 기반 양손 추적 시스템 - 데이터 처리 개선 버전
/// 왼손: 끌 고정, 오른손: 망치 고정
/// </summary>
public class HandController : MonoBehaviour
{
    [Header("립모션 설정")]
    public bool useLeapMotion = true;

    [Header("테스트 모드")]
    public bool enableTestMode = true;
    public KeyCode leftHandUpKey = KeyCode.W;
    public KeyCode leftHandDownKey = KeyCode.S;
    public KeyCode leftHandLeftKey = KeyCode.A;
    public KeyCode leftHandRightKey = KeyCode.D;
    public KeyCode hammerStrikeKey = KeyCode.Space;

    [Header("립모션 좌표 변환")]
    public bool useRawCoordinates = true; // 원본 좌표 사용 여부
    public float coordinateScale = 1f; // 좌표 스케일 (1 = 원본 크기)
    public bool invertZ = false; // Z축 반전 옵션

    [Header("손 시각화")]
    public Transform leftHandVisual;
    public Transform rightHandVisual;
    public float handMoveSpeed = 2f;
    public float smoothSpeed = 10f; // 부드러운 움직임을 위한 보간 속도

    [Header("채굴 설정")]
    public float strikeDetectionThreshold = 0.5f;
    public float maxStrikeDistance = 2.0f;
    public float velocityThreshold = 0.1f; // 더 낮춘 속도 임계값

    // 립모션 컨트롤러
    private Controller leapController;

    // 손 상태 데이터
    public Vector3 LeftHandPosition { get; private set; }
    public Vector3 RightHandPosition { get; private set; }
    public Quaternion LeftHandRotation { get; private set; }
    public Quaternion RightHandRotation { get; private set; }
    public float RightHandGrabStrength { get; private set; }
    public Vector3 RightHandVelocity { get; private set; }

    // 부드러운 움직임을 위한 변수
    private Vector3 targetLeftPos;
    private Vector3 targetRightPos;
    private Quaternion targetLeftRot;
    private Quaternion targetRightRot;

    // 타격 감지
    public bool IsStrikeDetected { get; private set; }
    private bool wasGripping = false;

    // 이벤트
    public System.Action<Vector3, Vector3, float> OnHammerStrike;

    // 테스트 모드 변수
    private Vector3 testLeftHandPos = new Vector3(-0.5f, 1.2f, 0f);
    private Vector3 testRightHandPos = new Vector3(0.5f, 1.2f, 0f);

    // 디버그용
    private float lastDebugTime = 0f;
    private bool hasReceivedValidData = false;

    void Start()
    {
        InitializeHandController();
    }

    void InitializeHandController()
    {
        Debug.Log("=== HandController 초기화 시작 ===");

        // 립모션 초기화
        if (useLeapMotion)
        {
            try
            {
                leapController = new Controller();

                // 립모션 설정 확인
                if (leapController.IsConnected)
                {
                    Debug.Log("립모션 디바이스 연결됨!");
                }
                else
                {
                    Debug.LogWarning("립모션 디바이스가 연결되지 않음 - 테스트 모드 활성화");
                    enableTestMode = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"립모션 초기화 실패: {e.Message}");
                useLeapMotion = false;
                enableTestMode = true;
            }
        }

        // 초기 위치 설정
        LeftHandPosition = testLeftHandPos;
        RightHandPosition = testRightHandPos;
        targetLeftPos = testLeftHandPos;
        targetRightPos = testRightHandPos;

        LeftHandRotation = Quaternion.identity;
        RightHandRotation = Quaternion.identity;
        targetLeftRot = Quaternion.identity;
        targetRightRot = Quaternion.identity;

        UpdateHandVisuals();
    }

    void Update()
    {
        bool leapDataReceived = false;

        // 립모션 데이터 처리
        if (useLeapMotion && leapController != null)
        {
            leapDataReceived = ProcessLeapMotionData();
        }

        // 립모션 데이터가 없으면 테스트 모드
        if (!leapDataReceived && enableTestMode)
        {
            UpdateTestModeHands();
        }

        // 부드러운 보간 적용
        ApplySmoothing();

        // 타격 감지 및 시각화 업데이트
        DetectHammerStrike();
        UpdateHandVisuals();

        // 디버그 출력 (10초마다)
        if (Time.time - lastDebugTime > 10f)
        {
            PrintDebugInfo(leapDataReceived);
            lastDebugTime = Time.time;
        }
    }

    bool ProcessLeapMotionData()
    {
        if (leapController == null || !leapController.IsConnected)
        {
            return false;
        }

        Frame frame = leapController.Frame();

        // 프레임에 손이 있는지 확인
        if (frame.Hands.Count == 0)
        {
            return false;
        }

        bool dataProcessed = false;

        foreach (Hand hand in frame.Hands)
        {
            if (hand == null) continue;

            // 립모션 데이터를 Unity 좌표로 변환
            Vector3 palmPos;
            Vector3 palmNormal;
            Vector3 direction;

            if (useRawCoordinates)
            {
                // 원본 좌표 그대로 사용 (립모션은 mm 단위)
                palmPos = new Vector3(
                    hand.PalmPosition.x,
                    hand.PalmPosition.y,
                    invertZ ? -hand.PalmPosition.z : hand.PalmPosition.z
                ) * coordinateScale;

                palmNormal = new Vector3(
                    hand.PalmNormal.x,
                    hand.PalmNormal.y,
                    invertZ ? -hand.PalmNormal.z : hand.PalmNormal.z
                );

                direction = new Vector3(
                    hand.Direction.x,
                    hand.Direction.y,
                    invertZ ? -hand.Direction.z : hand.Direction.z
                );
            }
            else
            {
                // 기존 변환 방식 (Unity 미터 단위로 변환)
                Vector3 leapPos = new Vector3(hand.PalmPosition.x, hand.PalmPosition.y, hand.PalmPosition.z);
                Vector3 leapNormal = new Vector3(hand.PalmNormal.x, hand.PalmNormal.y, hand.PalmNormal.z);
                Vector3 leapDir = new Vector3(hand.Direction.x, hand.Direction.y, hand.Direction.z);

                palmPos = ConvertLeapToUnity(leapPos);
                palmNormal = ConvertLeapDirectionToUnity(leapNormal);
                direction = ConvertLeapDirectionToUnity(leapDir);
            }

            if (hand.IsLeft)
            {
                targetLeftPos = palmPos;
                targetLeftRot = Quaternion.LookRotation(direction, palmNormal);
                dataProcessed = true;
            }
            else if (hand.IsRight)
            {
                targetRightPos = palmPos;
                targetRightRot = Quaternion.LookRotation(direction, palmNormal);

                // 오른손 추가 데이터
                RightHandGrabStrength = hand.GrabStrength;

                // PalmVelocity 처리
                if (useRawCoordinates)
                {
                    RightHandVelocity = new Vector3(
                        hand.PalmVelocity.x,
                        hand.PalmVelocity.y,
                        invertZ ? -hand.PalmVelocity.z : hand.PalmVelocity.z
                    ) * coordinateScale;
                }
                else
                {
                    Vector3 velocity = new Vector3(
                        hand.PalmVelocity.x,
                        hand.PalmVelocity.y,
                        hand.PalmVelocity.z
                    );
                    RightHandVelocity = velocity * 0.001f; // mm/s to m/s
                }

                dataProcessed = true;
            }
        }

        if (dataProcessed && !hasReceivedValidData)
        {
            hasReceivedValidData = true;
        }

        return dataProcessed;
    }

    Vector3 ConvertLeapToUnity(Vector3 leapVector)
    {
        // 립모션 좌표를 Unity 좌표로 변환 (mm to m)
        float x = leapVector.x * 0.001f;
        float y = leapVector.y * 0.001f;
        float z = leapVector.z * 0.001f;

        if (invertZ)
        {
            z = -z;
        }

        return new Vector3(x, y, z);
    }

    Vector3 ConvertLeapDirectionToUnity(Vector3 leapVector)
    {
        // 방향 벡터는 크기 변환 없이 방향만 변환
        float x = leapVector.x;
        float y = leapVector.y;
        float z = leapVector.z;

        if (invertZ)
        {
            z = -z;
        }

        return new Vector3(x, y, z).normalized;
    }

    void ApplySmoothing()
    {
        // 부드러운 보간 적용
        float deltaTime = Time.deltaTime * smoothSpeed;

        // 위치와 회전 보간
        LeftHandPosition = Vector3.Lerp(LeftHandPosition, targetLeftPos, deltaTime);
        RightHandPosition = Vector3.Lerp(RightHandPosition, targetRightPos, deltaTime);
        LeftHandRotation = Quaternion.Slerp(LeftHandRotation, targetLeftRot, deltaTime);
        RightHandRotation = Quaternion.Slerp(RightHandRotation, targetRightRot, deltaTime);

        // Y축 좌표가 너무 낮으면 경고
        if (LeftHandPosition.y < 0.1f && targetLeftPos.y > 0.5f)
        {
            Debug.LogWarning($"왼손 Y축 불일치! 목표: {targetLeftPos.y:F2}, 현재: {LeftHandPosition.y:F2}");
            // 강제로 Y축 동기화
            LeftHandPosition = new Vector3(LeftHandPosition.x, targetLeftPos.y, LeftHandPosition.z);
        }

        if (RightHandPosition.y < 0.1f && targetRightPos.y > 0.5f)
        {
            Debug.LogWarning($"오른손 Y축 불일치! 목표: {targetRightPos.y:F2}, 현재: {RightHandPosition.y:F2}");
            // 강제로 Y축 동기화
            RightHandPosition = new Vector3(RightHandPosition.x, targetRightPos.y, RightHandPosition.z);
        }
    }

    void UpdateTestModeHands()
    {
        // 테스트 모드 손 조작
        Vector3 leftMove = Vector3.zero;
        if (Input.GetKey(leftHandUpKey)) leftMove += Vector3.up;
        if (Input.GetKey(leftHandDownKey)) leftMove += Vector3.down;
        if (Input.GetKey(leftHandLeftKey)) leftMove += Vector3.left;
        if (Input.GetKey(leftHandRightKey)) leftMove += Vector3.right;

        testLeftHandPos += leftMove * handMoveSpeed * Time.deltaTime;
        testRightHandPos = testLeftHandPos + Vector3.right * 0.6f;

        targetLeftPos = testLeftHandPos;
        targetRightPos = testRightHandPos;

        // 테스트 타격
        if (Input.GetKeyDown(hammerStrikeKey))
        {
            RightHandGrabStrength = 1.0f;
            RightHandVelocity = Vector3.down * 0.5f;
        }
        else if (Input.GetKeyUp(hammerStrikeKey))
        {
            RightHandGrabStrength = 0f;
            RightHandVelocity = Vector3.zero;
        }
    }

    void DetectHammerStrike()
    {
        IsStrikeDetected = false;

        bool isGripping = RightHandGrabStrength > strikeDetectionThreshold;
        bool hasVelocity = RightHandVelocity.magnitude > velocityThreshold;
        float distance = Vector3.Distance(LeftHandPosition, RightHandPosition);
        bool isInRange = distance <= maxStrikeDistance;

        if (!wasGripping && isGripping && hasVelocity && isInRange)
        {
            IsStrikeDetected = true;

            Vector3 strikePosition = GetChiselTargetPoint();
            Vector3 strikeDirection = (strikePosition - RightHandPosition).normalized;

            OnHammerStrike?.Invoke(strikePosition, strikeDirection, RightHandGrabStrength);

            Debug.Log($"타격 감지! 위치: {strikePosition:F2}");
        }

        wasGripping = isGripping;
    }

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

            float scale = 1f + RightHandGrabStrength * 0.2f;
            rightHandVisual.localScale = Vector3.one * scale;
        }
    }

    public Vector3 GetChiselTargetPoint()
    {
        Vector3 chiselForward = LeftHandRotation * Vector3.forward;
        Ray chiselRay = new Ray(LeftHandPosition, chiselForward);

        RaycastHit hit;
        if (Physics.Raycast(chiselRay, out hit, 2f))
        {
            return hit.point;
        }

        return LeftHandPosition + chiselForward * 0.5f;
    }

    void PrintDebugInfo(bool leapDataReceived)
    {
        string status = useLeapMotion ?
            (leapDataReceived ? "립모션 활성" : "립모션 대기중") :
            "테스트 모드";

        Debug.Log($"[HandController] 상태: {status}");
        Debug.Log($"왼손: {LeftHandPosition:F2}, 오른손: {RightHandPosition:F2}");
        Debug.Log($"그립: {RightHandGrabStrength:F2}, 속도: {RightHandVelocity.magnitude:F2}");

        if (leapController != null)
        {
            Debug.Log($"립모션 연결: {leapController.IsConnected}, 서비스: {leapController.IsServiceConnected}");
        }
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
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(LeftHandPosition, maxStrikeDistance);

        // 끌 방향 - 초록색
        Gizmos.color = Color.green;
        Vector3 chiselForward = LeftHandRotation * Vector3.forward;
        Gizmos.DrawRay(LeftHandPosition, chiselForward * 0.5f);
    }
}