using UnityEngine;
using LibreFracture;
using System.Collections.Generic;

/// <summary>
/// 광물 블록 구조 및 힘 기반 채굴 로직 처리
/// 힘 강도에 따른 차별화된 채굴 효과와 더 관대한 채굴 시스템
/// </summary>
public class MineralBlock : MonoBehaviour
{
    [Header("채굴 설정")]
    public float miningRadius = 0.3f;
    public float gentleForce = 5f;
    public int chunksPerClick = 2;
    public LayerMask chunkLayer = -1;

    [Header("채굴 효과")]
    public AudioClip miningSound;
    public AudioClip[] chunkFallSounds;

    [Header("채굴 강도")]
    [Range(1f, 50f)]
    public float miningForceIntensity = 20f;

    [Header("스테이지별 설정")]
    public float hardness = 1.0f;
    public float gemQuality = 1.0f;

    // 참조
    private ChunkGraphManager chunkGraphManager;
    private AudioSource audioSource;
    private ChunkNode[] allChunks;
    private GemProtectionSystem gemProtectionSystem;
    private ChunkCounter chunkCounter;
    private GameManager gameManager;
    private HandController handController;

    void Start()
    {
        InitializeMineralBlock();
    }

    void InitializeMineralBlock()
    {
        Debug.Log("=== MineralBlock 초기화 시작 ===");
        FindSystemReferences();
        SubscribeToEvents();
        SetupInitialSettings();
        Debug.Log("=== MineralBlock 초기화 완료 ===");
    }

    void FindSystemReferences()
    {
        chunkGraphManager = GetComponent<ChunkGraphManager>();
        gemProtectionSystem = GetComponent<GemProtectionSystem>();
        chunkCounter = GetComponent<ChunkCounter>() ?? gameObject.AddComponent<ChunkCounter>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        gameManager = FindFirstObjectByType<GameManager>();
        handController = FindFirstObjectByType<HandController>();

        if (!gameManager) Debug.LogError("GameManager를 찾을 수 없습니다!");
        if (!handController) Debug.LogError("HandController를 찾을 수 없습니다!");
    }

    void SubscribeToEvents()
    {
        if (chunkCounter != null && gameManager != null)
            chunkCounter.OnChunkCountChanged += gameManager.OnMineralProgressChanged;

        if (handController != null)
            handController.OnHammerStrike += OnHammerStrike;
    }

    void SetupInitialSettings()
    {
        RefreshChunkCache();
        ApplyStageSettings();
    }

    public void ApplyStageSettings()
    {
        miningForceIntensity *= hardness;
        if (chunkGraphManager != null)
            chunkGraphManager.jointBreakForce *= hardness;

        Debug.Log($"스테이지 설정 적용 - 경도: {hardness}, 품질: {gemQuality}");
    }

    void RefreshChunkCache()
    {
        var validChunks = new List<ChunkNode>();
        ChunkNode[] foundChunks = GetComponentsInChildren<ChunkNode>();

        foreach (ChunkNode chunk in foundChunks)
        {
            if (chunk != null && chunk.gameObject != null)
                validChunks.Add(chunk);
        }
        allChunks = validChunks.ToArray();
    }

    void Update()
    {
        if (gameManager == null || !gameManager.IsGameStarted) return;

        if (Input.GetMouseButtonDown(0))
            HandleMouseClick();
    }

    void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, chunkLayer))
        {
            if (hit.collider.transform.IsChildOf(transform))
                MineAtPoint(hit.point, hit.normal);
        }
    }

    public void OnHammerStrike(Vector3 position, Vector3 direction, float strikeForce)
    {
        Debug.Log($"망치 타격 감지! 위치: {position}, 방향: {direction}, 힘: {strikeForce:F2}");
        if (gameManager == null || !gameManager.IsGameStarted)
        {
            Debug.Log("게임이 아직 시작되지 않았습니다.");
            return;
        }
        MineAtPointForce(position, -direction, strikeForce);
    }

    public void MineAtPointForce(Vector3 miningPoint, Vector3 surfaceNormal, float strikeForce)
    {
        Debug.Log($"힘 강도별 채굴 시작: {strikeForce * 100f:F0}%");

        if (gemProtectionSystem != null)
        {
            float adjustedForce = CalcAdjustedForce(strikeForce);
            gemProtectionSystem.CheckMiningImpactOnGems(miningPoint, adjustedForce);
        }

        CreateForceEffect(miningPoint, surfaceNormal, strikeForce);

        if (miningSound != null && audioSource != null)
        {
            float volume = Mathf.Lerp(0.3f, 1.0f, strikeForce);
            audioSource.PlayOneShot(miningSound, volume);
        }

        RemoveChunksForce(miningPoint, strikeForce, surfaceNormal);
    }

    public void MineAtPoint(Vector3 miningPoint, Vector3 surfaceNormal)
    {
        MineAtPointForce(miningPoint, surfaceNormal, 0.5f);
    }

    float CalcAdjustedForce(float strikeForce)
    {
        float baseForce = miningForceIntensity * 0.7f;
        float forceMultiplier = Mathf.Lerp(0.5f, 1.5f, strikeForce);
        return baseForce * forceMultiplier * hardness;
    }

    void CreateForceEffect(Vector3 position, Vector3 normal, float strikeForce)
    {
        int dustCount = CalcDustCount(strikeForce);
        int chipCount = CalcChipCount(strikeForce);

        Debug.Log($"채굴 효과: 먼지 {dustCount}개, 조각 {chipCount}개 (힘: {strikeForce * 100f:F0}%)");

        for (int i = 0; i < dustCount; i++)
            CreateDust(position, normal, strikeForce);

        for (int i = 0; i < chipCount; i++)
            CreateChip(position, normal, strikeForce);

        if (strikeForce > 0.8f)
            CreateDangerEffect(position, normal);
    }

    int CalcDustCount(float strikeForce)
    {
        if (strikeForce < 0.3f) return Random.Range(1, 3);
        else if (strikeForce < 0.7f) return Random.Range(3, 6);
        else return Random.Range(6, 11);
    }

    int CalcChipCount(float strikeForce)
    {
        if (strikeForce < 0.3f) return Random.Range(0, 2);
        else if (strikeForce < 0.7f) return Random.Range(1, 4);
        else return Random.Range(3, 7);
    }

    void CreateDust(Vector3 position, Vector3 normal, float strikeForce)
    {
        GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dust.transform.position = position + Random.insideUnitSphere * 0.1f;
        dust.transform.localScale = Vector3.one * Random.Range(0.015f, 0.04f) * (0.5f + strikeForce);

        Renderer dustRenderer = dust.GetComponent<Renderer>();
        float colorIntensity = 0.4f + (strikeForce * 0.3f);
        dustRenderer.material.color = new Color(0.7f * colorIntensity, 0.6f * colorIntensity, 0.4f * colorIntensity);

        Rigidbody dustRb = dust.AddComponent<Rigidbody>();
        Vector3 force = normal * Random.Range(1f, 3f) * (0.5f + strikeForce * 1.5f) + Random.insideUnitSphere * 0.5f;
        dustRb.AddForce(force, ForceMode.Impulse);

        Destroy(dust, 1.0f + (strikeForce * 1.0f));
    }

    void CreateChip(Vector3 position, Vector3 normal, float strikeForce)
    {
        GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chip.transform.position = position + Random.insideUnitSphere * 0.05f;
        chip.transform.localScale = Vector3.one * Random.Range(0.02f, 0.06f) * (0.7f + strikeForce);
        chip.transform.rotation = Random.rotation;

        Renderer chipRenderer = chip.GetComponent<Renderer>();
        float grayIntensity = 0.3f + (strikeForce * 0.2f);
        chipRenderer.material.color = new Color(0.5f + grayIntensity, 0.4f + grayIntensity, 0.3f + grayIntensity);

        Rigidbody chipRb = chip.AddComponent<Rigidbody>();
        Vector3 chipForce = normal * Random.Range(2f, 5f) * (1.0f + strikeForce * 2.0f) + Random.insideUnitSphere * 1f;
        chipRb.AddForce(chipForce, ForceMode.Impulse);

        Destroy(chip, 1.5f + (strikeForce * 1.5f));
    }

    void CreateDangerEffect(Vector3 position, Vector3 normal)
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject danger = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            danger.transform.position = position + Random.insideUnitSphere * 0.15f;
            danger.transform.localScale = Vector3.one * Random.Range(0.03f, 0.08f);

            Renderer dangerRenderer = danger.GetComponent<Renderer>();
            dangerRenderer.material.color = Color.red;
            dangerRenderer.material.SetFloat("_Metallic", 0.8f);

            Rigidbody dangerRb = danger.AddComponent<Rigidbody>();
            dangerRb.AddForce(normal * Random.Range(3f, 7f) + Random.insideUnitSphere * 2f, ForceMode.Impulse);

            Destroy(danger, 0.8f);
        }
        Debug.Log("위험한 힘! 보석 손상 위험 증가");
    }

    void RemoveChunksForce(Vector3 miningPoint, float strikeForce, Vector3 surfaceNormal)
    {
        var activeChunks = System.Array.FindAll(allChunks, chunk =>
            chunk != null && chunk.gameObject.activeInHierarchy
        );

        if (activeChunks.Length == 0)
        {
            Debug.Log("더 이상 채굴할 조각이 없습니다.");
            RefreshChunkCache();
            return;
        }

        System.Array.Sort(activeChunks, (a, b) =>
            Vector3.Distance(a.transform.position, miningPoint)
            .CompareTo(Vector3.Distance(b.transform.position, miningPoint))
        );

        int maxChunksToRemove = CalcChunksToRemove(strikeForce);
        int chunksRemoved = 0;

        foreach (ChunkNode chunk in activeChunks)
        {
            if (chunksRemoved >= maxChunksToRemove) break;

            if (Vector3.Distance(chunk.transform.position, miningPoint) <= miningRadius * (0.7f + strikeForce * 0.6f))
            {
                RemoveChunkForce(chunk, miningPoint, surfaceNormal, strikeForce);
                chunksRemoved++;
            }
        }

        Debug.Log($"채굴 완료: {chunksRemoved}개 조각 제거 (힘: {strikeForce * 100f:F0}%)");
    }

    int CalcChunksToRemove(float strikeForce)
    {
        if (strikeForce < 0.3f) return 1;
        else if (strikeForce < 0.7f) return Random.Range(1, 3);
        else return Random.Range(2, 4);
    }

    void RemoveChunkForce(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float strikeForce)
    {
        if (chunk == null) return;

        BreakChunkConnections(chunk);
        ApplyForcePhysics(chunk, miningPoint, surfaceNormal, strikeForce);

        if (chunkFallSounds != null && chunkFallSounds.Length > 0)
            StartCoroutine(PlayDelayedFallSound(Random.Range(0.1f, 0.6f)));
    }

    void BreakChunkConnections(ChunkNode chunk)
    {
        foreach (var joint in chunk.GetComponents<Joint>())
            Destroy(joint);
        foreach (var fixedJoint in chunk.GetComponents<FixedJoint>())
            Destroy(fixedJoint);
    }

    void ApplyForcePhysics(ChunkNode chunk, Vector3 miningPoint, Vector3 surfaceNormal, float strikeForce)
    {
        Rigidbody rb = chunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 forceDirection = (chunk.transform.position - miningPoint).normalized;
        forceDirection.y = Mathf.Max(forceDirection.y, 0.1f);

        float baseForce = gentleForce * hardness;
        float forceMultiplier = 0.8f + strikeForce * 1.4f;
        float finalForce = baseForce * forceMultiplier;

        rb.AddForce(forceDirection * finalForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * finalForce * 0.3f, ForceMode.Impulse);

        Debug.Log($"조각 물리력 적용: {finalForce:F1}");
    }

    System.Collections.IEnumerator PlayDelayedFallSound(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (chunkFallSounds != null && chunkFallSounds.Length > 0 && audioSource != null)
            audioSource.PlayOneShot(chunkFallSounds[Random.Range(0, chunkFallSounds.Length)], 0.5f);
    }

    void OnDestroy()
    {
        if (chunkCounter != null && gameManager != null)
            chunkCounter.OnChunkCountChanged -= gameManager.OnMineralProgressChanged;
        if (handController != null)
            handController.OnHammerStrike -= OnHammerStrike;
    }
}
