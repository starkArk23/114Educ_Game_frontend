using System;
using UnityEngine;

[DisallowMultipleComponent]
public class InspectableInteraction : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    private const string MovementLockId = "InspectableInteraction";

    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private string speaker;
    [SerializeField] private string title;
    [TextArea(2, 8)]
    [SerializeField] private string bodyText;
    [SerializeField] private string buttonLabel = "Continue";
    [SerializeField] private string promptText = "Press E to inspect";

    public void Interact()
    {
        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("[InspectableInteraction] No DialogueManager found in scene.");
            return;
        }

        string header = !string.IsNullOrWhiteSpace(title)
            ? title
            : (string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker);
        string[] options = { string.IsNullOrWhiteSpace(buttonLabel) ? "Continue" : buttonLabel.Trim() };

        PlayerMovement.AddMovementLock(MovementLockId);
        GameState.CanPlayerMove = false;
        manager.Show(header, bodyText ?? string.Empty, options, _ => CloseDialogue());
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>();

        return dialogueManager;
    }

    private void CloseDialogue()
    {
        GameState.CanPlayerMove = true;
        PlayerMovement.RemoveMovementLock(MovementLockId);
    }

    public bool TryGetInteractionPrompt(out string resolvedPromptText)
    {
        resolvedPromptText = string.IsNullOrWhiteSpace(promptText) ? "Press E to inspect" : promptText.Trim();
        return true;
    }
}