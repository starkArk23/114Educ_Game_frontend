using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject dialoguePanel;
    public TMP_Text titleText;
    public TMP_Text dialogueText;
    public Button choiceAButton;
    public Button choiceBButton;
    public Button choiceCButton;
    public Button choiceDButton;
    public ChoiceLogUI choiceLogUI;

    private Vector2 defaultPanelAnchoredPosition;
    private bool hasDefaultPanelPosition;

    private void Awake()
    {
        EnsureEventSystem();
    }

    private void Start()
    {
        EnsureEventSystem();
        CachePanelLayoutDefaults();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    // ✅ This is what ScenarioTester needs
    public void StartScenario(ScenarioData scenario)
    {
        if (scenario == null)
        {
            Debug.LogError("StartScenario: scenario is NULL");
            return;
        }

        PlayerMovement.AddMovementLock("Dialogue");
        PreparePanel();

        SetTitle(scenario.scenarioTitle);
        dialogueText.text = scenario.dialogueText;

        // Clear listeners
        ClearButton(choiceAButton);
        ClearButton(choiceBButton);
        ClearButton(choiceCButton);
        ClearButton(choiceDButton);

        // Hide all first
        SetButtonActive(choiceAButton, false);
        SetButtonActive(choiceBButton, false);
        SetButtonActive(choiceCButton, false);
        SetButtonActive(choiceDButton, false);

        // Show buttons depending on how many choices exist (2–4 recommended)
        int count = scenario.choices != null ? scenario.choices.Count : 0;

        if (count >= 1) SetupChoiceButton(choiceAButton, scenario.choices[0]);
        if (count >= 2) SetupChoiceButton(choiceBButton, scenario.choices[1]);
        if (count >= 3) SetupChoiceButton(choiceCButton, scenario.choices[2]);
        if (count >= 4) SetupChoiceButton(choiceDButton, scenario.choices[3]);

        if (count < 2)
            Debug.LogWarning("Scenario has less than 2 choices. Add at least 2 choices.");
        if (count > 4)
            Debug.LogWarning("Scenario has more than 4 choices. Only the first 4 are used.");
    }

    private void SetupChoiceButton(Button btn, ChoiceData choice)
    {
        if (btn == null) return;

        SetButtonActive(btn, true);

        // Set label text
        var tmp = GetButtonLabel(btn);
        if (tmp != null) tmp.text = choice.choiceText;

        btn.onClick.AddListener(() =>
        {
            dialoguePanel.SetActive(false);
            PlayerMovement.RemoveMovementLock("Dialogue");

            GameSession.CyberStatusEffect effect = new GameSession.CyberStatusEffect
            {
                delta = choice.cyberStatusDelta,
                source = "ScenarioChoice",
                reason = string.IsNullOrWhiteSpace(choice.choiceText) ? choice.feedbackText : choice.choiceText
            };

            GameSession.Instance.ApplyCyberStatusEffect(effect);

            Debug.Log($"Picked: {choice.choiceText}");
            Debug.Log($"CyberStatus delta: {choice.cyberStatusDelta}");
            Debug.Log($"Feedback: {choice.feedbackText}");
            string color = choice.cyberStatusDelta < 0 ? "red" : (choice.cyberStatusDelta > 0 ? "lime" : "cyan");
            string type = choice.cyberStatusDelta < 0 ? "Bad choice" : (choice.cyberStatusDelta > 0 ? "Good choice" : "Neutral choice");
            int delta = choice.cyberStatusDelta;

            if (choiceLogUI != null)
                choiceLogUI.Show($"<color={color}>{type}:</color> \"{choice.feedbackText}\"  <b>{delta:+#;-#;0}</b> Cyberstatus");
        });
        Debug.Log(choiceLogUI == null ? "choiceLogUI IS NULL" : "choiceLogUI OK");
    }

    private void ClearButton(Button btn)
    {
        if (btn != null) btn.onClick.RemoveAllListeners();
    }

    private void SetButtonActive(Button btn, bool active)
    {
        if (btn != null) btn.gameObject.SetActive(active);
    }
    public void Show(string title, string body, string[] choices, Action<int> onChoiceSelected)
    {
        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogError("Show: dialoguePanel or dialogueText is NULL");
            return;
        }

        EnsureEventSystem();

        PreparePanel();

        SetTitle(title);

        if (titleText == null && !string.IsNullOrEmpty(title))
            dialogueText.text = title + "\n\n" + body;
        else
            dialogueText.text = body;

        ClearButton(choiceAButton);
        ClearButton(choiceBButton);
        ClearButton(choiceCButton);
        ClearButton(choiceDButton);

        SetButtonActive(choiceAButton, false);
        SetButtonActive(choiceBButton, false);
        SetButtonActive(choiceCButton, false);
        SetButtonActive(choiceDButton, false);

        int count = choices != null ? Mathf.Min(choices.Length, 4) : 0;
        if (count >= 1) SetupChoiceButton(choiceAButton, choices[0], 0, onChoiceSelected);
        if (count >= 2) SetupChoiceButton(choiceBButton, choices[1], 1, onChoiceSelected);
        if (count >= 3) SetupChoiceButton(choiceCButton, choices[2], 2, onChoiceSelected);
        if (count >= 4) SetupChoiceButton(choiceDButton, choices[3], 3, onChoiceSelected);
    }

    private void SetupChoiceButton(Button btn, string label, int index, Action<int> onChoiceSelected)
    {
        if (btn == null)
            return;

        SetButtonActive(btn, true);

        var tmp = GetButtonLabel(btn);
        if (tmp != null)
            tmp.text = label;

        btn.onClick.AddListener(() =>
        {
            dialoguePanel.SetActive(false);
            onChoiceSelected?.Invoke(index);
        });
    }
    public void ShowDialogue(string text, string choiceAText, string choiceBText,
    System.Action onChoiceA, System.Action onChoiceB)
{
    PlayerMovement.AddMovementLock("Dialogue");
    EnsureEventSystem();
    PreparePanel();

    SetTitle(string.Empty);

    dialogueText.text = text;

    ClearButton(choiceAButton);
    ClearButton(choiceBButton);
    ClearButton(choiceCButton);
    ClearButton(choiceDButton);

    SetButtonActive(choiceAButton, true);
    SetButtonActive(choiceBButton, true);
    SetButtonActive(choiceCButton, false);
    SetButtonActive(choiceDButton, false);

    TMP_Text choiceALabel = GetButtonLabel(choiceAButton);
    if (choiceALabel != null)
        choiceALabel.text = choiceAText;

    TMP_Text choiceBLabel = GetButtonLabel(choiceBButton);
    if (choiceBLabel != null)
        choiceBLabel.text = choiceBText;

    choiceAButton.onClick.AddListener(() =>
    {
        dialoguePanel.SetActive(false);
        PlayerMovement.RemoveMovementLock("Dialogue");
        onChoiceA?.Invoke();
    });

    choiceBButton.onClick.AddListener(() =>
    {
        dialoguePanel.SetActive(false);
        PlayerMovement.RemoveMovementLock("Dialogue");
        onChoiceB?.Invoke();
    });
}

    private void PreparePanel()
    {
        if (dialoguePanel == null)
            return;

        CachePanelLayoutDefaults();
        dialoguePanel.SetActive(true);

        CanvasGroup canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
        if (panelRect != null && hasDefaultPanelPosition)
            panelRect.anchoredPosition = defaultPanelAnchoredPosition;
    }

    private void CachePanelLayoutDefaults()
    {
        if (hasDefaultPanelPosition || dialoguePanel == null)
            return;

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        defaultPanelAnchoredPosition = panelRect.anchoredPosition;
        hasDefaultPanelPosition = true;
    }

    private void SetTitle(string title)
    {
        if (titleText == null)
            return;

        titleText.text = string.IsNullOrWhiteSpace(title) ? "SECURE CHANNEL" : title.Trim();
    }

    private TMP_Text GetButtonLabel(Button btn)
    {
        return btn == null ? null : btn.GetComponentInChildren<TMP_Text>(true);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject root = new GameObject("EventSystem");
        root.AddComponent<EventSystem>();

        Type inputModuleType = ResolveInputModuleType();
        if (inputModuleType != null && typeof(BaseInputModule).IsAssignableFrom(inputModuleType))
        {
            root.AddComponent(inputModuleType);
            return;
        }

        root.AddComponent<StandaloneInputModule>();
    }

    private static Type ResolveInputModuleType()
    {
        Type inputSystemType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemType != null)
            return inputSystemType;

        AppDomain domain = AppDomain.CurrentDomain;
        var assemblies = domain.GetAssemblies();
        for (int index = 0; index < assemblies.Length; index++)
        {
            Type candidate = assemblies[index].GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (candidate != null)
                return candidate;
        }

        return null;
    }
}