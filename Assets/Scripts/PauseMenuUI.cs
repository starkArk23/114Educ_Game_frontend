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
    private PauseSavePanelController savePanelController;

    private void Start()
    {
        GameSession.EnsureExists();
        savePanelController = PauseSavePanelController.Create(this, pauseMenuUI);
        SetPaused(false);
    }

    private void Update()
    {
        if (!allowEscToggle)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ESC should not close the save panel - only save/back buttons can
            if (savePanelController != null && savePanelController.IsOpen)
            {
                return;
            }

            TogglePause();
        }
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
        CloseSavePanel();
        SetPaused(false);
    }

    public void OpenSavePanel()
    {
        if (!isPaused)
            SetPaused(true);

        if (savePanelController == null)
            savePanelController = PauseSavePanelController.Create(this, pauseMenuUI);

        savePanelController?.ShowPanel();
    }

    public void CloseSavePanel()
    {
        savePanelController?.HidePanel();
    }

    public void GoToMainMenu()
    {
        CloseSavePanel();
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

        // Always close save panel - only opens on explicit "Save" button click
        CloseSavePanel();

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(paused);

        savePanelController?.SetRootActive(paused);

        if (dimOverlay != null)
            dimOverlay.SetActive(paused);

        Time.timeScale = paused ? 0f : 1f;

        if (paused)
            PlayerMovement.AddMovementLock("Pause");
        else
            PlayerMovement.RemoveMovementLock("Pause");
    }
}
