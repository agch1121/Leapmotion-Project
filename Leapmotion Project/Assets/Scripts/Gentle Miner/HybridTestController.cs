using UnityEngine;

/// <summary>
/// 키보드(끌) + 마우스(망치) 하이브리드 테스트 컨트롤러
/// 정확도 시스템 테스트용
/// </summary>
public class HybridTestController : MonoBehaviour
{
    [Header("테스트 모드 설정")]
    public bool enableHybridTest = true;
    public bool overrideLeapMotion = false; // 립모션보다 우선

    [Header("끌 조작 (키보드)")]
    public KeyCode chiselUpKey = KeyCode.W;
    public KeyCode chiselDownKey = KeyCode.S;
    public KeyCode chiselLeftKey = KeyCode.A;
    public KeyCode chiselRightKey = KeyCode.D;
    public KeyCode chiselForwardKey = KeyCode.Q;
    public KeyCode chiselBackKey = KeyCode.E;
    public float chiselMoveSpeed = 2f;

    [Header("망치 조작 (마우스)")]
    public bool useMouseForHammer = true;
    public float mouseHeightOffset = 1.2f; // 마우스 Y축 기본 높이
    public float mouseSensitivity = 2f; // 휠 민감도 증가
    public float hammerDistanceFromCamera = 1.5f; // 카메라로부터 거리 (더 가깝게)

    [Header("타격 설정")]
    public KeyCode strikeKey = KeyCode.Space;
    public KeyCode alternativeStrikeKey = KeyCode.LeftControl; // Ctrl로도 타격 가능

    [Header("시각적 도움")]
    public bool showMouseProjection = true;
    public GameObject mouseProjectionSphere; // 마우스 위치 시각화용

    // 내부 변수
    private Vector3 testChiselPosition = new Vector3(-0.5f, 1.2f, 0f);
    private Vector3 testHammerPosition = new Vector3(0.5f, 1.2f, 0f);
    private Camera mainCamera;
    private HandController handController;

    void Start()
    {
        InitializeHybridController();
    }

    void InitializeHybridController()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }

        handController = FindFirstObjectByType<HandController>();

        // 마우스 위치 시각화 구체 생성
        if (showMouseProjection && mouseProjectionSphere == null)
        {
            CreateMouseProjectionSphere();
        }

        Debug.Log("HybridTestController 초기화 완료");
        Debug.Log("조작법: W/A/S/D/Q/E(끌), 마우스(망치), Space/Ctrl(타격)");
    }

    void CreateMouseProjectionSphere()
    {
        mouseProjectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mouseProjectionSphere.name = "MouseProjection";
        mouseProjectionSphere.transform.localScale = Vector3.one * 0.08f;

        // 콜라이더 제거
        Destroy(mouseProjectionSphere.GetComponent<Collider>());

        // 반투명 노란색 재질
        Material projectionMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        projectionMat.color = new Color(1f, 1f, 0f, 0.7f);
        SetMaterialTransparent(projectionMat);
        mouseProjectionSphere.GetComponent<Renderer>().material = projectionMat;
    }

    void SetMaterialTransparent(Material material)
    {
        material.SetFloat("_Surface", 1); // Transparent
        material.SetFloat("_Blend", 0);   // Alpha
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    void Update()
    {
        if (!enableHybridTest) return;

        // 하이브리드 모드가 활성화되면 HandController를 오버라이드
        if (ShouldOverrideHandController())
        {
            UpdateChiselWithKeyboard();
            UpdateHammerWithMouse();
            UpdateMouseProjection();
            HandleStrikeInput();
            ApplyToHandController();
        }
    }

    bool ShouldOverrideHandController()
    {
        if (overrideLeapMotion) return true;

        // 립모션이 연결되지 않았거나 데이터가 없으면 하이브리드 모드 사용
        if (handController != null)
        {
            // HandController의 enableTestMode가 true이거나 립모션 데이터가 없으면 사용
            return handController.enableTestMode;
        }

        return true;
    }

    void UpdateChiselWithKeyboard()
    {
        Vector3 chiselMove = Vector3.zero;

        // 기본 이동 (XZ 평면)
        if (Input.GetKey(chiselUpKey)) chiselMove += Vector3.forward;
        if (Input.GetKey(chiselDownKey)) chiselMove += Vector3.back;
        if (Input.GetKey(chiselLeftKey)) chiselMove += Vector3.left;
        if (Input.GetKey(chiselRightKey)) chiselMove += Vector3.right;

        // Y축 이동 (높이)
        if (Input.GetKey(chiselForwardKey)) chiselMove += Vector3.up;
        if (Input.GetKey(chiselBackKey)) chiselMove += Vector3.down;

        // 이동 적용
        testChiselPosition += chiselMove * chiselMoveSpeed * Time.deltaTime;

        // 합리적인 범위로 제한
        testChiselPosition.x = Mathf.Clamp(testChiselPosition.x, -3f, 3f);
        testChiselPosition.y = Mathf.Clamp(testChiselPosition.y, 0.5f, 3f);
        testChiselPosition.z = Mathf.Clamp(testChiselPosition.z, -2f, 5f);
    }

    void UpdateHammerWithMouse()
    {
        if (!useMouseForHammer || mainCamera == null) return;

        // 마우스 스크린 좌표를 월드 좌표로 변환
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = hammerDistanceFromCamera; // 카메라로부터의 기본 거리

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        // 마우스 휠로 앞뒤(Z축) 조절
        float mouseWheelInput = Input.GetAxis("Mouse ScrollWheel");
        hammerDistanceFromCamera += mouseWheelInput * mouseSensitivity;
        hammerDistanceFromCamera = Mathf.Clamp(hammerDistanceFromCamera, 0.5f, 4f); // 0.5m ~ 4m 범위

        // 업데이트된 거리로 다시 계산
        mousePos.z = hammerDistanceFromCamera;
        worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        testHammerPosition = new Vector3(worldPos.x, mouseHeightOffset, worldPos.z);

        // 합리적인 범위로 제한
        testHammerPosition.x = Mathf.Clamp(testHammerPosition.x, -3f, 3f);
        testHammerPosition.z = Mathf.Clamp(testHammerPosition.z, -2f, 5f);
    }

    void UpdateMouseProjection()
    {
        if (mouseProjectionSphere != null && useMouseForHammer)
        {
            mouseProjectionSphere.transform.position = testHammerPosition;
            mouseProjectionSphere.SetActive(showMouseProjection);
        }
    }

    void HandleStrikeInput()
    {
        if (Input.GetKeyDown(strikeKey) || Input.GetKeyDown(alternativeStrikeKey))
        {
            // HandController에 타격 신호 전달
            if (handController != null)
            {
                // 타격 시뮬레이션을 위해 임시로 GrabStrength와 Velocity 설정
                SimulateHammerStrike();
            }
        }
    }

    void SimulateHammerStrike()
    {
        // HandController의 타격 감지를 위해 필요한 값들 시뮬레이션
        Debug.Log($"하이브리드 타격! 끌: {testChiselPosition:F2}, 망치: {testHammerPosition:F2}");

        // 거리 계산
        float distance = Vector3.Distance(testChiselPosition, testHammerPosition);
        Debug.Log($"끌-망치 거리: {distance:F3}m");

        // HandController의 OnHammerStrike 이벤트 직접 호출 (테스트용)
        if (handController != null && handController.OnHammerStrike != null)
        {
            Vector3 strikeDirection = (testChiselPosition - testHammerPosition).normalized;
            float simulatedForce = 0.7f; // 임시 타격 강도

            handController.OnHammerStrike.Invoke(testHammerPosition, testChiselPosition, simulatedForce);
        }
    }

    void ApplyToHandController()
    {
        if (handController == null) return;

        // HandController의 public 프로퍼티에 값 직접 설정은 불가능하므로
        // 내부 변수에 접근해야 함 (reflection 또는 public setter 필요)

        // 대신 HandController의 테스트 모드 변수들을 업데이트
        // (HandController.cs에서 testLeftHandPos, testRightHandPos를 public으로 만들어야 함)

        // 임시로 Transform을 통해 시각적 표현만 업데이트
        UpdateHandVisuals();
    }

    void UpdateHandVisuals()
    {
        // HandController의 leftHandVisual, rightHandVisual에 직접 접근
        if (handController != null)
        {
            if (handController.leftHandVisual != null)
            {
                handController.leftHandVisual.position = testChiselPosition;
            }

            if (handController.rightHandVisual != null)
            {
                handController.rightHandVisual.position = testHammerPosition;
            }
        }
    }

    /// <summary>
    /// 하이브리드 모드 활성화/비활성화
    /// </summary>
    public void SetHybridMode(bool enabled)
    {
        enableHybridTest = enabled;

        if (mouseProjectionSphere != null)
        {
            mouseProjectionSphere.SetActive(enabled && showMouseProjection);
        }

        Debug.Log($"하이브리드 테스트 모드: {(enabled ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 현재 끌-망치 거리 반환
    /// </summary>
    public float GetCurrentDistance()
    {
        return Vector3.Distance(testChiselPosition, testHammerPosition);
    }

    /// <summary>
    /// 완벽한 정확도로 위치 자동 조정
    /// </summary>
    [ContextMenu("완벽 정확도 테스트")]
    public void SetPerfectAccuracy()
    {
        testHammerPosition = testChiselPosition + Vector3.right * 0.03f; // 3cm 옆
        Debug.Log("완벽 정확도로 설정됨");
    }

    /// <summary>
    /// 좋은 정확도로 위치 자동 조정
    /// </summary>
    [ContextMenu("좋은 정확도 테스트")]
    public void SetGoodAccuracy()
    {
        testHammerPosition = testChiselPosition + Vector3.right * 0.08f; // 8cm 옆
        Debug.Log("좋은 정확도로 설정됨");
    }

    void OnGUI()
    {
        if (!enableHybridTest) return;

        // 화면에 조작 가이드 표시
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("=== 하이브리드 테스트 모드 ===");
        GUILayout.Label("끌 조작: W/A/S/D (이동), Q/E (높이)");
        GUILayout.Label("망치 조작: 마우스 (위치), 휠 (앞뒤)");
        GUILayout.Label("타격: Space 또는 Ctrl");
        GUILayout.Label($"현재 거리: {GetCurrentDistance():F3}m");
        GUILayout.Label($"끌 위치: {testChiselPosition:F2}");
        GUILayout.Label($"망치 위치: {testHammerPosition:F2}");
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        // 생성한 오브젝트 정리
        if (mouseProjectionSphere != null)
        {
            Destroy(mouseProjectionSphere);
        }
    }
}