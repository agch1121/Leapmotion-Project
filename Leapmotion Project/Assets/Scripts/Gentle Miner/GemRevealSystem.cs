using LibreFracture;
using System.Collections;
using UnityEngine;

/// <summary>
/// 채굴 완료 시 카메라를 이동하여 보석을 보여주는 시스템
/// 70% 성공, 100% 완료, 실패별 차별화된 연출 제공
/// Instantiate 방식으로 보석 생성 및 관리
/// </summary>
public class GemRevealSystem : MonoBehaviour
{
    [Header("카메라 설정")]
    public Camera mainCamera;
    public Transform gemRevealPosition; // 보석을 보여줄 카메라 위치
    public Transform originalCameraPosition; // 원래 카메라 위치 (복구용)

    [Header("보석 설정")]
    public GameObject[] gemPrefabs; // 스테이지별 보석 프리팹들
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

    // 현재 활성화된 보석 인스턴스
    private GameObject currentGemInstance;

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
    /// 게임 시작 시 보석 미리보기 (파괴 절대 없음)
    /// </summary>
    public void StartGemPreview()
    {
        if (isRevealing) return;

        int currentStage = GetCurrentStageIndex();
        Debug.Log("보석 미리보기 연출 시작!");
        StartCoroutine(GemPreviewSequence(currentStage));
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
    /// 게임 시작 시 보석 미리보기 시퀀스 - Instantiate 방식
    /// </summary>
    IEnumerator GemPreviewSequence(int currentStage)
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 인스턴스 생성
        GameObject gemInstance = CreateGemInstance(currentStage);
        if (gemInstance != null)
        {
            currentGemInstance = gemInstance;

            // 등장 효과
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

        // 4단계: 보석 인스턴스 삭제
        DestroyCurrentGemInstance();

        // 5단계: 카메라를 원래 위치로 복구
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 70% 성공 연출 시퀀스 - Instantiate 방식
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

        // 5단계: 보석 인스턴스 삭제 및 카메라 복구
        DestroyCurrentGemInstance();
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 100% 완료 연출 시퀀스 - Instantiate 방식
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

        // 5단계: 보석 인스턴스 삭제 및 카메라 복구
        DestroyCurrentGemInstance();
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 보석 공개 연출 시퀀스 - Instantiate 방식 (파괴 포함)
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

        // 5단계: 카메라를 원래 위치로 복구 (보석은 자동으로 파괴됨)
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 채굴 중 보석 파괴 시퀀스 - Instantiate 방식
    /// </summary>
    IEnumerator GemDestructionSequence()
    {
        isRevealing = true;

        // 1단계: 카메라를 보석 위치로 이동
        yield return StartCoroutine(MoveCameraToGem());

        // 2단계: 보석 인스턴스 생성
        int currentStage = GetCurrentStageIndex();
        GameObject gemInstance = CreateGemInstance(currentStage);
        if (gemInstance != null)
        {
            currentGemInstance = gemInstance;
        }

        // 3단계: 짧은 대기 후 즉시 파괴
        yield return new WaitForSeconds(0.5f);

        // 4단계: 마우스 클릭과 동일한 효과 적용
        SimulateMouseClick();

        yield return new WaitForSeconds(2f);
        yield return StartCoroutine(ReturnCameraToOriginal());

        isRevealing = false;
    }

    /// <summary>
    /// 현재 스테이지 인덱스 가져오기
    /// </summary>
    int GetCurrentStageIndex()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.CurrentStage - 1; // 0 기반 인덱스
        }
        return 0; // 기본값
    }

    /// <summary>
    /// 보석 인스턴스 생성
    /// </summary>
    GameObject CreateGemInstance(int stageIndex)
    {
        if (gemPrefabs == null || stageIndex < 0 || stageIndex >= gemPrefabs.Length)
        {
            Debug.LogWarning($"보석 프리팹이 없습니다! 스테이지 인덱스: {stageIndex}");
            return null;
        }

        GameObject gemPrefab = gemPrefabs[stageIndex];
        if (gemPrefab == null)
        {
            Debug.LogWarning($"스테이지 {stageIndex + 1}의 보석 프리팹이 null입니다!");
            return null;
        }

        // 기존 인스턴스가 있으면 먼저 삭제
        DestroyCurrentGemInstance();

        // 새 인스턴스 생성
        Vector3 spawnPos = gemSpawnPoint ? gemSpawnPoint.position : Vector3.zero;
        Quaternion spawnRot = gemSpawnPoint ? gemSpawnPoint.rotation : Quaternion.identity;

        GameObject newInstance = Instantiate(gemPrefab, spawnPos, spawnRot);
        newInstance.name = $"GemInstance_Stage{stageIndex + 1}";

        Debug.Log($"보석 인스턴스 생성: {newInstance.name}");
        return newInstance;
    }

    /// <summary>
    /// 현재 보석 인스턴스 삭제
    /// </summary>
    void DestroyCurrentGemInstance()
    {
        if (currentGemInstance != null)
        {
            Debug.Log($"보석 인스턴스 삭제: {currentGemInstance.name}");
            Destroy(currentGemInstance);
            currentGemInstance = null;
        }
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
            t = Mathf.SmoothStep(0f, 1f, t);

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
    }

    /// <summary>
    /// 보석 생성 및 등장 효과 - Instantiate 방식
    /// </summary>
    IEnumerator SpawnGemWithEffect(int gemQuality)
    {
        if (gemSpawnPoint == null)
        {
            yield break;
        }

        // 현재 스테이지에 맞는 보석 인스턴스 생성
        int currentStage = GetCurrentStageIndex();
        GameObject gemInstance = CreateGemInstance(currentStage);

        if (gemInstance == null)
        {
            yield break;
        }

        currentGemInstance = gemInstance;

        // 초기에는 작게 시작
        currentGemInstance.transform.localScale = Vector3.zero;

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

            if (currentGemInstance != null)
            {
                currentGemInstance.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (currentGemInstance != null)
        {
            currentGemInstance.transform.localScale = targetScale;
        }
    }

    /// <summary>
    /// 70% 성공 회전 연출 - 한바퀴 회전
    /// </summary>
    IEnumerator RotateGemSuccess()
    {
        if (currentGemInstance == null) yield break;

        float rotationTime = 3f;
        float elapsedTime = 0f;
        Vector3 startRotation = currentGemInstance.transform.eulerAngles;

        while (elapsedTime < rotationTime)
        {
            float t = elapsedTime / rotationTime;
            float yRotation = Mathf.Lerp(0f, 360f, t);

            if (currentGemInstance != null)
            {
                currentGemInstance.transform.rotation = Quaternion.Euler(
                    startRotation.x,
                    startRotation.y + yRotation,
                    startRotation.z
                );
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (currentGemInstance != null)
        {
            currentGemInstance.transform.rotation = Quaternion.Euler(
                startRotation.x,
                startRotation.y + 360f,
                startRotation.z
            );
        }
    }

    /// <summary>
    /// 100% 완료 회전 연출 - 화려한 회전 + 상하 움직임
    /// </summary>
    IEnumerator RotateGemPerfect()
    {
        if (currentGemInstance == null) yield break;

        float rotationTime = 4f;
        float elapsedTime = 0f;
        Vector3 startRotation = currentGemInstance.transform.eulerAngles;
        Vector3 startPosition = currentGemInstance.transform.position;

        while (elapsedTime < rotationTime)
        {
            float t = elapsedTime / rotationTime;
            float yRotation = Mathf.Lerp(0f, 720f, t); // 2바퀴 회전

            if (currentGemInstance != null)
            {
                // 회전
                currentGemInstance.transform.rotation = Quaternion.Euler(
                    startRotation.x,
                    startRotation.y + yRotation,
                    startRotation.z
                );

                // 상하 움직임 (사인파)
                float yOffset = Mathf.Sin(t * Mathf.PI * 4) * 0.1f;
                currentGemInstance.transform.position = startPosition + Vector3.up * yOffset;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (currentGemInstance != null)
        {
            currentGemInstance.transform.position = startPosition;
            currentGemInstance.transform.rotation = Quaternion.Euler(
                startRotation.x,
                startRotation.y + 720f,
                startRotation.z
            );
        }
    }

    /// <summary>
    /// 성공 메시지 표시
    /// </summary>
    IEnumerator ShowSuccessMessage()
    {
        Debug.Log("채굴 성공! 보석을 성공적으로 보존했습니다!");
        yield return new WaitForSeconds(2f);
    }

    /// <summary>
    /// 완벽 메시지 표시
    /// </summary>
    IEnumerator ShowPerfectMessage()
    {
        Debug.Log("완벽한 채굴! 보석을 완벽하게 보존했습니다!");
        yield return new WaitForSeconds(2.5f);
    }

    /// <summary>
    /// Test.cs의 마우스 클릭과 정확히 동일한 효과 시뮬레이션
    /// </summary>
    void SimulateMouseClick()
    {
        if (currentGemInstance == null) return;

        MineralBlock mineralBlock = FindFirstObjectByType<MineralBlock>();
        if (mineralBlock == null) return;

        Vector3 gemCenter = currentGemInstance.transform.position;
        Vector3 surfaceNormal = Vector3.up;

        // MineralBlock의 MineAtPoint와 동일한 로직 실행
        GemProtectionSystem gemProtection = mineralBlock.GetComponent<GemProtectionSystem>();
        if (gemProtection != null)
        {
            float miningForce = 20f;
            gemProtection.CheckMiningImpactOnGems(gemCenter, miningForce);
        }

        CreateMiningEffect(gemCenter, surfaceNormal);

        if (gemBreakSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gemBreakSound);
        }

        // ChunkNode 직접 파괴
        ChunkNode[] chunks = currentGemInstance.GetComponentsInChildren<ChunkNode>();
        foreach (ChunkNode chunk in chunks)
        {
            if (chunk != null && chunk.gameObject != null)
            {
                BreakChunkConnections(chunk);
                ApplyGentleForce(chunk, surfaceNormal);
            }
        }
    }

    /// <summary>
    /// 채굴 효과 생성
    /// </summary>
    void CreateMiningEffect(Vector3 position, Vector3 normal)
    {
        // 채굴 먼지
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

        // 작은 돌조각
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
    /// ChunkNode 연결 끊기
    /// </summary>
    void BreakChunkConnections(ChunkNode chunk)
    {
        if (chunk == null) return;

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
    /// 부드러운 힘 적용
    /// </summary>
    void ApplyGentleForce(ChunkNode chunk, Vector3 surfaceNormal)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 gentleDirection = surfaceNormal + Random.insideUnitSphere * 0.3f;
        gentleDirection.y = Mathf.Max(gentleDirection.y, 0.1f);

        float gentleForce = 25f;
        rb.AddForce(gentleDirection * gentleForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * gentleForce * 0.2f, ForceMode.Impulse);
    }

    /// <summary>
    /// 보석을 부서뜨리는 효과
    /// </summary>
    IEnumerator BreakGemWithEffect()
    {
        if (currentGemInstance == null) yield break;

        yield return new WaitForSeconds(gemBreakDelay);
        SimulateMouseClick();
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

        mainCamera.transform.position = originalPosition;
        mainCamera.transform.rotation = originalRotation;
    }

    /// <summary>
    /// 시스템 정리 (OnDestroy 또는 수동 호출용)
    /// </summary>
    public void Cleanup()
    {
        DestroyCurrentGemInstance();
        isRevealing = false;
    }

    void OnDestroy()
    {
        Cleanup();
    }
}