using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ScriptedEncounterTrigger : MonoBehaviour, IInteractable, IQuestMarkerTarget
{
    [SerializeField] private CyberEventData eventData;
    [SerializeField] private bool triggerOnEnter = false;
    [SerializeField] private bool oneShot = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform questMarkerAnchor;
    [SerializeField] private Vector3 questMarkerOffset = new Vector3(0f, 2.1f, 0f);

    private bool hasTriggered;

    public bool ShouldShowQuestMarker
    {
        get
        {
            if (hasTriggered)
                return false;

            if (eventData == null)
                return false;

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return false;

            EventManager manager = EventManager.Instance;
            if (manager == null)
                return true;

            return manager.IsEventAvailable(eventData);
        }
    }

    public Transform QuestMarkerAnchor => questMarkerAnchor != null ? questMarkerAnchor : transform;
    public Vector3 QuestMarkerOffset => questMarkerOffset;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        EnsureQuestMarker();
    }

    private void Awake()
    {
        EnsureQuestMarker();
    }

    public void Interact()
    {
        if (triggerOnEnter)
            return;

        TryTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnEnter)
            return;

        if (!other.CompareTag(playerTag))
            return;

        TryTrigger();
    }

    private void TryTrigger()
    {
        if (hasTriggered)
            return;

        if (eventData == null)
        {
            Debug.LogWarning("[ScriptedEncounterTrigger] No eventData assigned.");
            return;
        }

        if (EventManager.Instance == null)
        {
            Debug.LogWarning("[ScriptedEncounterTrigger] No EventManager in scene.");
            return;
        }

        Debug.Log($"[ScriptedEncounterTrigger] Triggering event: {eventData.eventId}");
        EventManager.Instance.StartEvent(eventData);

        if (oneShot)
        {
            hasTriggered = true;
            gameObject.SetActive(false);
        }
    }

    private void EnsureQuestMarker()
    {
        if (GetComponent<QuestMarker>() == null)
            gameObject.AddComponent<QuestMarker>();
    }
}
