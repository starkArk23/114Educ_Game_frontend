using UnityEngine;

public interface IInteractable
{
    void Interact();
}

public interface IInteractionPromptProvider
{
    bool TryGetInteractionPrompt(out string promptText);
}

public interface IQuestMarkerTarget
{
    bool ShouldShowQuestMarker { get; }
    Transform QuestMarkerAnchor { get; }
    Vector3 QuestMarkerOffset { get; }
}
