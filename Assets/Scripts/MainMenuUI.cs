using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string gameSceneName = "GameScene";

    private void Awake()
    {
        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
    }

    public void Play()
{
    Time.timeScale = 1f;
    PlayerMovement.RemoveMovementLock("Pause");
    LoadingScreen.nextSceneName = gameSceneName;
    SceneManager.LoadScene(loadingSceneName);
}



    public void Quit()
    {
        Application.Quit();
    }
}
