using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryManager : MonoBehaviour
{
    private const string MovementLockId = "StoryDialogue";
    private const string MikeHintChoiceId = "ask_mike";
    private const string WakeTransitionNodeKey = "opening.wake";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string HallwaySceneName = "HallwayScene";
    private const string HallwayArrivalSpawnPointId = "FromRoomScene";
    private const string HallwayArrivalNodeKey = "chapter1.avi_intro";
    private const string HallwayReturnSpawnPointId = "FromHallway";
    private const float RequestStallTimeoutSeconds = 3f;

    private static readonly System.Collections.Generic.HashSet<string> ThreatMusicStartNodeKeys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
    {
        "opening.phone_choice",
        "chapter1.alert_intro",
        "chapter2.alley_intro",
        "chapter3.upload_setup"
    };

    private static readonly System.Collections.Generic.HashSet<string> ThreatMusicEndNodeKeys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
    {
        "opening.mentor_arrives",
        "chapter1.complete",
        "chapter2.complete",
        "chapter3.after_phase2"
    };

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
    private bool pendingMikeHintPresentation;
    private Coroutine presentationRoutine;
    private float requestStartedAt;
    private bool recoveryRequestInFlight;
    private bool suppressPresentation;

    private bool isShuttingDown;

    public string CurrentNodeKey => currentNode?.nodeKey ?? string.Empty;
    public string CurrentChapterKey => currentNode?.chapterKey ?? string.Empty;
    public bool CanContinueCurrentNode => !requestInFlight && currentNode != null && currentNode.canContinue;
    public bool IsRequestInFlight => requestInFlight;

    public bool IsPresentationComplete(string nodeKey)
    {
        if (string.IsNullOrWhiteSpace(nodeKey))
            return true;

        StoryNpcEntranceController mentorPresentation = ResolvePresentationController(nodeKey);
        if (mentorPresentation != null && !mentorPresentation.IsPresentationComplete(nodeKey))
            return false;

        Chapter1AviSceneController aviPresentation = mentorPresentation == null
            ? ResolveAviPresentationController(nodeKey)
            : null;
        if (aviPresentation != null && !aviPresentation.IsPresentationComplete(nodeKey))
            return false;

        return true;
    }

    private void OnEnable()
    {
        isShuttingDown = false;

        if (!autoStartOnEnable)
            return;

        // A pending restore means a saved game is being loaded — always resume from the
        // backend's persisted story node instead of using the inspector startNodeKey.
        if (GameSession.Instance.HasPendingRestore)
        {
            if (!IsHallwayScene())
                ResumeCurrentStory();
            return;
        }

        if (string.IsNullOrWhiteSpace(startNodeKey))
        {
            if (IsHallwayScene())
                return;

            ResumeCurrentStory();
            return;
        }

        StartStory();
    }

    private void OnDisable()
    {
        isShuttingDown = true;
        suppressPresentation = false;

        StopPresentationRoutine();
    }

    private void Start()
    {
        if (!autoStartOnEnable || !string.IsNullOrWhiteSpace(startNodeKey) || !IsHallwayScene())
            return;

        if (TryHandleHallwayArrivalStart())
            return;

        ResumeCurrentStory();
    }

    private void Update()
    {
        if (requestInFlight && !recoveryRequestInFlight)
            RecoverStalledRequestIfNeeded();

        if (!Input.GetKeyDown(KeyCode.H))
            return;

        TryTriggerMikeHint();
    }

    public void StartStory()
    {
        if (requestInFlight)
            return;

        GameSession session = GameSession.Instance;
        MarkRequestStarted();
        StartCoroutine(session.GetCurrentStoryNode(startNodeKey, HandleNodeResponse));
    }

    public void ResumeCurrentStory()
    {
        if (requestInFlight)
            return;

        MarkRequestStarted();
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

        // If the player already advanced past avi_intro (fast click before this routine
        // completed its frame wait), do not re-fetch the arrival node — the backend would
        // return avi_intro as the "current" node regardless of actual progress, causing the
        // dialog to repeat and interrupting the in-progress anchor_intro presentation.
        if (!string.IsNullOrEmpty(CurrentNodeKey))
            yield break;

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

        MarkRequestStarted();
        StartCoroutine(GameSession.Instance.RegisterStoryInteraction(interactionId, groupKey, (node, error) =>
        {
            MarkRequestCompleted();

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
        if (!CanHandleAsyncCallback())
            return;

        MarkRequestCompleted();

        if (!string.IsNullOrEmpty(error))
        {
            pendingMikeHintPresentation = false;
            ReportError(error);
            return;
        }

        if (node == null)
        {
            pendingMikeHintPresentation = false;
            ReportError("Story response was empty.");
            return;
        }

        QueueNodePresentation(node);
    }

    private void QueueNodePresentation(GameSession.StoryNodeDetail node)
    {
        if (!CanHandleAsyncCallback())
            return;

        if (suppressPresentation)
        {
            currentNode = node;
            currentChoices = node?.choices ?? new List<GameSession.StoryChoiceDetail>();

            DialogueManager manager = ResolveDialogueManager();
            if (manager != null)
                manager.HideDialoguePanel();

            return;
        }

        if (ShouldPresentMikeHintOverlay(node))
        {
            currentNode = node;
            currentChoices = node.choices ?? new List<GameSession.StoryChoiceDetail>();
            pendingMikeHintPresentation = false;
            ShowMikeHintOverlay(node);
            return;
        }

        pendingMikeHintPresentation = false;

        StopPresentationRoutine();

        presentationRoutine = StartCoroutine(PresentNodeRoutine(node));
    }

    private void StopPresentationRoutine()
    {
        if (presentationRoutine == null)
            return;

        StopCoroutine(presentationRoutine);
        presentationRoutine = null;

        CameraFocusController focusController = FindFirstObjectByType<CameraFocusController>();
        if (focusController != null)
            focusController.ClearFocusTarget(false);
    }

    private bool CanHandleAsyncCallback()
    {
        return !isShuttingDown && this != null && gameObject != null && isActiveAndEnabled;
    }

    private void RecoverStalledRequestIfNeeded()
    {
        if (Time.unscaledTime - requestStartedAt < RequestStallTimeoutSeconds)
            return;

        recoveryRequestInFlight = true;
        requestInFlight = false;
        pendingMikeHintPresentation = false;
        Debug.LogWarning("[StoryManager] Story request stalled; resyncing current node from backend.", this);
        StartCoroutine(RecoverStalledRequestRoutine());
    }

    private IEnumerator RecoverStalledRequestRoutine()
    {
        yield return StartCoroutine(GameSession.Instance.GetCurrentStoryNode(null, (node, error) =>
        {
            recoveryRequestInFlight = false;
            HandleNodeResponse(node, error);
        }));
    }

    private void MarkRequestStarted()
    {
        requestInFlight = true;
        requestStartedAt = Time.unscaledTime;
    }

    private void MarkRequestCompleted()
    {
        requestInFlight = false;
        requestStartedAt = 0f;
    }

    private System.Collections.IEnumerator PresentNodeRoutine(GameSession.StoryNodeDetail node)
    {
        currentNode = node;
        currentChoices = node.choices ?? new List<GameSession.StoryChoiceDetail>();

        // Capture the scene name before any yield so that scene transitions that occur
        // while WaitForPresentationGate is running do not corrupt the suppression checks below.
        bool startedInHallwayScene = IsHallwayScene();

        // Switch background music based on threat phase transitions.
        if (ThreatMusicStartNodeKeys.Contains(node.nodeKey))
            GameSession.Instance.PlayThreatMusicOverride();
        else if (ThreatMusicEndNodeKeys.Contains(node.nodeKey))
            GameSession.Instance.ClearThreatMusicOverride();

        // opening.wake must not trigger in the hallway — the player must first walk through
        // the hallway exit point into SystemCoreScene, where the wake blink fires normally.
        if (startedInHallwayScene && string.Equals(node.nodeKey, WakeTransitionNodeKey, StringComparison.Ordinal))
        {
            presentationRoutine = null;
            yield break;
        }

        // opening.wake must not re-fire when returning to this scene from HallwayScene.
        // The FromHallway spawn marks a post-hallway arrival, not the initial wake-up path.
        if (string.Equals(node.nodeKey, WakeTransitionNodeKey, StringComparison.Ordinal)
            && RuntimeSceneTransition.ConsumeLatestArrival(SceneManager.GetActiveScene().name, HallwayReturnSpawnPointId))
        {
            presentationRoutine = null;
            yield break;
        }

        if (node.gateProgress != null && choiceLogUI != null)
            choiceLogUI.Show($"Progress: {node.gateProgress.currentCount}/{node.gateProgress.requiredCount}");

        yield return WaitForPresentationGate(node);

        // Guard against this coroutine continuing after the StoryManager is torn down
        // (e.g. the scene changed while WaitForPresentationGate was running its camera hold).
        if (!CanHandleAsyncCallback())
        {
            presentationRoutine = null;
            yield break;
        }

        // anchor_intro dialog belongs in SystemCoreScene (where the anchor object lives).
        // Use startedInHallwayScene (captured before yields) instead of IsHallwayScene() to
        // prevent a race where the scene has already transitioned to SystemCoreScene by the
        // time WaitForPresentationGate finishes, which would make IsHallwayScene() return
        // false and cause the dialog to surface prematurely in the hallway context.
        if (startedInHallwayScene && string.Equals(node.nodeKey, AnchorIntroNodeKey, StringComparison.Ordinal))
        {
            ReleaseMovement();
            presentationRoutine = null;
            yield break;
        }

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
        bool mentorPending = mentorPresentation != null && !mentorPresentation.IsPresentationComplete(node.nodeKey);
        Chapter1AviSceneController aviPresentation = mentorPresentation == null
            ? ResolveAviPresentationController(node.nodeKey)
            : null;
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
        {
            focusController.ClearFocusTarget(false);

            if (focusController.SmoothTime > 0f)
                yield return new WaitForSeconds(focusController.SmoothTime);
        }
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
        pendingMikeHintPresentation = IsMikeHintChoice(selectedChoice);
        requestInFlight = true;
        StartCoroutine(GameSession.Instance.SubmitStoryChoice(currentNode.nodeKey, selectedChoice.id, HandleNodeResponse));
    }

    private void TryTriggerMikeHint()
    {
        if (requestInFlight || currentNode == null)
        MarkRequestCompleted();

        if (!TryGetMikeHintChoice(out GameSession.StoryChoiceDetail mikeHintChoice))
            return;

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
            return;

        pendingMikeHintPresentation = true;
        requestInFlight = true;
        StartCoroutine(GameSession.Instance.SubmitStoryChoice(currentNode.nodeKey, mikeHintChoice.id, HandleNodeResponse));
    }

    private void ContinueCurrentNode()
    {
        if (requestInFlight || currentNode == null)
            return;

        requestInFlight = true;
        StartCoroutine(GameSession.Instance.ContinueStoryNode(currentNode.nodeKey, HandleNodeResponse));
    }

    private bool TryGetMikeHintChoice(out GameSession.StoryChoiceDetail mikeHintChoice)
    {
        if (currentChoices != null)
        {
            for (int index = 0; index < currentChoices.Count; index++)
            {
                GameSession.StoryChoiceDetail candidate = currentChoices[index];
                if (IsMikeHintChoice(candidate))
                {
                    mikeHintChoice = candidate;
                    return true;
                }
            }
        }

        mikeHintChoice = null;
        return false;
    }

    private static bool IsMikeHintChoice(GameSession.StoryChoiceDetail choice)
    {
        if (choice == null)
            return false;

        if (string.Equals(choice.id, MikeHintChoiceId, StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(choice.label)
            && choice.label.IndexOf("MIKE", StringComparison.OrdinalIgnoreCase) >= 0
            && choice.trustTokenCost > 0;
    }

    private bool ShouldPresentMikeHintOverlay(GameSession.StoryNodeDetail node)
    {
        return pendingMikeHintPresentation
            && node != null
            && string.Equals(node.speaker, "Mike", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowMikeHintOverlay(GameSession.StoryNodeDetail node)
    {
        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            ReportError("No DialogueManager was found in the scene.");
            return;
        }

        LockMovement();
        manager.ShowHintOverlay(GetNodeTitle(node), node.bodyText, ContinueFromMikeHint);
    }

    private void ContinueFromMikeHint()
    {
        DialogueManager manager = ResolveDialogueManager();
        if (manager != null)
            manager.HideHintOverlay();

        if (currentNode != null && currentNode.canContinue)
        {
            ContinueCurrentNode();
            return;
        }

        CloseStoryDialogue();
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
        suppressPresentation = true;
        LockMovement();

        DialogueManager manager = ResolveDialogueManager();
        if (manager != null)
            manager.HideDialoguePanel();

        BeginWakeTransition();
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
        if (dialogueManager != null && dialogueManager.isActiveAndEnabled && dialogueManager.HasUsableUi)
            return dialogueManager;

        DialogueManager[] managers = FindObjectsByType<DialogueManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        DialogueManager enabledCandidate = null;
        DialogueManager fallbackCandidate = null;

        for (int index = 0; index < managers.Length; index++)
        {
            DialogueManager candidate = managers[index];
            if (candidate == null)
                continue;

            if (candidate.isActiveAndEnabled && candidate.HasUsableUi)
            {
                enabledCandidate = candidate;
                break;
            }

            if (fallbackCandidate == null && candidate.HasUsableUi)
                fallbackCandidate = candidate;
        }

        dialogueManager = enabledCandidate ?? fallbackCandidate;

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
        DialogueManager manager = ResolveDialogueManager();
        if (manager != null)
            manager.HideHintOverlay();

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