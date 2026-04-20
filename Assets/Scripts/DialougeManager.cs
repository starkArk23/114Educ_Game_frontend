using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
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

    private void Start()
    {
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
        dialoguePanel.SetActive(true);

        // Keep your earlier panel-fix safety
        var cg = dialoguePanel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        var rt = dialoguePanel.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = Vector2.zero;

        // Set dialogue
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
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = choice.choiceText;

        btn.onClick.AddListener(() =>
        {
            dialoguePanel.SetActive(false);
            PlayerMovement.RemoveMovementLock("Dialogue");

            // For now: log the "effect" + feedback
            Debug.Log($"Picked: {choice.choiceText}");
            Debug.Log($"CyberStatus delta (placeholder): {choice.cyberStatusDelta}");
            Debug.Log($"Feedback: {choice.feedbackText}");
            string color = (choice.cyberStatusDelta < 0) ? "red" : "lime";
            string type = (choice.cyberStatusDelta < 0) ? "Bad choice" : "Good choice";
            int delta = choice.cyberStatusDelta;

            choiceLogUI.Show($"<color={color}>{type}:</color> \"{choice.feedbackText}\"  <b>{delta}</b> Cyberstatus");
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

        dialoguePanel.SetActive(true);

        var cg = dialoguePanel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        var rt = dialoguePanel.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = Vector2.zero;

        if (titleText != null)
            titleText.text = title;

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

        var tmp = btn.GetComponentInChildren<TMP_Text>();
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
    dialoguePanel.SetActive(true);

    var cg = dialoguePanel.GetComponent<CanvasGroup>();
    if (cg != null) cg.alpha = 1f;

    var rt = dialoguePanel.GetComponent<RectTransform>();
    if (rt != null) rt.anchoredPosition = Vector2.zero;

    dialogueText.text = text;

    ClearButton(choiceAButton);
    ClearButton(choiceBButton);
    ClearButton(choiceCButton);
    ClearButton(choiceDButton);

    SetButtonActive(choiceAButton, true);
    SetButtonActive(choiceBButton, true);
    SetButtonActive(choiceCButton, false);
    SetButtonActive(choiceDButton, false);

    choiceAButton.GetComponentInChildren<TMP_Text>().text = choiceAText;
    choiceBButton.GetComponentInChildren<TMP_Text>().text = choiceBText;

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
}