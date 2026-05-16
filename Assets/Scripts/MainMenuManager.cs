using UnityEngine;
using UnityEngine.Audio;
using TMPro; // Required to control TextMeshPro text elements!

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject walkthroughPanel;

    [Header("Audio Configurations")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("Cyber Terminal Text System")]
    [SerializeField] private TextMeshProUGUI randomTextDisplay; // The text component on screen
    [SerializeField] private string[] randomPhrases;          // Your list of random sentences
    [SerializeField] private float changeInterval = 3f;        // Time in seconds (3 seconds)

    private float timer;

    private void Start()
    {
        // Automatically hide panels on game boot
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (walkthroughPanel != null) walkthroughPanel.SetActive(false);

        // Display the first random text immediately on boot
        ShowRandomText();
    }

    private void Update()
    {
        // A simple, reliable timer running every frame
        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            ShowRandomText();
            timer = 0f; // Reset the timer back to 0
        }
    }

    private void ShowRandomText()
    {
        // Safety check: Make sure we actually assigned a text object and have phrases to read
        if (randomTextDisplay != null && randomPhrases != null && randomPhrases.Length > 0)
        {
            // Pick a completely random index from our array list
            int randomIndex = Random.Range(0, randomPhrases.Length);

            // Inject that text into your UI display component
            randomTextDisplay.text = randomPhrases[randomIndex];
        }
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

    // SETTINGS BUTTON (Toggle Open/Close)
    public void OnSettingsButton()
    {
        if (settingsPanel == null) return;
        bool isOpen = settingsPanel.activeSelf;
        settingsPanel.SetActive(!isOpen);

        if (!isOpen && walkthroughPanel != null)
            walkthroughPanel.SetActive(false);
    }

    // CLOSE SETTINGS BUTTON
    public void OnCloseSettingsButton()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // WALKTHROUGH BUTTON (Toggle Open/Close)
    public void OnWalkthroughButton()
    {
        if (walkthroughPanel == null) return;
        bool isOpen = walkthroughPanel.activeSelf;
        walkthroughPanel.SetActive(!isOpen);

        if (!isOpen && settingsPanel != null)
            settingsPanel.SetActive(false);
    }
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        Debug.Log("Fullscreen mode set to: " + isFullscreen);
    }
    // CLOSE WALKTHROUGH BUTTON
    public void OnCloseWalkthroughButton()
    {
        if (walkthroughPanel != null)
            walkthroughPanel.SetActive(false);
    }

    // DYNAMIC AUDIO SLIDER TRIGGER
    public void SetMusicVolume(float volume)
    {
        if (mainMixer != null)
        {
            mainMixer.SetFloat("MusicVol", Mathf.Log10(volume) * 20);
        }
    }
}