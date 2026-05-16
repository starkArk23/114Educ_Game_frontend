using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Chapter1CoreSceneSetup : MonoBehaviour
{
    private const string SupportedSceneName = "SystemCoreScene";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string ExploreGateNodeKey = "chapter1.explore_gate";
    private const string AviObjectName = "AviStoryNPC";
    private const string AnchorObjectName = "SystemAnchorPoint";
    private const string AnchorReferenceObjectName = "IdlePoint";
    private const string LightPanelObjectName = "FloatingLightPanelPoint";
    private const string HolographicWindowObjectName = "HolographicWindowPoint";
    private const string MaintenanceBotObjectName = "IdleMaintenanceBotPoint";
    private const string CoolingVentObjectName = "CoolingVentPanelPoint";
    private const string ArchiveShelfObjectName = "ArchiveShelfPoint";
    private static readonly Vector3 AnchorOffset = new Vector3(1.15f, 0.85f, 0f);
    private static readonly Vector3 AviOffset = new Vector3(-0.75f, 0f, 0f);
    private static readonly Vector3 LightPanelOffset = new Vector3(1.7f, 0.8f, 0f);
    private static readonly Vector3 HolographicWindowOffset = new Vector3(2.7f, 0.85f, 0f);
    private static readonly Vector3 MaintenanceBotOffset = new Vector3(3.55f, -0.2f, 0f);
    private static readonly Vector3 CoolingVentOffset = new Vector3(1.8f, -0.85f, 0f);
    private static readonly Vector3 ArchiveShelfOffset = new Vector3(4.2f, 0.25f, 0f);
    private static Sprite panelSprite;

    private void Awake()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureAviPlacement();
        EnsureAnchorInteraction();
        EnsureStoryProps();
    }

    private void EnsureAviPlacement()
    {
        GameObject aviObject = GameObject.Find(AviObjectName);
        Transform referenceTransform = ResolveAnchorReference();
        if (aviObject == null || referenceTransform == null)
            return;

        aviObject.transform.position = referenceTransform.position + AviOffset;
    }

    private void EnsureAnchorInteraction()
    {
        Transform referenceTransform = ResolveAnchorReference();
        if (referenceTransform == null)
            return;

        GameObject anchorObject = GameObject.Find(AnchorObjectName);
        if (anchorObject == null)
        {
            anchorObject = new GameObject(AnchorObjectName);
            anchorObject.layer = 3;
            anchorObject.transform.position = referenceTransform.position + AnchorOffset;

            SpriteRenderer spriteRenderer = anchorObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ResolvePanelSprite();
            spriteRenderer.color = new Color(0.56f, 0.92f, 1f, 0.88f);
            spriteRenderer.sortingLayerName = "ActorsFront";
            spriteRenderer.sortingOrder = 3;
            anchorObject.transform.localScale = new Vector3(0.95f, 1.2f, 1f);
        }

        anchorObject.transform.position = referenceTransform.position + AnchorOffset;

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
            Chapter1StoryText.AnchorTitle,
            Chapter1StoryText.AnchorBody,
            Chapter1StoryText.DefaultInteractionPrompt);
    }

    private void EnsureStoryProps()
    {
        Transform referenceTransform = ResolveAnchorReference();
        if (referenceTransform == null)
            return;

        EnsureStoryProp(
            LightPanelObjectName,
            referenceTransform.position + LightPanelOffset,
            new Vector2(1.15f, 0.4f),
            new Color(0.75f, 0.95f, 1f, 0.9f),
            "chapter1.light_panel",
            Chapter1StoryText.LightPanelTitle,
            Chapter1StoryText.LightPanelBody,
            Chapter1StoryText.DefaultInteractionPrompt);
        EnsureStoryProp(
            HolographicWindowObjectName,
            referenceTransform.position + HolographicWindowOffset,
            new Vector2(1.35f, 0.8f),
            new Color(0.62f, 0.9f, 1f, 0.78f),
            "chapter1.holographic_window",
            Chapter1StoryText.HolographicWindowTitle,
            Chapter1StoryText.HolographicWindowBody,
            Chapter1StoryText.DefaultInteractionPrompt);
        EnsureStoryProp(
            MaintenanceBotObjectName,
            referenceTransform.position + MaintenanceBotOffset,
            new Vector2(0.7f, 0.7f),
            new Color(0.75f, 1f, 0.86f, 0.94f),
            "chapter1.maintenance_bot",
            Chapter1StoryText.MaintenanceBotTitle,
            Chapter1StoryText.MaintenanceBotBody,
            Chapter1StoryText.DefaultInteractionPrompt);
        EnsureStoryProp(
            CoolingVentObjectName,
            referenceTransform.position + CoolingVentOffset,
            new Vector2(1.05f, 0.35f),
            new Color(0.7f, 0.88f, 1f, 0.82f),
            "chapter1.cooling_vent",
            Chapter1StoryText.CoolingVentTitle,
            Chapter1StoryText.CoolingVentBody,
            Chapter1StoryText.DefaultInteractionPrompt);
        EnsureStoryProp(
            ArchiveShelfObjectName,
            referenceTransform.position + ArchiveShelfOffset,
            new Vector2(0.9f, 1.25f),
            new Color(0.88f, 0.95f, 1f, 0.9f),
            "chapter1.archive_shelf",
            Chapter1StoryText.ArchiveShelfTitle,
            Chapter1StoryText.ArchiveShelfBody,
            Chapter1StoryText.DefaultInteractionPrompt);
    }

    private void EnsureStoryProp(string objectName, Vector3 worldPosition, Vector2 colliderSize, Color color, string interactionId, string title, string bodyText, string prompt)
    {
        GameObject storyProp = GameObject.Find(objectName);
        if (storyProp == null)
        {
            storyProp = new GameObject(objectName);
            storyProp.layer = 3;

            SpriteRenderer spriteRenderer = storyProp.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ResolvePanelSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingLayerName = "ActorsFront";
            spriteRenderer.sortingOrder = 3;
            storyProp.transform.localScale = new Vector3(colliderSize.x, colliderSize.y, 1f);

            BoxCollider2D collider = storyProp.AddComponent<BoxCollider2D>();
            collider.size = colliderSize;
        }

        storyProp.transform.position = worldPosition;

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

    private Transform ResolveAnchorReference()
    {
        GameObject referenceObject = GameObject.Find(AnchorReferenceObjectName);
        return referenceObject != null ? referenceObject.transform : null;
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
        panelSprite.name = AnchorObjectName;
        return panelSprite;
    }

    private static bool SupportsCurrentScene()
    {
        return string.Equals(SceneManager.GetActiveScene().name, SupportedSceneName, StringComparison.Ordinal);
    }
}