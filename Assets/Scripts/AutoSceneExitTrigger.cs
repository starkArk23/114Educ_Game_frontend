using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class AutoSceneExitTrigger : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;
    [SerializeField] private string interactionId;
    [SerializeField] private string groupKey;
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private string requiredChapterKey;
    [SerializeField] private string[] activeNodeKeys = Array.Empty<string>();
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool continueCurrentNodeBeforeTransition;

    private Collider2D triggerCollider;
    private bool requestInFlight;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Update()
    {
        if (!enabled || RuntimeSceneTransition.IsTransitioning || requestInFlight)
            return;

        Collider2D playerCollider = ResolvePlayerCollider();
        if (triggerCollider == null || playerCollider == null)
            return;

        if (!triggerCollider.IsTouching(playerCollider))
            return;

        TryHandleTrigger(playerCollider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandleTrigger(other);
    }

    private void TryHandleTrigger(Collider2D other)
    {
        if (!enabled || RuntimeSceneTransition.IsTransitioning || requestInFlight)
            return;

        if (other == null || !other.CompareTag(playerTag))
            return;

        StoryManager manager = ResolveStoryManager();
        if (ShouldContinueCurrentNode(manager))
        {
            StartCoroutine(ContinueStoryThenHandleTrigger());
            return;
        }

        if (!IsAvailable())
            return;

        if (!string.IsNullOrWhiteSpace(interactionId) || !string.IsNullOrWhiteSpace(groupKey))
        {
            StartCoroutine(RegisterInteractionAndTransition());
            return;
        }

        BeginTransition();
    }

    private IEnumerator ContinueStoryThenHandleTrigger()
    {
        StoryManager manager = ResolveStoryManager();
        if (manager == null)
            yield break;

        requestInFlight = true;

        GameSession.StoryNodeDetail storyNode = null;
        string requestError = null;
        yield return StartCoroutine(manager.ContinueCurrentNodeSilently((node, error) =>
        {
            storyNode = node;
            requestError = error;
        }));

        requestInFlight = false;

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            Debug.LogError($"[AutoSceneExitTrigger] {requestError}", this);
            yield break;
        }

        if (storyNode == null)
        {
            Debug.LogWarning("[AutoSceneExitTrigger] Story continuation did not return a node response.", this);
            yield break;
        }

        if (!IsAvailable())
            yield break;

        if (!string.IsNullOrWhiteSpace(interactionId) || !string.IsNullOrWhiteSpace(groupKey))
        {
            yield return StartCoroutine(RegisterInteractionAndTransition());
            yield break;
        }

        BeginTransition();
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
            Debug.LogError($"[AutoSceneExitTrigger] {requestError}", this);
            yield break;
        }

        if (storyNode == null)
        {
            Debug.LogWarning("[AutoSceneExitTrigger] Story interaction did not return a node response.", this);
            yield break;
        }

        BeginTransition();
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void BeginTransition()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[AutoSceneExitTrigger] Target scene name is required.", this);
            return;
        }

        RuntimeSceneTransition.TransitionTo(targetSceneName, targetSpawnPointId);
    }

    private bool IsAvailable()
    {
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

    private bool ShouldContinueCurrentNode(StoryManager manager)
    {
        if (!continueCurrentNodeBeforeTransition || manager == null || !manager.CanContinueCurrentNode)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredChapterKey)
            && !string.Equals(manager.CurrentChapterKey, requiredChapterKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (activeNodeKeys == null || activeNodeKeys.Length == 0)
            return false;

        string currentNodeKey = manager.CurrentNodeKey;
        for (int index = 0; index < activeNodeKeys.Length; index++)
        {
            string nodeKey = activeNodeKeys[index];
            if (string.IsNullOrWhiteSpace(nodeKey))
                continue;

            if (string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private Collider2D ResolvePlayerCollider()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        return player != null ? player.GetComponent<Collider2D>() : null;
    }
}