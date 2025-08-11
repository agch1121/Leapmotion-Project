using UnityEngine;
using Leap;

/// <summary>
/// 립모션 기반 양손 추적 시스템 - 강화된 망치 타격 감지
/// 왼손: 끌 고정, 오른손: 망치 고정
/// </summary>
public class HandController : MonoBehaviour
{
    [Header("립모션 설정")]
    public bool useLeapMotion = true;
    public float LeftHandGrabStrength { get; private set; }

    [Header("커스텀 쥐는 강도 시스템")]
    public bool useCustomGrabStrength = true;
    private GripCalculator gripCalculator;

    [Header("채굴 동작 검출")]
    public float maxVelocityForStrike = 2.0f; // 타격 속도 계산용 최댓값
    public float minDownwardVelocity = 0.05f; // 최소 아래쪽 속도 (기존 0.2f에서 적당히 완화)
    public float minTotalVelocity = 0.1f; // 최소 전체 속도 (기존 0.3f에서 적당히 완화)

    [Header("테스트 모드")]
    public bool enableTestMode = true;
    public KeyCode leftHandUpKey = KeyCode.W;
    public KeyCode leftHandDownKey = KeyCode.S;
    public KeyCode leftHandLeftKey = KeyCode.A;
    public KeyCode leftHandRightKey = KeyCode.D;
    public KeyCode hammerStrikeKey = KeyCode.Space;

    [Header("립모션 좌표 변환")]
    public bool useRawCoordinates = true;
    public float coordinateScale = 1f;
    public bool invertZ = false;

    [Header("손 시각화")]
    public Transform leftHandVisual;
    public Transform rightHandVisual;
    public float handMoveSpeed = 2f;
    public float smoothSpeed = 10f;

    [Header("채굴 설정")]
    public float strikeDetectionThreshold = 0.3f;
    public float maxStrikeDistance = 2.0f;
    public float velocityThreshold = 0.03f;

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

        InitializeGripCalculator();

        // 립모션 초기화
        if (useLeapMotion)
        {
            try
            {
                leapController = new Controller();

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

    void InitializeGripCalculator()
    {
        if (useCustomGrabStrength)
        {
            gripCalculator = GetComponent<GripCalculator>();
            if (gripCalculator == null)
            {
                gripCalculator = gameObject.AddComponent<GripCalculator>();
                Debug.Log("GripCalculator 컴포넌트 자동 생성됨");
            }

            Debug.Log("커스텀 쥐는 강도 시스템 활성화");
        }
        else
        {
            Debug.Log("기본 Leap Motion GrabStrength 사용");
        }
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

        if (frame.Hands.Count == 0)
        {
            return false;
        }

        bool dataProcessed = false;

        foreach (Hand hand in frame.Hands)
        {
            if (hand == null) continue;

            Vector3 palmPos;
            Vector3 palmNormal;
            Vector3 direction;

            if (useRawCoordinates)
            {
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
                LeftHandGrabStrength = hand.GrabStrength;
                dataProcessed = true;
            }
            else if (hand.IsRight)
            {
                targetRightPos = palmPos;
                targetRightRot = Quaternion.LookRotation(direction, palmNormal);

                // 수정된 부분: 커스텀 쥐는 강도 사용
                if (useCustomGrabStrength && gripCalculator != null)
                {
                    RightHandGrabStrength = gripCalculator.CustomGrabStrength;
                }
                else
                {
                    RightHandGrabStrength = hand.GrabStrength;
                }

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
        float deltaTime = Time.deltaTime * smoothSpeed;

        LeftHandPosition = Vector3.Lerp(LeftHandPosition, targetLeftPos, deltaTime);
        RightHandPosition = Vector3.Lerp(RightHandPosition, targetRightPos, deltaTime);
        LeftHandRotation = Quaternion.Slerp(LeftHandRotation, targetLeftRot, deltaTime);
        RightHandRotation = Quaternion.Slerp(RightHandRotation, targetRightRot, deltaTime);

        // Y좌표가 너무 낮으면 경고
        if (LeftHandPosition.y < 0.1f && targetLeftPos.y > 0.5f)
        {
            Debug.LogWarning($"왼손 Y좌표 비정상! 목표: {targetLeftPos.y:F2}, 현재: {LeftHandPosition.y:F2}");
            LeftHandPosition = new Vector3(LeftHandPosition.x, targetLeftPos.y, LeftHandPosition.z);
        }

        if (RightHandPosition.y < 0.1f && targetRightPos.y > 0.5f)
        {
            Debug.LogWarning($"오른손 Y좌표 비정상! 목표: {targetRightPos.y:F2}, 현재: {RightHandPosition.y:F2}");
            RightHandPosition = new Vector3(RightHandPosition.x, targetRightPos.y, RightHandPosition.z);
        }
    }

    void UpdateTestModeHands()
    {
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
    public bool enableDetailedDebug = true;
    public bool forceEnableStrike = false; // 강제 채굴 활성화 (테스트용)
    void DetectHammerStrike()
    {
        IsStrikeDetected = false;

        // 모든 조건을 개별적으로 체크하고 로그 출력
        bool isGripping = RightHandGrabStrength > strikeDetectionThreshold;
        bool hasVelocity = RightHandVelocity.magnitude > velocityThreshold;
        float distance = Vector3.Distance(LeftHandPosition, RightHandPosition);
        bool isInRange = distance <= maxStrikeDistance;

        bool hasDownwardMotion = CheckDownward();
        bool hasSwingMotion = CheckSwing();
        bool meetsMinimumForce = CheckMinForce();

        // 상세한 디버그 로그
        if (enableDetailedDebug && isGripping)
        {
            Debug.Log($"=== 채굴 조건 체크 ===");
            Debug.Log($"쥐는 강도: {RightHandGrabStrength:F3} > {strikeDetectionThreshold} = {isGripping}");
            Debug.Log($"속도: {RightHandVelocity.magnitude:F3} > {velocityThreshold} = {hasVelocity}");
            Debug.Log($"거리: {distance:F2} <= {maxStrikeDistance} = {isInRange}");
            Debug.Log($"아래쪽 움직임: {-RightHandVelocity.y:F3} > {minDownwardVelocity} = {hasDownwardMotion}");
            Debug.Log($"휘두르기: {hasSwingMotion}");
            Debug.Log($"최소 힘: {CalcStrikeForce():F3} > 0.03 = {meetsMinimumForce}");
            Debug.Log($"이전 쥐기 상태: {wasGripping}");
        }

        // 강제 모드 또는 기존 조건
        bool shouldStrike = false;

        if (forceEnableStrike)
        {
            // 강제 모드: 쥐기만 하면 채굴
            shouldStrike = !wasGripping && isGripping;
            if (enableDetailedDebug && shouldStrike)
            {
                Debug.Log("강제 모드로 채굴 실행!");
            }
        }
        else
        {
            // 매우 완화된 조건들
            bool basicCondition = !wasGripping && isGripping && isInRange;

            // 속도 조건을 더욱 완화 - 하나라도 만족하면 OK
            bool anyMotion = hasVelocity || hasDownwardMotion || hasSwingMotion;

            // 힘 조건도 더 완화
            bool hasAnyForce = RightHandGrabStrength > 0.1f; // 매우 낮은 기준

            shouldStrike = basicCondition && (anyMotion || hasAnyForce);

            if (enableDetailedDebug && basicCondition)
            {
                Debug.Log($"기본 조건 만족, 추가 조건: 움직임={anyMotion}, 힘={hasAnyForce}");
            }
        }

        if (shouldStrike)
        {
            IsStrikeDetected = true;
            ExecuteStrike();
        }

        wasGripping = isGripping;
    }

    void ExecuteStrike()
    {
        Vector3 strikePosition = GetChiselTargetPoint();
        Vector3 strikeDirection = (strikePosition - RightHandPosition).normalized;
        float strikeForce = CalcStrikeForce();

        // 최소 힘이라도 보장
        if (strikeForce < 0.1f)
        {
            strikeForce = 0.1f;
        }

        Debug.Log($"채굴 실행! 힘: {strikeForce:F3}, 위치: {strikePosition}");

        OnHammerStrike?.Invoke(strikePosition, strikeDirection, strikeForce);
    }

    bool CheckDownward()
    {
        float downwardVelocity = -RightHandVelocity.y;
        bool result = downwardVelocity > 0.02f; // 매우 작은 값

        if (enableDetailedDebug)
        {
            Debug.Log($"아래쪽 속도: {downwardVelocity:F3} > 0.02 = {result}");
        }

        return result;
    }

    bool CheckSwing()
    {
        float totalSpeed = RightHandVelocity.magnitude;

        if (totalSpeed < minTotalVelocity) // 0.1f
        {
            if (enableDetailedDebug)
            {
                Debug.Log($"총 속도 부족: {totalSpeed:F3} < {minTotalVelocity}");
            }
            return false;
        }

        // 아래쪽 움직임 비율 계산
        float downwardSpeed = Mathf.Abs(RightHandVelocity.y);
        float downwardRatio = totalSpeed > 0.01f ? downwardSpeed / totalSpeed : 0f;

        // 아래비율 조건을 5%로 대폭 완화 (기존 20%에서)
        bool result = downwardRatio > 0.05f; // 기존 0.2f에서 0.05f로 완화

        if (enableDetailedDebug)
        {
            Debug.Log($"휘두르기 체크: 총속도={totalSpeed:F3}, 아래비율={downwardRatio:F3} > 0.05 = {result}");
        }

        return result;
    }


    bool CheckMinForce()
    {
        float calculatedForce = CalcStrikeForce();
        bool result = calculatedForce > 0.01f; // 매우 낮은 기준

        if (enableDetailedDebug)
        {
            Debug.Log($"계산된 힘: {calculatedForce:F3} > 0.01 = {result}");
        }

        return result;
    }

    float CalcStrikeForce()
    {
        // 속도 기여분 (60%)
        float velocityContribution = Mathf.Clamp01(RightHandVelocity.magnitude / maxVelocityForStrike) * 0.6f;

        // 쥐는 강도 기여분 (40%)
        float gripContribution = RightHandGrabStrength * 0.4f;

        return Mathf.Clamp01(velocityContribution + gripContribution);
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

        string gripType = useCustomGrabStrength ? "커스텀" : "기본";

        Debug.Log($"[HandController] 상태: {status}");
        Debug.Log($"왼손: {LeftHandPosition:F2}, 오른손: {RightHandPosition:F2}");
        Debug.Log($"쥐는 강도 ({gripType}): {RightHandGrabStrength:F2}, 속도: {RightHandVelocity.magnitude:F2}");

        if (useCustomGrabStrength && gripCalculator != null)
        {
            Debug.Log($"커스텀 쥐는 강도 상세: 민감도={gripCalculator.sensitivity:F1}");
        }

        if (leapController != null)
        {
            Debug.Log($"립모션 연결: {leapController.IsConnected}, 서비스: {leapController.IsServiceConnected}");
        }
    }

    public void SetCustomGripEnabled(bool enabled)
    {
        useCustomGrabStrength = enabled;

        if (enabled && gripCalculator == null)
        {
            InitializeGripCalculator();
        }

        Debug.Log($"커스텀 쥐는 강도 시스템: {(enabled ? "활성화" : "비활성화")}");
    }

    public string GetGripSystemInfo()
    {
        if (useCustomGrabStrength && gripCalculator != null)
        {
            return $"커스텀 (민감도: {gripCalculator.sensitivity:F1})";
        }
        return "기본 Leap Motion";
    }

    [ContextMenu("쥐는 강도 시스템 전환")]
    public void ToggleGripSystem()
    {
        SetCustomGripEnabled(!useCustomGrabStrength);
    }

    [ContextMenu("현재 손 상태 출력")]
    public void PrintCurrentHandStatus()
    {
        Debug.Log("=== 현재 손 상태 ===");
        Debug.Log($"쥐는 강도 시스템: {GetGripSystemInfo()}");
        Debug.Log($"왼손 위치: {LeftHandPosition:F2}");
        Debug.Log($"오른손 위치: {RightHandPosition:F2}");
        Debug.Log($"쥐는 강도: {RightHandGrabStrength:F2}");
        Debug.Log($"손 속도: {RightHandVelocity.magnitude:F2}");
        Debug.Log($"타격 감지: {IsStrikeDetected}");
        Debug.Log("===================");
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

        // 쥐는 강도 시각화 - 주황색 (강도에 따라 크기 변함)
        if (useCustomGrabStrength)
        {
            Gizmos.color = Color.magenta;
            float gripSize = 0.02f + (RightHandGrabStrength * 0.08f);
            Gizmos.DrawSphere(RightHandPosition + Vector3.up * 0.1f, gripSize);
        }
    }
}