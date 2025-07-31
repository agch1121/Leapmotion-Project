using UnityEngine;

public class Sword : MonoBehaviour
{
    private void Start()
    {
        Collider[] colliders = GetComponents<Collider>();
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            Debug.Log($"Rigidbody isKinematic: {rb.isKinematic}");
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Debug.Log($"Collider {i}: {colliders[i].GetType().Name}, IsTrigger = {colliders[i].isTrigger}, Enabled = {colliders[i].enabled}");
        }

        // 검에 Rigidbody가 없다면 추가 (Kinematic으로)
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // 중력이나 물리 영향 받지 않음
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Stone"))
        {
            Debug.Log("돌 태그 확인 완료! 돌을 파괴합니다.");
            Destroy(collision.gameObject);
        }
    }
}