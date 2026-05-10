using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseSavePanelController
{
    private readonly PauseMenu pauseMenu;
    private readonly GameObject pauseMenuRoot;
    private readonly RectTransform pauseMenuRect;
    private readonly List<GameObject> menuObjects = new List<GameObject>();
    private readonly Dictionary<int, GameSession.SaveSlotInfo> slotsByNumber = new Dictionary<int, GameSession.SaveSlotInfo>();

    private Button saveButton;
    private GameObject panelRoot;
    private TMP_Text statusText;
    private GameObject confirmRoot;
    private TMP_Text confirmText;
    private readonly Button[] slotButtons = new Button[5];

    private int pendingSlotNumber = -1;
    private bool hasLoadedSlots;
    private bool isRefreshingSlots;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private PauseSavePanelController(PauseMenu pauseMenu, GameObject pauseMenuRoot)
    {
        this.pauseMenu = pauseMenu;
        this.pauseMenuRoot = pauseMenuRoot;
        pauseMenuRect = pauseMenuRoot != null ? pauseMenuRoot.GetComponent<RectTransform>() : null;
    }

    public static PauseSavePanelController Create(PauseMenu pauseMenu, GameObject pauseMenuRoot)
    {
        PauseSavePanelController controller = new PauseSavePanelController(pauseMenu, pauseMenuRoot);
        controller.Initialize();
        return controller;
    }

    public void ShowPanel()
    {
        if (panelRoot == null)
            return;

        SetMainMenuVisible(false);
        panelRoot.SetActive(true);
        HideConfirmation();
        hasLoadedSlots = false;
        isRefreshingSlots = true;
        SetStatus("Loading save slots...");
        SetButtonsInteractable(true);
        UpdateSlotLabels();
        pauseMenu.StartCoroutine(RefreshSlots());
    }

    public void HidePanel()
    {
        HideConfirmation();

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
            return;

        GameSession.EnsureExists();

        for (int index = 0; index < pauseMenuRect.childCount; index++)
        {
            menuObjects.Add(pauseMenuRect.GetChild(index).gameObject);
        }

        Button templateButton = FindButton("ResumeButton") ?? UnityEngine.Object.FindFirstObjectByType<Button>();
        if (templateButton == null)
            return;

        TMP_Text titleTemplate = pauseMenuRoot.GetComponentInChildren<TMP_Text>(true);

        bool createdSaveButton = CreateSaveButton(templateButton);
        if (createdSaveButton && saveButton != null)
            menuObjects.Add(saveButton.gameObject);

        CreatePanel(templateButton, titleTemplate);
    }

    private bool CreateSaveButton(Button templateButton)
    {
        saveButton = FindButton("SaveButton");
        bool createdSaveButton = false;

        if (saveButton == null)
        {
            saveButton = UnityEngine.Object.Instantiate(templateButton, templateButton.transform.parent);
            saveButton.name = "SaveButton";
            createdSaveButton = true;
        }

        SetButtonLabel(saveButton, "Save Game");
        saveButton.onClick = new Button.ButtonClickedEvent();
        saveButton.onClick.AddListener(pauseMenu.OpenSavePanel);

        RectTransform saveRect = saveButton.GetComponent<RectTransform>();
        RectTransform resumeRect = templateButton.GetComponent<RectTransform>();
        saveRect.anchoredPosition = new Vector2(resumeRect.anchoredPosition.x, -300f);
        saveRect.sizeDelta = new Vector2(800f, saveRect.sizeDelta.y);
        saveRect.SetSiblingIndex(Mathf.Max(0, pauseMenuRect.childCount - 1));

        return createdSaveButton;
    }

    private void CreatePanel(Button templateButton, TMP_Text titleTemplate)
    {
        panelRoot = new GameObject("SaveSlotPanel", typeof(RectTransform));
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRoot.transform.SetParent(pauseMenuRoot.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRoot.SetActive(false);

        TMP_Text panelTitle = CreateTitle(titleTemplate, panelRoot.transform, "Save Progress", -60f, 58f);
        panelTitle.alignment = TextAlignmentOptions.Center;
        statusText = CreateTitle(titleTemplate, panelRoot.transform, string.Empty, -120f, 28f);
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.enableWordWrapping = false;

        for (int slotIndex = 0; slotIndex < slotButtons.Length; slotIndex++)
        {
            int slotNumber = slotIndex + 1;
            Button slotButton = UnityEngine.Object.Instantiate(templateButton, panelRoot.transform);
            slotButton.name = $"SaveSlotButton{slotNumber}";
            slotButton.onClick = new Button.ButtonClickedEvent();
            slotButton.onClick.AddListener(() => OnSlotPressed(slotNumber));

            RectTransform slotRect = slotButton.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 1f);
            slotRect.anchorMax = new Vector2(0.5f, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.anchoredPosition = new Vector2(0f, -180f - (slotIndex * 96f));
            slotRect.sizeDelta = new Vector2(800f, slotRect.sizeDelta.y);

            SetButtonLabel(slotButton, BuildEmptyLabel(slotNumber));
            slotButtons[slotIndex] = slotButton;
        }

        Button backButton = UnityEngine.Object.Instantiate(templateButton, panelRoot.transform);
        backButton.name = "SavePanelBackButton";
    backButton.onClick = new Button.ButtonClickedEvent();
        backButton.onClick.AddListener(pauseMenu.CloseSavePanel);
        SetButtonLabel(backButton, "Back");

        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 1f);
        backRect.anchorMax = new Vector2(0.5f, 1f);
        backRect.pivot = new Vector2(0.5f, 1f);
        backRect.anchoredPosition = new Vector2(0f, -660f);
        backRect.sizeDelta = new Vector2(800f, backRect.sizeDelta.y);

        CreateConfirmationOverlay(templateButton, titleTemplate);
    }

    private void CreateConfirmationOverlay(Button templateButton, TMP_Text titleTemplate)
    {
        confirmRoot = new GameObject("OverwriteConfirmPanel", typeof(RectTransform), typeof(Image));
        RectTransform confirmRect = confirmRoot.GetComponent<RectTransform>();
        confirmRoot.transform.SetParent(panelRoot.transform, false);
        confirmRect.anchorMin = Vector2.zero;
        confirmRect.anchorMax = Vector2.one;
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;
        confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        confirmText = CreateTitle(titleTemplate, confirmRoot.transform, "Overwrite this slot?", -420f, 38f);
        confirmText.alignment = TextAlignmentOptions.Center;
        confirmText.enableWordWrapping = true;

        Button confirmButton = UnityEngine.Object.Instantiate(templateButton, confirmRoot.transform);
        confirmButton.name = "ConfirmOverwriteButton";
    confirmButton.onClick = new Button.ButtonClickedEvent();
        confirmButton.onClick.AddListener(ConfirmOverwrite);
        SetButtonLabel(confirmButton, "Confirm Overwrite");

        RectTransform confirmButtonRect = confirmButton.GetComponent<RectTransform>();
        confirmButtonRect.anchorMin = new Vector2(0.5f, 1f);
        confirmButtonRect.anchorMax = new Vector2(0.5f, 1f);
        confirmButtonRect.pivot = new Vector2(0.5f, 1f);
        confirmButtonRect.anchoredPosition = new Vector2(0f, -560f);
        confirmButtonRect.sizeDelta = new Vector2(800f, confirmButtonRect.sizeDelta.y);

        Button cancelButton = UnityEngine.Object.Instantiate(templateButton, confirmRoot.transform);
        cancelButton.name = "CancelOverwriteButton";
    cancelButton.onClick = new Button.ButtonClickedEvent();
        cancelButton.onClick.AddListener(HideConfirmation);
        SetButtonLabel(cancelButton, "Cancel");

        RectTransform cancelButtonRect = cancelButton.GetComponent<RectTransform>();
        cancelButtonRect.anchorMin = new Vector2(0.5f, 1f);
        cancelButtonRect.anchorMax = new Vector2(0.5f, 1f);
        cancelButtonRect.pivot = new Vector2(0.5f, 1f);
        cancelButtonRect.anchoredPosition = new Vector2(0f, -680f);
        cancelButtonRect.sizeDelta = new Vector2(800f, cancelButtonRect.sizeDelta.y);

        confirmRoot.SetActive(false);
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
            SetStatus($"Saving as {GameSession.Instance.OperatorName}");
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

        pauseMenu.StartCoroutine(ResolveSlotSelection(slotNumber));
    }

    private IEnumerator ResolveSlotSelection(int slotNumber)
    {
        if (!hasLoadedSlots || !slotsByNumber.ContainsKey(slotNumber))
        {
            SetStatus(!hasLoadedSlots ? "Loading save slots..." : $"Checking Slot {slotNumber}...");
            SetButtonsInteractable(false);
            yield return pauseMenu.StartCoroutine(RefreshSlots());

            if (!hasLoadedSlots)
            {
                SetStatus("Save slots are unavailable right now. Try reopening the panel.");
                yield break;
            }
        }

        if (slotsByNumber.ContainsKey(slotNumber))
        {
            ShowOverwriteConfirmation(slotNumber);
            yield break;
        }

        yield return pauseMenu.StartCoroutine(SaveToSlot(slotNumber));
    }

    private void ShowOverwriteConfirmation(int slotNumber)
    {
        pendingSlotNumber = slotNumber;
        confirmText.text = $"Slot {slotNumber} already has saved progress. Overwrite it?";
        confirmRoot.SetActive(true);
    }

    private void ConfirmOverwrite()
    {
        int slotNumber = pendingSlotNumber;
        pendingSlotNumber = -1;
        HideConfirmation();

        if (slotNumber > 0)
            pauseMenu.StartCoroutine(SaveToSlot(slotNumber));
    }

    private IEnumerator SaveToSlot(int slotNumber)
    {
        if (!hasLoadedSlots)
        {
            SetStatus("Save slots are unavailable right now. Try reopening the panel.");
            yield break;
        }

        SetButtonsInteractable(false);
        SetStatus($"Saving to Slot {slotNumber}...");

        GameSession.SaveSlotInfo savedSlot = null;
        string saveError = null;
        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.SaveToSlot(slotNumber, null, (slot, error) =>
        {
            savedSlot = slot;
            saveError = error;
        }));

        if (!string.IsNullOrEmpty(saveError))
        {
            SetButtonsInteractable(hasLoadedSlots);
            SetStatus(saveError);
            yield break;
        }

        if (savedSlot != null)
            slotsByNumber[slotNumber] = savedSlot;

        UpdateSlotLabels();
        SetStatus("Refreshing save slots...");
        yield return pauseMenu.StartCoroutine(RefreshSlots());

        if (!hasLoadedSlots)
        {
            SetStatus($"Saved to Slot {slotNumber}, but slot refresh failed.");
            yield break;
        }

        SetStatus($"Saved to Slot {slotNumber}.");
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
        for (int index = 0; index < menuObjects.Count; index++)
        {
            GameObject menuObject = menuObjects[index];
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

    private void HideConfirmation()
    {
        pendingSlotNumber = -1;
        if (confirmRoot != null)
            confirmRoot.SetActive(false);
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