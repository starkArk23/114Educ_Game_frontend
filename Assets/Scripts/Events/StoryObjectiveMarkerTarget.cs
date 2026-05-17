using System;
using UnityEngine;

[DisallowMultipleComponent]
public class StoryObjectiveMarkerTarget : MonoBehaviour, IQuestMarkerTarget
{
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private MonoBehaviour interactableSource;
    [SerializeField] private string requiredChapterKey;
    [SerializeField] private string[] activeNodeKeys = Array.Empty<string>();
    [SerializeField] private Transform questMarkerAnchor;
    [SerializeField] private Vector3 questMarkerOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private bool hideWhenDisabled = true;
    [SerializeField] private bool requireInteractable = true;

    public bool ShouldShowQuestMarker
    {
        get
        {
            if (hideWhenDisabled && (!isActiveAndEnabled || !gameObject.activeInHierarchy))
                return false;

            if (requireInteractable && ResolveInteractable() == null)
                return false;

            StoryManager manager = ResolveStoryManager();
            if (manager == null)
                return false;

            if (!string.IsNullOrWhiteSpace(requiredChapterKey) &&
                !string.Equals(manager.CurrentChapterKey, requiredChapterKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (activeNodeKeys == null || activeNodeKeys.Length == 0)
                return !string.IsNullOrWhiteSpace(requiredChapterKey);

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
    }

    public Transform QuestMarkerAnchor => questMarkerAnchor != null ? questMarkerAnchor : transform;
    public Vector3 QuestMarkerOffset => questMarkerOffset;

    /// <summary>
    /// Configure this target at runtime (e.g. from a scene-setup script that adds
    /// interactable components dynamically). Call before the <see cref="QuestMarker"/>
    /// that references this target runs its first <c>LateUpdate</c>.
    /// </summary>
    public void ConfigureRuntime(string chapterKey, string[] nodeKeys, bool requireInteractableValue)
    {
        requiredChapterKey = chapterKey ?? string.Empty;
        activeNodeKeys = nodeKeys ?? Array.Empty<string>();
        requireInteractable = requireInteractableValue;
    }

    private void Reset()
    {
        if (interactableSource == null)
            interactableSource = GetComponent<MonoBehaviour>();
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }

    private IInteractable ResolveInteractable()
    {
        if (interactableSource != null)
            return interactableSource as IInteractable;

        return GetComponent<IInteractable>()
            ?? GetComponentInParent<IInteractable>()
            ?? GetComponentInChildren<IInteractable>();
    }
}