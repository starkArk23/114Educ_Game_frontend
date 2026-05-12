using System;
using System.Collections.Generic;
using UnityEngine;

public class StoryManager : MonoBehaviour
{
    private const string MovementLockId = "StoryDialogue";

    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private ChoiceLogUI choiceLogUI;
    [SerializeField] private bool autoStartOnEnable;
    [SerializeField] private string startNodeKey;

    private GameSession.StoryNodeDetail currentNode;
    private List<GameSession.StoryChoiceDetail> currentChoices = new List<GameSession.StoryChoiceDetail>();
    private bool requestInFlight;

    public string CurrentNodeKey => currentNode?.nodeKey ?? string.Empty;
    public string CurrentChapterKey => currentNode?.chapterKey ?? string.Empty;

    private void OnEnable()
    {
        if (autoStartOnEnable)
            StartStory();
    }

    public void StartStory()
    {
        if (requestInFlight)
            return;

        GameSession session = GameSession.Instance;
        StartCoroutine(session.GetCurrentStoryNode(startNodeKey, HandleNodeResponse));
    }

    public void ResumeCurrentStory()
    {
        if (requestInFlight)
            return;

        StartCoroutine(GameSession.Instance.GetCurrentStoryNode(null, HandleNodeResponse));
    }

    public void HandleWorldInteraction(string interactionId, string groupKey, string speaker, string title, string bodyText)
    {
        if (requestInFlight)
            return;

        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            string header = !string.IsNullOrWhiteSpace(title) ? title : speaker;
            ShowDialogue(header, bodyText, new[] { "Continue" }, _ => RegisterInteraction(interactionId, groupKey));
            return;
        }

        RegisterInteraction(interactionId, groupKey);
    }

    private void RegisterInteraction(string interactionId, string groupKey)
    {
        if (requestInFlight)
            return;

        requestInFlight = true;
        StartCoroutine(GameSession.Instance.RegisterStoryInteraction(interactionId, groupKey, (node, error) =>
        {
            requestInFlight = false;

            if (!string.IsNullOrEmpty(error))
            {
                ReportError(error);
                return;
            }

            if (node == null)
                return;

            bool advancedNode = currentNode == null || !string.Equals(currentNode.nodeKey, node.nodeKey, StringComparison.Ordinal);
            currentNode = node;

            if (advancedNode)
            {
                PresentNode(node);
                return;
            }

            if (node.gateProgress != null && choiceLogUI != null)
                choiceLogUI.Show($"Progress: {node.gateProgress.currentCount}/{node.gateProgress.requiredCount}");

            ReleaseMovement();
        }));
    }

    private void HandleNodeResponse(GameSession.StoryNodeDetail node, string error)
    {
        requestInFlight = false;

        if (!string.IsNullOrEmpty(error))
        {
            ReportError(error);
            return;
        }

        if (node == null)
        {
            ReportError("Story response was empty.");
            return;
        }

        PresentNode(node);
    }

    private void PresentNode(GameSession.StoryNodeDetail node)
    {
        currentNode = node;
        currentChoices = node.choices ?? new List<GameSession.StoryChoiceDetail>();

        if (node.gateProgress != null && choiceLogUI != null)
            choiceLogUI.Show($"Progress: {node.gateProgress.currentCount}/{node.gateProgress.requiredCount}");

        if (currentChoices.Count > 0)
        {
            string[] labels = new string[currentChoices.Count];
            for (int index = 0; index < currentChoices.Count; index++)
            {
                GameSession.StoryChoiceDetail choice = currentChoices[index];
                labels[index] = choice.trustTokenCost > 0
                    ? $"{choice.label} (-{choice.trustTokenCost} Token)"
                    : choice.label;
            }

            ShowDialogue(GetNodeTitle(node), node.bodyText, labels, OnDialogueSelection);
            return;
        }

        if (node.canContinue)
        {
            ShowDialogue(GetNodeTitle(node), node.bodyText, new[] { "Continue" }, _ => ContinueCurrentNode());
            return;
        }

        ShowDialogue(GetNodeTitle(node), node.bodyText, new[] { node.endChapter ? "Close" : "Continue" }, _ => CloseStoryDialogue());
    }

    private void OnDialogueSelection(int selectionIndex)
    {
        if (requestInFlight)
            return;

        if (currentNode == null)
        {
            ReleaseMovement();
            return;
        }

        if (currentChoices == null || selectionIndex < 0 || selectionIndex >= currentChoices.Count)
        {
            ReleaseMovement();
            return;
        }

        GameSession.StoryChoiceDetail selectedChoice = currentChoices[selectionIndex];
        requestInFlight = true;
        StartCoroutine(GameSession.Instance.SubmitStoryChoice(currentNode.nodeKey, selectedChoice.id, HandleNodeResponse));
    }

    private void ContinueCurrentNode()
    {
        if (requestInFlight || currentNode == null)
            return;

        requestInFlight = true;
        StartCoroutine(GameSession.Instance.ContinueStoryNode(currentNode.nodeKey, HandleNodeResponse));
    }

    private void ShowDialogue(string title, string bodyText, string[] choices, Action<int> onChoiceSelected)
    {
        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            ReportError("No DialogueManager was found in the scene.");
            return;
        }

        PlayerMovement.AddMovementLock(MovementLockId);
        GameState.CanPlayerMove = false;
        manager.Show(title, bodyText, choices, onChoiceSelected);
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>();

        return dialogueManager;
    }

    private string GetNodeTitle(GameSession.StoryNodeDetail node)
    {
        if (node == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(node.title))
            return node.title;

        return string.IsNullOrWhiteSpace(node.speaker) ? string.Empty : node.speaker;
    }

    private void CloseStoryDialogue()
    {
        ReleaseMovement();
    }

    private void ReleaseMovement()
    {
        GameState.CanPlayerMove = true;
        PlayerMovement.RemoveMovementLock(MovementLockId);
    }

    private void ReportError(string error)
    {
        Debug.LogError($"[StoryManager] {error}");
        if (choiceLogUI != null && !string.IsNullOrWhiteSpace(error))
            choiceLogUI.Show(error);
        ReleaseMovement();
    }
}