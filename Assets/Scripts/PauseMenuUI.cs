using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private GameObject dimOverlay;
    [SerializeField] private bool allowEscToggle = true;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused;

    private void Start()
    {
        SetPaused(false);
    }

    private void Update()
    {
        if (!allowEscToggle)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void GoToMainMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void Pause()
    {
        SetPaused(true);
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(paused);

        if (dimOverlay != null)
            dimOverlay.SetActive(paused);

        Time.timeScale = paused ? 0f : 1f;

        if (paused)
            PlayerMovement.AddMovementLock("Pause");
        else
            PlayerMovement.RemoveMovementLock("Pause");
    }
}
