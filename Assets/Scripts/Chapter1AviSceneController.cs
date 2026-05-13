using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Chapter1AviSceneController : MonoBehaviour
{
    private const string SupportedSceneName = "HallwayScene";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string ExploreGateNodeKey = "chapter1.explore_gate";
    private const string AviObjectName = "AviStoryNPC";
    private const string AnchorObjectName = "HallwayRightMarker";
    private const string SpawnObjectName = "FromRoomScene";
    private const string LightPanelObjectName = "FloatingLightPanelPoint";
    private const string ThreatMonitorObjectName = "ThreatMonitorPoint";
    private const string CoreConsoleObjectName = "CoreConsolePoint";
    private static readonly Vector3 AnchorStandOffset = new Vector3(-0.8f, 0.15f, 0f);
    private static readonly Vector3 LightPanelPosition = new Vector3(-10.15f, 0.78f, 0f);
    private static readonly Vector3 ThreatMonitorPosition = new Vector3(-9.25f, -0.32f, 0f);
    private static readonly Vector3 CoreConsolePosition = new Vector3(-8.35f, 0.82f, 0f);
    private static Sprite panelSprite;

    [SerializeField] private float moveSpeed = 1.75f;

    private StoryManager storyManager;
    private Transform aviTransform;
    private Transform anchorTransform;
    private Transform spawnTransform;
    private SpriteRenderer aviSpriteRenderer;
    private bool anchorPresentationComplete;
    private bool runtimeInteractionsReady;

    public Transform PresentationTransform => aviTransform != null ? aviTransform : transform;

    private void Awake()
    {
        storyManager = GetComponent<StoryManager>();
        EnsureSceneReferences();
        EnsureGuideInteraction();
        EnsureRuntimeInteractions();
    }

    private void Update()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureSceneReferences();
        EnsureGuideInteraction();
        EnsureRuntimeInteractions();

        if (storyManager == null || aviTransform == null || anchorTransform == null)
            return;

        if (string.Equals(storyManager.CurrentNodeKey, AnchorIntroNodeKey, StringComparison.Ordinal))
        {
            UpdateAnchorMovement();
            return;
        }

        if (!anchorPresentationComplete && string.Equals(storyManager.CurrentNodeKey, ExploreGateNodeKey, StringComparison.Ordinal))
            anchorPresentationComplete = true;

        if (anchorPresentationComplete)
            aviTransform.position = GetAnchorStandPosition();
    }

    public bool ControlsNode(string nodeKey)
    {
        if (!SupportsCurrentScene())
            return false;

        EnsureSceneReferences();
        return aviTransform != null
            && anchorTransform != null
            && string.Equals(nodeKey, AnchorIntroNodeKey, StringComparison.Ordinal);
    }

    public bool IsPresentationComplete(string nodeKey)
    {
        if (!ControlsNode(nodeKey))
            return true;

        return anchorPresentationComplete;
    }

    private void UpdateAnchorMovement()
    {
        Vector3 targetPosition = GetAnchorStandPosition();
        Vector3 currentPosition = aviTransform.position;
        Vector3 delta = targetPosition - currentPosition;

        if (delta.sqrMagnitude <= 0.0004f)
        {
            aviTransform.position = targetPosition;
            anchorPresentationComplete = true;
            return;
        }

        aviTransform.position = Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * Time.deltaTime);
        UpdateFacing(delta);
    }

    private void UpdateFacing(Vector3 delta)
    {
        if (aviSpriteRenderer == null || Mathf.Abs(delta.x) <= 0.01f)
            return;

        aviSpriteRenderer.flipX = delta.x < 0f;
    }

    private Vector3 GetAnchorStandPosition()
    {
        return anchorTransform.position + AnchorStandOffset;
    }

    private void EnsureSceneReferences()
    {
        if (storyManager == null)
            storyManager = GetComponent<StoryManager>();

        if (aviTransform == null)
        {
            GameObject aviObject = GameObject.Find(AviObjectName);
            if (aviObject != null)
            {
                aviTransform = aviObject.transform;
                aviSpriteRenderer = aviObject.GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        if (anchorTransform == null)
        {
            GameObject anchorObject = GameObject.Find(AnchorObjectName);
            if (anchorObject != null)
                anchorTransform = anchorObject.transform;
        }

        if (spawnTransform == null)
        {
            GameObject spawnObject = GameObject.Find(SpawnObjectName);
            if (spawnObject != null)
                spawnTransform = spawnObject.transform;
        }
    }

    private void EnsureGuideInteraction()
    {
        if (!SupportsCurrentScene() || aviTransform == null)
            return;

        if (aviTransform.GetComponent<Chapter1AviGuideInteraction>() == null)
            aviTransform.gameObject.AddComponent<Chapter1AviGuideInteraction>();
    }

    private void EnsureRuntimeInteractions()
    {
        if (runtimeInteractionsReady || !SupportsCurrentScene())
            return;

        EnsureSceneReferences();
        EnsureAnchorInteraction();
        EnsureStoryProp(
            LightPanelObjectName,
            LightPanelPosition,
            new Vector2(1.15f, 0.4f),
            new Color(0.75f, 0.95f, 1f, 0.9f),
            "chapter1.light_panel",
            "Floating Light Panel",
            "A translucent panel hovers near the wall, softly shifting between cool white and pale blue. You tap it, and the lighting subtly changes.\n\nAvi: Oh! That adjusts the lighting.\n\n[She squints up at the ceiling.]\n\nAvi: The Mentor says bright lights help with focus... but honestly, they just strain my eyes. Don't tell him I said that...",
            "Press E to check");
        EnsureStoryProp(
            ThreatMonitorObjectName,
            ThreatMonitorPosition,
            new Vector2(1.05f, 0.55f),
            new Color(0.67f, 0.98f, 0.9f, 0.92f),
            "chapter1.monitor",
            "Threat Monitor",
            "A monitor scrolls through low-priority anomaly pings. Avi keeps sneaking glances back to make sure none of them spike while you're looking around.",
            "Press E to check");
        EnsureStoryProp(
            CoreConsoleObjectName,
            CoreConsolePosition,
            new Vector2(0.95f, 0.5f),
            new Color(0.9f, 0.88f, 1f, 0.92f),
            "chapter1.console",
            "Core Console",
            "A maintenance console scrolls through integrity checks and routing logs. Avi uses stations like this to catch weak signals before they become real threats.",
            "Press E to check");

        runtimeInteractionsReady = true;
    }

    private void EnsureAnchorInteraction()
    {
        if (anchorTransform == null)
            return;

        GameObject anchorObject = anchorTransform.gameObject;
        anchorObject.layer = 3;

        BoxCollider2D collider = anchorObject.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = anchorObject.AddComponent<BoxCollider2D>();

        collider.size = new Vector2(0.95f, 1.2f);
        collider.offset = Vector2.zero;
        collider.isTrigger = false;

        Chapter1RuntimeStoryInteraction interaction = anchorObject.GetComponent<Chapter1RuntimeStoryInteraction>();
        if (interaction == null)
            interaction = anchorObject.AddComponent<Chapter1RuntimeStoryInteraction>();

        interaction.Configure(
            AnchorIntroNodeKey,
            "chapter1.anchor",
            "chapter1.anchor_activation",
            "SYSTEM",
            "System Anchor",
            "The crystal glows brighter for a moment. A faint hum resonates through the hall.",
            "Press E to check");
    }

    private void EnsureStoryProp(string objectName, Vector3 worldPosition, Vector2 colliderSize, Color color, string interactionId, string title, string bodyText, string prompt)
    {
        GameObject storyProp = GameObject.Find(objectName);
        if (storyProp == null)
        {
            storyProp = new GameObject(objectName);
            storyProp.layer = 3;
            storyProp.transform.position = worldPosition;

            SpriteRenderer spriteRenderer = storyProp.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ResolvePanelSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingLayerName = "ActorsFront";
            spriteRenderer.sortingOrder = 3;
            storyProp.transform.localScale = new Vector3(colliderSize.x, colliderSize.y, 1f);

            BoxCollider2D collider = storyProp.AddComponent<BoxCollider2D>();
            collider.size = colliderSize;
        }

        Chapter1RuntimeStoryInteraction interaction = storyProp.GetComponent<Chapter1RuntimeStoryInteraction>();
        if (interaction == null)
            interaction = storyProp.AddComponent<Chapter1RuntimeStoryInteraction>();

        interaction.Configure(
            ExploreGateNodeKey,
            interactionId,
            "chapter1.room_explore",
            "Avi",
            title,
            bodyText,
            prompt);
    }

    private static Sprite ResolvePanelSprite()
    {
        if (panelSprite != null)
            return panelSprite;

        panelSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            16f);
        panelSprite.name = LightPanelObjectName;
        return panelSprite;
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