using UnityEngine;

public class ButtonManager : MonoBehaviour
{
    public static ButtonManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private GameObject currentMineral;
    public float rotationAmount = 10f;

    public void SetMineral(GameObject mineral)
    {
        currentMineral = mineral;
    }

    public void RotateLeft()
    {
        currentMineral?.transform.Rotate(Vector3.up, -rotationAmount);
    }

    public void RotateRight()
    {
        currentMineral?.transform.Rotate(Vector3.up, rotationAmount);
    }

    public void GameQuit()
    {
        Application.Quit();
        Debug.Log("게임 종료");
        // 추후에 메인 메뉴로 이동 기능 구현
    }

    public void GameRestart()
    {
        GameManager.Instance.RestartCurrentStage();
        Debug.Log("게임 재시작");
    }

    public void NextStage()
    {
        GameManager.Instance.ProceedToNextStage();
    }

    public GameObject GetCurrentMineral()
    {
        return currentMineral;
    }

}
