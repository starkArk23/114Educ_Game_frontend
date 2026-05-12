using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 0.8f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private string defaultPromptText = "Press E to interact";

    private IInteractable currentInteractable;
    private string currentPromptText = string.Empty;
    private InteractionPromptUI promptUi;

    private void Update()
    {
        RefreshNearestInteractable();

        if (Input.GetKeyDown(interactionKey) && PlayerMovement.CanMove)
            TryInteract();
    }

    private void TryInteract()
    {
        if (currentInteractable == null)
            RefreshNearestInteractable();

        if (currentInteractable == null)
            return;

        currentInteractable.Interact();
    }

    private void RefreshNearestInteractable()
    {
        if (!PlayerMovement.CanMove)
        {
            SetCurrentInteractable(null, string.Empty);
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactableLayer);
        IInteractable nearestInteractable = null;
        string promptText = string.Empty;
        float nearestDistance = float.MaxValue;

        for (int index = 0; index < hits.Length; index++)
        {
            Collider2D hit = hits[index];
            if (hit == null)
                continue;

            IInteractable interactable = ResolveInteractable(hit);
            if (interactable == null)
                continue;

            Vector2 closestPoint = hit.ClosestPoint(transform.position);
            float distance = ((Vector2)transform.position - closestPoint).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestInteractable = interactable;
            promptText = ResolvePromptText(interactable);
        }

        SetCurrentInteractable(nearestInteractable, promptText);
    }

    private IInteractable ResolveInteractable(Collider2D hit)
    {
        IInteractable interactable = hit.GetComponent<IInteractable>();
        if (interactable == null)
            interactable = hit.GetComponentInParent<IInteractable>();
        if (interactable == null)
            interactable = hit.GetComponentInChildren<IInteractable>();

        return interactable;
    }

    private string ResolvePromptText(IInteractable interactable)
    {
        if (interactable is IInteractionPromptProvider promptProvider)
        {
            if (promptProvider.TryGetInteractionPrompt(out string promptText))
                return promptText ?? string.Empty;

            return string.Empty;
        }

        return defaultPromptText;
    }

    private void SetCurrentInteractable(IInteractable interactable, string promptText)
    {
        currentInteractable = interactable;
        currentPromptText = promptText ?? string.Empty;
        UpdatePromptVisibility();
    }

    private void UpdatePromptVisibility()
    {
        if (currentInteractable == null || string.IsNullOrWhiteSpace(currentPromptText))
        {
            if (promptUi != null)
                promptUi.Hide();
            return;
        }

        ResolvePromptUi().Show(currentPromptText.Trim());
    }

    private InteractionPromptUI ResolvePromptUi()
    {
        if (promptUi == null)
            promptUi = InteractionPromptUI.ResolveOrCreate();

        return promptUi;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}