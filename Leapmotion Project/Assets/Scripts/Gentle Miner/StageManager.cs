using UnityEngine;
using LibreFracture;

/// <summary>
/// 스테이지별 보석 관리 및 난이도 조절 시스템
/// 각 스테이지마다 다른 보석 종류와 배치를 관리
/// </summary>
public class StageManager : MonoBehaviour
{
    [System.Serializable]
    public class StageConfig
    {
        [Header("스테이지 정보")]
        public string stageName = "다이아몬드 광산";
        public string description = "첫 번째 채굴 도전";

        [Header("광물 블록 설정")]
        public GameObject mineralBlockPrefab; // 스테이지용 광물 프리팹
        public float mineralHardness = 1.0f; // 광물 경도

        [Header("보석 설정")]
        public GameObject[] gemPrefabs; // 보석 프리팹들
        public int gemCount = 3; // 보석 개수
        public float gemQuality = 1.0f; // 보석 품질 배율
        public float gemProtectionRadius = 0.4f; // 보호 반경

        [Header("난이도 설정")]
        public float damageThreshold = 15f; // 보석 피해 임계값
        public int freeHitCount = 1; // 무료 충격 횟수
        public float jointBreakForce = 50f; // 조각 연결 강도
    }

    [Header("스테이지 구성")]
    public StageConfig[] stageConfigs = new StageConfig[3];

    [Header("스폰 위치")]
    public Transform mineralSpawnPoint; // 광물 블록 스폰 위치
    public Transform[] gemSpawnPoints; // 보석 스폰 위치들

    [Header("현재 스테이지")]
    [SerializeField] private int currentStageIndex = 0;
    [SerializeField] private GameObject currentMineralBlock;
    [SerializeField] private GameObject[] currentGems;

    // 시스템 참조
    private GemProtectionSystem gemProtectionSystem;
    private MineralBlock mineralBlockScript;
    private ChunkGraphManager chunkGraphManager;

    // 이벤트
    public System.Action<int> OnStageInitialized;
    public System.Action<int> OnStageCompleted;

    void Start()
    {
        // 기본 스테이지 설정이 비어있으면 생성
        if (stageConfigs.Length == 0 || stageConfigs[0].stageName == null)
        {
            CreateDefaultStageConfigs();
        }
    }

    void CreateDefaultStageConfigs()
    {
        stageConfigs = new StageConfig[3];

        // 스테이지 1: 쉬움 - 에메랄드
        stageConfigs[0] = new StageConfig
        {
            stageName = "에메랄드 광산",
            description = "첫 번째 채굴 도전 - 조심스럽게 접근하세요",
            mineralHardness = 1.0f,
            gemCount = 2,
            gemQuality = 1.2f,
            gemProtectionRadius = 0.5f,
            damageThreshold = 20f,
            freeHitCount = 2,
            jointBreakForce = 40f
        };

        // 스테이지 2: 보통 - 다이아몬드
        stageConfigs[1] = new StageConfig
        {
            stageName = "다이아몬드 동굴",
            description = "더 단단한 광물 - 정밀한 작업이 필요합니다",
            mineralHardness = 1.3f,
            gemCount = 3,
            gemQuality = 1.0f,
            gemProtectionRadius = 0.4f,
            damageThreshold = 15f,
            freeHitCount = 1,
            jointBreakForce = 60f
        };

        // 스테이지 3: 어려움 - 루비
        stageConfigs[2] = new StageConfig
        {
            stageName = "루비 심층부",
            description = "최고 난이도 - 마스터 채굴자만 도전하세요",
            mineralHardness = 1.5f,
            gemCount = 4,
            gemQuality = 0.8f,
            gemProtectionRadius = 0.3f,
            damageThreshold = 12f,
            freeHitCount = 1,
            jointBreakForce = 80f
        };

        Debug.Log("기본 스테이지 구성 생성 완료");
    }

    /// <summary>
    /// 스테이지 초기화
    /// </summary>
    public void InitializeStage(int stageNumber)
    {
        if (stageNumber < 1 || stageNumber > stageConfigs.Length)
        {
            Debug.LogError($"잘못된 스테이지 번호: {stageNumber}");
            return;
        }

        currentStageIndex = stageNumber - 1; // 0 기반 인덱스로 변환
        StageConfig config = stageConfigs[currentStageIndex];

        Debug.Log($"=== 스테이지 {stageNumber} 초기화: {config.stageName} ===");

        // 이전 스테이지 정리
        CleanupCurrentStage();

        // 새 광물 블록 생성
        CreateMineralBlock(config);

        // 보석들 배치
        SetupGems(config);

        // 시스템 설정 적용
        ApplyStageSettings(config);

        Debug.Log($"스테이지 {stageNumber} 초기화 완료");
        OnStageInitialized?.Invoke(stageNumber);
    }

    void CleanupCurrentStage()
    {
        // 현재 광물 블록 제거
        if (currentMineralBlock != null)
        {
            DestroyImmediate(currentMineralBlock);
            currentMineralBlock = null;
        }

        // 현재 보석들 제거
        if (currentGems != null)
        {
            foreach (GameObject gem in currentGems)
            {
                if (gem != null)
                {
                    DestroyImmediate(gem);
                }
            }
            currentGems = null;
        }

        // 떨어진 조각들 정리
        CleanupFallenChunks();

        Debug.Log("이전 스테이지 정리 완료");
    }

    void CreateMineralBlock(StageConfig config)
    {
        Vector3 spawnPos = mineralSpawnPoint ? mineralSpawnPoint.position : Vector3.zero;

        // 광물 블록 프리팹이 있으면 사용, 없으면 현재 오브젝트 사용
        if (config.mineralBlockPrefab != null)
        {
            currentMineralBlock = Instantiate(config.mineralBlockPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 현재 오브젝트를 광물 블록으로 사용
            currentMineralBlock = gameObject;
        }

        // MineralBlock 스크립트 설정
        mineralBlockScript = currentMineralBlock.GetComponent<MineralBlock>();
        if (mineralBlockScript == null)
        {
            mineralBlockScript = currentMineralBlock.AddComponent<MineralBlock>();
        }

        // ChunkGraphManager 설정
        chunkGraphManager = currentMineralBlock.GetComponent<ChunkGraphManager>();
        if (chunkGraphManager != null)
        {
            chunkGraphManager.totalMass = 10f * config.mineralHardness;
            chunkGraphManager.jointBreakForce = config.jointBreakForce;
        }

        Debug.Log($"광물 블록 생성: 경도 {config.mineralHardness}");

        // MineralBlock을 ButtonManager에 설정(회전 기능 부여를 위함)
        ButtonManager.Instance.SetMineral(currentMineralBlock);
    }

    void SetupGems(StageConfig config)
    {
        // GemProtectionSystem 찾기 또는 생성
        gemProtectionSystem = currentMineralBlock.GetComponent<GemProtectionSystem>();
        if (gemProtectionSystem == null)
        {
            gemProtectionSystem = currentMineralBlock.AddComponent<GemProtectionSystem>();
        }

        // 보석들 배치
        currentGems = new GameObject[config.gemCount];

        for (int i = 0; i < config.gemCount && i < gemSpawnPoints.Length; i++)
        {
            // 보석 스폰 위치 결정
            Vector3 gemPos = gemSpawnPoints[i] ? gemSpawnPoints[i].position :
                           Vector3.zero + Random.insideUnitSphere * 2f;

            // 보석 생성 (프리팹이 있으면 사용, 없으면 기본 구체)
            GameObject gemPrefab = null;
            if (config.gemPrefabs != null && config.gemPrefabs.Length > 0)
            {
                gemPrefab = config.gemPrefabs[i % config.gemPrefabs.Length];
            }

            if (gemPrefab != null)
            {
                currentGems[i] = Instantiate(gemPrefab, gemPos, Quaternion.identity);
            }
            else
            {
                // 기본 보석 생성
                currentGems[i] = CreateDefaultGem(gemPos, i);
            }

            // 보석을 광물 블록의 자식으로 설정
            currentGems[i].transform.SetParent(currentMineralBlock.transform);
            currentGems[i].tag = "Gem";

            Debug.Log($"보석 {i + 1} 배치: {currentGems[i].name}");
        }

        // GemProtectionSystem에 보석들 등록
        RegisterGemsToProtectionSystem(config);
    }

    GameObject CreateDefaultGem(Vector3 position, int gemIndex)
    {
        GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gem.name = $"Gem_{gemIndex + 1}";
        gem.transform.position = position;
        gem.transform.localScale = Vector3.one * 0.3f;

        // 스테이지별 색상
        Color gemColor = GetStageGemColor(currentStageIndex);
        gem.GetComponent<Renderer>().material.color = gemColor;

        // 반짝이는 재질 설정
        Material gemMaterial = gem.GetComponent<Renderer>().material;
        gemMaterial.SetFloat("_Metallic", 0.8f);
        gemMaterial.SetFloat("_Smoothness", 0.9f);

        return gem;
    }

    Color GetStageGemColor(int stageIndex)
    {
        switch (stageIndex)
        {
            case 0: return Color.white;      // 다이아몬드 - 화이트
            case 1: return Color.green;      // 에메랄드 - 그린
            case 2: return Color.red;        // 루비 - 레드
            default: return Color.white;
        }
    }

    void RegisterGemsToProtectionSystem(StageConfig config)
    {
        // GemProtectionSystem의 gems 배열 설정
        var gemDataList = new System.Collections.Generic.List<GemProtectionSystem.GemData>();

        for (int i = 0; i < currentGems.Length; i++)
        {
            if (currentGems[i] != null)
            {
                var gemData = new GemProtectionSystem.GemData
                {
                    gemObject = currentGems[i],
                    gemName = $"{stageConfigs[currentStageIndex].stageName} 보석 {i + 1}",
                    damageThreshold = config.damageThreshold,
                    protectionRadius = config.gemProtectionRadius * config.gemQuality,
                    freeHitCount = config.freeHitCount,
                    currentCondition = 100f
                };

                gemDataList.Add(gemData);
            }
        }

        // GemProtectionSystem에 반영 (리플렉션 사용)
        var gemsField = typeof(GemProtectionSystem).GetField("gems");
        if (gemsField != null)
        {
            gemsField.SetValue(gemProtectionSystem, gemDataList.ToArray());
        }
    }


    void ApplyStageSettings(StageConfig config)
    {
        // MineralBlock 설정
        if (mineralBlockScript != null)
        {
            mineralBlockScript.hardness = config.mineralHardness;
            mineralBlockScript.gemQuality = config.gemQuality;

            // 설정 변경 후 MineralBlock에서 실제 시스템에 적용
            mineralBlockScript.ApplyStageSettings();
        }
    }

    public GameObject GetCurrentMineralBlock()
    {
        return currentMineralBlock;
    }
    void CleanupFallenChunks()
    {
        // 수동으로 정리
        ChunkNode[] fallenChunks = FindObjectsByType<ChunkNode>(FindObjectsSortMode.None);
        foreach (ChunkNode chunk in fallenChunks)
        {
            if (chunk != null && !chunk.transform.IsChildOf(transform))
            {
                DestroyImmediate(chunk.gameObject);
            }
        }

        Debug.Log("떨어진 조각들 정리 완료");
    }

    /// <summary>
    /// 스테이지 재시작
    /// </summary>
    public void RestartStage(int stageNumber)
    {
        Debug.Log($"스테이지 {stageNumber} 재시작");
        InitializeStage(stageNumber);
    }

    /// <summary>
    /// 현재 스테이지 정보 반환
    /// </summary>
    public StageConfig GetCurrentStageConfig()
    {
        if (currentStageIndex >= 0 && currentStageIndex < stageConfigs.Length)
        {
            return stageConfigs[currentStageIndex];
        }
        return null;
    }

    /// <summary>
    /// 스테이지 완료 처리
    /// </summary>
    public void CompleteStage(int stageNumber)
    {
        Debug.Log($"스테이지 {stageNumber} 완료!");
        OnStageCompleted?.Invoke(stageNumber);
    }
}