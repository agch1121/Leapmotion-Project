using UnityEngine;
using System.Collections;

public class GemProtectionSystem : MonoBehaviour
{
    [System.Serializable]
    public class GemData
    {
        [Header("보석 기본 정보")]
        public GameObject gemObject;
        public string gemName = "다이아몬드";

        [Header("보호 설정")]
        public float damageThreshold = 15f; // 이 수치 이상의 충격부터 손상 위험
        public float protectionRadius = 0.4f; // 보석 주변 보호 영역
        public int freeHitCount = 1; // 무료로 견딜 수 있는 타격 횟수

        [Header("손상 상태")]
        [Range(0f, 100f)]
        public float currentCondition = 100f; // 현재 보석 상태 (100% = 완벽)

        [Header("시각적 효과")]
        public Material perfectMaterial;
        public Material damagedMaterial;
        public Material heavilyDamagedMaterial;

        // 내부 상태 변수들
        [HideInInspector] public int receivedHits = 0; // 받은 타격 횟수
        [HideInInspector] public bool isProtected = true; // 현재 보호 상태인지
        [HideInInspector] public bool isDestroyed = false; // 완전 파괴 여부
    }

    [Header("보석 목록")]
    public GemData[] gems;

    [Header("디버그")]
    public bool showProtectionRadius = true;
    public bool enableDebugLogs = true;

    private void Start()
    {
        InitializeGems();
    }

    /// <summary>
    /// 모든 보석 초기화
    /// </summary>
    private void InitializeGems()
    {
        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                // 보석에 태그 설정
                gem.gemObject.tag = "Gem";

                // Rigidbody 설정 (Kinematic)
                Rigidbody gemRb = gem.gemObject.GetComponent<Rigidbody>();
                if (gemRb == null)
                {
                    gemRb = gem.gemObject.AddComponent<Rigidbody>();
                }
                gemRb.isKinematic = true;

                // 초기 상태 설정
                gem.receivedHits = 0;
                gem.isProtected = true;
                gem.currentCondition = 100f;

                // 초기 머티리얼 적용
                UpdateGemVisuals(gem);

                if (enableDebugLogs)
                    Debug.Log($"보석 초기화: {gem.gemName} - 보호반경: {gem.protectionRadius}m");
            }
        }
    }

    /// <summary>
    /// 채굴 충격이 보석에 영향을 주는지 확인 (Test.cs의 MineAtPoint에서 호출)
    /// </summary>
    public void CheckMiningImpactOnGems(Vector3 miningPoint, float impactForce)
    {
        foreach (GemData gem in gems)
        {
            if (gem.gemObject == null || gem.isDestroyed) continue;

            float distance = Vector3.Distance(gem.gemObject.transform.position, miningPoint);

            // 보석 보호 영역 내에서 채굴이 발생했는지 확인
            if (distance <= gem.protectionRadius)
            {
                ProcessGemImpact(gem, impactForce, distance);
            }
        }
    }

    /// <summary>
    /// 보석에 대한 충격 처리
    /// </summary>
    private void ProcessGemImpact(GemData gem, float impactForce, float distance)
    {
        // 충격 강도가 임계값 미만이면 무시
        if (impactForce < gem.damageThreshold)
        {
            if (enableDebugLogs)
                Debug.Log($"{gem.gemName}: 충격이 약해 영향 없음 ({impactForce:F1} < {gem.damageThreshold})");
            return;
        }

        gem.receivedHits++;

        // 거리에 따른 충격 감소 계산
        float distanceMultiplier = 1f - (distance / gem.protectionRadius);
        float actualImpact = impactForce * distanceMultiplier;

        if (enableDebugLogs)
            Debug.Log($"{gem.gemName}: 충격 감지! 타격 #{gem.receivedHits}, 실제충격: {actualImpact:F1}");

        // 보호 타격 횟수 이하면 무시 (보호막 역할)
        if (gem.receivedHits <= gem.freeHitCount)
        {
            ShowProtectionEffect(gem);
            if (enableDebugLogs)
                Debug.Log($"{gem.gemName}: 보호막으로 충격 흡수! (무료 타격: {gem.receivedHits}/{gem.freeHitCount})");
            return;
        }

        // 보호 해제 및 손상 적용
        gem.isProtected = false;
        ApplyGemDamage(gem, actualImpact);
    }

    /// <summary>
    /// 보석에 손상 적용
    /// </summary>
    private void ApplyGemDamage(GemData gem, float damage)
    {
        // 손상 계산 (기본적으로 충격의 2배만큼 상태 감소)
        float damageAmount = damage * 2f;
        gem.currentCondition = Mathf.Max(0f, gem.currentCondition - damageAmount);

        if (enableDebugLogs)
            Debug.Log($"{gem.gemName} 손상! 상태: {gem.currentCondition:F1}% (손상량: -{damageAmount:F1})");

        // 시각적 업데이트
        UpdateGemVisuals(gem);

        // 손상 효과
        ShowDamageEffect(gem);

        // 완전 파괴 체크
        if (gem.currentCondition <= 0f && !gem.isDestroyed)
        {
            gem.isDestroyed = true;
            OnGemDestroyed(gem);
        }
    }

    /// <summary>
    /// 보석 시각적 상태 업데이트
    /// </summary>
    private void UpdateGemVisuals(GemData gem)
    {
        if (gem.gemObject == null) return;

        MeshRenderer renderer = gem.gemObject.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        // 상태에 따른 머티리얼 변경
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
    /// 보호막 효과 표시
    /// </summary>
    private void ShowProtectionEffect(GemData gem)
    {
        // 보호막 시각 효과 (파란색 구체)
        StartCoroutine(CreateProtectionEffect(gem.gemObject.transform.position));
    }

    /// <summary>
    /// 손상 효과 표시
    /// </summary>
    private void ShowDamageEffect(GemData gem)
    {
        // 손상 시각 효과 (빨간색 파티클)
        StartCoroutine(CreateDamageEffect(gem.gemObject.transform.position));
    }

    /// <summary>
    /// 보석 완전 파괴 처리
    /// </summary>
    private void OnGemDestroyed(GemData gem)
    {
        if (enableDebugLogs)
            Debug.Log($"{gem.gemName} 완전 파괴됨!");

        // 파괴 효과
        StartCoroutine(CreateDestructionEffect(gem.gemObject.transform.position));

        // 여기에 점수 감점이나 게임 오버 로직 추가 가능
        // 예: ScoreManager.Instance.OnGemDestroyed(gem);
    }

    /// <summary>
    /// 보호막 시각 효과 코루틴
    /// </summary>
    private IEnumerator CreateProtectionEffect(Vector3 position)
    {
        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shield.transform.position = position;
        shield.transform.localScale = Vector3.one * 0.3f;

        MeshRenderer shieldRenderer = shield.GetComponent<MeshRenderer>();
        shieldRenderer.material.color = new Color(0f, 0.5f, 1f, 0.3f); // 반투명 파란색

        Destroy(shield.GetComponent<Collider>()); // 콜라이더 제거

        // 확대 애니메이션
        float elapsed = 0f;
        float duration = 0.5f;
        Vector3 startScale = shield.transform.localScale;
        Vector3 endScale = startScale * 2f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            shield.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            Color color = shieldRenderer.material.color;
            color.a = Mathf.Lerp(0.3f, 0f, t);
            shieldRenderer.material.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(shield);
    }

    /// <summary>
    /// 손상 효과 코루틴
    /// </summary>
    private IEnumerator CreateDamageEffect(Vector3 position)
    {
        // 작은 빨간 파티클들
        for (int i = 0; i < 8; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particle.transform.position = position + Random.insideUnitSphere * 0.1f;
            particle.transform.localScale = Vector3.one * 0.02f;

            MeshRenderer particleRenderer = particle.GetComponent<MeshRenderer>();
            particleRenderer.material.color = Color.red;

            Rigidbody particleRb = particle.AddComponent<Rigidbody>();
            particleRb.AddForce(Random.insideUnitSphere * 3f, ForceMode.Impulse);

            Destroy(particle.GetComponent<Collider>());
            Destroy(particle, 1f);
        }

        yield return null;
    }

    /// <summary>
    /// 파괴 효과 코루틴
    /// </summary>
    private IEnumerator CreateDestructionEffect(Vector3 position)
    {
        // 큰 폭발 효과
        for (int i = 0; i < 15; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = position + Random.insideUnitSphere * 0.2f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.03f, 0.08f);
            fragment.transform.rotation = Random.rotation;

            MeshRenderer fragmentRenderer = fragment.GetComponent<MeshRenderer>();
            fragmentRenderer.material.color = new Color(1f, 0.3f, 0.3f); // 붉은색 파편

            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.AddForce(Random.insideUnitSphere * 8f, ForceMode.Impulse);

            Destroy(fragment.GetComponent<Collider>());
            Destroy(fragment, 3f);
        }

        yield return null;
    }

    /// <summary>
    /// 현재 모든 보석의 상태 반환 (점수 시스템용)
    /// </summary>
    public GemData[] GetAllGems()
    {
        return gems;
    }

    /// <summary>
    /// 특정 보석의 점수 계산
    /// </summary>
    public int CalculateGemScore(GemData gem)
    {
        if (gem.isDestroyed) return 0;
        if (gem.currentCondition >= 90f) return 100; // 완벽한 보석
        if (gem.currentCondition >= 70f) return 70;  // 약간 손상
        if (gem.currentCondition >= 30f) return 30;  // 많이 손상
        return 10; // 거의 파괴 직전
    }

    /// <summary>
    /// 디버그용: 보석 상태 출력
    /// </summary>
    [ContextMenu("보석 상태 출력")]
    public void PrintGemStatus()
    {
        Debug.Log("=== 보석 상태 ===");
        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                string status = gem.isDestroyed ? "파괴됨" : $"{gem.currentCondition:F1}%";
                string protection = gem.isProtected ? "보호됨" : "취약";
                Debug.Log($"{gem.gemName}: {status} ({protection}) - 타격: {gem.receivedHits}회");
            }
        }
        Debug.Log("==============");
    }

    /// <summary>
    /// Gizmos로 보호 영역 표시
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showProtectionRadius) return;

        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                Gizmos.color = gem.isProtected ? Color.blue : Color.red;
                Gizmos.DrawWireSphere(gem.gemObject.transform.position, gem.protectionRadius);

                // 보석 이름 표시
                UnityEditor.Handles.Label(gem.gemObject.transform.position + Vector3.up * 0.3f, gem.gemName);
            }
        }
    }
}