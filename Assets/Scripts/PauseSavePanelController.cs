using System;
using System.Collections;
using System.Collections.Generic;
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
        SetStatus("Loading save slots...");
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

        CreateSaveButton(templateButton);
        menuObjects.Add(saveButton.gameObject);
        CreatePanel(templateButton, titleTemplate);
    }

    private void CreateSaveButton(Button templateButton)
    {
        saveButton = UnityEngine.Object.Instantiate(templateButton, templateButton.transform.parent);
        saveButton.name = "SaveButton";
        SetButtonLabel(saveButton, "Save Game");
        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(pauseMenu.OpenSavePanel);

        RectTransform resumeRect = templateButton.GetComponent<RectTransform>();
        RectTransform saveRect = saveButton.GetComponent<RectTransform>();
        saveRect.anchoredPosition = new Vector2(resumeRect.anchoredPosition.x, -480f);
        saveRect.SetSiblingIndex(Mathf.Max(0, pauseMenuRect.childCount - 1));
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
        statusText = CreateTitle(titleTemplate, panelRoot.transform, string.Empty, -860f, 28f);
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.enableWordWrapping = true;

        for (int slotIndex = 0; slotIndex < slotButtons.Length; slotIndex++)
        {
            int slotNumber = slotIndex + 1;
            Button slotButton = UnityEngine.Object.Instantiate(templateButton, panelRoot.transform);
            slotButton.name = $"SaveSlotButton{slotNumber}";
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => OnSlotPressed(slotNumber));

            RectTransform slotRect = slotButton.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 1f);
            slotRect.anchorMax = new Vector2(0.5f, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.anchoredPosition = new Vector2(0f, -180f - (slotIndex * 120f));

            SetButtonLabel(slotButton, BuildEmptyLabel(slotNumber));
            slotButtons[slotIndex] = slotButton;
        }

        Button backButton = UnityEngine.Object.Instantiate(templateButton, panelRoot.transform);
        backButton.name = "SavePanelBackButton";
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(pauseMenu.CloseSavePanel);
        SetButtonLabel(backButton, "Back");

        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 1f);
        backRect.anchorMax = new Vector2(0.5f, 1f);
        backRect.pivot = new Vector2(0.5f, 1f);
        backRect.anchoredPosition = new Vector2(0f, -820f);

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
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(ConfirmOverwrite);
        SetButtonLabel(confirmButton, "Confirm Overwrite");

        RectTransform confirmButtonRect = confirmButton.GetComponent<RectTransform>();
        confirmButtonRect.anchorMin = new Vector2(0.5f, 1f);
        confirmButtonRect.anchorMax = new Vector2(0.5f, 1f);
        confirmButtonRect.pivot = new Vector2(0.5f, 1f);
        confirmButtonRect.anchoredPosition = new Vector2(0f, -560f);

        Button cancelButton = UnityEngine.Object.Instantiate(templateButton, confirmRoot.transform);
        cancelButton.name = "CancelOverwriteButton";
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(HideConfirmation);
        SetButtonLabel(cancelButton, "Cancel");

        RectTransform cancelButtonRect = cancelButton.GetComponent<RectTransform>();
        cancelButtonRect.anchorMin = new Vector2(0.5f, 1f);
        cancelButtonRect.anchorMax = new Vector2(0.5f, 1f);
        cancelButtonRect.pivot = new Vector2(0.5f, 1f);
        cancelButtonRect.anchoredPosition = new Vector2(0f, -680f);

        confirmRoot.SetActive(false);
    }

    private IEnumerator RefreshSlots()
    {
        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.ListSaveSlots((slots, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
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

            UpdateSlotLabels();
            SetStatus($"Saving as {GameSession.Instance.OperatorName}");
        }));
    }

    private void OnSlotPressed(int slotNumber)
    {
        if (slotsByNumber.ContainsKey(slotNumber))
        {
            pendingSlotNumber = slotNumber;
            confirmText.text = $"Slot {slotNumber} already has saved progress. Overwrite it?";
            confirmRoot.SetActive(true);
            return;
        }

        pauseMenu.StartCoroutine(SaveToSlot(slotNumber));
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
        SetButtonsInteractable(false);
        SetStatus($"Saving to Slot {slotNumber}...");

        GameSession.SaveSlotInfo savedSlot = null;
        string saveError = null;
        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.SaveToSlot(slotNumber, $"Slot {slotNumber}", (slot, error) =>
        {
            savedSlot = slot;
            saveError = error;
        }));

        SetButtonsInteractable(true);

        if (!string.IsNullOrEmpty(saveError))
        {
            SetStatus(saveError);
            yield break;
        }

        if (savedSlot != null)
            slotsByNumber[slotNumber] = savedSlot;

        UpdateSlotLabels();
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

            string scene = string.IsNullOrWhiteSpace(slot.currentScene) ? "No scene" : slot.currentScene;
            string location = string.IsNullOrWhiteSpace(slot.currentLocation) ? "No location" : slot.currentLocation;
            SetButtonLabel(slotButtons[slotIndex], $"Slot {slotNumber}\n{scene}\n{location}");
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
        if (saveButton != null)
            saveButton.interactable = interactable;

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
            rect.sizeDelta = new Vector2(900f, 120f);
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
            label.text = text;
    }

    private static string BuildEmptyLabel(int slotNumber)
    {
        return $"Slot {slotNumber}\nEmpty";
    }
}