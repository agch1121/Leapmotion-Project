using UnityEngine;
using Leap;
using System.Collections.Generic;

public class SwordSwing : MonoBehaviour
{
    public GameObject sword;
    public Controller leapController;
    private Rigidbody swordRigidbody;
    public float grabDistance = 2f; // 손과 검 사이의 거리
    private Vector3 initialPosition; // 검의 초기 위치
    private bool isSwinging = false;
    private bool isHolding = false;

    private void Start()
    {
        leapController = new Controller();
        swordRigidbody = sword.GetComponent<Rigidbody>();
        initialPosition = sword.transform.position; // 검의 초기 위치 저장
    }

    private void Update()
    {
        if (leapController.IsConnected)
        {
            var frame = leapController.Frame();
            var hands = frame.Hands;

            if (hands.Count > 0)
            {
                var hand = hands[0]; // 첫 번째 손을 사용
                Vector3 handPosition = new Vector3(hand.PalmPosition.x, hand.PalmPosition.y, hand.PalmPosition.z);

                if (hand.GrabStrength > 0.8f && !isHolding) // 손이 검을 잡았을 때
                {
                    isHolding = true;
                    swordRigidbody.isKinematic = true; // 검을 고정
                    GrabSword(handPosition);
                }
                else if (hand.GrabStrength < 0.2f && isHolding)
                {
                    ReleaseSword(); // 손이 검을 놓았을 때
                }

                //if (isHolding && hand.PinchStrength > 0.8f && !isSwinging)
                //{
                //    isSwinging = true;
                //    swordRigidbody.AddForce( * 500f); // 검에 힘을 추가하여 휘두르기
                //}
                //else if (isSwinging && hand.PinchStrength < 0.2f)
                //{
                //    isSwinging = false; // 휘두르기 중지
                //}
            }
        }
    }

    void GrabSword(Vector3 handPosition)
    {
        Collider[] colliders = Physics.OverlapSphere(handPosition, grabDistance);
        foreach (var collider in colliders)
        {
            if (collider.gameObject == sword)
            {
                sword.transform.position = handPosition; // 손 위치로 검 이동
                swordRigidbody.isKinematic = true; // 검을 고정
                return;
            }
        }
    }

    // 검을 쥔 손을 핌
    void ReleaseSword()
    {
        swordRigidbody.isKinematic = false; // 검을 자유롭게 움직일 수 있도록 설정
        sword.transform.position = initialPosition; // 검을 초기 위치로 되돌리기
        isHolding = false;
        isSwinging = false;
    }
}