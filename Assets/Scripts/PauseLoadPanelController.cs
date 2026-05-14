using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseLoadPanelController
{
    private const string LoadPanelName = "LoadSlotPanel";
    private const string SavePanelName = "SaveSlotPanel";

    private readonly PauseMenu pauseMenu;
    private readonly GameObject pauseMenuRoot;
    private readonly RectTransform pauseMenuRect;
    private readonly Dictionary<int, GameSession.SaveSlotInfo> slotsByNumber = new Dictionary<int, GameSession.SaveSlotInfo>();

    private Button loadButton;
    private GameObject panelRoot;
    private TMP_Text statusText;
    private readonly Button[] slotButtons = new Button[5];

    private bool hasLoadedSlots;
    private bool isRefreshingSlots;

    public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;

    private PauseLoadPanelController(PauseMenu pauseMenu, GameObject pauseMenuRoot)
    {
        this.pauseMenu = pauseMenu;
        this.pauseMenuRoot = pauseMenuRoot;
        pauseMenuRect = pauseMenuRoot != null ? pauseMenuRoot.GetComponent<RectTransform>() : null;
    }

    public static PauseLoadPanelController Create(PauseMenu pauseMenu, GameObject pauseMenuRoot)
    {
        PauseLoadPanelController controller = new PauseLoadPanelController(pauseMenu, pauseMenuRoot);
        controller.Initialize();
        return controller;
    }

    public void ShowPanel()
    {
        if (panelRoot == null)
            return;

        SetMainMenuVisible(false);
        panelRoot.SetActive(true);
        hasLoadedSlots = false;
        isRefreshingSlots = true;
        SetStatus("Loading save slots...");
        SetButtonsInteractable(false);
        UpdateSlotLabels();
        pauseMenu.StartCoroutine(RefreshSlots());
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        SetMainMenuVisible(true);
    }

    public void SetRootActive(bool active)
    {
        if (!active)
            HidePanel();
    }

    private void Initialize()
    {
        if (pauseMenuRoot == null || pauseMenuRect == null)
        {
            Debug.LogWarning("PauseLoadPanelController could not initialize because the pause menu root is missing a RectTransform.");
            return;
        }

        GameSession.EnsureExists();

        GameObject stalePanelRoot = FindDirectChild(LoadPanelName);
        if (stalePanelRoot != null)
            stalePanelRoot.SetActive(false);

        if (stalePanelRoot != null)
            UnityEngine.Object.Destroy(stalePanelRoot);

        Button templateButton = FindButton("ResumeButton") ?? UnityEngine.Object.FindFirstObjectByType<Button>();
        if (templateButton == null)
            return;

        TMP_Text titleTemplate = pauseMenuRoot.GetComponentInChildren<TMP_Text>(true);

        CreateLoadButton(templateButton);
        CreatePanel(templateButton, titleTemplate);
        HidePanel();
    }

    private void CreateLoadButton(Button templateButton)
    {
        loadButton = FindButton("LoadButton");
        bool createdLoadButton = false;

        if (loadButton == null)
        {
            loadButton = UnityEngine.Object.Instantiate(templateButton, templateButton.transform.parent);
            loadButton.name = "LoadButton";
            createdLoadButton = true;
        }

        SetButtonLabel(loadButton, "Load Game");
        loadButton.onClick = new Button.ButtonClickedEvent();
        loadButton.onClick.AddListener(pauseMenu.OpenLoadPanel);

        if (createdLoadButton)
        {
            RectTransform loadRect = loadButton.GetComponent<RectTransform>();
            RectTransform resumeRect = templateButton.GetComponent<RectTransform>();
            loadRect.anchoredPosition = new Vector2(resumeRect.anchoredPosition.x, -390f);

            LayoutElement loadLayout = loadButton.GetComponent<LayoutElement>();
            if (loadLayout == null)
                loadLayout = loadButton.gameObject.AddComponent<LayoutElement>();

            loadLayout.preferredWidth = Mathf.Max(resumeRect.rect.width, 400f);
            loadRect.SetSiblingIndex(Mathf.Max(0, pauseMenuRect.childCount - 1));
        }
    }

    private void CreatePanel(Button templateButton, TMP_Text titleTemplate)
    {
        panelRoot = new GameObject(LoadPanelName, typeof(RectTransform));
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRoot.transform.SetParent(pauseMenuRoot.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRoot.SetActive(false);

        TMP_Text panelTitle = CreateTitle(titleTemplate, panelRoot.transform, "Load Progress", -60f, 58f);
        panelTitle.alignment = TextAlignmentOptions.Center;
        statusText = CreateTitle(titleTemplate, panelRoot.transform, string.Empty, -120f, 28f);
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.enableWordWrapping = false;

        for (int slotIndex = 0; slotIndex < slotButtons.Length; slotIndex++)
        {
            int slotNumber = slotIndex + 1;
            Button slotButton = UnityEngine.Object.Instantiate(templateButton, panelRoot.transform);
            slotButton.name = $"LoadSlotButton{slotNumber}";
            slotButton.onClick = new Button.ButtonClickedEvent();
            slotButton.onClick.AddListener(() => OnSlotPressed(slotNumber));

            RectTransform slotRect = slotButton.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 1f);
            slotRect.anchorMax = new Vector2(0.5f, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.anchoredPosition = new Vector2(0f, -180f - (slotIndex * 96f));

            LayoutElement slotLayout = slotButton.GetComponent<LayoutElement>();
            if (slotLayout == null)
                slotLayout = slotButton.gameObject.AddComponent<LayoutElement>();
            slotLayout.preferredWidth = 600f;

            SetButtonLabel(slotButton, BuildEmptyLabel(slotNumber));
            slotButtons[slotIndex] = slotButton;
        }

        Button backButton = UnityEngine.Object.Instantiate(templateButton, panelRoot.transform);
        backButton.name = "LoadPanelBackButton";
        backButton.onClick = new Button.ButtonClickedEvent();
        backButton.onClick.AddListener(pauseMenu.CloseLoadPanel);
        SetButtonLabel(backButton, "Back");

        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 1f);
        backRect.anchorMax = new Vector2(0.5f, 1f);
        backRect.pivot = new Vector2(0.5f, 1f);
        backRect.anchoredPosition = new Vector2(0f, -660f);

        LayoutElement backLayout = backButton.GetComponent<LayoutElement>();
        if (backLayout == null)
            backLayout = backButton.gameObject.AddComponent<LayoutElement>();
        backLayout.preferredWidth = 600f;
    }

    private IEnumerator RefreshSlots()
    {
        isRefreshingSlots = true;

        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.ListSaveSlots((slots, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                hasLoadedSlots = false;
                slotsByNumber.Clear();
                UpdateSlotLabels();
                SetStatus(error);
                return;
            }

            slotsByNumber.Clear();
            for (int index = 0; index < slots.Count; index++)
            {
                GameSession.SaveSlotInfo slot = slots[index];
                if (slot != null)
                    slotsByNumber[slot.slotNumber] = slot;
            }

            hasLoadedSlots = true;
            UpdateSlotLabels();
            SetStatus(slotsByNumber.Count > 0 ? "Choose a save slot to load." : "No saved progress found.");
        }));

        isRefreshingSlots = false;
        SetButtonsInteractable(true);
    }

    private void OnSlotPressed(int slotNumber)
    {
        if (isRefreshingSlots)
        {
            SetStatus("Wait for save slots to finish loading.");
            return;
        }

        pauseMenu.StartCoroutine(LoadFromSlot(slotNumber));
    }

    private IEnumerator LoadFromSlot(int slotNumber)
    {
        if (!hasLoadedSlots)
        {
            SetStatus("Save slots are unavailable right now. Try reopening the panel.");
            yield break;
        }

        if (!slotsByNumber.TryGetValue(slotNumber, out GameSession.SaveSlotInfo slot) || slot == null || string.IsNullOrWhiteSpace(slot.id))
        {
            SetStatus($"Slot {slotNumber} is empty.");
            yield break;
        }

        SetButtonsInteractable(false);
        SetStatus($"Loading Slot {slotNumber}...");

        GameSession.SaveSlotDetail loadedSlot = null;
        string loadError = null;
        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.LoadSaveSlot(slot.id, (result, error) =>
        {
            loadedSlot = result;
            loadError = error;
        }));

        if (!string.IsNullOrEmpty(loadError))
        {
            SetButtonsInteractable(true);
            SetStatus(loadError);
            yield break;
        }

        string targetScene = loadedSlot != null && !string.IsNullOrWhiteSpace(loadedSlot.currentScene)
            ? loadedSlot.currentScene
            : "RoomScene";

        pauseMenu.BeginLoadedGameTransition(targetScene);
    }

    private void UpdateSlotLabels()
    {
        for (int slotIndex = 0; slotIndex < slotButtons.Length; slotIndex++)
        {
            int slotNumber = slotIndex + 1;
            if (!slotsByNumber.TryGetValue(slotNumber, out GameSession.SaveSlotInfo slot) || slot == null)
            {
                SetButtonLabel(slotButtons[slotIndex], BuildEmptyLabel(slotNumber));
                continue;
            }

            SetButtonLabel(slotButtons[slotIndex], BuildOccupiedLabel(slotNumber, slot));
        }
    }

    private void SetMainMenuVisible(bool visible)
    {
        for (int index = 0; index < pauseMenuRect.childCount; index++)
        {
            GameObject menuObject = pauseMenuRect.GetChild(index).gameObject;
            if (menuObject == panelRoot
                || string.Equals(menuObject.name, SavePanelName, StringComparison.Ordinal)
                || string.Equals(menuObject.name, LoadPanelName, StringComparison.Ordinal))
                continue;

            if (menuObject != null)
                menuObject.SetActive(visible);
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        for (int index = 0; index < slotButtons.Length; index++)
        {
            if (slotButtons[index] != null)
                slotButtons[index].interactable = interactable;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private Button FindButton(string name)
    {
        Button[] buttons = pauseMenuRoot.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            if (buttons[index] != null && string.Equals(buttons[index].name, name, StringComparison.Ordinal))
                return buttons[index];
        }

        return null;
    }

    private GameObject FindDirectChild(string name)
    {
        for (int index = 0; index < pauseMenuRect.childCount; index++)
        {
            Transform child = pauseMenuRect.GetChild(index);
            if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                return child.gameObject;
        }

        return null;
    }

    private static TMP_Text CreateTitle(TMP_Text template, Transform parent, string text, float anchoredY, float fontSize)
    {
        TMP_Text label;
        if (template != null)
        {
            label = UnityEngine.Object.Instantiate(template, parent);
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
            rect.sizeDelta = new Vector2(1200f, 100f);
            rect.anchoredPosition = new Vector2(0f, anchoredY);
        }

        label.fontSize = fontSize;
        label.text = text;
        return label;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
        {
            label.text = text;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private static string BuildEmptyLabel(int slotNumber)
    {
        return $"Slot {slotNumber}: Empty";
    }

    private static string BuildOccupiedLabel(int slotNumber, GameSession.SaveSlotInfo slot)
    {
        string slotName = string.IsNullOrWhiteSpace(slot.slotName) ? "UNKNOWN" : slot.slotName.Trim();
        string savedAt = FormatSavedAt(slot.lastPlayedAt);
        return $"Slot {slotNumber}: {slotName} - {savedAt}";
    }

    private static string FormatSavedAt(string lastPlayedAt)
    {
        if (DateTime.TryParse(lastPlayedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            return parsed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

        return "Unknown save time";
    }
}