using UnityEngine;

public class HammerMover : MonoBehaviour
{
    // 회전 속도
    public float rotationSpeed = 25f;
    // 최소 각도
    public float minAngle = 105f;
    // 최대 각도
    public float maxAngle = 115f;

    // 회전 방향
    private int direction = 1;

    void Update()
    {
        // 현재 Y축 각도
        float currentAngle = transform.localEulerAngles.y;

        // 각도 범위 보정
        if (currentAngle > 180) currentAngle -= 360;

        // 방향 전환
        if (currentAngle <= minAngle && direction == -1)
        {
            direction = 1;
        }
        else if (currentAngle >= maxAngle && direction == 1)
        {
            direction = -1;
        }

        // 회전량 계산
        float rotationAmount = rotationSpeed * Time.deltaTime * direction;

        // Y축 회전 적용
        transform.Rotate(Vector3.up, rotationAmount, Space.Self);
    }
}