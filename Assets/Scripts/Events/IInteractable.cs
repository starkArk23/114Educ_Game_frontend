using UnityEngine;

public interface IInteractable
{
    void Interact();
}

public interface IQuestMarkerTarget
{
    bool ShouldShowQuestMarker { get; }
    Transform QuestMarkerAnchor { get; }
    Vector3 QuestMarkerOffset { get; }
}
