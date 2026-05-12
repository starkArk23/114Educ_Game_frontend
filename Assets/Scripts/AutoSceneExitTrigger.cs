using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class AutoSceneExitTrigger : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;
    [SerializeField] private string playerTag = "Player";

    private Collider2D triggerCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled || RuntimeSceneTransition.IsTransitioning)
            return;

        if (other == null || !other.CompareTag(playerTag))
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[AutoSceneExitTrigger] Target scene name is required.", this);
            return;
        }

        RuntimeSceneTransition.TransitionTo(targetSceneName, targetSpawnPointId);
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}