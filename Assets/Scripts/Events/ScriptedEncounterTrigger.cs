using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ScriptedEncounterTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private CyberEventData eventData;
    [SerializeField] private bool triggerOnEnter = false;
    [SerializeField] private bool oneShot = true;
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
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
}
