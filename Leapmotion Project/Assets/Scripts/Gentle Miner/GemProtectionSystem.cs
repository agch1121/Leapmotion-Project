using UnityEngine;
using System.Collections;

/// <summary>
/// 광물 내 보석 보호 및 손상 관리 시스템.
/// 채굴 충격으로부터 보석의 내구도를 관리하고, 손상 및 파괴 처리.
/// </summary>
public class GemProtectionSystem : MonoBehaviour
{
    #region GemData Class

    /// <summary>
    /// 개별 보석의 데이터와 상태를 저장하는 클래스
    /// </summary>
    [System.Serializable]
    public class GemData
    {
        [Header("보석 기본 정보")]
        public GameObject gemObject; // 보석 게임 오브젝트
        public string gemName = "다이아몬드"; // 보석 이름

        [Header("보호 설정")]
        public float damageThreshold = 15f; // 손상을 받기 시작하는 최소 충격량
        public float protectionRadius = 0.4f; // 충격 감지 보호 반경
        public int freeHitCount = 1; // 손상 없이 견딜 수 있는 충격 횟수

        [Header("손상 상태")]
        [Range(0f, 100f)]
        public float currentCondition = 100f; // 현재 보석 내구도 (100% = 완벽)

        [Header("시각적 효과")]
        public Material perfectMaterial; // 완벽 상태 재질
        public Material damagedMaterial; // 손상 상태 재질
        public Material heavilyDamagedMaterial; // 심한 손상 상태 재질

        // --- 내부 상태 변수 (인스펙터 숨김) ---
        [HideInInspector] public int receivedHits = 0; // 받은 총 충격 횟수
        [HideInInspector] public bool isProtected = true; // 현재 보호 상태 여부 (freeHitCount 소진 전)
        [HideInInspector] public bool isDestroyed = false; // 파괴 여부
    }

    #endregion

    #region 변수 및 이벤트

    [Header("보석 목록")]
    public GemData[] gems; // 스테이지의 모든 보석 데이터 배열

    [Header("파괴 연출 시스템")]
    public GemRevealSystem gemRevealSystem; // 보석 파괴/성공 연출 시스템 참조

    [Header("디버그")]
    public bool showProtectionRadius = true; // 씬(Scene)에서 보호 반경 기즈모 표시 여부
    public bool enableDebugLogs = true; // 디버그 로그 출력 여부

    [Header("이벤트 시스템")]
    public System.Action OnAnyGemDestroyed; // 보석이 하나라도 파괴될 때 발생
    public System.Action<GemData> OnSpecificGemDestroyed; // 특정 보석이 파괴될 때 발생
    public System.Action<GemData> OnGemConditionChanged; // 보석의 내구도 변경 시 발생

    #endregion

    #region 초기화

    private void Start()
    {
        InitializeGems();

        // GemRevealSystem 참조가 없으면 씬에서 찾기
        if (gemRevealSystem == null)
        {
            gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
        }
    }

    /// <summary>
    /// 모든 보석의 초기 상태 설정.
    /// 물리 설정, 태그, 내부 상태 변수 초기화 수행.
    /// </summary>
    private void InitializeGems()
    {
        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                gem.gemObject.tag = "Gem";

                // Rigidbody 컴포넌트 확인 및 설정 (물리적 충돌 방지)
                Rigidbody gemRb = gem.gemObject.GetComponent<Rigidbody>();
                if (gemRb == null)
                {
                    gemRb = gem.gemObject.AddComponent<Rigidbody>();
                }
                gemRb.isKinematic = true;

                // 상태 변수 초기화
                gem.receivedHits = 0;
                gem.isProtected = true;
                gem.currentCondition = 100f;
                gem.isDestroyed = false;

                // 초기 시각적 상태 업데이트
                UpdateGemVisuals(gem);

                if (enableDebugLogs)
                    Debug.Log($"보석 초기화: {gem.gemName} - 보호범위: {gem.protectionRadius}m");
            }
        }
    }

    #endregion

    #region 충격 처리

    /// <summary>
    /// 채굴 지점과 충격량을 받아 모든 보석에 대한 영향 검사.
    /// </summary>
    /// <param name="miningPoint">채굴 발생 위치</param>
    /// <param name="impactForce">채굴 충격량</param>
    public void CheckMiningImpactOnGems(Vector3 miningPoint, float impactForce)
    {
        foreach (GemData gem in gems)
        {
            if (gem.gemObject == null || gem.isDestroyed) continue;

            // 보석과 충격 지점 사이의 거리 계산
            float distance = Vector3.Distance(gem.gemObject.transform.position, miningPoint);

            // 충격이 보호 반경 내에서 발생했는지 확인
            if (distance <= gem.protectionRadius)
            {
                ProcessGemImpact(gem, impactForce, distance);
            }
        }
    }

    /// <summary>
    /// 개별 보석에 가해지는 충격 처리.
    /// 충격량이 임계값 미만이면 무시, freeHitCount가 남았으면 보호 효과, 아니면 손상 적용.
    /// </summary>
    /// <param name="gem">대상 보석</param>
    /// <param name="impactForce">원본 충격량</param>
    /// <param name="distance">충격 지점과의 거리</param>
    private void ProcessGemImpact(GemData gem, float impactForce, float distance)
    {
        // 충격량이 손상 임계값보다 낮으면 무시
        if (impactForce < gem.damageThreshold)
        {
            if (enableDebugLogs)
                Debug.Log($"{gem.gemName}: 충격이 약해 영향 없음 ({impactForce:F1} < {gem.damageThreshold})");
            return;
        }

        gem.receivedHits++;

        // 거리에 따라 실제 충격량 보정 (가까울수록 강함)
        float distanceMultiplier = 1f - (distance / gem.protectionRadius);
        float actualImpact = impactForce * distanceMultiplier;

        if (enableDebugLogs)
            Debug.Log($"{gem.gemName}: 충격 감지! 충격 #{gem.receivedHits}, 실제충격: {actualImpact:F1}");

        // 무료 충격 횟수가 남아있으면 보호 효과만 주고 데미지는 없음
        if (gem.receivedHits <= gem.freeHitCount)
        {
            ShowProtectionEffect(gem);
            if (enableDebugLogs)
                Debug.Log($"{gem.gemName}: 보호막으로 충격 흡수! (무료 충격: {gem.receivedHits}/{gem.freeHitCount})");
            return;
        }

        // 보호 상태 해제 후 실제 손상 적용
        gem.isProtected = false;
        ApplyGemDamage(gem, actualImpact);
    }

    /// <summary>
    /// 보석 내구도 감소 및 시각적 효과 업데이트.
    /// 내구도가 0 이하가 되면 파괴 처리.
    /// </summary>
    /// <param name="gem">대상 보석</param>
    /// <param name="damage">적용할 손상량</param>
    private void ApplyGemDamage(GemData gem, float damage)
    {
        float damageAmount = damage * 2f;
        gem.currentCondition = Mathf.Max(0f, gem.currentCondition - damageAmount); // 0 밑으로 안내려감

        if (enableDebugLogs)
            Debug.Log($"{gem.gemName} 손상! 상태: {gem.currentCondition:F1}% (손상량: -{damageAmount:F1})");

        UpdateGemVisuals(gem);
        ShowDamageEffect(gem);

        // 보석 상태 변경 이벤트 호출
        OnGemConditionChanged?.Invoke(gem);

        // 내구도가 0 이하이고 아직 파괴되지 않았다면 파괴 처리
        if (gem.currentCondition <= 0f && !gem.isDestroyed)
        {
            gem.isDestroyed = true;
            Debug.Log($"{gem.gemName} 완전히 파괴됨!");
            OnGemDestroyed(gem);
        }
    }

    /// <summary>
    /// 보석 파괴 시 처리.
    /// 파괴 연출 시작 및 관련 이벤트 호출.
    /// </summary>
    /// <param name="gem">파괴된 보석</param>
    private void OnGemDestroyed(GemData gem)
    {
        // 게임 시작 전에는 파괴 연출 방지
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null && !gameManager.IsGameStarted) return;

        // 파괴 연출 시스템에 연출 시작 요청
        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemDestruction();
        }

        StartCoroutine(CreateDestructionEffect(gem.gemObject.transform.position));

        // 파괴 관련 이벤트 호출
        OnSpecificGemDestroyed?.Invoke(gem);
        OnAnyGemDestroyed?.Invoke();
        OnGemConditionChanged?.Invoke(gem); // 상태 변경 이벤트도 호출

        Debug.Log($"보석 파괴 이벤트 발생: {gem.gemName}");
    }

    #endregion

    #region 시각적/효과 처리

    /// <summary>
    /// 보석 내구도에 따라 재질(Material) 변경.
    /// </summary>
    /// <param name="gem">대상 보석</param>
    private void UpdateGemVisuals(GemData gem)
    {
        if (gem.gemObject == null) return;

        MeshRenderer renderer = gem.gemObject.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        // 내구도 구간에 따라 다른 재질 적용
        if (gem.currentCondition > 70f && gem.perfectMaterial != null)
        {
            renderer.material = gem.perfectMaterial;
        }
        else if (gem.currentCondition > 30f && gem.damagedMaterial != null)
        {
            renderer.material = gem.damagedMaterial;
        }
        else if (gem.heavilyDamagedMaterial != null)
        {
            renderer.material = gem.heavilyDamagedMaterial;
        }
    }

    /// <summary>
    /// 보호 효과(무료 충격) 시각 효과 표시.
    /// </summary>
    private void ShowProtectionEffect(GemData gem)
    {
        StartCoroutine(CreateProtectionEffect(gem.gemObject.transform.position));
    }

    /// <summary>
    /// 손상 시각 효과 표시.
    /// </summary>
    private void ShowDamageEffect(GemData gem)
    {
        StartCoroutine(CreateDamageEffect(gem.gemObject.transform.position));
    }

    /// <summary>
    /// 보호막 효과(파란 구체가 커지며 사라짐) 코루틴.
    /// </summary>
    private IEnumerator CreateProtectionEffect(Vector3 position)
    {
        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shield.transform.position = position;
        shield.transform.localScale = Vector3.one * 0.3f;
        MeshRenderer shieldRenderer = shield.GetComponent<MeshRenderer>();
        shieldRenderer.material.color = new Color(0f, 0.5f, 1f, 0.3f);
        Destroy(shield.GetComponent<Collider>());

        float elapsed = 0f;
        float duration = 1f;
        Vector3 startScale = shield.transform.localScale;
        Vector3 endScale = startScale * 2f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            shield.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            Color color = shieldRenderer.material.color;
            color.a = Mathf.Lerp(0.3f, 0f, t); // 점점 투명해짐
            shieldRenderer.material.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(shield);
    }

    /// <summary>
    /// 손상 효과(빨간 파편) 코루틴.
    /// </summary>
    private IEnumerator CreateDamageEffect(Vector3 position)
    {
        for (int i = 0; i < 8; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particle.transform.position = position + Random.insideUnitSphere * 0.1f;
            particle.transform.localScale = Vector3.one * 0.02f;
            particle.GetComponent<MeshRenderer>().material.color = Color.red;
            Rigidbody particleRb = particle.AddComponent<Rigidbody>();
            particleRb.AddForce(Random.insideUnitSphere * 3f, ForceMode.Impulse);
            Destroy(particle.GetComponent<Collider>());
            Destroy(particle, 1f);
        }
        yield return null;
    }

    /// <summary>
    /// 파괴 효과(붉은 조각들) 코루틴.
    /// </summary>
    private IEnumerator CreateDestructionEffect(Vector3 position)
    {
        for (int i = 0; i < 15; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = position + Random.insideUnitSphere * 0.2f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.03f, 0.08f);
            fragment.transform.rotation = Random.rotation;
            fragment.GetComponent<MeshRenderer>().material.color = new Color(1f, 0.3f, 0.3f);
            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.AddForce(Random.insideUnitSphere * 8f, ForceMode.Impulse);
            Destroy(fragment.GetComponent<Collider>());
            Destroy(fragment, 3f);
        }
        yield return null;
    }

    #endregion

    #region 유틸리티 및 디버그

    /// <summary>
    /// 현재 관리 중인 모든 보석 데이터 배열 반환.
    /// </summary>
    public GemData[] GetAllGems()
    {
        return gems;
    }

    /// <summary>
    /// 특정 보석의 상태에 따른 점수 반환 (ScoreSystem에서 사용).
    /// </summary>
    public int CalculateGemScore(GemData gem)
    {
        if (gem.isDestroyed) return 0;
        if (gem.currentCondition >= 90f) return 100;
        if (gem.currentCondition >= 70f) return 70;
        if (gem.currentCondition >= 30f) return 30;
        return 10;
    }

    /// <summary>
    /// 파괴된 보석이 하나라도 있는지 확인.
    /// </summary>
    public bool HasAnyGemDestroyed()
    {
        foreach (GemData gem in gems)
        {
            if (gem.isDestroyed) return true;
        }
        return false;
    }

    /// <summary>
    /// 에디터에서 보석의 보호 반경을 기즈모로 시각화.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showProtectionRadius) return;

        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                // 보호 상태에 따라 색상 변경 (파랑: 안전, 빨강: 위험)
                Gizmos.color = gem.isProtected ? Color.blue : Color.red;
                Gizmos.DrawWireSphere(gem.gemObject.transform.position, gem.protectionRadius);

#if UNITY_EDITOR
                // 보석 이름 표시
                UnityEditor.Handles.Label(gem.gemObject.transform.position + Vector3.up * 0.3f, gem.gemName);
#endif
            }
        }
    }

    #endregion
}