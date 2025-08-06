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
        // 현재 씬을 다시 로드하여 게임을 재시작
        // UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        // 현재 스테이지를 다시 시작하는 로직 구현

        Debug.Log("게임 재시작");
    }

    public void NextStage()
    {

    }

    public GameObject GetCurrentMineral()
    {
        return currentMineral;
    }

}
