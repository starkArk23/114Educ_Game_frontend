using UnityEngine;
using UnityEngine.SceneManagement;

public class EscToMainMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Time.timeScale = 1f; // safety in case you paused before
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}