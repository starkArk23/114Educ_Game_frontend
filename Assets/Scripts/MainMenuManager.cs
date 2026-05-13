using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject walkthroughPanel;

    private void Start()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (walkthroughPanel != null) walkthroughPanel.SetActive(false);
    }

    // EXIT BUTTON
    public void OnExitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // SETTINGS BUTTON
    public void OnSettingsButton()
    {
        if (settingsPanel == null) return;
        bool isOpen = settingsPanel.activeSelf;
        settingsPanel.SetActive(!isOpen);
        if (!isOpen && walkthroughPanel != null)
            walkthroughPanel.SetActive(false);
    }

    public void OnCloseSettingsButton()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // WALKTHROUGH BUTTON
    public void OnWalkthroughButton()
    {
        if (walkthroughPanel == null) return;
        bool isOpen = walkthroughPanel.activeSelf;
        walkthroughPanel.SetActive(!isOpen);
        if (!isOpen && settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OnCloseWalkthroughButton()
    {
        if (walkthroughPanel != null)
            walkthroughPanel.SetActive(false);
    }
}