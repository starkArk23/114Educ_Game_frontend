using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ScenePortalInteraction : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    private enum PortalAvailability
    {
        Unavailable,
        StoryState,
        CompletedNode
    }

    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;
    [SerializeField] private string interactionId;
    [SerializeField] private string groupKey;
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private string requiredChapterKey;
    [SerializeField] private string[] activeNodeKeys = Array.Empty<string>();
    [SerializeField] private string[] completedNodeKeys = Array.Empty<string>();
    [SerializeField] private string promptText = "Press E to enter";
    [SerializeField] private string unavailablePromptText;

    private bool requestInFlight;

    public void Interact()
    {
        PortalAvailability availability = ResolveAvailability();
        if (availability == PortalAvailability.Unavailable || requestInFlight)
            return;

        if (availability == PortalAvailability.StoryState
            && (!string.IsNullOrWhiteSpace(interactionId) || !string.IsNullOrWhiteSpace(groupKey)))
        {
            StartCoroutine(RegisterInteractionAndTransition());
            return;
        }

        RuntimeSceneTransition.TransitionTo(targetSceneName, targetSpawnPointId);
    }

    private IEnumerator RegisterInteractionAndTransition()
    {
        requestInFlight = true;

        GameSession.StoryNodeDetail storyNode = null;
        string requestError = null;
        yield return StartCoroutine(GameSession.Instance.RegisterStoryInteraction(interactionId, groupKey, (node, error) =>
        {
            storyNode = node;
            requestError = error;
        }));

        requestInFlight = false;

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            Debug.LogError($"[ScenePortalInteraction] {requestError}");
            yield break;
        }

        if (storyNode == null)
        {
            Debug.LogWarning("[ScenePortalInteraction] Story interaction did not return a node response.");
            yield break;
        }

        RuntimeSceneTransition.TransitionTo(targetSceneName, targetSpawnPointId);
    }

    private PortalAvailability ResolveAvailability()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[ScenePortalInteraction] Target scene name is required.");
            return PortalAvailability.Unavailable;
        }

        if (HasCompletedNodeUnlock())
            return PortalAvailability.CompletedNode;

        if (string.IsNullOrWhiteSpace(requiredChapterKey) && (activeNodeKeys == null || activeNodeKeys.Length == 0))
            return PortalAvailability.StoryState;

        StoryManager manager = ResolveStoryManager();
        if (manager == null)
            return PortalAvailability.Unavailable;

        if (!string.IsNullOrWhiteSpace(requiredChapterKey)
            && !string.Equals(manager.CurrentChapterKey, requiredChapterKey, StringComparison.Ordinal))
        {
            return PortalAvailability.Unavailable;
        }

        if (activeNodeKeys == null || activeNodeKeys.Length == 0)
            return PortalAvailability.StoryState;

        string currentNodeKey = manager.CurrentNodeKey;
        for (int index = 0; index < activeNodeKeys.Length; index++)
        {
            string nodeKey = activeNodeKeys[index];
            if (string.IsNullOrWhiteSpace(nodeKey))
                continue;

            if (string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
                return PortalAvailability.StoryState;
        }

        return PortalAvailability.Unavailable;
    }

    private bool HasCompletedNodeUnlock()
    {
        if (completedNodeKeys == null || completedNodeKeys.Length == 0)
            return false;

        GameSession session = GameSession.Instance;
        if (session == null)
            return false;

        for (int index = 0; index < completedNodeKeys.Length; index++)
        {
            string nodeKey = completedNodeKeys[index];
            if (string.IsNullOrWhiteSpace(nodeKey))
                continue;

            if (session.HasCompletedStoryNode(nodeKey))
                return true;
        }

        return false;
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }

    public bool TryGetInteractionPrompt(out string resolvedPromptText)
    {
        if (ResolveAvailability() != PortalAvailability.Unavailable)
        {
            resolvedPromptText = string.IsNullOrWhiteSpace(promptText) ? "Press E to enter" : promptText.Trim();
            return true;
        }

        resolvedPromptText = string.IsNullOrWhiteSpace(unavailablePromptText) ? string.Empty : unavailablePromptText.Trim();
        return !string.IsNullOrWhiteSpace(resolvedPromptText);
    }
}