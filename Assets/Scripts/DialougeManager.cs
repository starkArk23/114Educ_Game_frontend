using System;
using System.Collections;
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

    [Header("Typing")]
    [SerializeField] private float dialogueTypeSpeed = 0.02f;
    [SerializeField] private float punctuationPause = 0.04f;
    [SerializeField] private float linePause = 0.12f;

    private Vector2 defaultPanelAnchoredPosition;
    private bool hasDefaultPanelPosition;
    private Coroutine dialogueTypingCoroutine;

    public bool HasUsableUi
    {
        get
        {
            EnsureUiReferences();
            return dialoguePanel != null && dialogueText != null;
        }
    }

    private void Awake()
    {
        EnsureEventSystem();
        EnsureUiReferences();
    }

    private void Start()
    {
        EnsureEventSystem();
        EnsureUiReferences();
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

        ResetChoices();

        int count = scenario.choices != null ? scenario.choices.Count : 0;

        BeginDialogueBody(scenario.scenarioTitle, scenario.dialogueText, () =>
        {
            if (count >= 1) SetupChoiceButton(choiceAButton, scenario.choices[0]);
            if (count >= 2) SetupChoiceButton(choiceBButton, scenario.choices[1]);
            if (count >= 3) SetupChoiceButton(choiceCButton, scenario.choices[2]);
            if (count >= 4) SetupChoiceButton(choiceDButton, scenario.choices[3]);
        });

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
        EnsureUiReferences();

        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogError("Show: dialoguePanel or dialogueText is NULL");
            return;
        }

        EnsureEventSystem();

        PreparePanel();

        SetTitle(title);

        ResetChoices();

        int count = choices != null ? Mathf.Min(choices.Length, 4) : 0;
        BeginDialogueBody(title, body, () =>
        {
            if (count >= 1) SetupChoiceButton(choiceAButton, choices[0], 0, onChoiceSelected);
            if (count >= 2) SetupChoiceButton(choiceBButton, choices[1], 1, onChoiceSelected);
            if (count >= 3) SetupChoiceButton(choiceCButton, choices[2], 2, onChoiceSelected);
            if (count >= 4) SetupChoiceButton(choiceDButton, choices[3], 3, onChoiceSelected);
        });
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
        EnsureUiReferences();
    PreparePanel();

    SetTitle(string.Empty);

    ResetChoices();

    BeginDialogueBody(string.Empty, text, () =>
    {
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
    });
}

    private void ResetChoices()
    {
        ClearButton(choiceAButton);
        ClearButton(choiceBButton);
        ClearButton(choiceCButton);
        ClearButton(choiceDButton);

        SetButtonActive(choiceAButton, false);
        SetButtonActive(choiceBButton, false);
        SetButtonActive(choiceCButton, false);
        SetButtonActive(choiceDButton, false);
    }

    private void BeginDialogueBody(string title, string body, Action onComplete)
    {
        EnsureUiReferences();

        if (dialogueText == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (dialogueTypingCoroutine != null)
        {
            StopCoroutine(dialogueTypingCoroutine);
            dialogueTypingCoroutine = null;
        }

        string fullText = ComposeBodyText(title, body);
        if (dialogueTypeSpeed <= 0f)
        {
            dialogueText.text = fullText;
            onComplete?.Invoke();
            return;
        }

        dialogueTypingCoroutine = StartCoroutine(TypeDialogueBody(fullText, onComplete));
    }

    private string ComposeBodyText(string title, string body)
    {
        string safeBody = body ?? string.Empty;
        if (titleText == null && !string.IsNullOrWhiteSpace(title))
            return title.Trim() + "\n\n" + safeBody;

        return safeBody;
    }

    private IEnumerator TypeDialogueBody(string fullText, Action onComplete)
    {
        dialogueText.text = string.Empty;

        for (int index = 0; index < fullText.Length; index++)
        {
            char nextCharacter = fullText[index];
            dialogueText.text = fullText.Substring(0, index + 1);

            if (nextCharacter == '\n')
            {
                yield return new WaitForSeconds(linePause);
                continue;
            }

            float delay = dialogueTypeSpeed;
            if (nextCharacter == '.' || nextCharacter == ',' || nextCharacter == '!' || nextCharacter == '?' || nextCharacter == ':' || nextCharacter == ';')
                delay += punctuationPause;

            yield return new WaitForSeconds(delay);
        }

        dialogueTypingCoroutine = null;
        onComplete?.Invoke();
    }

    private void PreparePanel()
    {
        EnsureUiReferences();

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
        EnsureUiReferences();

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

    private void EnsureUiReferences()
    {
        if (dialoguePanel == null)
        {
            GameObject panel = FindNamedChild("DialoguePanel");
            if (panel != null)
                dialoguePanel = panel;
        }

        if (titleText == null)
            titleText = FindComponentByName<TMP_Text>("TitleText");

        if (dialogueText == null)
            dialogueText = FindComponentByName<TMP_Text>("DialogueText");

        if (choiceAButton == null)
            choiceAButton = FindComponentByName<Button>("ChoiceA");

        if (choiceBButton == null)
            choiceBButton = FindComponentByName<Button>("ChoiceB");

        if (choiceCButton == null)
            choiceCButton = FindComponentByName<Button>("ChoiceC");

        if (choiceDButton == null)
            choiceDButton = FindComponentByName<Button>("ChoiceD");

        if (choiceLogUI == null)
            choiceLogUI = FindFirstObjectByType<ChoiceLogUI>(FindObjectsInactive.Include);
    }

    private static GameObject FindNamedChild(string objectName)
    {
        GameObject direct = GameObject.Find(objectName);
        if (direct != null)
            return direct;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                return candidate.gameObject;
        }

        return null;
    }

    private static T FindComponentByName<T>(string objectName) where T : Component
    {
        GameObject target = FindNamedChild(objectName);
        return target != null ? target.GetComponent<T>() : null;
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