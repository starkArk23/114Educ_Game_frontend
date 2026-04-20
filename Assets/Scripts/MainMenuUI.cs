using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string gameSceneName = "GameScene";

    public void Play()
{
    LoadingScreen.nextSceneName = gameSceneName;
    SceneManager.LoadScene(loadingSceneName);
}



    public void Quit()
    {
        Application.Quit();
    }
}
