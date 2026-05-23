using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private const string LoadPanelName = "LoadSlotPanel";
    private const string LoadStatusName = "LoadStatusText";

    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string gameSceneName = "DreamScene";
    [SerializeField] private TMP_InputField operatorNameInput;
    [SerializeField] private Button loadButton;
    [SerializeField] private GameObject loadPanelRoot;
    [SerializeField] private TMP_Text loadStatusText;
    [SerializeField] private Button closeLoadPanelButton;
    [SerializeField] private Button[] loadSlotButtons = new Button[5];

    private readonly List<GameSession.SaveSlotInfo> cachedSaveSlots = new List<GameSession.SaveSlotInfo>();
    private bool loadUiInitialized;

    private void Awake()
    {
        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
        EnsureLoadUi();
        CloseLoadPanel();
        PreFillOperatorNameFromPrefs();
    }

    private void PreFillOperatorNameFromPrefs()
    {
        if (operatorNameInput == null)
            return;
        if (!string.IsNullOrWhiteSpace(operatorNameInput.text))
            return;
        string saved = PlayerPrefs.GetString("SavedOperatorName", string.Empty);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            operatorNameInput.text = saved;
            LoadingScreen.operatorName = saved;
        }
    }

    public void Play()
    {
        SetOperatorNameFromInput();
        GameSession.EnsureExists();
        GameSession.Instance.PrepareNewGame();

        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
        LoadingScreen.skipNameEntry = !string.IsNullOrWhiteSpace(LoadingScreen.operatorName);
        LoadingScreen.nextSceneName = gameSceneName;
        SceneManager.LoadScene(loadingSceneName);
    }

    public void RefreshContinueSlots()
    {
        StartCoroutine(RefreshContinueSlotsRoutine(true));
    }

    public void OpenLoadPanel()
    {
        EnsureLoadUi();

        if (loadPanelRoot == null)
        {
            Debug.LogWarning("[MainMenuUI] Unable to open the load panel because no UI root is available.");
            return;
        }

        loadPanelRoot.SetActive(true);
        UpdateLoadSlotLabels();
        StartCoroutine(RefreshContinueSlotsRoutine(true));
    }

    public void CloseLoadPanel()
    {
        if (loadPanelRoot != null)
            loadPanelRoot.SetActive(false);
    }

    public void ContinueFromSlotNumber(int slotNumber)
    {
        StartCoroutine(ContinueFromSlotNumberRoutine(slotNumber));
    }

    private IEnumerator RefreshContinueSlotsRoutine(bool updateUi)
    {
        SetOperatorNameFromInput();
        GameSession.EnsureExists();

        if (updateUi)
        {
            SetLoadButtonsInteractable(false);
            SetLoadStatus("Loading save slots...");
        }

        List<GameSession.SaveSlotInfo> slots = null;
        string error = null;

        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.ListSaveSlots((result, requestError) =>
        {
            slots = result;
            error = requestError;
        }));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning($"[MainMenuUI] Unable to list save slots: {error}");

            cachedSaveSlots.Clear();
            UpdateLoadSlotLabels();
            if (updateUi)
            {
                SetLoadButtonsInteractable(true);
                SetLoadStatus(error);
            }

            yield break;
        }

        cachedSaveSlots.Clear();
        if (slots != null)
            cachedSaveSlots.AddRange(slots);

        UpdateLoadSlotLabels();

        if (updateUi)
        {
            SetLoadButtonsInteractable(true);
            SetLoadStatus(cachedSaveSlots.Count > 0 ? "Choose a save slot to load." : "No save data found.");
        }
    }

    private IEnumerator ContinueFromSlotNumberRoutine(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > 5)
        {
            SetLoadStatus("Choose a valid save slot.");
            yield break;
        }

        SetOperatorNameFromInput();
        SetLoadButtonsInteractable(false);
        SetLoadStatus($"Loading Slot {slotNumber}...");

        if (cachedSaveSlots.Count == 0)
            yield return StartCoroutine(RefreshContinueSlotsRoutine(true));

        GameSession.SaveSlotInfo slot = null;
        for (int index = 0; index < cachedSaveSlots.Count; index++)
        {
            GameSession.SaveSlotInfo candidate = cachedSaveSlots[index];
            if (candidate != null && candidate.slotNumber == slotNumber)
            {
                slot = candidate;
                break;
            }
        }

        if (slot == null || string.IsNullOrWhiteSpace(slot.id))
        {
            Debug.LogWarning($"[MainMenuUI] Slot {slotNumber} is empty.");
            SetLoadButtonsInteractable(true);
            SetLoadStatus($"Slot {slotNumber} is empty.");
            yield break;
        }

        GameSession.SaveSlotDetail loadedSlot = null;
        string loadError = null;

        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.LoadSaveSlot(slot.id, (result, error) =>
        {
            loadedSlot = result;
            loadError = error;
        }));

        if (!string.IsNullOrEmpty(loadError))
        {
            Debug.LogWarning($"[MainMenuUI] Unable to continue from slot {slotNumber}: {loadError}");
            SetLoadButtonsInteractable(true);
            SetLoadStatus(loadError);
            yield break;
        }

        string targetScene = loadedSlot != null && !string.IsNullOrWhiteSpace(loadedSlot.currentScene)
            ? loadedSlot.currentScene
            : gameSceneName;

        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
        SceneManager.LoadScene(targetScene);
    }

    private void SetOperatorNameFromInput()
    {
        if (operatorNameInput == null)
            return;

        string candidate = operatorNameInput.text != null ? operatorNameInput.text.Trim() : string.Empty;
        if (!string.IsNullOrWhiteSpace(candidate))
            LoadingScreen.operatorName = candidate;
    }

    private void EnsureLoadUi()
    {
        if (loadUiInitialized)
            return;

        loadButton = loadButton != null ? loadButton : FindButtonByName("LoadButton");
        loadPanelRoot = loadPanelRoot != null ? loadPanelRoot : FindChildByName(LoadPanelName);
        loadStatusText = loadStatusText != null ? loadStatusText : FindTextByName(LoadStatusName);
        closeLoadPanelButton = closeLoadPanelButton != null ? closeLoadPanelButton : FindButtonByName("CloseLoadPanelButton");

        AssignExistingLoadSlotButtons();

        if (loadPanelRoot == null)
            CreateLoadPanel();

        WireLoadUi();
        UpdateLoadSlotLabels();
        loadUiInitialized = true;
    }

    private void AssignExistingLoadSlotButtons()
    {
        for (int index = 0; index < loadSlotButtons.Length; index++)
        {
            if (loadSlotButtons[index] != null)
                continue;

            loadSlotButtons[index] = FindButtonByName($"LoadSlotButton{index + 1}");
        }
    }

    private void WireLoadUi()
    {
        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(OpenLoadPanel);
            loadButton.onClick.AddListener(OpenLoadPanel);
        }

        if (closeLoadPanelButton != null)
        {
            closeLoadPanelButton.onClick.RemoveListener(CloseLoadPanel);
            closeLoadPanelButton.onClick.AddListener(CloseLoadPanel);
        }

        for (int index = 0; index < loadSlotButtons.Length; index++)
        {
            Button button = loadSlotButtons[index];
            if (button == null)
                continue;

            int slotNumber = index + 1;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ContinueFromSlotNumber(slotNumber));
        }
    }

    private void CreateLoadPanel()
    {
        // Find the MainMenuUI child panel (the Image panel containing the menu buttons).
        // The load panel is parented to it so it shares the same visual background,
        // matching how the pause menu's SaveSlotPanel works.
        Transform menuPanel = transform.Find("MainMenuUI");
        Transform panelParent = menuPanel != null ? menuPanel : transform;

        Button templateButton = panelParent.GetComponentInChildren<Button>(true);
        TMP_Text titleTemplate = panelParent.GetComponentInChildren<TMP_Text>(true);

        // Plain RectTransform only — no Image overlay — same as SaveSlotPanel.
        loadPanelRoot = new GameObject(LoadPanelName, typeof(RectTransform));
        loadPanelRoot.transform.SetParent(panelParent, false);

        RectTransform panelRect = loadPanelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        TMP_Text panelTitle = CreateTitle(titleTemplate, loadPanelRoot.transform, "Load Save", -60f, 42f);
        panelTitle.alignment = TextAlignmentOptions.Center;
        loadStatusText = CreateTitle(titleTemplate, loadPanelRoot.transform, "Choose a save slot to load.", -120f, 26f);
        loadStatusText.alignment = TextAlignmentOptions.Center;
        loadStatusText.enableWordWrapping = false;

        for (int index = 0; index < loadSlotButtons.Length; index++)
        {
            int slotNumber = index + 1;
            Button slotButton;

            if (templateButton != null)
            {
                slotButton = Instantiate(templateButton, loadPanelRoot.transform);
                slotButton.name = $"LoadSlotButton{slotNumber}";
                slotButton.onClick = new Button.ButtonClickedEvent();

                RectTransform slotRect = slotButton.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 1f);
                slotRect.anchorMax = new Vector2(0.5f, 1f);
                slotRect.pivot = new Vector2(0.5f, 1f);
                slotRect.anchoredPosition = new Vector2(0f, -180f - (index * 96f));

                LayoutElement slotLayout = slotButton.GetComponent<LayoutElement>();
                if (slotLayout == null)
                    slotLayout = slotButton.gameObject.AddComponent<LayoutElement>();
                slotLayout.preferredWidth = 600f;

                SetButtonLabel(slotButton, BuildEmptySlotLabel(slotNumber));
            }
            else
            {
                slotButton = CreateButton(loadPanelRoot.transform, $"LoadSlotButton{slotNumber}", BuildEmptySlotLabel(slotNumber), new Vector2(0.5f, 1f), new Vector2(0f, -180f - (index * 96f)));
            }

            loadSlotButtons[index] = slotButton;
        }

        if (templateButton != null)
        {
            closeLoadPanelButton = Instantiate(templateButton, loadPanelRoot.transform);
            closeLoadPanelButton.name = "CloseLoadPanelButton";
            closeLoadPanelButton.onClick = new Button.ButtonClickedEvent();

            RectTransform backRect = closeLoadPanelButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.5f, 1f);
            backRect.anchorMax = new Vector2(0.5f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.anchoredPosition = new Vector2(0f, -660f);

            LayoutElement backLayout = closeLoadPanelButton.GetComponent<LayoutElement>();
            if (backLayout == null)
                backLayout = closeLoadPanelButton.gameObject.AddComponent<LayoutElement>();
            backLayout.preferredWidth = 600f;

            SetButtonLabel(closeLoadPanelButton, "Back");
        }
        else
        {
            closeLoadPanelButton = CreateButton(loadPanelRoot.transform, "CloseLoadPanelButton", "Back", new Vector2(0.5f, 1f), new Vector2(0f, -660f));
        }
    }

    private TMP_Text CreateTitle(TMP_Text template, Transform parent, string text, float anchoredY, float fontSize)
    {
        TMP_Text label;
        if (template != null)
        {
            label = Instantiate(template, parent);
            label.name = text.Replace(" ", string.Empty) + "Text";
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, anchoredY);
        }
        else
        {
            GameObject textObject = new GameObject(text.Replace(" ", string.Empty) + "Text", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            label = textObject.AddComponent<TextMeshProUGUI>();
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(900f, 60f);
            rect.anchoredPosition = new Vector2(0f, anchoredY);
        }

        label.fontSize = fontSize;
        label.text = text;
        return label;
    }

    private void UpdateLoadSlotLabels()
    {
        for (int index = 0; index < loadSlotButtons.Length; index++)
        {
            Button button = loadSlotButtons[index];
            if (button == null)
                continue;

            int slotNumber = index + 1;
            GameSession.SaveSlotInfo slot = FindCachedSlot(slotNumber);
            SetButtonLabel(button, slot == null ? BuildEmptySlotLabel(slotNumber) : BuildOccupiedSlotLabel(slot));
        }
    }

    private GameSession.SaveSlotInfo FindCachedSlot(int slotNumber)
    {
        for (int index = 0; index < cachedSaveSlots.Count; index++)
        {
            GameSession.SaveSlotInfo slot = cachedSaveSlots[index];
            if (slot != null && slot.slotNumber == slotNumber)
                return slot;
        }

        return null;
    }

    private void SetLoadButtonsInteractable(bool interactable)
    {
        for (int index = 0; index < loadSlotButtons.Length; index++)
        {
            if (loadSlotButtons[index] != null)
                loadSlotButtons[index].interactable = interactable;
        }

        if (closeLoadPanelButton != null)
            closeLoadPanelButton.interactable = interactable;
    }

    private void SetLoadStatus(string message)
    {
        if (loadStatusText != null)
            loadStatusText.text = message;
    }

    private string BuildEmptySlotLabel(int slotNumber)
    {
        return $"Slot {slotNumber} - Empty";
    }

    private string BuildOccupiedSlotLabel(GameSession.SaveSlotInfo slot)
    {
        string slotName = string.IsNullOrWhiteSpace(slot.slotName) ? "Unnamed Save" : slot.slotName.Trim();
        string sceneName = string.IsNullOrWhiteSpace(slot.currentScene) ? gameSceneName : slot.currentScene.Trim();
        return $"Slot {slot.slotNumber} - {slotName}\nScene: {sceneName}";
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 anchor, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(720f, 72f);
        rectTransform.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.89f, 0.74f, 0.27f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text buttonText = CreateText(buttonObject.transform, $"{objectName}Label", label, 24f, new Vector2(0.5f, 0.5f), Vector2.zero);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.enableWordWrapping = true;

        return button;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, Vector2 anchor, Vector2 anchoredPosition)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(900f, 60f);
        rectTransform.anchoredPosition = anchoredPosition;

        TextMeshProUGUI textLabel = textObject.GetComponent<TextMeshProUGUI>();
        textLabel.text = text;
        textLabel.fontSize = fontSize;
        textLabel.alignment = TextAlignmentOptions.Center;
        textLabel.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            textLabel.font = TMP_Settings.defaultFontAsset;

        return textLabel;
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
    }

    private Button FindButtonByName(string objectName)
    {
        Transform match = FindTransformByName(objectName);
        return match != null ? match.GetComponent<Button>() : null;
    }

    private TMP_Text FindTextByName(string objectName)
    {
        Transform match = FindTransformByName(objectName);
        return match != null ? match.GetComponent<TMP_Text>() : null;
    }

    private GameObject FindChildByName(string objectName)
    {
        Transform match = FindTransformByName(objectName);
        return match != null ? match.gameObject : null;
    }

    private Transform FindTransformByName(string objectName)
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

            if (!candidate.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }



    public void Quit()
    {
        Application.Quit();
    }
}
