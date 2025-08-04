using UnityEngine;
using LibreFracture;

/// <summary>
/// 광물 블록 구조 및 채굴 로직 처리 (기획서)
/// 각 광물 블록의 속성과 LibreFracture 시스템 통합 관리
/// </summary>
public class MineralBlock : MonoBehaviour
{
    [Header("광물 블록 정보")]
    public string mineralType = "다이아몬드";
    public float hardness = 1.0f; // 광물 경도 (채굴 난이도)
    public Color mineralColor = Color.white;

    [Header("보석 설정")]
    public int gemCount = 3; // 포함된 보석 개수
    public float gemQuality = 1.0f; // 보석 품질 배율

    [Header("LibreFracture 설정")]
    public float totalMass = 10f;
    public float jointBreakForce = 50f;

    // 시스템 참조
    private ChunkGraphManager chunkGraphManager;
    private GemProtectionSystem gemProtectionSystem;
    private ChunkCounter chunkCounter;


    // 블록 상태
    private bool blockInitialized = false;
    private int initialChunkCount = 0;

    void Start()
    {
        InitializeMineralBlock();
    }

    void InitializeMineralBlock()
    {
        Debug.Log($"=== MineralBlock 초기화: {mineralType} ===");

        // 시스템 컴포넌트 설정
        SetupChunkGraphManager();
        SetupGemProtectionSystem();
        SetupChunkCounter();
        SetupGameManager();

        // 광물 속성 적용
        ApplyMineralProperties();

        blockInitialized = true;

        Debug.Log($"MineralBlock 초기화 완료: {mineralType} (경도: {hardness})");
    }

    /// <summary>
    /// ChunkGraphManager 설정
    /// </summary>
    void SetupChunkGraphManager()
    {
        chunkGraphManager = GetComponent<ChunkGraphManager>();
        if (chunkGraphManager == null)
        {
            chunkGraphManager = gameObject.AddComponent<ChunkGraphManager>();
        }

        // 광물 경도에 따른 설정 조정
        chunkGraphManager.totalMass = totalMass * hardness;
        chunkGraphManager.jointBreakForce = jointBreakForce * hardness;

        Debug.Log("ChunkGraphManager 설정 완료");
    }

    /// <summary>
    /// GemProtectionSystem 설정
    /// </summary>
    void SetupGemProtectionSystem()
    {
        gemProtectionSystem = GetComponent<GemProtectionSystem>();
        if (gemProtectionSystem == null)
        {
            Debug.LogWarning("GemProtectionSystem이 없습니다!");
            return;
        }

        // 보석 품질에 따른 보호 설정 조정
        var gems = gemProtectionSystem.GetAllGems();
        foreach (var gem in gems)
        {
            gem.damageThreshold = gem.damageThreshold * gemQuality;
            gem.protectionRadius = gem.protectionRadius * gemQuality;
        }

        Debug.Log("GemProtectionSystem 설정 완료");
    }

    /// <summary>
    /// ChunkCounter 설정
    /// </summary>
    void SetupChunkCounter()
    {
        chunkCounter = GetComponent<ChunkCounter>();
        if (chunkCounter == null)
        {
            chunkCounter = gameObject.AddComponent<ChunkCounter>();
        }

        // 초기 조각 수 저장
        initialChunkCount = chunkCounter.TotalChunksAtStart;

        Debug.Log($"ChunkCounter 설정 완료: {initialChunkCount}개 조각");
    }

    /// <summary>
    /// GameManager 연동
    /// </summary>
    void SetupGameManager()
    {

        Debug.Log("GameManager 연동 완료");
    }

    /// <summary>
    /// 광물 속성에 따른 설정 적용
    /// </summary>
    void ApplyMineralProperties()
    {
        // 광물 종류별 특성 적용
        switch (mineralType.ToLower())
        {
            case "다이아몬드":
                hardness = 1.0f;
                mineralColor = Color.white;
                gemQuality = 1.2f;
                break;

            case "에메랄드":
                hardness = 1.3f;
                mineralColor = Color.green;
                gemQuality = 1.1f;
                break;

            case "루비":
                hardness = 1.5f;
                mineralColor = Color.red;
                gemQuality = 1.0f;
                break;

            case "사파이어":
                hardness = 1.7f;
                mineralColor = Color.blue;
                gemQuality = 0.9f;
                break;
        }

        // 시각적 색상 적용
        ApplyVisualColor();

        Debug.Log($"광물 속성 적용: {mineralType} (경도: {hardness}, 품질: {gemQuality})");
    }

    /// <summary>
    /// 광물 색상 시각적 적용
    /// </summary>
    void ApplyVisualColor()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = mineralColor;
        }

        // 자식 조각들에도 색상 적용
        ChunkNode[] chunks = GetComponentsInChildren<ChunkNode>();
        foreach (ChunkNode chunk in chunks)
        {
            Renderer chunkRenderer = chunk.GetComponent<Renderer>();
            if (chunkRenderer != null)
            {
                chunkRenderer.material.color = mineralColor;
            }
        }
    }

    /// <summary>
    /// 광물 블록 리셋
    /// </summary>
    public void ResetMineralBlock()
    {
        Debug.Log($"{mineralType} 블록 리셋");

        // 각 시스템 리셋
        if (chunkCounter != null)
        {
            // ChunkCounter 리셋 메서드 호출
            var resetMethod = chunkCounter.GetType().GetMethod("ResetCounter");
            if (resetMethod != null)
            {
                resetMethod.Invoke(chunkCounter, null);
            }
        }

        if (gemProtectionSystem != null)
        {
            // 보석 상태 리셋
            var gems = gemProtectionSystem.GetAllGems();
            foreach (var gem in gems)
            {
                gem.currentCondition = 100f;
                gem.receivedHits = 0;
                gem.isProtected = true;
                gem.isDestroyed = false;
            }
        }

        Debug.Log($"{mineralType} 블록 리셋 완료");
    }

    /// <summary>
    /// 블록 상태 정보 반환
    /// </summary>
    public (string type, float hardness, int chunks, float progress) GetBlockStatus()
    {
        float progress = chunkCounter?.MiningProgress ?? 0f;
        int currentChunks = chunkCounter?.CurrentActiveChunks ?? 0;

        return (mineralType, hardness, currentChunks, progress);
    }

    /// <summary>
    /// 채굴 가능 여부 확인
    /// </summary>
    public bool CanMine()
    {
        if (!blockInitialized) return false;

        return chunkCounter?.CurrentActiveChunks > 0;
    }

    /// <summary>
    /// 광물 종류 변경 (스테이지 전환용)
    /// </summary>
    public void ChangeMineralType(string newType)
    {
        mineralType = newType;
        ApplyMineralProperties();

        Debug.Log($"광물 종류 변경: {newType}");
    }

    [ContextMenu("블록 상태 출력")]
    public void PrintBlockStatus()
    {
        var status = GetBlockStatus();

        Debug.Log("=== MineralBlock 상태 ===");
        Debug.Log($"종류: {status.type}");
        Debug.Log($"경도: {status.hardness}");
        Debug.Log($"남은 조각: {status.chunks}");
        Debug.Log($"채굴 진행: {status.progress * 100f:F1}%");
        Debug.Log($"채굴 가능: {CanMine()}");
        Debug.Log("========================");
    }

    [ContextMenu("블록 리셋")]
    public void ResetBlock()
    {
        ResetMineralBlock();
    }
}