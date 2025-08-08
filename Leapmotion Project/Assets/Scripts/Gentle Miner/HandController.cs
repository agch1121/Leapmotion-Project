using UnityEngine;
using Leap;

/// <summary>
/// 강화된 HandController - 도구 탈부착 감지 기능 추가
/// 기존 기능에 손가락 펼치기/웅크리기 감지 추가
/// </summary>
public class HandController : MonoBehaviour
{
    [Header("립모션 설정")]
    public bool useLeapMotion = true;

    [Header("커스텀 쥐는 강도 시스템")]
    public bool useCustomGrabStrength = true;
    private GripCalculator gripCalculator;

    [Header("타격 동작 감지")]
    public float maxVelocityForStrike = 2.0f;
    public float minDownwardVelocity = 0.2f;
    public float minTotalVelocity = 0.3f;

    [Header("도구 탈부착 감지")]
    [Range(0.1f, 1.0f)]
    public float handOpenThreshold = 0.8f;     // 손 펼치기 감지 임계값
    [Range(0.1f, 1.0f)]
    public float handCloseThreshold = 0.3f;    // 손 웅크리기 감지 임계값
    public float stateChangeDelay = 0.5f;      // 상태 변경 지연 시간 (오인식 방지)

    [Header("테스트 모드")]
    public bool enableTestMode = true;
    public KeyCode leftHandUpKey = KeyCode.W;
    public KeyCode leftHandDownKey = KeyCode.S;
    public KeyCode leftHandLeftKey = KeyCode.A;
    public KeyCode leftHandRightKey = KeyCode.D;
    public KeyCode hammerStrikeKey = KeyCode.Space;
    public KeyCode toggleToolsKey = KeyCode.T;  // 도구 토글 테스트키

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
    public float strikeDetectionThreshold = 0.5f;
    public float maxStrikeDistance = 2.0f;
    public float velocityThreshold = 0.1f;

    // 립모션 컨트롤러
    private Controller leapController;

    // 손 상태 데이터
    public Vector3 LeftHandPosition { get; private set; }
    public Vector3 RightHandPosition { get; private set; }
    public Quaternion LeftHandRotation { get; private set; }
    public Quaternion RightHandRotation { get; private set; }
    public float RightHandGrabStrength { get; private set; }
    public Vector3 RightHandVelocity { get; private set; }

    // 도구 상태 관리
    public enum ToolState
    {
        Attached,    // 도구 착용
        Detached     // 맨손
    }

    [Header("현재 도구 상태")]
    [SerializeField] private ToolState currentToolState = ToolState.Attached;
    public ToolState CurrentToolState => currentToolState;

    // 손가락 상태 추적
    private float lastStateChangeTime = 0f;
    private bool pendingStateChange = false;
    private ToolState pendingToolState;

    // 부드러운 움직임을 위한 변수
    private Vector3 targetLeftPos;
    private Vector3 targetRightPos;
    private Quaternion targetLeftRot;
    private Quaternion targetRightRot;

    // 타격 감지
    public bool IsStrikeDetected { get; private set; }
    private bool wasGripping = false;

    // 이벤트
    public System.Action<Vector3, Vector3, float> OnHammerStrike; // (hammerPos, chiselTarget, force)
    public System.Action<ToolState> OnToolStateChanged;  // 새로운 이벤트

    // 테스트 모드 변수
    private Vector3 testLeftHandPos = new Vector3(-0.5f, 1.2f, 0f);
    private Vector3 testRightHandPos = new Vector3(0.5f, 1.2f, 0f);
    private bool testToolsEnabled = true;

    // 디버그용
    private float lastDebugTime = 0f;
    private bool hasReceivedValidData = false;

    // ★ 끌 타겟 좌표 제공용
    [SerializeField] private ToolSystem toolSystem;

    void Start()
    {
        InitializeHandController();
    }

    void InitializeHandController()
    {
        Debug.Log("=== 강화된 HandController 초기화 시작 ===");

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

        Debug.Log("=== 강화된 HandController 초기화 완료 ===");
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

        // 도구 상태 감지 및 업데이트
        UpdateToolState();

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

    /// <summary>
    /// 도구 상태 감지 및 업데이트 (새로운 기능)
    /// </summary>
    void UpdateToolState()
    {
        bool shouldDetachTools = DetectHandOpen();
        bool shouldAttachTools = DetectHandClosed();

        // 상태 변경 로직
        if (shouldDetachTools && currentToolState == ToolState.Attached)
        {
            if (!pendingStateChange)
            {
                pendingStateChange = true;
                pendingToolState = ToolState.Detached;
                lastStateChangeTime = Time.time;
            }
            else if (pendingToolState == ToolState.Detached &&
                     Time.time - lastStateChangeTime >= stateChangeDelay)
            {
                ChangeToolState(ToolState.Detached);
                pendingStateChange = false;
            }
        }
        else if (shouldAttachTools && currentToolState == ToolState.Detached)
        {
            if (!pendingStateChange)
            {
                pendingStateChange = true;
                pendingToolState = ToolState.Attached;
                lastStateChangeTime = Time.time;
            }
            else if (pendingToolState == ToolState.Attached &&
                     Time.time - lastStateChangeTime >= stateChangeDelay)
            {
                ChangeToolState(ToolState.Attached);
                pendingStateChange = false;
            }
        }
        else if (shouldDetachTools && pendingToolState == ToolState.Attached ||
                 shouldAttachTools && pendingToolState == ToolState.Detached)
        {
            // 반대 동작이 감지되면 대기 중인 상태 변경 취소
            pendingStateChange = false;
        }
    }

    /// <summary>
    /// 손 펼치기 감지 (모든 손가락이 펴져 있는지)
    /// </summary>
    bool DetectHandOpen()
    {
        if (!useLeapMotion || leapController == null)
        {
            // 테스트 모드에서는 키보드 입력으로 처리
            return false;
        }

        Frame frame = leapController.Frame();
        Hand rightHand = frame.Hands.Find(h => h.IsRight);

        if (rightHand == null) return false;

        // 모든 손가락이 펴져 있는지 확인
        int extendedFingers = 0;
        foreach (Finger finger in rightHand.fingers)
        {
            if (finger.IsExtended)
            {
                extendedFingers++;
            }
        }

        // 5개 손가락 모두 펴져 있고, GrabStrength가 낮은 경우
        bool allFingersExtended = extendedFingers >= 4; // 4개 이상 (엄지 제외 가능)
        bool lowGrabStrength = RightHandGrabStrength < (1f - handOpenThreshold);

        return allFingersExtended && lowGrabStrength;
    }

    /// <summary>
    /// 손 웅크리기 감지 (주먹 쥐기)
    /// </summary>
    bool DetectHandClosed()
    {
        if (!useLeapMotion || leapController == null)
        {
            // 테스트 모드에서는 키보드 입력으로 처리
            return false;
        }

        Frame frame = leapController.Frame();
        Hand rightHand = frame.Hands.Find(h => h.IsRight);

        if (rightHand == null) return false;

        // 대부분의 손가락이 접혀 있는지 확인
        int extendedFingers = 0;
        foreach (Finger finger in rightHand.fingers)
        {
            if (finger.IsExtended)
            {
                extendedFingers++;
            }
        }

        // 1개 이하 손가락만 펴져 있고, GrabStrength가 높은 경우
        bool mostFingersfolded = extendedFingers <= 1;
        bool highGrabStrength = RightHandGrabStrength > handCloseThreshold;

        return mostFingersfolded && highGrabStrength;
    }

    /// <summary>
    /// 도구 상태 변경
    /// </summary>
    void ChangeToolState(ToolState newState)
    {
        if (currentToolState != newState)
        {
            ToolState previousState = currentToolState;
            currentToolState = newState;

            Debug.Log($"도구 상태 변경: {previousState} → {newState}");

            // 이벤트 발생
            OnToolStateChanged?.Invoke(newState);
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

        // 테스트 도구 토글
        if (Input.GetKeyDown(toggleToolsKey))
        {
            testToolsEnabled = !testToolsEnabled;
            ChangeToolState(testToolsEnabled ? ToolState.Attached : ToolState.Detached);
        }
    }

    void DetectHammerStrike()
    {
        // 도구가 탈착된 상태면 타격 감지 안함
        if (currentToolState == ToolState.Detached)
        {
            IsStrikeDetected = false;
            wasGripping = false;
            return;
        }

        IsStrikeDetected = false;

        bool isGripping = RightHandGrabStrength > strikeDetectionThreshold;
        bool hasVelocity = RightHandVelocity.magnitude > velocityThreshold;
        float distance = Vector3.Distance(LeftHandPosition, RightHandPosition);
        bool isInRange = distance <= maxStrikeDistance;

        // 새로 추가된 조건들
        bool hasDownwardMotion = CheckDownward();
        bool hasSwingMotion = CheckSwing();
        bool meetsMinimumForce = CheckMinForce();

        // 강화된 타격 감지 조건
        if (!wasGripping && isGripping && hasVelocity && isInRange &&
            hasDownwardMotion && hasSwingMotion && meetsMinimumForce)
        {
            IsStrikeDetected = true;

            // 실제 망치 위치와 끌 타겟 위치 분리 전달 (핵심 수정)
            Vector3 actualHammerPosition = RightHandPosition;
            Vector3 chiselTargetPosition = GetChiselTargetPoint();
            Vector3 strikeDirection = (chiselTargetPosition - actualHammerPosition).normalized;

            // 타격 힘 강도 계산 (속도 + 쥐는 강도)
            float strikeForce = CalcStrikeForce();

            // 디버그 로그
            Debug.Log($"=== 타격 감지 디버그 ===");
            Debug.Log($"실제 망치 위치: {actualHammerPosition}");
            Debug.Log($"끌 타겟 위치: {chiselTargetPosition}");
            Debug.Log($"거리 계산용: 망치={actualHammerPosition}, 타겟={chiselTargetPosition}");
            Debug.Log("==================");

            // 이벤트: (hammerPos, chiselTarget, force)
            OnHammerStrike?.Invoke(actualHammerPosition, chiselTargetPosition, strikeForce);

            Debug.Log($"망치 타격 감지! 망치위치: {actualHammerPosition:F2}, 타겟위치: {chiselTargetPosition:F2}");
        }

        wasGripping = isGripping;
    }

    bool CheckDownward()
    {
        float downwardVelocity = -RightHandVelocity.y;
        return downwardVelocity > minDownwardVelocity;
    }

    bool CheckSwing()
    {
        float totalSpeed = RightHandVelocity.magnitude;
        float downwardSpeed = Mathf.Abs(RightHandVelocity.y);

        if (totalSpeed < minTotalVelocity) return false;

        float downwardRatio = downwardSpeed / totalSpeed;
        return downwardRatio > 0.3f;
    }

    bool CheckMinForce()
    {
        float calculatedForce = CalcStrikeForce();
        return calculatedForce > 0.1f;
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
        // 우선 ToolSystem에서 끌 팁 좌표를 받아 사용 (정확도 ↑)
        if (toolSystem != null)
        {
            return toolSystem.GetChiselTargetPosition();
        }

        // 폴백: 왼손 진행방향으로 레이캐스트
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
        Debug.Log($"도구 상태: {currentToolState}");

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

    /// <summary>
    /// 수동으로 도구 상태 설정 (테스트용)
    /// </summary>
    public void SetToolState(ToolState state)
    {
        ChangeToolState(state);
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
        Debug.Log($"도구 상태: {currentToolState}");
        Debug.Log("===================");
    }

    [ContextMenu("도구 상태 토글")]
    public void ToggleToolState()
    {
        ToolState newState = currentToolState == ToolState.Attached ?
                            ToolState.Detached : ToolState.Attached;
        ChangeToolState(newState);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 왼손 (끌) - 파란색
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(LeftHandPosition, 0.05f);

        // 오른손 (망치) - 빨간색
        Gizmos.color = currentToolState == ToolState.Attached ? Color.red : Color.gray;
        Gizmos.DrawWireSphere(RightHandPosition, 0.05f);

        // 타격 범위 - 노란색
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(LeftHandPosition, maxStrikeDistance);

        // 끌 방향 - 초록색
        Gizmos.color = Color.green;
        Vector3 chiselForward = LeftHandRotation * Vector3.forward;
        Gizmos.DrawRay(LeftHandPosition, chiselForward * 0.5f);

        // 쥐는 강도 시각화 - 자홍색 (강도에 따라 크기 변함)
        if (useCustomGrabStrength)
        {
            Gizmos.color = Color.magenta;
            float gripSize = 0.02f + (RightHandGrabStrength * 0.08f);
            Gizmos.DrawSphere(RightHandPosition + Vector3.up * 0.1f, gripSize);
        }

        // 도구 상태 표시
        if (currentToolState == ToolState.Detached)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(RightHandPosition + Vector3.up * 0.15f, Vector3.one * 0.05f);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제는 다른 시스템에서 처리
    }
}
