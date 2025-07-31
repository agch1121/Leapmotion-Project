using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("기본 설정")]
    public GameObject stonePrefab;
    public Transform player; // 플레이어(손) 위치
    public float stoneSpeed = 10f;
    public int numberOfStones = 4;
    public float spawnRadius = 3f; // 돌이 생성될 반경

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(SpawnStones());
        }
    }

    private IEnumerator SpawnStones()
    {
        for (int i = 0; i < numberOfStones; i++)
        {
            // 플레이어 주변 랜덤 위치에서 돌 생성 (위쪽과 옆쪽에서)
            Vector3 randomDirection = Random.onUnitSphere;
            randomDirection.y = Mathf.Abs(randomDirection.y) + 0.5f; // y값을 더 높게 해서 위쪽에서 생성
            randomDirection = randomDirection.normalized;

            Vector3 spawnPosition = transform.position + randomDirection * spawnRadius;

            GameObject newStone = Instantiate(stonePrefab, spawnPosition, Quaternion.identity);

            // Stone 태그 확인 및 추가
            if (!newStone.CompareTag("Stone"))
            {
                newStone.tag = "Stone";
            }

            // Rigidbody 확인 및 추가
            Rigidbody rb = newStone.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = newStone.AddComponent<Rigidbody>();
            }

            // 플레이어 방향으로 속도 직접 설정 (더 자연스러운 발사)
            Vector3 direction = (player.position - spawnPosition).normalized;

            // 속도 직접 설정으로 더 자연스러운 발사 효과
            rb.linearVelocity = direction * stoneSpeed;

            Debug.Log($"돌 생성 위치: {spawnPosition}, 플레이어 방향: {direction}, 속력: {stoneSpeed}");

            // 10초 후 자동 삭제 (메모리 관리)
            Destroy(newStone, 10f);
            yield return new WaitForSeconds(2f); // 돌 생성 간격
        }

        Debug.Log($"{numberOfStones}개의 돌이 생성되었습니다");
    }
}