using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class DialogueManager : MonoBehaviour
{
    private const string MikePortraitAssetPath = "Assets/Characters/Story_Chars/Mike/mikeee.png";

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

    [Header("Hint Overlay")]
    [SerializeField] private Sprite mikeHintPortrait;

    private Vector2 defaultPanelAnchoredPosition;
    private bool hasDefaultPanelPosition;
    private Coroutine dialogueTypingCoroutine;
    private string activeDialogueFullText = string.Empty;
    private Action activeDialogueComplete;
    // Frame counter set when typing is skipped via CompleteDialogueBodyImmediately.
    // Choice buttons ignore their handlers on this same frame so that the keystroke or
    // click that triggered the skip cannot also advance the dialogue in one input event.
    private int skipCompletedFrame = -1;
    private Coroutine hintTypingCoroutine;
    private string activeHintFullText = string.Empty;
    private Action activeHintContinue;
    private TextOverflowModes defaultDialogueOverflowMode = TextOverflowModes.Overflow;
    private bool hasDefaultDialogueOverflowMode;
    private int pagedDialogueButtonIndex = -1;
    private int pagedDialoguePageCount;
    private int pagedDialogueCurrentPage = 1;
    private Action<int> pagedDialogueChoiceHandler;
    private GameObject hintOverlayPanel;
    private Image hintPortraitImage;
    private TMP_Text hintTitleText;
    private TMP_Text hintBodyText;
    private Button hintContinueButton;

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

        HideHintOverlay();
    }

    private void Update()
    {
        bool skipInput = Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetMouseButtonDown(0);

        if (dialogueTypingCoroutine != null && dialoguePanel != null && dialoguePanel.activeInHierarchy)
        {
            if (skipInput)
                CompleteDialogueBodyImmediately();
        }

        if (hintTypingCoroutine != null && hintOverlayPanel != null && hintOverlayPanel.activeInHierarchy)
        {
            if (skipInput)
                CompleteHintTypingImmediately();
        }
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
            if (Time.frameCount == skipCompletedFrame)
                return;

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

    private bool TryShowPagedDialogue(string title, string body, string continueLabel, int buttonIndex, Action<int> onChoiceSelected)
    {
        if (dialogueText == null)
            return false;

        CacheDialogueTextLayoutDefaults();
        dialogueText.overflowMode = TextOverflowModes.Page;

        string fullText = ComposeBodyText(title, body);
        dialogueText.text = fullText;
        dialogueText.pageToDisplay = 1;
        dialogueText.ForceMeshUpdate();

        int pageCount = Mathf.Max(1, dialogueText.textInfo.pageCount);
        if (pageCount <= 1)
        {
            RestoreDialogueTextLayout();
            return false;
        }

        activeDialogueFullText = fullText;
        activeDialogueComplete = null;
        pagedDialogueButtonIndex = buttonIndex;
        pagedDialoguePageCount = pageCount;
        pagedDialogueCurrentPage = 1;
        pagedDialogueChoiceHandler = onChoiceSelected;

        SetupPagedDialogueButton(choiceAButton, continueLabel);
        return true;
    }

    private void SetupPagedDialogueButton(Button button, string label)
    {
        if (button == null)
            return;

        SetButtonActive(button, true);

        TMP_Text buttonLabel = GetButtonLabel(button);
        if (buttonLabel != null)
            buttonLabel.text = string.IsNullOrWhiteSpace(label) ? "Continue" : label;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AdvancePagedDialogue);
    }

    private void AdvancePagedDialogue()
    {
        if (dialogueText == null)
            return;

        if (pagedDialogueCurrentPage < pagedDialoguePageCount)
        {
            pagedDialogueCurrentPage++;
            dialogueText.pageToDisplay = pagedDialogueCurrentPage;
            return;
        }

        int buttonIndex = pagedDialogueButtonIndex;
        Action<int> choiceHandler = pagedDialogueChoiceHandler;
        pagedDialogueButtonIndex = -1;
        pagedDialoguePageCount = 0;
        pagedDialogueCurrentPage = 1;
        pagedDialogueChoiceHandler = null;
        RestoreDialogueTextLayout();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        choiceHandler?.Invoke(buttonIndex);
    }

    public void Show(string title, string body, string[] choices, Action<int> onChoiceSelected)
    {
        EnsureUiReferences();
        HideHintOverlay();

        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogError("Show: dialoguePanel or dialogueText is NULL");
            return;
        }

        EnsureEventSystem();

        PreparePanel();

        SetTitle(title);

        ResetChoices();
        RestoreDialogueTextLayout();

        int count = choices != null ? Mathf.Min(choices.Length, 4) : 0;

        // Always use the typewriter path (BeginDialogueBody) so the effect is consistent
        // across every scene and node. The old TryShowPagedDialogue shortcut set the full
        // text immediately, bypassing the typewriter for single-continue long nodes.
        BeginDialogueBody(title, body, () =>
        {
            if (count >= 1) SetupChoiceButton(choiceAButton, choices[0], 0, onChoiceSelected);
            if (count >= 2) SetupChoiceButton(choiceBButton, choices[1], 1, onChoiceSelected);
            if (count >= 3) SetupChoiceButton(choiceCButton, choices[2], 2, onChoiceSelected);
            if (count >= 4) SetupChoiceButton(choiceDButton, choices[3], 3, onChoiceSelected);
        });
    }

    public void ShowAutoAdvance(string title, string body, Action onBodyComplete)
    {
        EnsureUiReferences();
        HideHintOverlay();

        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogError("ShowAutoAdvance: dialoguePanel or dialogueText is NULL");
            return;
        }

        EnsureEventSystem();

        PreparePanel();
        SetTitle(title);
        ResetChoices();
        RestoreDialogueTextLayout();
        BeginDialogueBody(title, body, onBodyComplete);
    }

    public void HideDialoguePanel()
    {
        RestoreDialogueTextLayout();
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    public void ShowHintOverlay(string title, string body, Action onContinue)
    {
        EnsureUiReferences();
        EnsureHintOverlay();

        if (hintOverlayPanel == null || hintBodyText == null || hintContinueButton == null)
        {
            Debug.LogError("ShowHintOverlay: hint overlay UI is not available.");
            return;
        }

        HideDialoguePanel();

        if (hintTitleText != null)
            hintTitleText.text = string.IsNullOrWhiteSpace(title) ? "Mike" : title.Trim();

        if (hintPortraitImage != null)
        {
            hintPortraitImage.sprite = ResolveMikeHintPortrait();
            hintPortraitImage.enabled = hintPortraitImage.sprite != null;
        }

        if (hintTypingCoroutine != null)
        {
            StopCoroutine(hintTypingCoroutine);
            hintTypingCoroutine = null;
        }

        string fullBody = body ?? string.Empty;
        activeHintFullText = fullBody;
        activeHintContinue = onContinue;

        hintContinueButton.onClick.RemoveAllListeners();
        hintContinueButton.onClick.AddListener(OnHintContinueClicked);
        hintOverlayPanel.SetActive(true);

        if (dialogueTypeSpeed <= 0f)
        {
            hintBodyText.text = fullBody;
            activeHintFullText = string.Empty;
        }
        else
        {
            hintTypingCoroutine = StartCoroutine(TypeHintBody(fullBody));
        }
    }

    private void OnHintContinueClicked()
    {
        if (hintTypingCoroutine != null)
        {
            CompleteHintTypingImmediately();
            return;
        }

        Action onContinue = activeHintContinue;
        activeHintContinue = null;
        onContinue?.Invoke();
    }

    private void CompleteHintTypingImmediately()
    {
        if (hintTypingCoroutine != null)
        {
            StopCoroutine(hintTypingCoroutine);
            hintTypingCoroutine = null;
        }

        if (hintBodyText != null)
            hintBodyText.text = activeHintFullText;

        activeHintFullText = string.Empty;
    }

    private IEnumerator TypeHintBody(string fullText)
    {
        if (hintBodyText != null)
            hintBodyText.text = string.Empty;

        for (int index = 0; index < fullText.Length; index++)
        {
            char nextCharacter = fullText[index];
            if (hintBodyText != null)
                hintBodyText.text = fullText.Substring(0, index + 1);

            if (nextCharacter == '\n')
            {
                yield return new WaitForSecondsRealtime(linePause);
                continue;
            }

            float delay = dialogueTypeSpeed;
            if (nextCharacter == '.' || nextCharacter == ',' || nextCharacter == '!' || nextCharacter == '?' || nextCharacter == ':' || nextCharacter == ';')
                delay += punctuationPause;

            yield return new WaitForSecondsRealtime(delay);
        }

        hintTypingCoroutine = null;
        activeHintFullText = string.Empty;
    }

    public void HideHintOverlay()
    {
        if (hintOverlayPanel != null)
            hintOverlayPanel.SetActive(false);
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
            // Ignore the handler when the button was activated on the same frame as a
            // typing-skip input (Space/Enter/click). The same input event that completed
            // the typing animation must not also advance the dialogue immediately.
            if (Time.frameCount == skipCompletedFrame)
                return;

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
        RestoreDialogueTextLayout();

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
        activeDialogueFullText = fullText;
        activeDialogueComplete = onComplete;

        if (dialogueTypeSpeed <= 0f)
        {
            dialogueText.text = fullText;
            FinalizeDialogueBody();
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
                yield return new WaitForSecondsRealtime(linePause);
                continue;
            }

            float delay = dialogueTypeSpeed;
            if (nextCharacter == '.' || nextCharacter == ',' || nextCharacter == '!' || nextCharacter == '?' || nextCharacter == ':' || nextCharacter == ';')
                delay += punctuationPause;

            yield return new WaitForSecondsRealtime(delay);
        }

        FinalizeDialogueBody();
    }

    private void CompleteDialogueBodyImmediately()
    {
        // Record the frame so that choice buttons set up by FinalizeDialogueBody's
        // onComplete callback can ignore the click/keypress that triggered this skip.
        skipCompletedFrame = Time.frameCount;

        if (dialogueText == null)
        {
            FinalizeDialogueBody();
            return;
        }

        if (dialogueTypingCoroutine != null)
        {
            StopCoroutine(dialogueTypingCoroutine);
            dialogueTypingCoroutine = null;
        }

        dialogueText.text = activeDialogueFullText ?? string.Empty;
        FinalizeDialogueBody();
    }

    private void FinalizeDialogueBody()
    {
        dialogueTypingCoroutine = null;

        Action onComplete = activeDialogueComplete;
        activeDialogueComplete = null;
        activeDialogueFullText = string.Empty;
        onComplete?.Invoke();
    }

    private void PreparePanel()
    {
        EnsureUiReferences();
        CacheDialogueTextLayoutDefaults();

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

    private void EnsureHintOverlay()
    {
        if (hintOverlayPanel != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            return;

        RectTransform parentRect = canvas.transform as RectTransform;
        if (parentRect == null)
            return;

        hintOverlayPanel = new GameObject("MikeHintOverlay", typeof(RectTransform), typeof(Image));
        hintOverlayPanel.transform.SetParent(parentRect, false);

        RectTransform panelRect = hintOverlayPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(440f, 168f);

        Image panelImage = hintOverlayPanel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);

        GameObject portraitObject = new GameObject("MikePortrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(hintOverlayPanel.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 1f);
        portraitRect.anchorMax = new Vector2(0f, 1f);
        portraitRect.pivot = new Vector2(0f, 1f);
        portraitRect.anchoredPosition = new Vector2(16f, -16f);
        portraitRect.sizeDelta = new Vector2(76f, 76f);

        hintPortraitImage = portraitObject.GetComponent<Image>();
        hintPortraitImage.preserveAspect = true;

        hintTitleText = CreateHintText(
            "HintTitleText",
            hintOverlayPanel.transform,
            new Vector2(112f, -16f),
            new Vector2(312f, 28f),
            titleText,
            22f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft);

        hintBodyText = CreateHintText(
            "HintBodyText",
            hintOverlayPanel.transform,
            new Vector2(112f, -48f),
            new Vector2(300f, 88f),
            dialogueText,
            18f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        hintBodyText.enableWordWrapping = true;

        hintContinueButton = CreateHintButton("HintContinueButton", hintOverlayPanel.transform, new Vector2(-16f, 16f), new Vector2(112f, 34f));
        hintOverlayPanel.SetActive(false);
    }

    private TMP_Text CreateHintText(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, TMP_Text template, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.color = template.color;
        }
        else
        {
            text.color = Color.white;
        }

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        return text;
    }

    private Button CreateHintButton(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(1f, 0f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.17f, 0.32f, 0.47f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;

        TMP_Text label = CreateHintText(
            "Label",
            buttonObject.transform,
            new Vector2(0f, 0f),
            sizeDelta,
            GetButtonLabel(choiceAButton),
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.text = "Continue";

        return button;
    }

    private Sprite ResolveMikeHintPortrait()
    {
        if (mikeHintPortrait != null)
            return mikeHintPortrait;

#if UNITY_EDITOR
        mikeHintPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(MikePortraitAssetPath);
#endif

        return mikeHintPortrait;
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

    private void CacheDialogueTextLayoutDefaults()
    {
        if (hasDefaultDialogueOverflowMode || dialogueText == null)
            return;

        defaultDialogueOverflowMode = dialogueText.overflowMode;
        hasDefaultDialogueOverflowMode = true;
    }

    private void RestoreDialogueTextLayout()
    {
        if (dialogueText == null)
            return;

        if (hasDefaultDialogueOverflowMode)
            dialogueText.overflowMode = defaultDialogueOverflowMode;

        dialogueText.pageToDisplay = 1;
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