using UnityEngine;
using UnityEngine.UI;

public class ForceUI : MonoBehaviour
{
    [Header("UI 요소")]
    public Slider forceSlider;
    public Image fillImage;
    public Text forceText;

    [Header("색상 설정")]
    public Color lowForceColor = Color.green;
    public Color mediumForceColor = Color.yellow;
    public Color highForceColor = Color.red;

    private ForceDetector forceDetector;

    void Start()
    {
        forceDetector = FindObjectOfType<ForceDetector>();

        if (forceSlider != null)
        {
            forceSlider.minValue = 0f;
            forceSlider.maxValue = 1f;
        }
    }

    void Update()
    {
        if (forceDetector == null) return;

        float currentForce = forceDetector.GetCurrentForce();
        UpdateUI(currentForce);
    }

    void UpdateUI(float force)
    {
        // 슬라이더 업데이트
        if (forceSlider != null)
        {
            forceSlider.value = force;
        }

        // 텍스트 업데이트 (구간: 55%, 85%)
        if (forceText != null)
        {
            string forceLevel = "";
            if (force < 0.55f) forceLevel = " (약함)";
            else if (force < 0.85f) forceLevel = " (보통)";
            else forceLevel = " (강함)";

            forceText.text = $"최종 힘: {(force * 100):F0}%{forceLevel}";
        }

        // 색상 업데이트
        if (fillImage != null)
        {
            if (force < 0.55f)
                fillImage.color = lowForceColor;
            else if (force < 0.85f)
                fillImage.color = mediumForceColor;
            else
                fillImage.color = highForceColor;
        }
    }
}