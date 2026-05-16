using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Chapter1AviSceneController : MonoBehaviour
{
    private const string SupportedSceneName = "HallwayScene";
    private const string AviIntroNodeKey = "chapter1.avi_intro";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string AviObjectName = "AviStoryNPC";
    private const string AnchorObjectName = "HallwayRightMarker";
    private const string HallwayExitObjectName = "HallwayReturnExit";
    private static readonly Vector3 ExitTargetOffset = new Vector3(0f, 0f, 0f);

    private StoryManager storyManager;
    private Transform aviTransform;
    private SpriteRenderer aviSpriteRenderer;
    private Vector3 aviStartPosition;
    private Vector3 aviExitTargetPosition;
    private bool hasCachedAviStartPosition;
    private bool hasExitTargetPosition;
    private bool exitPresentationStarted;
    private bool exitPresentationComplete;
    private float exitAnimationTimeOffset;
    private Sprite aviIdleSprite;
    private Sprite[] aviRightWalkSprites;

    public Transform PresentationTransform => aviTransform != null ? aviTransform : transform;

    private void Awake()
    {
        storyManager = GetComponent<StoryManager>();
        exitAnimationTimeOffset = UnityEngine.Random.Range(0f, 100f);
        EnsureSceneReferences();
        EnsureGuideInteraction();
        EnsureAnchorInteraction();
    }

    private void Update()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureSceneReferences();
        EnsureGuideInteraction();
        EnsureAnchorInteraction();
        UpdateExitPresentation();
    }

    public bool ControlsNode(string nodeKey)
    {
        if (!SupportsCurrentScene() || string.IsNullOrWhiteSpace(nodeKey))
            return false;

        return string.Equals(nodeKey, AviIntroNodeKey, StringComparison.Ordinal)
            || string.Equals(nodeKey, AnchorIntroNodeKey, StringComparison.Ordinal)
            || string.Equals(nodeKey, "chapter1.explore_gate", StringComparison.Ordinal);
    }

    public bool IsPresentationComplete(string nodeKey)
    {
        if (!string.Equals(nodeKey, AnchorIntroNodeKey, StringComparison.Ordinal))
            return true;

        return exitPresentationComplete;
    }

    private void EnsureSceneReferences()
    {
        if (storyManager == null)
            storyManager = GetComponent<StoryManager>();

        if (aviTransform == null)
        {
            GameObject aviObject = GameObject.Find(AviObjectName);
            if (aviObject != null)
                aviTransform = aviObject.transform;
        }

        if (aviTransform != null && aviSpriteRenderer == null)
            aviSpriteRenderer = aviTransform.GetComponentInChildren<SpriteRenderer>(true);

        if (aviTransform != null && !hasCachedAviStartPosition)
        {
            aviStartPosition = aviTransform.position;
            hasCachedAviStartPosition = true;
        }

        if (!hasExitTargetPosition)
        {
            GameObject exitObject = GameObject.Find(HallwayExitObjectName);
            if (exitObject != null)
            {
                aviExitTargetPosition = exitObject.transform.position + ExitTargetOffset;
                hasExitTargetPosition = true;
            }
        }

        if (aviSpriteRenderer != null && aviIdleSprite == null)
            aviIdleSprite = aviSpriteRenderer.sprite;

        EnsureWalkSpritesLoaded();
    }

    private void EnsureGuideInteraction()
    {
        if (!SupportsCurrentScene() || aviTransform == null)
            return;

        aviTransform.gameObject.layer = 3;

        BoxCollider2D collider = aviTransform.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = aviTransform.gameObject.AddComponent<BoxCollider2D>();

        collider.enabled = true;
        collider.isTrigger = false;
        collider.offset = new Vector2(-0.05f, -0.71f);
        collider.size = new Vector2(1.17f, 2.46f);

        if (aviTransform.GetComponent<Chapter1AviGuideInteraction>() == null)
            aviTransform.gameObject.AddComponent<Chapter1AviGuideInteraction>();
    }

    private void EnsureAnchorInteraction()
    {
        GameObject anchorObject = GameObject.Find(AnchorObjectName);
        if (anchorObject == null)
            return;

        anchorObject.layer = 3;

        Chapter1RuntimeStoryInteraction interaction = anchorObject.GetComponent<Chapter1RuntimeStoryInteraction>();
        if (interaction == null)
            interaction = anchorObject.AddComponent<Chapter1RuntimeStoryInteraction>();

        interaction.Configure(
            AnchorIntroNodeKey,
            "chapter1.anchor",
            "chapter1.anchor_activation",
            "SYSTEM",
            Chapter1StoryText.AnchorTitle,
            Chapter1StoryText.AnchorBody,
            Chapter1StoryText.DefaultInteractionPrompt);
    }

    private void UpdateExitPresentation()
    {
        if (aviTransform == null || aviSpriteRenderer == null)
            return;

        string currentNodeKey = storyManager != null ? storyManager.CurrentNodeKey : string.Empty;
        bool shouldRunExitPresentation = string.Equals(currentNodeKey, AnchorIntroNodeKey, StringComparison.Ordinal);

        if (exitPresentationComplete)
        {
            SetAviVisible(false);
            return;
        }

        if (!shouldRunExitPresentation)
        {
            if (hasCachedAviStartPosition)
                aviTransform.position = aviStartPosition;

            ApplyIdleVisual();
            SetAviVisible(true);
            exitPresentationStarted = false;
            return;
        }

        if (!hasExitTargetPosition)
        {
            exitPresentationComplete = true;
            SetAviVisible(false);
            return;
        }

        SetAviVisible(true);

        if (!exitPresentationStarted)
            exitPresentationStarted = true;

        Vector3 currentPosition = aviTransform.position;
        Vector3 targetPosition = aviExitTargetPosition;
        Vector3 delta = targetPosition - currentPosition;
        float remainingDistance = delta.magnitude;

        if (remainingDistance <= 0.01f)
        {
            aviTransform.position = targetPosition;
            exitPresentationComplete = true;
            SetAviVisible(false);
            return;
        }

        aviTransform.position = Vector3.MoveTowards(currentPosition, targetPosition, 1.35f * Time.deltaTime);
        ApplyWalkVisual();
    }

    private void ApplyIdleVisual()
    {
        if (aviSpriteRenderer == null)
            return;

        if (aviIdleSprite != null)
            aviSpriteRenderer.sprite = aviIdleSprite;
    }

    private void ApplyWalkVisual()
    {
        if (aviSpriteRenderer == null)
            return;

        if (aviRightWalkSprites != null && aviRightWalkSprites.Length > 0)
        {
            int frameIndex = Mathf.FloorToInt((Time.time + exitAnimationTimeOffset) * 4f) % aviRightWalkSprites.Length;
            Sprite sprite = aviRightWalkSprites[frameIndex];
            if (sprite != null)
                aviSpriteRenderer.sprite = sprite;
        }
    }

    private void SetAviVisible(bool visible)
    {
        if (aviSpriteRenderer != null)
            aviSpriteRenderer.enabled = visible;

        if (aviTransform != null)
        {
            BoxCollider2D collider = aviTransform.GetComponent<BoxCollider2D>();
            if (collider != null)
                collider.enabled = visible;
        }
    }

    private void EnsureWalkSpritesLoaded()
    {
        if (aviRightWalkSprites != null)
            return;

#if UNITY_EDITOR
        aviRightWalkSprites = new[]
        {
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Story_Chars/avi_char/avi_right side left walk.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Story_Chars/avi_char/avi_right sside leg walk.png")
        };
#else
        aviRightWalkSprites = Array.Empty<Sprite>();
#endif
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