using Leap;
using UnityEngine;

public class ForceDetector : MonoBehaviour
{
    private Controller leapController;

    [Header("힘 조절 설정")]
    public float minGrabThreshold = 0.3f;
    public float grabSensitivity = 0.005f;

    [Header("디버그 표시")]
    public bool showDebugValues = true;

    private float currentForce = 0f;
    private Hand rightHand;

    private void Start()
    {
        leapController = new Controller();
    }

    private void Update()
    {
        UpdateHandData();
        CalculateForce();
    }

    void UpdateHandData()
    {
        Frame frame = leapController.Frame();
        rightHand = frame.Hands.Find(h => h.IsRight);
    }

    void CalculateForce()
    {
        if (rightHand == null)
        {
            currentForce = 0f;
            return;
        }

        float rawGrabStrength = rightHand.GrabStrength;

        // 안전장치
        if (rawGrabStrength < minGrabThreshold)
        {
            currentForce = 0f;
            return;
        }

        // 그랩 힘 조정
        float adjustedGrabStrength = Mathf.Pow(rawGrabStrength, 3f) * grabSensitivity;

        // 속도 계산
        Vector3 velocity = rightHand.PalmVelocity;
        float handSpeed = velocity.magnitude;
        float normalizedSpeed = Mathf.Clamp(handSpeed / 5f, 0f, 1f);

        // 최종 힘 = 그랩 + 속도
        currentForce = adjustedGrabStrength + normalizedSpeed;
        currentForce = Mathf.Clamp01(currentForce);
    }

    public float GetCurrentForce()
    {
        return currentForce;
    }
}