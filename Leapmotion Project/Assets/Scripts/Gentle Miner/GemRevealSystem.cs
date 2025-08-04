using LibreFracture;
using System.Collections;
using UnityEngine;

/// <summary>
/// 채굴 완료 시 카메라를 이동하여 보석을 보여주는 시스템
/// 70% 성공, 100% 완료, 실패별 차별화된 연출 제공
/// </summary>
public class GemRevealSystem : MonoBehaviour
{
    [Header("카메라 설정")]
    public Camera mainCamera;
    public Transform gemRevealPosition; // 보석을 보여줄 카메라 위치
    public Transform originalCameraPosition; // 원래 카메라 위치 (복구용)

    [Header("보석 설정")]
    public GameObject[] gemPrefabs; // 결과 보석들 (품질별로 준비) - 사용 안함
    public GameObject actualGem; // 실제 LibreFracture가 적용된 보석 오브젝트 (인스펙터에서 연결)
    public Transform gemSpawnPoint; // 보석이 나타날 위치

    [Header("연출 설정")]
    public float cameraTransitionTime = 2f; // 카메라 이동 시간
    public float gemDisplayTime = 2f; // 보석을 보여주는 시간
    public float gemBreakDelay = 1f; // 보석이 부서지기까지 대기 시간

    [Header("효과")]
    public ParticleSystem revealEffect; // 보석 등장 효과
    public AudioClip gemRevealSound; // 보석 등장 사운드
    public AudioClip gemBreakSound; // 보석 부서지는 사운드

    private AudioSource audioSource;
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

        // 실제 보석 오브젝트 초기 상태 설정
        if (actualGem != null)
        {
            actualGem.SetActive(false); // 게임 시작 시 비활성화
        }
    }

    /// <summary>
    /// 게임 시작 시 보석 미리보기 (파괴 절대 없음)
    /// </summary>
    public void StartGemPreview()
    {
        if (isRevealing) return;

        Debug.Log("보석 미리보기 연출 시작! (파괴 절대 안함)");
        StartCoroutine(GemPreviewSequence());
    }

    /// <summary>
    /// 70% 성공 시 보석 성공 연출 - 회전 + 보존
    /// </summary>
    public void StartGemSuccessReveal(int gemQuality)
    {
        if (isRevealing) return;

        Debug.Log($"보석 성공 연출 시작! 품질: {gemQuality} (회전 + 보존)");
        StartCoroutine(GemSuccessSequence(gemQuality));
    }

    /// <summary>
    /// 100% 완료 시 보석 완벽 연출 - 특별 회전 + 보존
    /// </summary>
    public void StartGemPerfectReveal(int gemQuality)
    {
        if (isRevealing) return;

        Debug.Log($"보석 완벽 연출 시작! 품질: {gemQuality} (특별 회전 + 보존)");
        StartCoroutine(GemPerfectSequence(gemQuality));
    }

    /// <summary>
    /// 채굴 완료 시 보석 공개 시작 (기존 - 파괴 포함)
    /// </summary>
    public void StartGemReveal(int gemQuality)
    {
        if (isRevealing) return;

        Debug.Log($"보석 최종 연출 시작! 품질: {gemQuality} (파괴 있음)");
        StartCoroutine(GemRevealSequence(gemQuality));
    }

    /// <summary>
    /// 보석이 채굴 중 파괴될 때 즉시 연출
    /// </summary>
    public void StartGemDestruction()
    {
        if (isRevealing) return;

        Debug.Log("보석 즉시 파괴 연출 시작! (0점 달성)");
        StartCoroutine(GemDestructionSequence());
    }

    /// <summary>
    /// 게임 시작 시 보석 미리보기 시퀀스 (파괴 절대 없음)
    /// </summary>
    IEnumerator GemPreviewSequence()
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 활성화 (파괴 없이)
        if (actualGem != null)
        {
            actualGem.SetActive(true);
            actualGem.transform.position = gemSpawnPoint.position;
            actualGem.transform.rotation = gemSpawnPoint.rotation;
            actualGem.transform.localScale = Vector3.one;

            // 등장 효과만
            if (revealEffect != null)
            {
                revealEffect.transform.position = gemSpawnPoint.position;
                revealEffect.Play();
            }

            if (gemRevealSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(gemRevealSound);
            }
        }

        // 3단계: 보석을 잠시 보여주기
        yield return new WaitForSeconds(gemDisplayTime);

        // 4단계: 보석을 숨기기 (파괴 절대 안함!)
        if (actualGem != null)
        {
            actualGem.SetActive(false);
        }

        // 5단계: 카메라를 원래 위치로 복구
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 70% 성공 연출 시퀀스 - 회전 + 보존
    /// </summary>
    IEnumerator GemSuccessSequence(int gemQuality)
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 생성 및 등장 효과
        yield return StartCoroutine(SpawnGemWithEffect(gemQuality));

        // 3단계: 성공 회전 연출
        yield return StartCoroutine(RotateGemSuccess());

        // 4단계: 성공 메시지 표시
        yield return StartCoroutine(ShowSuccessMessage());

        // 5단계: 카메라를 원래 위치로 복구
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 100% 완료 연출 시퀀스 - 특별 회전 + 보존
    /// </summary>
    IEnumerator GemPerfectSequence(int gemQuality)
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 생성 및 등장 효과
        yield return StartCoroutine(SpawnGemWithEffect(gemQuality));

        // 3단계: 완벽 회전 연출 (더 화려하게)
        yield return StartCoroutine(RotateGemPerfect());

        // 4단계: 완벽 메시지 표시
        yield return StartCoroutine(ShowPerfectMessage());

        // 5단계: 카메라를 원래 위치로 복구
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 보석 공개 연출 시퀀스 (기존 - 파괴 포함)
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

        // 4단계: 보석 부서뜨리기 (최종 연출에서만!)
        yield return StartCoroutine(BreakGemWithEffect());

        // 5단계: 카메라를 원래 위치로 복구
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 채굴 중 보석 파괴 시퀀스 (즉시 파괴 - 카메라 복구 안함)
    /// </summary>
    IEnumerator GemDestructionSequence()
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 활성화
        if (actualGem != null)
        {
            actualGem.SetActive(true);
            actualGem.transform.position = gemSpawnPoint.position;
            actualGem.transform.rotation = gemSpawnPoint.rotation;
            actualGem.transform.localScale = Vector3.one;
        }

        // 3단계: 짧은 대기 후 즉시 파괴
        yield return new WaitForSeconds(0.5f);

        // 4단계: 마우스 클릭과 동일한 효과 적용
        SimulateMouseClick();

        // 5단계: 파괴 장면을 계속 보여주기 (카메라 복구 안함)
        // 게임 오버 상태이므로 카메라는 보석 위치에 그대로 유지

        isRevealing = false;
    }

    /// <summary>
    /// 카메라를 보석 위치로 부드럽게 이동
    /// </summary>
    IEnumerator MoveCameraToGem()
    {
        if (mainCamera == null || gemRevealPosition == null) yield break;

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
    }

    /// <summary>
    /// 보석 생성 및 등장 효과
    /// </summary>
    IEnumerator SpawnGemWithEffect(int gemQuality)
    {
        if (gemSpawnPoint == null || actualGem == null)
        {
            yield break;
        }

        // 실제 보석 활성화 및 위치 설정
        actualGem.SetActive(true);
        actualGem.transform.position = gemSpawnPoint.position;
        actualGem.transform.rotation = gemSpawnPoint.rotation;

        // 초기에는 작게 시작
        actualGem.transform.localScale = Vector3.zero;

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

            actualGem.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        actualGem.transform.localScale = targetScale;
    }

    /// <summary>
    /// 70% 성공 회전 연출 - 한바퀴 회전
    /// </summary>
    IEnumerator RotateGemSuccess()
    {
        if (actualGem == null) yield break;

        float rotationTime = 3f; // 3초에 걸쳐 회전
        float elapsedTime = 0f;
        Vector3 startRotation = actualGem.transform.eulerAngles;

        // Y축으로 360도 회전
        while (elapsedTime < rotationTime)
        {
            float t = elapsedTime / rotationTime;
            float yRotation = Mathf.Lerp(0f, 360f, t);

            actualGem.transform.rotation = Quaternion.Euler(
                startRotation.x,
                startRotation.y + yRotation,
                startRotation.z
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 회전값 정확히 설정
        actualGem.transform.rotation = Quaternion.Euler(
            startRotation.x,
            startRotation.y + 360f,
            startRotation.z
        );
    }

    /// <summary>
    /// 100% 완료 회전 연출 - 화려한 회전 + 상하 움직임
    /// </summary>
    IEnumerator RotateGemPerfect()
    {
        if (actualGem == null) yield break;

        float rotationTime = 4f; // 4초에 걸쳐 회전
        float elapsedTime = 0f;
        Vector3 startRotation = actualGem.transform.eulerAngles;
        Vector3 startPosition = actualGem.transform.position;

        // Y축으로 720도 회전 + 상하 움직임
        while (elapsedTime < rotationTime)
        {
            float t = elapsedTime / rotationTime;
            float yRotation = Mathf.Lerp(0f, 720f, t); // 2바퀴 회전

            // 회전
            actualGem.transform.rotation = Quaternion.Euler(
                startRotation.x,
                startRotation.y + yRotation,
                startRotation.z
            );

            // 상하 움직임 (사인파)
            float yOffset = Mathf.Sin(t * Mathf.PI * 4) * 0.1f; // 4번 위아래 움직임
            actualGem.transform.position = startPosition + Vector3.up * yOffset;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 위치/회전 정확히 설정
        actualGem.transform.position = startPosition;
        actualGem.transform.rotation = Quaternion.Euler(
            startRotation.x,
            startRotation.y + 720f,
            startRotation.z
        );
    }

    /// <summary>
    /// 성공 메시지 표시
    /// </summary>
    IEnumerator ShowSuccessMessage()
    {
        Debug.Log("🎉 채굴 성공! 보석을 성공적으로 보존했습니다!");

        // TODO: 실제 UI 텍스트 표시
        // - "채굴 성공!" 메시지
        // - 보석 보존 성공 알림
        // - 획득 점수 표시

        yield return new WaitForSeconds(2f); // 2초간 메시지 표시
    }

    /// <summary>
    /// 완벽 메시지 표시
    /// </summary>
    IEnumerator ShowPerfectMessage()
    {
        Debug.Log("🏆 완벽한 채굴! 보석을 완벽하게 보존했습니다!");

        // TODO: 실제 UI 텍스트 표시
        // - "완벽한 채굴!" 메시지
        // - 보너스 점수 강조
        // - 특별 달성 효과

        yield return new WaitForSeconds(2.5f); // 2.5초간 메시지 표시
    }

    /// <summary>
    /// 품질에 따른 보석 선택 (현재는 사용하지 않음 - actualGem 사용)
    /// </summary>
    GameObject SelectGemByQuality(int quality)
    {
        // actualGem을 사용하므로 이 함수는 더 이상 필요 없음
        // 하지만 호환성을 위해 유지
        return actualGem;
    }

    /// <summary>
    /// Test.cs의 마우스 클릭과 정확히 동일한 효과 시뮬레이션
    /// </summary>
    void SimulateMouseClick()
    {
        if (actualGem == null) return;

        Test testScript = FindFirstObjectByType<Test>();
        if (testScript == null) return;

        // 보석 중앙 지점과 표면 법선 계산
        Vector3 gemCenter = actualGem.transform.position;
        Vector3 surfaceNormal = Vector3.up;

        // Test.cs의 MineAtPoint와 동일한 로직 직접 실행
        // 1. 보석 보호 시스템에 충격 전달
        GemProtectionSystem gemProtection = testScript.GetComponent<GemProtectionSystem>();
        if (gemProtection != null)
        {
            float miningForce = 20f;
            gemProtection.CheckMiningImpactOnGems(gemCenter, miningForce);
        }

        // 2. 채굴 효과 생성 (Test.cs와 동일)
        CreateMiningEffect(gemCenter, surfaceNormal);

        // 3. 채굴 사운드 재생
        if (gemBreakSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gemBreakSound);
        }

        // 4. ChunkNode 직접 파괴 (Test.cs의 RemoveChunkGently와 동일)
        ChunkNode[] chunks = actualGem.GetComponentsInChildren<ChunkNode>();

        foreach (ChunkNode chunk in chunks)
        {
            if (chunk != null && chunk.gameObject != null)
            {
                // Test.cs와 동일한 방식으로 연결 끊기
                BreakChunkConnections(chunk);

                // Test.cs와 동일한 방식으로 힘 적용
                ApplyGentleForce(chunk, surfaceNormal);
            }
        }
    }

    /// <summary>
    /// Test.cs의 CreateMiningEffect와 동일
    /// </summary>
    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 채굴 먼지 (작은 양)
        for (int i = 0; i < 5; i++)
        {
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.transform.position = position + Random.insideUnitSphere * 0.1f;
            dust.transform.localScale = Vector3.one * Random.Range(0.02f, 0.05f);

            Renderer dustRenderer = dust.GetComponent<Renderer>();
            dustRenderer.material.color = new Color(0.7f, 0.6f, 0.4f, 0.8f);

            Rigidbody dustRb = dust.AddComponent<Rigidbody>();
            Vector3 force = normal * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.5f;
            dustRb.AddForce(force, ForceMode.Impulse);

            Destroy(dust, 1.5f);
        }

        // 작은 돌조각 (1-2개만)
        int chipCount = Random.Range(1, 3);
        for (int i = 0; i < chipCount; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.transform.position = position + Random.insideUnitSphere * 0.05f;
            chip.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
            chip.transform.rotation = Random.rotation;

            Renderer chipRenderer = chip.GetComponent<Renderer>();
            chipRenderer.material.color = new Color(0.5f, 0.4f, 0.3f);

            Rigidbody chipRb = chip.AddComponent<Rigidbody>();
            Vector3 chipForce = normal * Random.Range(2f, 5f) + Random.insideUnitSphere * 1f;
            chipRb.AddForce(chipForce, ForceMode.Impulse);

            Destroy(chip, 2f);
        }
    }

    /// <summary>
    /// Test.cs의 BreakChunkConnections와 동일
    /// </summary>
    void BreakChunkConnections(ChunkNode chunk)
    {
        if (chunk == null) return;

        // 모든 Joint 제거
        Joint[] joints = chunk.GetComponents<Joint>();
        foreach (Joint joint in joints)
        {
            if (joint != null)
                Destroy(joint);
        }

        FixedJoint[] fixedJoints = chunk.GetComponents<FixedJoint>();
        foreach (FixedJoint fixedJoint in fixedJoints)
        {
            if (fixedJoint != null)
                Destroy(fixedJoint);
        }
    }

    /// <summary>
    /// Test.cs의 ApplyGentleForce와 동일
    /// </summary>
    void ApplyGentleForce(ChunkNode chunk, Vector3 surfaceNormal)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        // 자연스러운 방향으로 부드럽게
        Vector3 gentleDirection = surfaceNormal + Random.insideUnitSphere * 0.3f;
        gentleDirection.y = Mathf.Max(gentleDirection.y, 0.1f);

        float gentleForce = 25f; // 보석용으로 더 강하게
        rb.AddForce(gentleDirection * gentleForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
    }

    /// <summary>
    /// 보석을 부서뜨리는 효과
    /// </summary>
    IEnumerator BreakGemWithEffect()
    {
        if (actualGem == null) yield break;

        // 파괴 대기 시간
        yield return new WaitForSeconds(gemBreakDelay);

        // 마우스 클릭과 같은 충격 적용
        SimulateMouseClick();
    }

    /// <summary>
    /// 간단한 파괴 효과 (LibreFracture가 없는 경우)
    /// </summary>
    void CreateSimpleBreakEffect()
    {
        Vector3 gemCenter = actualGem.transform.position;

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
    /// 카메라를 원래 위치로 복구
    /// </summary>
    IEnumerator ReturnCameraToOriginal()
    {
        if (mainCamera == null) yield break;

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
    }
}