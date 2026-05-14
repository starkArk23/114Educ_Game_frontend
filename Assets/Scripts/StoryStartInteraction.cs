using UnityEngine;

public class StoryStartInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private StoryManager storyManager;

    public void Interact()
    {
        StoryManager manager = ResolveStoryManager();
        if (manager == null)
        {
            Debug.LogWarning("[StoryStartInteraction] No StoryManager found in scene.");
            return;
        }

        manager.ResumeCurrentStory();
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }
}