using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Chapter1AviSceneController : MonoBehaviour
{
    private const string SupportedSceneName = "HallwayScene";
    private const string AviObjectName = "AviStoryNPC";

    private StoryManager storyManager;
    private Transform aviTransform;

    public Transform PresentationTransform => aviTransform != null ? aviTransform : transform;

    private void Awake()
    {
        storyManager = GetComponent<StoryManager>();
        EnsureSceneReferences();
        EnsureGuideInteraction();
    }

    private void Update()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureSceneReferences();
        EnsureGuideInteraction();
    }

    public bool ControlsNode(string nodeKey)
    {
        return false;
    }

    public bool IsPresentationComplete(string nodeKey)
    {
        return true;
    }

    private void EnsureSceneReferences()
    {
        if (storyManager == null)
            storyManager = GetComponent<StoryManager>();

        if (aviTransform == null)
        {
            GameObject aviObject = GameObject.Find(AviObjectName);
            if (aviObject != null)
                aviTransform = aviObject.transform;
        }
    }

    private void EnsureGuideInteraction()
    {
        if (!SupportsCurrentScene() || aviTransform == null)
            return;

        if (aviTransform.GetComponent<Chapter1AviGuideInteraction>() == null)
            aviTransform.gameObject.AddComponent<Chapter1AviGuideInteraction>();
    }

    private static bool SupportsCurrentScene()
    {
        return string.Equals(SceneManager.GetActiveScene().name, SupportedSceneName, StringComparison.Ordinal);
    }
}

[DisallowMultipleComponent]
public class Chapter1AviGuideInteraction : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    private const string MovementLockId = "Chapter1AviGuide";
    private const string ExploreGateNodeKey = "chapter1.explore_gate";

    public void Interact()
    {
        if (!IsAvailable())
            return;

        DialogueManager dialogueManager = FindFirstObjectByType<DialogueManager>();
        if (dialogueManager == null)
        {
            Debug.LogWarning("[Chapter1AviGuideInteraction] No DialogueManager found in scene.", this);
            return;
        }

        PlayerMovement.AddMovementLock(MovementLockId);
        dialogueManager.Show(
            "Avi",
            "Avi seems very focused on her professional monitoring. Best not to disturb her for a while.",
            new[] { "Continue" },
            _ => PlayerMovement.RemoveMovementLock(MovementLockId));
    }

    public bool TryGetInteractionPrompt(out string promptText)
    {
        if (!IsAvailable())
        {
            promptText = string.Empty;
            return false;
        }

        promptText = "Press E to talk";
        return true;
    }

    private bool IsAvailable()
    {
        StoryManager storyManager = FindFirstObjectByType<StoryManager>();
        return storyManager != null
            && string.Equals(storyManager.CurrentNodeKey, ExploreGateNodeKey, StringComparison.Ordinal);
    }
}

[DisallowMultipleComponent]
public class Chapter1RuntimeStoryInteraction : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    private string requiredNodeKey;
    private string interactionId;
    private string groupKey;
    private string speaker;
    private string title;
    private string bodyText;
    private string promptText;

    public void Configure(string requiredNodeKeyValue, string interactionIdValue, string groupKeyValue, string speakerValue, string titleValue, string bodyTextValue, string promptTextValue)
    {
        requiredNodeKey = requiredNodeKeyValue ?? string.Empty;
        interactionId = interactionIdValue ?? string.Empty;
        groupKey = groupKeyValue ?? string.Empty;
        speaker = speakerValue ?? string.Empty;
        title = titleValue ?? string.Empty;
        bodyText = bodyTextValue ?? string.Empty;
        promptText = promptTextValue ?? string.Empty;
    }

    public void Interact()
    {
        if (!IsAvailable())
            return;

        StoryManager storyManager = FindFirstObjectByType<StoryManager>();
        if (storyManager == null)
        {
            Debug.LogWarning("[Chapter1RuntimeStoryInteraction] No StoryManager found in scene.", this);
            return;
        }

        storyManager.HandleWorldInteraction(interactionId, groupKey, speaker, title, bodyText);
    }

    public bool TryGetInteractionPrompt(out string resolvedPromptText)
    {
        if (!IsAvailable())
        {
            resolvedPromptText = string.Empty;
            return false;
        }

        resolvedPromptText = string.IsNullOrWhiteSpace(promptText) ? "Press E to check" : promptText.Trim();
        return true;
    }

    private bool IsAvailable()
    {
        StoryManager storyManager = FindFirstObjectByType<StoryManager>();
        if (storyManager == null)
            return false;

        return string.IsNullOrWhiteSpace(requiredNodeKey)
            || string.Equals(storyManager.CurrentNodeKey, requiredNodeKey, StringComparison.Ordinal);
    }
}