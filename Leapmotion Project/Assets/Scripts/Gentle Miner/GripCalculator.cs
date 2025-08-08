using UnityEngine;
using Leap;

/// <summary>
/// 손가락 굽힘 각도 기반의 커스텀 쥐는 강도 계산 시스템
/// Leap Motion의 GrabStrength 대신 손가락 각도 데이터를 직접 계산하여
/// 더 정밀한 '쥐는 강도' 값을 제공
/// </summary>
public class GripCalculator : MonoBehaviour
{
    [Header("민감도 설정")]
    [Range(0.1f, 2.0f)]
    public float sensitivity = 1.0f; // 최종 계산값에 곱해줄 민감도 배율
    [Range(0.5f, 1.5f)]
    public float thumbWeight = 1.0f; // 엄지손가락에 부여할 가중치 (다른 손가락보다 영향력 크게/작게 조정)
    [Range(0.1f, 1.0f)]
    public float minimumThreshold = 0.1f; // 손가락 굽힘이 이 값 미만이면 0으로 처리

    [Header("디버그")]
    public bool enableDebugLogs = false; // 디버그 로그 출력 여부
    public bool showFingerValues = false; // 손가락별 값 표시 여부 (미사용)

    // 계산된 최종 쥐는 강도 값
    public float CustomGrabStrength { get; private set; }
    // 각 손가락별 굽힘 값 (0~1 범위)
    public float[] FingerCurlValues { get; private set; } = new float[5];

    // 손가락 이름 배열 (디버그 출력용)
    private readonly string[] fingerNames = { "엄지", "검지", "중지", "약지", "새끼" };

    // Leap Motion Controller 참조
    private Controller leapController;

    // 디버그 로그 주기를 제어하기 위한 시간 기록 변수
    private float lastConsoleDebugTime = 0f;

    void Start()
    {
        // Leap Motion 컨트롤러 생성
        leapController = new Controller();
        Debug.Log("GripCalculator 초기화 완료");
    }

    void Update()
    {
        // Leap Motion 연결 안 됐으면 강도 0
        if (leapController == null || !leapController.IsConnected)
        {
            CustomGrabStrength = 0f;
            return;
        }

        // 현재 프레임 데이터 가져오기
        Frame frame = leapController.Frame();

        // 오른손 데이터만 찾기
        Hand rightHand = frame.Hands.Find(h => h.IsRight);

        if (rightHand != null)
        {
            // 손가락 각도를 기반으로 쥐는 강도 계산
            CustomGrabStrength = CalculateGrip(rightHand);
        }
        else
        {
            CustomGrabStrength = 0f;
        }

        // 디버그 로그 (1초마다)
        if (enableDebugLogs && Time.time - lastConsoleDebugTime > 1f)
        {
            PrintQuickStatus();
            lastConsoleDebugTime = Time.time;
        }
    }

    /// <summary>
    /// 한 손의 손가락 데이터를 기반으로 평균 굽힘 강도를 계산
    /// </summary>
    float CalculateGrip(Hand hand)
    {
        if (hand.fingers == null || hand.fingers.Length == 0)
            return 0f;

        float totalFingerCurl = 0f;
        int validFingerCount = 0;

        // 각 손가락 굽힘 계산
        for (int i = 0; i < hand.fingers.Length && i < 5; i++)
        {
            Finger finger = hand.fingers[i];
            if (finger == null) continue;

            float fingerCurl = 0f;

            // 엄지손가락은 별도의 계산식 사용 + 가중치 적용
            if (finger.Type == Finger.FingerType.THUMB)
            {
                fingerCurl = CalcThumbCurl(finger, hand);
                fingerCurl *= thumbWeight;
            }
            else
            {
                // 일반 손가락 굽힘 계산
                fingerCurl = CalcFingerCurl(finger);
            }

            // 최소 임계값 미만은 0으로 처리
            if (fingerCurl < minimumThreshold)
                fingerCurl = 0f;

            // 손가락별 결과 저장
            FingerCurlValues[i] = fingerCurl;

            totalFingerCurl += fingerCurl;
            validFingerCount++;
        }

        if (validFingerCount == 0) return 0f;

        // 평균 굽힘 계산 후 민감도 적용
        float averageCurl = totalFingerCurl / validFingerCount;
        float result = Mathf.Clamp01(averageCurl * sensitivity);

        // 30프레임마다 디버그 출력
        if (enableDebugLogs && Time.frameCount % 30 == 0)
        {
            LogFingerCurls(result);
        }

        return result;
    }

    /// <summary>
    /// 엄지손가락 굽힘 계산
    /// palmNormal과 thumb 방향 벡터의 dot값을 기반으로 계산
    /// </summary>
    float CalcThumbCurl(Finger thumb, Hand hand)
    {
        try
        {
            Vector3 thumbDirection = new Vector3(
                thumb.Direction.x,
                thumb.Direction.y,
                thumb.Direction.z
            );

            Vector3 palmNormal = new Vector3(
                hand.PalmNormal.x,
                hand.PalmNormal.y,
                hand.PalmNormal.z
            );

            // 두 벡터 사이 각도를 dot product로 계산
            float dot = Vector3.Dot(thumbDirection, palmNormal);

            // dot값을 특정 구간(-0.7~0.3)에서 0~1로 매핑
            float curl = Mathf.InverseLerp(-0.7f, 0.3f, dot);

            return Mathf.Clamp01(curl);
        }
        catch (System.Exception e)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"엄지 굽힘 계산 오류: {e.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// 검지~새끼손가락 굽힘 계산
    /// 뼈 방향(dot)과 손가락 전체 방향을 함께 고려
    /// </summary>
    float CalcFingerCurl(Finger finger)
    {
        try
        {
            float totalCurl = 0f;
            int boneCount = 0;

            // 중간마디(intermediate)와 끝마디(distal) 방향 차이로 굽힘 계산
            if (finger.bones != null && finger.bones.Length >= 3)
            {
                Bone intermediate = finger.bones[2];
                Bone distal = finger.bones[3];

                if (intermediate != null && distal != null)
                {
                    Vector3 intermediateDir = new Vector3(
                        intermediate.Direction.x,
                        intermediate.Direction.y,
                        intermediate.Direction.z
                    );

                    Vector3 distalDir = new Vector3(
                        distal.Direction.x,
                        distal.Direction.y,
                        distal.Direction.z
                    );

                    // 두 뼈 방향의 dot값을 이용해 굽힘 정도 계산
                    float dot = Vector3.Dot(intermediateDir, distalDir);
                    float curl = 1.0f - Mathf.Clamp01(dot); // dot가 1이면 곧게 펴짐(0), 0이면 90도 굽힘(1)

                    totalCurl += curl;
                    boneCount++;
                }
            }

            // 손가락 전체의 방향에서 y축 기반 세로 굽힘도 계산
            Vector3 fingerDirection = new Vector3(
                finger.Direction.x,
                finger.Direction.y,
                finger.Direction.z
            );

            float verticalCurl = Mathf.Clamp01(-fingerDirection.y + 0.5f);
            totalCurl += verticalCurl;
            boneCount++;

            return boneCount > 0 ? totalCurl / boneCount : 0f;
        }
        catch (System.Exception e)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"손가락 굽힘 계산 오류: {e.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// 손가락별 굽힘 값 + 최종 강도 로그 출력
    /// </summary>
    void LogFingerCurls(float finalResult)
    {
        string fingerInfo = "손가락 굽힘: ";

        for (int i = 0; i < FingerCurlValues.Length; i++)
        {
            fingerInfo += $"{fingerNames[i]}={FingerCurlValues[i]:F2} ";
        }

        Debug.Log($"{fingerInfo}| 최종 결과: {finalResult:F2}");
    }

    /// <summary>
    /// 손가락별 굽힘을 막대 그래프로 출력 (1초마다)
    /// </summary>
    void PrintQuickStatus()
    {
        string fingerBars = "";
        for (int i = 0; i < FingerCurlValues.Length; i++)
        {
            float value = FingerCurlValues[i];
            int barLength = Mathf.RoundToInt(value * 5);
            string bar = new string('█', barLength) + new string('░', 5 - barLength);
            fingerBars += $"{fingerNames[i]}:{bar} ";
        }

        Debug.Log($"[Grip] 총:{CustomGrabStrength:F2} | {fingerBars}");
    }

    /// <summary>
    /// 0~1 사이로 정규화된 최종 쥐는 강도 반환
    /// </summary>
    public float GetNormalizedGrabStrength()
    {
        return CustomGrabStrength;
    }

    /// <summary>
    /// 특정 손가락의 굽힘 값 반환
    /// </summary>
    public float GetFingerCurl(int fingerIndex)
    {
        if (fingerIndex >= 0 && fingerIndex < FingerCurlValues.Length)
            return FingerCurlValues[fingerIndex];
        return 0f;
    }

    /// <summary>
    /// 인스펙터 메뉴에서 호출 가능 (ContextMenu)
    /// 현재 쥐는 강도 및 손가락 굽힘 상태를 상세 출력
    /// </summary>
    [ContextMenu("현재 쥐는 강도 상태 출력")]
    public void PrintGripStatus()
    {
        Debug.Log("=== 커스텀 쥐는 강도 상태 ===");
        Debug.Log($"최종 쥐는 강도: {CustomGrabStrength:F3}");
        Debug.Log($"민감도: {sensitivity}");
        Debug.Log($"엄지 가중치: {thumbWeight}");

        for (int i = 0; i < FingerCurlValues.Length; i++)
        {
            Debug.Log($"{fingerNames[i]}: {FingerCurlValues[i]:F3}");
        }
        Debug.Log("===========================");
    }
}
