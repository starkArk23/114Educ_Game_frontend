using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ScenePortalInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;
    [SerializeField] private string interactionId;
    [SerializeField] private string groupKey;
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private string requiredChapterKey;
    [SerializeField] private string[] activeNodeKeys = Array.Empty<string>();

    private bool requestInFlight;

    public void Interact()
    {
        if (!IsAvailable() || requestInFlight)
            return;

        if (!string.IsNullOrWhiteSpace(interactionId) || !string.IsNullOrWhiteSpace(groupKey))
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

    private bool IsAvailable()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[ScenePortalInteraction] Target scene name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(requiredChapterKey) && (activeNodeKeys == null || activeNodeKeys.Length == 0))
            return true;

        StoryManager manager = ResolveStoryManager();
        if (manager == null)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredChapterKey)
            && !string.Equals(manager.CurrentChapterKey, requiredChapterKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (activeNodeKeys == null || activeNodeKeys.Length == 0)
            return true;

        string currentNodeKey = manager.CurrentNodeKey;
        for (int index = 0; index < activeNodeKeys.Length; index++)
        {
            string nodeKey = activeNodeKeys[index];
            if (string.IsNullOrWhiteSpace(nodeKey))
                continue;

            if (string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
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
}