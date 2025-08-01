using UnityEngine;
using System.Collections;
using LibreFracture;

/// <summary>
/// 채굴 완료 시 카메라를 이동하여 보석을 보여주는 시스템
/// </summary>
public class GemRevealSystem : MonoBehaviour
{
    [Header("카메라 설정")]
    public Camera mainCamera;
    public Transform gemRevealPosition; // 보석을 보여줄 카메라 위치
    public Transform originalCameraPosition; // 원래 카메라 위치 (복구용)

    [Header("보석 설정")]
    public GameObject[] gemPrefabs; // 결과 보석들 (품질별로 준비)
    public Transform gemSpawnPoint; // 보석이 나타날 위치

    [Header("연출 설정")]
    public float cameraTransitionTime = 2f; // 카메라 이동 시간
    public float gemDisplayTime = 2f; // 보석을 보여주는 시간
    public float gemBreakDelay = 2f; // 보석이 부서지기까지 대기 시간

    [Header("효과")]
    public ParticleSystem revealEffect; // 보석 등장 효과
    public AudioClip gemRevealSound; // 보석 등장 사운드
    public AudioClip gemBreakSound; // 보석 부서지는 사운드

    private AudioSource audioSource;
    private GameObject currentGem;
    private bool isRevealing = false;

    // 카메라 원본 상태 저장
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Start()
    {
        // 오디오 소스 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 원본 카메라 위치 저장
        if (mainCamera != null)
        {
            originalPosition = mainCamera.transform.position;
            originalRotation = mainCamera.transform.rotation;
        }
    }

    /// <summary>
    /// 채굴 완료 시 보석 공개 시작
    /// </summary>
    public void StartGemReveal(int gemQuality)
    {
        if (isRevealing) return;

        Debug.Log($"🎬 보석 공개 연출 시작! 품질: {gemQuality}");
        StartCoroutine(GemRevealSequence(gemQuality));
    }

    /// <summary>
    /// 보석 공개 연출 시퀀스
    /// </summary>
    IEnumerator GemRevealSequence(int gemQuality)
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 생성 및 등장 효과
        yield return StartCoroutine(SpawnGemWithEffect(gemQuality));

        // 3단계: 보석을 잠시 보여주기
        yield return new WaitForSeconds(gemDisplayTime);

        // 4단계: 보석 부서뜨리기
        yield return StartCoroutine(BreakGemWithEffect());

        // 5단계: 카메라를 원래 위치로 복구
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
        Debug.Log("🎬 보석 공개 연출 완료!");
    }

    /// <summary>
    /// 카메라를 보석 위치로 부드럽게 이동
    /// </summary>
    IEnumerator MoveCameraToGem()
    {
        if (mainCamera == null || gemRevealPosition == null) yield break;

        Debug.Log("📹 카메라 이동 시작");

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        Vector3 targetPos = gemRevealPosition.position;
        Quaternion targetRot = gemRevealPosition.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < cameraTransitionTime)
        {
            float t = elapsedTime / cameraTransitionTime;
            // 부드러운 커브 적용
            t = Mathf.SmoothStep(0f, 1f, t);

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 위치 정확히 설정
        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;

        Debug.Log("📹 카메라 이동 완료");
    }

    /// <summary>
    /// 보석 생성 및 등장 효과
    /// </summary>
    IEnumerator SpawnGemWithEffect(int gemQuality)
    {
        if (gemSpawnPoint == null) yield break;

        Debug.Log($"💎 보석 생성 시작 (품질: {gemQuality})");

        // 보석 선택 (품질에 따라)
        GameObject gemToSpawn = SelectGemByQuality(gemQuality);
        if (gemToSpawn == null) yield break;

        // 보석 생성
        //currentGem = Instantiate(gemToSpawn, gemSpawnPoint.position, gemSpawnPoint.rotation);

        currentGem = gemPrefabs[0];
        currentGem.SetActive(true);
        // 초기에는 작게 시작
        currentGem.transform.localScale = Vector3.zero;

        // 등장 효과
        if (revealEffect != null)
        {
            revealEffect.transform.position = gemSpawnPoint.position;
            revealEffect.Play();
        }

        // 등장 사운드
        if (gemRevealSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gemRevealSound);
        }

        // 보석 크기 애니메이션
        float scaleTime = 1f;
        float elapsedTime = 0f;
        Vector3 targetScale = Vector3.one;

        while (elapsedTime < scaleTime)
        {
            float t = elapsedTime / scaleTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            currentGem.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        currentGem.transform.localScale = targetScale;
        Debug.Log("💎 보석 등장 완료");
    }

    /// <summary>
    /// 품질에 따른 보석 선택
    /// </summary>
    GameObject SelectGemByQuality(int quality)
    {
        if (gemPrefabs == null || gemPrefabs.Length == 0)
        {
            Debug.LogWarning("보석 프리팹이 설정되지 않았습니다!");
            return null;
        }

        // 품질을 배열 인덱스로 변환 (0~100 점수를 배열 크기에 맞게)
        int index = Mathf.Clamp(quality * gemPrefabs.Length / 100, 0, gemPrefabs.Length - 1);
        return gemPrefabs[index];
    }

    /// <summary>
    /// 보석을 부서뜨리는 효과
    /// </summary>
    IEnumerator BreakGemWithEffect()
    {
        if (currentGem == null) yield break;

        Debug.Log("💥 보석 파괴 시작");

        // 파괴 대기 시간
        yield return new WaitForSeconds(gemBreakDelay);

        // 파괴 사운드
        if (gemBreakSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gemBreakSound);
        }

        // LibreFracture가 적용된 보석이라면 물리적 충격 가하기
        ChunkGraphManager gemChunkManager = currentGem.GetComponent<ChunkGraphManager>();
        if (gemChunkManager != null)
        {
            // 보석 중앙에 강한 충격 가하기
            Vector3 gemCenter = currentGem.transform.position;
            ApplyBreakingForce(gemCenter);

            Debug.Log("💥 LibreFracture 보석 파괴 적용");
        }
        else
        {
            // 일반적인 파괴 효과 (파티클 등)
            CreateSimpleBreakEffect();
            Debug.Log("💥 일반 보석 파괴 효과 적용");
            // 보석 페이드아웃
            yield return StartCoroutine(FadeOutGem());
        }

        Debug.Log("💥 보석 파괴 완료");
    }

    /// <summary>
    /// 보석에 파괴 충격 적용
    /// </summary>
    void ApplyBreakingForce(Vector3 center)
    {
        // 보석의 모든 ChunkNode에 충격 적용
        ChunkNode[] chunks = currentGem.GetComponentsInChildren<ChunkNode>();

        foreach (ChunkNode chunk in chunks)
        {
            Rigidbody rb = chunk.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 중심에서 바깭쪽으로 폭발하는 힘
                Vector3 direction = (chunk.transform.position - center).normalized;
                if (direction.magnitude < 0.1f) // 너무 가까우면 랜덤 방향
                    direction = Random.insideUnitSphere.normalized;

                float force = Random.Range(10f, 20f);
                rb.AddForce(direction * force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * force * 0.5f, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// 간단한 파괴 효과 (LibreFracture가 없는 경우)
    /// </summary>
    void CreateSimpleBreakEffect()
    {
        Vector3 gemCenter = currentGem.transform.position;

        // 파편 효과
        for (int i = 0; i < 10; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = gemCenter + Random.insideUnitSphere * 0.2f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
            fragment.transform.rotation = Random.rotation;

            // 반짝이는 재질
            Renderer fragRenderer = fragment.GetComponent<Renderer>();
            fragRenderer.material.color = Color.white;
            fragRenderer.material.SetFloat("_Metallic", 0.8f);
            fragRenderer.material.SetFloat("_Smoothness", 0.9f);

            // 물리 적용
            Rigidbody fragRb = fragment.AddComponent<Rigidbody>();
            Vector3 direction = Random.insideUnitSphere;
            fragRb.AddForce(direction * Random.Range(5f, 15f), ForceMode.Impulse);

            // 자동 삭제
            Destroy(fragment, 3f);
        }
    }

    /// <summary>
    /// 보석 페이드아웃
    /// </summary>
    IEnumerator FadeOutGem()
    {
        if (currentGem == null) yield break;

        Renderer gemRenderer = currentGem.GetComponent<Renderer>();
        if (gemRenderer == null) yield break;

        Material gemMaterial = gemRenderer.material;
        Color originalColor = gemMaterial.color;

        float fadeTime = 1f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeTime)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeTime);
            gemMaterial.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Destroy(currentGem);
        currentGem = null;
    }

    /// <summary>
    /// 카메라를 원래 위치로 복구
    /// </summary>
    IEnumerator ReturnCameraToOriginal()
    {
        if (mainCamera == null) yield break;

        Debug.Log("카메라 복구 시작");

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < cameraTransitionTime)
        {
            float t = elapsedTime / cameraTransitionTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            mainCamera.transform.position = Vector3.Lerp(startPos, originalPosition, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, originalRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 위치 정확히 설정
        mainCamera.transform.position = originalPosition;
        mainCamera.transform.rotation = originalRotation;

        Debug.Log("카메라 복구 완료");
    }

    /// <summary>
    /// 수동으로 연출 테스트
    /// </summary>
    [ContextMenu("보석 연출 테스트")]
    public void TestGemReveal()
    {
        StartGemReveal(75); // 75점 품질로 테스트
    }
}