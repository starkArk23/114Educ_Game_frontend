using UnityEngine;

public class StoryInteractionPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private string interactionId;
    [SerializeField] private string groupKey;
    [SerializeField] private string speaker;
    [SerializeField] private string title;
    [TextArea(2, 8)]
    [SerializeField] private string bodyText;

    public void Interact()
    {
        StoryManager manager = ResolveStoryManager();
        if (manager == null)
        {
            Debug.LogWarning("[StoryInteractionPoint] No StoryManager found in scene.");
            return;
        }

        manager.HandleWorldInteraction(interactionId, groupKey, speaker, title, bodyText);
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }
}