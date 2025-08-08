using UnityEngine;
using System.Collections;

/// <summary>
/// 망치 타격 정확도 측정 및 피드백 시스템
/// 망치와 끌 타겟 사이의 거리 기반으로 정확도 계산
/// </summary>
public class AccuracySystem : MonoBehaviour
{
    [Header("정확도 기준 설정")]
    [Range(0.05f, 0.2f)]
    public float perfectDistance = 0.1f;
    [Range(0.1f, 0.3f)]
    public float goodDistance = 0.2f;
    [Range(0.2f, 0.5f)]
    public float allowedDistance = 0.3f;

    [Header("점수 설정")]
    public int perfectScore = 100;
    public int goodScore = 70;
    public int allowedScore = 40;
    public int failScore = 10;

    [Header("시각적 피드백")]
    public GameObject accuracyFeedbackPrefab;
    public Material perfectMaterial;
    public Material goodMaterial;
    public Material allowedMaterial;
    public Material failMaterial;

    [Header("오디오 피드백")]
    public AudioClip perfectHitSound;
    public AudioClip goodHitSound;
    public AudioClip allowedHitSound;
    public AudioClip failHitSound;

    [Header("디버그")]
    public bool enableDebugLogs = true;
    public bool showAccuracyGizmos = true;

    // 참조
    private HandController handController;
    private ToolSystem toolSystem;
    private ScoreSystem scoreSystem;
    private UIManager uiManager;
    private AudioSource audioSource;

    // 정확도 상태
    public enum AccuracyLevel
    {
        Perfect,
        Good,
        Allowed,
        Failed
    }

    public AccuracyLevel LastAccuracy { get; private set; }
    public float LastDistance { get; private set; }
    public int LastScore { get; private set; }
    public bool IsSystemEnabled { get; private set; } = true;

    public System.Action<AccuracyLevel, float, int> OnAccuracyMeasured;

    void Start()
    {
        InitializeAccuracySystem();
    }

    void InitializeAccuracySystem()
    {
        handController = FindFirstObjectByType<HandController>();
        toolSystem = FindFirstObjectByType<ToolSystem>();
        scoreSystem = FindFirstObjectByType<ScoreSystem>();
        uiManager = FindFirstObjectByType<UIManager>();

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (handController != null)
            handController.OnHammerStrike += OnHammerStrike;

        CreateDefaultMaterials();
        Debug.Log("AccuracySystem 초기화 완료");
    }

    void CreateDefaultMaterials()
    {
        if (perfectMaterial == null)
        {
            perfectMaterial = new Material(Shader.Find("Standard"));
            perfectMaterial.color = Color.green;
            perfectMaterial.SetFloat("_Metallic", 0.5f);
        }
        if (goodMaterial == null)
        {
            goodMaterial = new Material(Shader.Find("Standard"));
            goodMaterial.color = Color.yellow;
            goodMaterial.SetFloat("_Metallic", 0.5f);
        }
        if (allowedMaterial == null)
        {
            allowedMaterial = new Material(Shader.Find("Standard"));
            allowedMaterial.color = new Color(1f, 0.5f, 0f); // [변경] 주황색
            allowedMaterial.SetFloat("_Metallic", 0.5f);
        }
        if (failMaterial == null)
        {
            failMaterial = new Material(Shader.Find("Standard"));
            failMaterial.color = Color.red;
            failMaterial.SetFloat("_Metallic", 0.5f);
        }
    }

    void OnHammerStrike(Vector3 hammerPosition, Vector3 chiselTarget, float strikeForce)
    {
        if (!IsSystemEnabled) return;
        MeasureAccuracy(hammerPosition, chiselTarget);
    }

    public void MeasureAccuracy(Vector3 hammerPosition, Vector3 chiselTarget)
    {
        Vector3 hammerPos2D = new Vector3(hammerPosition.x, 0, hammerPosition.z);
        Vector3 targetPos2D = new Vector3(chiselTarget.x, 0, chiselTarget.z);
        float distance = Vector3.Distance(hammerPos2D, targetPos2D);

        LastDistance = distance;
        AccuracyLevel accuracy = CalculateAccuracyLevel(distance);
        LastAccuracy = accuracy;
        int score = CalculateAccuracyScore(accuracy);
        LastScore = score;

        CreateAccuracyFeedback(hammerPosition, accuracy);
        PlayAccuracySound(accuracy);

        scoreSystem?.AddScore(score);
        OnAccuracyMeasured?.Invoke(accuracy, distance, score);

        if (enableDebugLogs)
            LogAccuracyResult(distance, accuracy, score);
    }

    AccuracyLevel CalculateAccuracyLevel(float distance)
    {
        if (distance <= perfectDistance) return AccuracyLevel.Perfect;
        else if (distance <= goodDistance) return AccuracyLevel.Good;
        else if (distance <= allowedDistance) return AccuracyLevel.Allowed;
        else return AccuracyLevel.Failed;
    }

    int CalculateAccuracyScore(AccuracyLevel accuracy)
    {
        return accuracy switch
        {
            AccuracyLevel.Perfect => perfectScore,
            AccuracyLevel.Good => goodScore,
            AccuracyLevel.Allowed => allowedScore,
            AccuracyLevel.Failed => failScore,
            _ => 0
        };
    }

    void CreateAccuracyFeedback(Vector3 position, AccuracyLevel accuracy)
    {
        GameObject feedback = accuracyFeedbackPrefab != null
            ? Instantiate(accuracyFeedbackPrefab, position, Quaternion.identity)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        feedback.name = $"AccuracyFeedback_{accuracy}";
        feedback.transform.localScale = Vector3.one * 0.1f;
        Destroy(feedback.GetComponent<Collider>());

        Renderer renderer = feedback.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = GetMaterialForAccuracy(accuracy);

        StartCoroutine(AnimateAccuracyFeedback(feedback, accuracy));
    }

    Material GetMaterialForAccuracy(AccuracyLevel accuracy)
    {
        return accuracy switch
        {
            AccuracyLevel.Perfect => perfectMaterial,
            AccuracyLevel.Good => goodMaterial,
            AccuracyLevel.Allowed => allowedMaterial,
            AccuracyLevel.Failed => failMaterial,
            _ => failMaterial
        };
    }

    IEnumerator AnimateAccuracyFeedback(GameObject feedback, AccuracyLevel accuracy)
    {
        if (feedback == null) yield break;
        float animationTime = 1.5f;
        Vector3 startScale = feedback.transform.localScale;
        Vector3 maxScale = startScale * 1.5f;
        Vector3 startPos = feedback.transform.position;
        float elapsed = 0f;

        while (elapsed < animationTime * 0.3f)
        {
            float t = elapsed / (animationTime * 0.3f);
            feedback.transform.localScale = Vector3.Lerp(startScale, maxScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < animationTime * 0.7f)
        {
            float t = elapsed / (animationTime * 0.7f);
            feedback.transform.localScale = Vector3.Lerp(maxScale, startScale * 0.5f, t);
            feedback.transform.position = Vector3.Lerp(startPos, startPos + Vector3.up * 0.3f, t);

            Renderer renderer = feedback.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = renderer.material.color;
                color.a = Mathf.Lerp(1f, 0f, t);
                renderer.material.color = color;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(feedback);
    }

    void PlayAccuracySound(AccuracyLevel accuracy)
    {
        if (audioSource == null) return;
        AudioClip clipToPlay = accuracy switch
        {
            AccuracyLevel.Perfect => perfectHitSound,
            AccuracyLevel.Good => goodHitSound,
            AccuracyLevel.Allowed => allowedHitSound,
            AccuracyLevel.Failed => failHitSound,
            _ => null
        };
        if (clipToPlay != null)
            audioSource.PlayOneShot(clipToPlay);
    }

    void LogAccuracyResult(float distance, AccuracyLevel accuracy, int score)
    {
        string accuracyText = accuracy switch
        {
            AccuracyLevel.Perfect => "완벽",
            AccuracyLevel.Good => "좋음",
            AccuracyLevel.Allowed => "허용",
            AccuracyLevel.Failed => "실패",
            _ => "알 수 없음"
        };
        Debug.Log($"타격 정확도: {accuracyText} | 거리: {distance:F3}m | 점수: +{score}");
    }

    public void SetSystemEnabled(bool enabled)
    {
        IsSystemEnabled = enabled;
        if (enableDebugLogs)
            Debug.Log($"정확도 시스템: {(enabled ? "활성화" : "비활성화")}");
    }

    public AccuracyLevel GetCurrentPotentialAccuracy(Vector3 hammerPosition, Vector3 chiselTarget)
    {
        if (!IsSystemEnabled) return AccuracyLevel.Failed;
        Vector3 hammerPos2D = new Vector3(hammerPosition.x, 0, hammerPosition.z);
        Vector3 targetPos2D = new Vector3(chiselTarget.x, 0, chiselTarget.z);
        float distance = Vector3.Distance(hammerPos2D, targetPos2D);
        return CalculateAccuracyLevel(distance);
    }

    public (float perfect, float good, float allowed) GetAccuracyDistances()
    {
        return (perfectDistance, goodDistance, allowedDistance);
    }

    void OnDrawGizmosSelected()
    {
        if (!showAccuracyGizmos || !Application.isPlaying) return;
        if (handController == null) return;

        Vector3 chiselTarget = handController.GetChiselTargetPoint();
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(chiselTarget, perfectDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(chiselTarget, goodDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(chiselTarget, allowedDistance);
    }

    void OnDestroy()
    {
        if (handController != null)
            handController.OnHammerStrike -= OnHammerStrike;
    }
}
