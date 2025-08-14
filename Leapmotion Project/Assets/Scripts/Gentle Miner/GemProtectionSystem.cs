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
        public int freeHitCount = 1; // 무료로 견딜 수 있는 충격 횟수

        [Header("손상 상태")]
        [Range(0f, 100f)]
        public float currentCondition = 100f; // 현재 보석 상태 (100% = 완벽)

        [Header("시각적 효과")]
        public Material perfectMaterial;
        public Material damagedMaterial;
        public Material heavilyDamagedMaterial;

        // 내부 상태 변수들
        [HideInInspector] public int receivedHits = 0;
        [HideInInspector] public bool isProtected = true;
        [HideInInspector] public bool isDestroyed = false;
    }

    [Header("보석 목록")]
    public GemData[] gems;

    [Header("파괴 연출 시스템")]
    public GemRevealSystem gemRevealSystem;

    [Header("디버그")]
    public bool showProtectionRadius = true;
    public bool enableDebugLogs = true;

    [Header("이벤트 시스템")]
    public System.Action OnAnyGemDestroyed;
    public System.Action<GemData> OnSpecificGemDestroyed;
    public System.Action<GemData> OnGemConditionChanged; // [추가] 보석 상태 변경 이벤트

    private void Start()
    {
        InitializeGems();

        if (gemRevealSystem == null)
        {
            gemRevealSystem = FindFirstObjectByType<GemRevealSystem>();
        }
    }

    private void InitializeGems()
    {
        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                gem.gemObject.tag = "Gem";

                Rigidbody gemRb = gem.gemObject.GetComponent<Rigidbody>();
                if (gemRb == null)
                {
                    gemRb = gem.gemObject.AddComponent<Rigidbody>();
                }
                gemRb.isKinematic = true;

                gem.receivedHits = 0;
                gem.isProtected = true;
                gem.currentCondition = 100f;
                gem.isDestroyed = false;

                UpdateGemVisuals(gem);

                if (enableDebugLogs)
                    Debug.Log($"보석 초기화: {gem.gemName} - 보호범위: {gem.protectionRadius}m");
            }
        }
    }

    public void CheckMiningImpactOnGems(Vector3 miningPoint, float impactForce)
    {
        foreach (GemData gem in gems)
        {
            if (gem.gemObject == null || gem.isDestroyed) continue;

            float distance = Vector3.Distance(gem.gemObject.transform.position, miningPoint);

            if (distance <= gem.protectionRadius)
            {
                ProcessGemImpact(gem, impactForce, distance);
            }
        }
    }

    private void ProcessGemImpact(GemData gem, float impactForce, float distance)
    {
        if (impactForce < gem.damageThreshold)
        {
            if (enableDebugLogs)
                Debug.Log($"{gem.gemName}: 충격이 약해 영향 없음 ({impactForce:F1} < {gem.damageThreshold})");
            return;
        }

        gem.receivedHits++;

        float distanceMultiplier = 1f - (distance / gem.protectionRadius);
        float actualImpact = impactForce * distanceMultiplier;

        if (enableDebugLogs)
            Debug.Log($"{gem.gemName}: 충격 감지! 충격 #{gem.receivedHits}, 실제충격: {actualImpact:F1}");

        if (gem.receivedHits <= gem.freeHitCount)
        {
            ShowProtectionEffect(gem);
            if (enableDebugLogs)
                Debug.Log($"{gem.gemName}: 보호막으로 충격 흡수! (무료 충격: {gem.receivedHits}/{gem.freeHitCount})");
            return;
        }

        gem.isProtected = false;
        ApplyGemDamage(gem, actualImpact);
    }

    private void ApplyGemDamage(GemData gem, float damage)
    {
        float damageAmount = damage * 2f;
        gem.currentCondition = Mathf.Max(0f, gem.currentCondition - damageAmount);

        if (enableDebugLogs)
            Debug.Log($"{gem.gemName} 손상! 상태: {gem.currentCondition:F1}% (손상량: -{damageAmount:F1})");

        UpdateGemVisuals(gem);
        ShowDamageEffect(gem);

        // [추가] 보석 상태 변경 이벤트 호출
        OnGemConditionChanged?.Invoke(gem);

        if (gem.currentCondition <= 0f && !gem.isDestroyed)
        {
            gem.isDestroyed = true;
            Debug.Log($"{gem.gemName} 완전히 파괴됨!");
            OnGemDestroyed(gem);
        }
    }

    private void UpdateGemVisuals(GemData gem)
    {
        if (gem.gemObject == null) return;

        MeshRenderer renderer = gem.gemObject.GetComponent<MeshRenderer>();
        if (renderer == null) return;

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

    private void ShowProtectionEffect(GemData gem)
    {
        StartCoroutine(CreateProtectionEffect(gem.gemObject.transform.position));
    }

    private void ShowDamageEffect(GemData gem)
    {
        StartCoroutine(CreateDamageEffect(gem.gemObject.transform.position));
    }

    private void OnGemDestroyed(GemData gem)
    {
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null && !gameManager.IsGameStarted) return;

        if (gemRevealSystem != null)
        {
            gemRevealSystem.StartGemDestruction();
        }

        StartCoroutine(CreateDestructionEffect(gem.gemObject.transform.position));

        OnSpecificGemDestroyed?.Invoke(gem);
        OnAnyGemDestroyed?.Invoke();

        // [추가] 보석 파괴 시에도 상태 변경 이벤트 호출
        OnGemConditionChanged?.Invoke(gem);

        Debug.Log($"보석 파괴 이벤트 발생: {gem.gemName}");
    }

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
            color.a = Mathf.Lerp(0.3f, 0f, t);
            shieldRenderer.material.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(shield);
    }

    private IEnumerator CreateDamageEffect(Vector3 position)
    {
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

    private IEnumerator CreateDestructionEffect(Vector3 position)
    {
        for (int i = 0; i < 15; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = position + Random.insideUnitSphere * 0.2f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.03f, 0.08f);
            fragment.transform.rotation = Random.rotation;

            MeshRenderer fragmentRenderer = fragment.GetComponent<MeshRenderer>();
            fragmentRenderer.material.color = new Color(1f, 0.3f, 0.3f);

            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.AddForce(Random.insideUnitSphere * 8f, ForceMode.Impulse);

            Destroy(fragment.GetComponent<Collider>());
            Destroy(fragment, 3f);
        }

        yield return null;
    }

    public GemData[] GetAllGems()
    {
        return gems;
    }

    public int CalculateGemScore(GemData gem)
    {
        if (gem.isDestroyed) return 0;
        if (gem.currentCondition >= 90f) return 100;
        if (gem.currentCondition >= 70f) return 70;
        if (gem.currentCondition >= 30f) return 30;
        return 10;
    }

    public bool HasAnyGemDestroyed()
    {
        foreach (GemData gem in gems)
        {
            if (gem.isDestroyed) return true;
        }
        return false;
    }

    [ContextMenu("보석 상태 출력")]
    public void PrintGemStatus()
    {
        Debug.Log("=== 보석 상태 ===");
        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                string status = gem.isDestroyed ? "파괴됨" : $"{gem.currentCondition:F1}%";
                string protection = gem.isProtected ? "보호됨" : "노출";
                Debug.Log($"{gem.gemName}: {status} ({protection}) - 충격: {gem.receivedHits}회");
            }
        }
        Debug.Log("==============");
    }

    [ContextMenu("첫 번째 보석 강제 파괴 테스트")]
    public void TestForceDestroyFirstGem()
    {
        if (gems.Length > 0 && gems[0].gemObject != null)
        {
            Debug.Log("첫 번째 보석 강제 파괴 테스트 시작");
            Debug.Log($"GemRevealSystem 연결 상태: {(gemRevealSystem != null ? "연결됨" : "연결 안됨")}");

            gems[0].currentCondition = 0f;
            gems[0].isDestroyed = true;
            OnGemDestroyed(gems[0]);
        }
        else
        {
            Debug.LogError("첫 번째 보석이 없거나 gemObject가 null입니다!");
        }
    }

    [ContextMenu("첫 번째 보석에 연속 손상 테스트")]
    public void TestContinuousDamageFirstGem()
    {
        if (gems.Length > 0 && gems[0].gemObject != null && !gems[0].isDestroyed)
        {
            Debug.Log("첫 번째 보석에 연속 손상 적용 시작");

            Vector3 gemPosition = gems[0].gemObject.transform.position;

            for (int i = 0; i < 10; i++)
            {
                CheckMiningImpactOnGems(gemPosition, 30f);

                if (gems[0].isDestroyed)
                {
                    Debug.Log($"보석이 {i + 1}번째 충격으로 파괴되었습니다!");
                    break;
                }
                else
                {
                    Debug.Log($"{i + 1}번째 충격 후 보석 상태: {gems[0].currentCondition:F1}%");
                }
            }
        }
        else
        {
            Debug.LogError("첫 번째 보석이 없거나 이미 파괴되었습니다!");
        }
    }

    [ContextMenu("시스템 연결 상태 확인")]
    public void CheckSystemConnections()
    {
        Debug.Log("=== 시스템 연결 상태 ===");
        Debug.Log($"GemRevealSystem: {(gemRevealSystem != null ? "연결됨" : "연결 안됨")}");
        Debug.Log($"보석 개수: {gems.Length}");

        for (int i = 0; i < gems.Length; i++)
        {
            if (gems[i].gemObject != null)
            {
                Debug.Log($"보석 {i}: {gems[i].gemName} - 상태: {gems[i].currentCondition:F1}% - 파괴됨: {gems[i].isDestroyed}");
            }
            else
            {
                Debug.Log($"보석 {i}: gemObject가 null!");
            }
        }
        Debug.Log("=====================");
    }

    private void OnDrawGizmosSelected()
    {
        if (!showProtectionRadius) return;

        foreach (GemData gem in gems)
        {
            if (gem.gemObject != null)
            {
                Gizmos.color = gem.isProtected ? Color.blue : Color.red;
                Gizmos.DrawWireSphere(gem.gemObject.transform.position, gem.protectionRadius);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(gem.gemObject.transform.position + Vector3.up * 0.3f, gem.gemName);
#endif
            }
        }
    }
}