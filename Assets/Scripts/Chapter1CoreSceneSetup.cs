using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Chapter1CoreSceneSetup : MonoBehaviour
{
    private const string SupportedSceneName = "SystemCoreScene";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string AviObjectName = "AviStoryNPC";
    private const string AnchorObjectName = "SystemAnchorPoint";
    private const string AnchorReferenceObjectName = "IdlePoint";
    private static readonly Vector3 AnchorOffset = new Vector3(1.15f, 0.85f, 0f);
    private static readonly Vector3 AviOffset = new Vector3(-0.75f, 0f, 0f);
    private static Sprite panelSprite;

    private void Awake()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureAviPlacement();
        EnsureAnchorInteraction();
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