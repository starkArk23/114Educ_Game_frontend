using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 0.8f;
    [SerializeField] private LayerMask interactableLayer;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        Collider2D hit = Physics2D.OverlapCircle(
            transform.position,
            interactionRadius,
            interactableLayer
        );
        if (hit == null)
            return;

        IInteractable interactable = hit.GetComponent<IInteractable>();
        if (interactable == null)
            interactable = hit.GetComponentInParent<IInteractable>();
        if (interactable == null)
            interactable = hit.GetComponentInChildren<IInteractable>();

        if (interactable != null)
            interactable.Interact();
        else
            Debug.Log("[PlayerInteraction] No IInteractable found on target.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}