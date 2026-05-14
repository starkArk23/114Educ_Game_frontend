using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class AutoSceneExitTrigger : MonoBehaviour
{
    private const int OverlapBufferSize = 8;

    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;
    [SerializeField] private string interactionId;
    [SerializeField] private string groupKey;
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private string requiredChapterKey;
    [SerializeField] private string[] activeNodeKeys = Array.Empty<string>();
    [SerializeField] private string[] continueNodeKeys = Array.Empty<string>();
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool continueCurrentNodeBeforeTransition;

    private Collider2D triggerCollider;
    private bool requestInFlight;
    private readonly Collider2D[] overlapResults = new Collider2D[OverlapBufferSize];

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

        if (triggerCollider == null)
            return;

        Collider2D playerCollider = FindOverlappingPlayerCollider();
        if (playerCollider == null)
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

        Collider2D playerCollider = ResolvePlayerCollider(other);
        if (playerCollider == null)
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

            if (!string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
                continue;

            return manager.IsPresentationComplete(currentNodeKey);
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

        if (continueNodeKeys == null || continueNodeKeys.Length == 0)
            return false;

        string currentNodeKey = manager.CurrentNodeKey;
        for (int index = 0; index < continueNodeKeys.Length; index++)
        {
            string nodeKey = continueNodeKeys[index];
            if (string.IsNullOrWhiteSpace(nodeKey))
                continue;

            if (string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private Collider2D FindOverlappingPlayerCollider()
    {
        ContactFilter2D filter = default;
        filter.useTriggers = true;

        int hitCount = triggerCollider.Overlap(filter, overlapResults);
        for (int index = 0; index < hitCount; index++)
        {
            Collider2D playerCollider = ResolvePlayerCollider(overlapResults[index]);
            if (playerCollider != null)
                return playerCollider;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return null;

        Collider2D playerRootCollider = player.GetComponent<Collider2D>();
        return playerRootCollider != null && triggerCollider.IsTouching(playerRootCollider)
            ? playerRootCollider
            : null;
    }

    private Collider2D ResolvePlayerCollider(Collider2D candidate)
    {
        if (candidate == null)
            return null;

        if (candidate.CompareTag(playerTag))
            return candidate;

        Rigidbody2D attachedBody = candidate.attachedRigidbody;
        if (attachedBody != null)
        {
            GameObject attachedObject = attachedBody.gameObject;
            if (attachedObject != null && attachedObject.CompareTag(playerTag))
                return candidate;
        }

        Transform root = candidate.transform.root;
        if (root != null && root.CompareTag(playerTag))
            return candidate;

        return null;
    }
}