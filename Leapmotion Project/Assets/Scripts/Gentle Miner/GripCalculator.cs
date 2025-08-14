using UnityEngine;
using Leap;

/// <summary>
/// 손가락 굽힘 각도 기반의 커스텀 쥐는 강도 계산 시스템
/// 기존 Leap Motion GrabStrength 대신 더 정밀한 측정 제공
/// </summary>
public class GripCalculator : MonoBehaviour
{
    [Header("민감도 설정")]
    [Range(0.1f, 2.0f)]
    public float sensitivity = 1.0f;
    [Range(0.5f, 1.5f)]
    public float thumbWeight = 1.0f; // 엄지손가락 가중치
    [Range(0.1f, 1.0f)]
    public float minimumThreshold = 0.1f; // 최소 감지 임계값

    [Header("디버그")]
    public bool enableDebugLogs = false;
    public bool showFingerValues = false;

    // 계산된 커스텀 쥐는 강도
    public float CustomGrabStrength { get; private set; }
    public float[] FingerCurlValues { get; private set; } = new float[5];

    // 손가락 이름 배열
    private readonly string[] fingerNames = { "엄지", "검지", "중지", "약지", "새끼" };

    // Leap Motion 컨트롤러 참조
    private Controller leapController;

    // 실시간 디버그용 변수들
    private float lastConsoleDebugTime = 0f;

    void Start()
    {
        leapController = new Controller();
        Debug.Log("GripCalculator 초기화 완료");
    }

    void Update()
    {
        if (leapController == null || !leapController.IsConnected)
        {
            CustomGrabStrength = 0f;
            return;
        }

        Frame frame = leapController.Frame();
        Hand rightHand = frame.Hands.Find(h => h.IsRight);

        if (rightHand != null)
        {
            CustomGrabStrength = CalculateGrip(rightHand);
        }
        else
        {
            CustomGrabStrength = 0f;
        }

        // 실시간 콘솔 디버그 (1초마다)
        if (enableDebugLogs && Time.time - lastConsoleDebugTime > 1f)
        {
            PrintQuickStatus();
            lastConsoleDebugTime = Time.time;
        }
    }

    float CalculateGrip(Hand hand)
    {
        if (hand.fingers == null || hand.fingers.Length == 0)
        {
            return 0f;
        }

        float totalFingerCurl = 0f;
        int validFingerCount = 0;

        for (int i = 0; i < hand.fingers.Length && i < 5; i++)
        {
            Finger finger = hand.fingers[i];
            if (finger == null) continue;

            float fingerCurl = 0f;

            if (finger.Type == Finger.FingerType.THUMB)
            {
                fingerCurl = CalcThumbCurl(finger, hand);
                fingerCurl *= thumbWeight;
            }
            else
            {
                fingerCurl = CalcFingerCurl(finger);
            }

            if (fingerCurl < minimumThreshold)
                fingerCurl = 0f;

            FingerCurlValues[i] = fingerCurl;
            totalFingerCurl += fingerCurl;
            validFingerCount++;
        }

        if (validFingerCount == 0) return 0f;

        float averageCurl = totalFingerCurl / validFingerCount;
        float result = Mathf.Clamp01(averageCurl * sensitivity);

        if (enableDebugLogs && Time.frameCount % 30 == 0)
        {
            LogFingerCurls(result);
        }

        return result;
    }

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

            float dot = Vector3.Dot(thumbDirection, palmNormal);
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

    float CalcFingerCurl(Finger finger)
    {
        try
        {
            float totalCurl = 0f;
            int boneCount = 0;

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

                    float dot = Vector3.Dot(intermediateDir, distalDir);
                    float curl = 1.0f - Mathf.Clamp01(dot);

                    totalCurl += curl;
                    boneCount++;
                }
            }

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

    void LogFingerCurls(float finalResult)
    {
        string fingerInfo = "손가락 굽힘: ";

        for (int i = 0; i < FingerCurlValues.Length; i++)
        {
            fingerInfo += $"{fingerNames[i]}={FingerCurlValues[i]:F2} ";
        }

        Debug.Log($"{fingerInfo}| 최종 결과: {finalResult:F2}");
    }

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

    public float GetNormalizedGrabStrength()
    {
        return CustomGrabStrength;
    }

    public float GetFingerCurl(int fingerIndex)
    {
        if (fingerIndex >= 0 && fingerIndex < FingerCurlValues.Length)
            return FingerCurlValues[fingerIndex];
        return 0f;
    }
}