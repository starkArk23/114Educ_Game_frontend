using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryManager : MonoBehaviour
{
    private const string MovementLockId = "StoryDialogue";
    private const string WakeTransitionNodeKey = "opening.wake";
    private const string HallwaySceneName = "HallwayScene";
    private const string HallwayArrivalSpawnPointId = "FromRoomScene";
    private const string HallwayArrivalNodeKey = "chapter1.avi_intro";

    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private ChoiceLogUI choiceLogUI;
    [SerializeField] private bool autoStartOnEnable;
    [SerializeField] private string startNodeKey;
    [SerializeField] private float mentorEntranceCameraHold = 0.15f;
    [SerializeField] private string wakeTransitionSceneName = "RoomScene";
    [SerializeField] private string wakeTransitionSpawnPointId;
    [SerializeField] private int wakeTransitionBlinkCount = 2;
    [SerializeField] private float wakeTransitionDelaySeconds = 2f;

    private GameSession.StoryNodeDetail currentNode;
    private List<GameSession.StoryChoiceDetail> currentChoices = new List<GameSession.StoryChoiceDetail>();
    private bool requestInFlight;
    private Coroutine presentationRoutine;

    public string CurrentNodeKey => currentNode?.nodeKey ?? string.Empty;
    public string CurrentChapterKey => currentNode?.chapterKey ?? string.Empty;
    public bool CanContinueCurrentNode => !requestInFlight && currentNode != null && currentNode.canContinue;
    public bool IsRequestInFlight => requestInFlight;

    private void OnEnable()
    {
        if (!autoStartOnEnable)
            return;

        if (string.IsNullOrWhiteSpace(startNodeKey))
        {
            if (IsHallwayScene())
                return;

            ResumeCurrentStory();
            return;
        }

        StartStory();
    }

    private void Start()
    {
        if (!autoStartOnEnable || !string.IsNullOrWhiteSpace(startNodeKey) || !IsHallwayScene())
            return;

        if (TryHandleHallwayArrivalStart())
            return;

        ResumeCurrentStory();
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

    public void RefreshCurrentNodePresentation()
    {
        if (requestInFlight || currentNode == null)
            return;

        QueueNodePresentation(currentNode);
    }

    private bool TryHandleHallwayArrivalStart()
    {
        if (!IsHallwayScene())
            return false;

        if (!RuntimeSceneTransition.ConsumeLatestArrival(HallwaySceneName, HallwayArrivalSpawnPointId))
            return false;

        StartCoroutine(RefreshHallwayArrivalStoryRoutine());
        return true;
    }

    private static bool IsHallwayScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return string.Equals(activeScene.name, HallwaySceneName, StringComparison.Ordinal);
    }

    private IEnumerator RefreshHallwayArrivalStoryRoutine()
    {
        for (int frame = 0; frame < 5; frame++)
            yield return null;

        float timeoutSeconds = 1f;
        while (requestInFlight && timeoutSeconds > 0f)
        {
            timeoutSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager != null)
            manager.HideDialoguePanel();

        if (string.Equals(CurrentNodeKey, HallwayArrivalNodeKey, StringComparison.Ordinal))
        {
            RefreshCurrentNodePresentation();
            yield break;
        }

        StartCoroutine(GameSession.Instance.GetCurrentStoryNode(HallwayArrivalNodeKey, HandleNodeResponse));
    }

    public IEnumerator ContinueCurrentNodeSilently(Action<GameSession.StoryNodeDetail, string> onComplete)
    {
        if (requestInFlight)
        {
            onComplete?.Invoke(null, "A story request is already in progress.");
            yield break;
        }

        if (currentNode == null)
        {
            onComplete?.Invoke(null, "No current story node is active.");
            yield break;
        }

        if (!currentNode.canContinue)
        {
            onComplete?.Invoke(currentNode, null);
            yield break;
        }

        requestInFlight = true;
        yield return StartCoroutine(GameSession.Instance.ContinueStoryNode(currentNode.nodeKey, (node, error) =>
        {
            requestInFlight = false;

            if (!string.IsNullOrEmpty(error))
            {
                ReportError(error);
                onComplete?.Invoke(null, error);
                return;
            }

            if (node == null)
            {
                const string emptyResponseError = "Story response was empty.";
                ReportError(emptyResponseError);
                onComplete?.Invoke(null, emptyResponseError);
                return;
            }

            currentNode = node;
            currentChoices = node.choices ?? new List<GameSession.StoryChoiceDetail>();
            onComplete?.Invoke(node, null);
        }));
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
                QueueNodePresentation(node);
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

        QueueNodePresentation(node);
    }

    private void QueueNodePresentation(GameSession.StoryNodeDetail node)
    {
        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);

        presentationRoutine = StartCoroutine(PresentNodeRoutine(node));
    }

    private System.Collections.IEnumerator PresentNodeRoutine(GameSession.StoryNodeDetail node)
    {
        currentNode = node;
        currentChoices = node.choices ?? new List<GameSession.StoryChoiceDetail>();

        if (node.gateProgress != null && choiceLogUI != null)
            choiceLogUI.Show($"Progress: {node.gateProgress.currentCount}/{node.gateProgress.requiredCount}");

        yield return WaitForPresentationGate(node);

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
            presentationRoutine = null;
            yield break;
        }

        if (ShouldAutoTransitionWakeNode(node))
        {
            ShowWakeTransitionDialogue(node);
            presentationRoutine = null;
            yield break;
        }

        if (node.canContinue)
        {
            ShowDialogue(GetNodeTitle(node), node.bodyText, new[] { "Continue" }, _ => ContinueCurrentNode());
            presentationRoutine = null;
            yield break;
        }

        ShowDialogue(GetNodeTitle(node), node.bodyText, new[] { node.endChapter ? "Close" : "Continue" }, _ => CloseStoryDialogue());
        presentationRoutine = null;
    }

    private System.Collections.IEnumerator WaitForPresentationGate(GameSession.StoryNodeDetail node)
    {
        if (node == null)
            yield break;

        StoryNpcEntranceController mentorPresentation = ResolvePresentationController(node.nodeKey);
        Chapter1AviSceneController aviPresentation = mentorPresentation == null
            ? ResolveAviPresentationController(node.nodeKey)
            : null;

        bool mentorPending = mentorPresentation != null && !mentorPresentation.IsPresentationComplete(node.nodeKey);
        bool aviPending = aviPresentation != null && !aviPresentation.IsPresentationComplete(node.nodeKey);
        if (!mentorPending && !aviPending)
            yield break;

        LockMovement();

        CameraFocusController focusController = FindFirstObjectByType<CameraFocusController>();
        Transform focusTarget = mentorPending
            ? mentorPresentation.transform
            : aviPresentation.PresentationTransform;
        if (focusController != null && focusTarget != null)
            focusController.SetFocusTarget(focusTarget, true);

        yield return new WaitUntil(() =>
            (mentorPresentation == null || mentorPresentation.IsPresentationComplete(node.nodeKey))
            && (aviPresentation == null || aviPresentation.IsPresentationComplete(node.nodeKey)));

        if (mentorEntranceCameraHold > 0f)
            yield return new WaitForSeconds(mentorEntranceCameraHold);

        if (focusController != null)
            focusController.ClearFocusTarget(false);
    }

    private StoryNpcEntranceController ResolvePresentationController(string nodeKey)
    {
        StoryNpcEntranceController[] controllers = FindObjectsByType<StoryNpcEntranceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < controllers.Length; index++)
        {
            StoryNpcEntranceController controller = controllers[index];
            if (controller == null)
                continue;

            if (controller.ControlsNode(nodeKey))
                return controller;
        }

        return null;
    }

    private Chapter1AviSceneController ResolveAviPresentationController(string nodeKey)
    {
        if (string.IsNullOrWhiteSpace(nodeKey))
            return null;

        Chapter1AviSceneController controller = GetComponent<Chapter1AviSceneController>();
        if (controller == null)
            controller = gameObject.AddComponent<Chapter1AviSceneController>();

        return controller.ControlsNode(nodeKey) ? controller : null;
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

    private bool ShouldAutoTransitionWakeNode(GameSession.StoryNodeDetail node)
    {
        return node != null
            && node.canContinue
            && string.Equals(node.nodeKey, WakeTransitionNodeKey, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(wakeTransitionSceneName);
    }

    private void ShowWakeTransitionDialogue(GameSession.StoryNodeDetail node)
    {
        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            ReportError("No DialogueManager was found in the scene.");
            return;
        }

        LockMovement();
        manager.ShowAutoAdvance(GetNodeTitle(node), node.bodyText, BeginWakeTransition);
    }

    private void BeginWakeTransition()
    {
        if (requestInFlight || currentNode == null || !ShouldAutoTransitionWakeNode(currentNode))
            return;

        StartCoroutine(BeginWakeTransitionRoutine(currentNode.nodeKey));
    }

    private IEnumerator BeginWakeTransitionRoutine(string nodeKey)
    {
        requestInFlight = true;

        GameSession.StoryNodeDetail nextNode = null;
        string requestError = null;
        yield return StartCoroutine(GameSession.Instance.ContinueStoryNode(nodeKey, (node, error) =>
        {
            nextNode = node;
            requestError = error;
        }));

        requestInFlight = false;

        if (!string.IsNullOrEmpty(requestError))
        {
            ReportError(requestError);
            yield break;
        }

        if (nextNode == null)
        {
            ReportError("Wake transition did not return the next story node.");
            yield break;
        }

        currentNode = nextNode;
        currentChoices = nextNode.choices ?? new List<GameSession.StoryChoiceDetail>();

        DialogueManager manager = ResolveDialogueManager();
        if (wakeTransitionDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(wakeTransitionDelaySeconds);

        if (manager != null)
            manager.HideDialoguePanel();

        RuntimeSceneTransition.TransitionWithWakeBlink(wakeTransitionSceneName, wakeTransitionSpawnPointId, wakeTransitionBlinkCount);
    }

    private void ShowDialogue(string title, string bodyText, string[] choices, Action<int> onChoiceSelected)
    {
        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            ReportError("No DialogueManager was found in the scene.");
            return;
        }

        LockMovement();
        manager.Show(title, bodyText, choices, onChoiceSelected);
    }

    private void LockMovement()
    {
        PlayerMovement.AddMovementLock(MovementLockId);
        GameState.CanPlayerMove = false;
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager != null && dialogueManager.HasUsableUi)
            return dialogueManager;

        DialogueManager[] managers = FindObjectsByType<DialogueManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < managers.Length; index++)
        {
            DialogueManager candidate = managers[index];
            if (candidate != null && candidate.HasUsableUi)
            {
                dialogueManager = candidate;
                return dialogueManager;
            }
        }

        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);

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