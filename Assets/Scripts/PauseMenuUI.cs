using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    private const string FallbackCanvasName = "PauseMenuCanvas";
    private const string FallbackOverlayName = "PauseDimOverlay";
    private const string FallbackPanelName = "PauseMenuUI";

    [Header("UI")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private GameObject dimOverlay;
    [SerializeField] private bool allowEscToggle = true;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused;
    private PauseSavePanelController savePanelController;

    private void Awake()
    {
        EnsureUiReferences();
    }

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

    private void EnsureUiReferences()
    {
        if (pauseMenuUI == null)
            pauseMenuUI = FindInactiveObject(FallbackPanelName);

        if (dimOverlay == null)
            dimOverlay = FindInactiveObject(FallbackOverlayName);

        if (pauseMenuUI == null)
            BuildFallbackPauseMenuUi();

        if (dimOverlay == null && pauseMenuUI != null)
        {
            Transform overlayTransform = pauseMenuUI.transform.parent != null
                ? pauseMenuUI.transform.parent.Find(FallbackOverlayName)
                : null;
            if (overlayTransform != null)
                dimOverlay = overlayTransform.gameObject;
        }

        EnsureEventSystem();
    }

    private void BuildFallbackPauseMenuUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(FallbackCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (dimOverlay == null)
            dimOverlay = CreateOverlay(canvas.transform);

        if (pauseMenuUI == null)
            pauseMenuUI = CreatePausePanel(canvas.transform);
    }

    private GameObject CreateOverlay(Transform parent)
    {
        GameObject overlay = new GameObject(FallbackOverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);

        RectTransform rectTransform = overlay.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = overlay.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);

        overlay.SetActive(false);
        return overlay;
    }

    private GameObject CreatePausePanel(Transform parent)
    {
        GameObject panel = new GameObject(FallbackPanelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(560f, 520f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.12f, 0.15f, 0.2f, 0.96f);

        VerticalLayoutGroup layoutGroup = panel.GetComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.spacing = 18f;
        layoutGroup.padding = new RectOffset(40, 40, 40, 40);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateLabel(panel.transform, "PauseTitle", "Paused", 44f);
        CreateButton(panel.transform, "ResumeButton", "Resume", Resume);
        CreateButton(panel.transform, "MainMenuButton", "Main Menu", GoToMainMenu);
        CreateButton(panel.transform, "QuitButton", "Quit", QuitGame);

        panel.SetActive(false);
        return panel;
    }

    private void CreateButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(0f, 72f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.89f, 0.74f, 0.27f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 72f;
        layoutElement.minHeight = 72f;

        CreateLabel(buttonObject.transform, $"{objectName}Label", label, 30f);
    }

    private TMP_Text CreateLabel(Transform parent, string objectName, string text, float fontSize)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        return label;
    }

    private GameObject FindInactiveObject(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
                continue;

            if (candidate.hideFlags != HideFlags.None)
                continue;

            if (candidate.gameObject.scene.IsValid())
                return candidate.gameObject;
        }

        return null;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
