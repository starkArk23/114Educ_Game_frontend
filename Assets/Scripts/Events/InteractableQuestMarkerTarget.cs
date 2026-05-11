using UnityEngine;

[DisallowMultipleComponent]
public class InteractableQuestMarkerTarget : MonoBehaviour, IQuestMarkerTarget
{
    [SerializeField] private MonoBehaviour interactableSource;
    [SerializeField] private Transform questMarkerAnchor;
    [SerializeField] private Vector3 questMarkerOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private bool hideWhenDisabled = true;

    public bool ShouldShowQuestMarker
    {
        get
        {
            if (hideWhenDisabled && (!isActiveAndEnabled || !gameObject.activeInHierarchy))
                return false;

            return ResolveInteractable() != null;
        }
    }

    public Transform QuestMarkerAnchor => questMarkerAnchor != null ? questMarkerAnchor : transform;
    public Vector3 QuestMarkerOffset => questMarkerOffset;

    private void Reset()
    {
        if (interactableSource == null)
            interactableSource = GetComponent<MonoBehaviour>();
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