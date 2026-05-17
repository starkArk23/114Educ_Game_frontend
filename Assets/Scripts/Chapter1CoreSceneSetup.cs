using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Chapter1CoreSceneSetup : MonoBehaviour
{
    private const string SupportedSceneName = "SystemCoreScene";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string ExploreGateNodeKey = "chapter1.explore_gate";
    private const string ExploreGroupKey = "chapter1.room_explore";
    private const string AviObjectName = "AviStoryNPC";
    // COREHUB is the crystal already placed in the scene — use it as the anchor object.
    private const string AnchorObjectName = "COREHUB";
    private const string AnchorReferenceObjectName = "IdlePoint";
    private const int InteractableLayer = 3;
    private static readonly Vector3 AviOffset = new Vector3(-0.75f, 0f, 0f);

    private void Awake()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureAviPlacement();
        EnsureAnchorInteraction();
        EnsureExploreInteractions();
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
        GameObject anchorObject = GameObject.Find(AnchorObjectName);
        if (anchorObject == null)
            return;

        anchorObject.layer = InteractableLayer;

        // Remove InspectableInteraction so Chapter1RuntimeStoryInteraction is found first
        // by PlayerInteraction.GetComponent<IInteractable>().
        InspectableInteraction inspectable = anchorObject.GetComponent<InspectableInteraction>();
        if (inspectable != null)
            Destroy(inspectable);

        BoxCollider2D collider = anchorObject.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = anchorObject.AddComponent<BoxCollider2D>();

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

    private void EnsureExploreInteractions()
    {
        // The five interactable explore objects listed in chapter1.explore_gate.
        // Any three must be examined to satisfy the gate (requiredCount: 3).
        ConfigureExploreItem("computerhub1",  "chapter1.lightpanel",  Chapter1StoryText.LightPanelTitle,      Chapter1StoryText.LightPanelBody);
        ConfigureExploreItem("computerhub2",  "chapter1.holowindow",  Chapter1StoryText.HolographicWindowTitle, Chapter1StoryText.HolographicWindowBody);
        ConfigureExploreItem("computerhub3",  "chapter1.mainbot",     Chapter1StoryText.MaintenanceBotTitle,  Chapter1StoryText.MaintenanceBotBody);
        ConfigureExploreItem("computer",      "chapter1.coolingvent", Chapter1StoryText.CoolingVentTitle,     Chapter1StoryText.CoolingVentBody);
        ConfigureExploreItem("AccessPanel",   "chapter1.archive",     Chapter1StoryText.ArchiveShelfTitle,    Chapter1StoryText.ArchiveShelfBody);
    }

    private static void ConfigureExploreItem(string objectName, string interactionId, string title, string bodyText)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null)
            return;

        obj.layer = InteractableLayer;

        // Replace InspectableInteraction so the story interaction component is resolved.
        InspectableInteraction inspectable = obj.GetComponent<InspectableInteraction>();
        if (inspectable != null)
            Destroy(inspectable);

        BoxCollider2D collider = obj.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = obj.AddComponent<BoxCollider2D>();

        collider.isTrigger = false;

        Chapter1RuntimeStoryInteraction interaction = obj.GetComponent<Chapter1RuntimeStoryInteraction>();
        if (interaction == null)
            interaction = obj.AddComponent<Chapter1RuntimeStoryInteraction>();

        interaction.Configure(
            ExploreGateNodeKey,
            interactionId,
            ExploreGroupKey,
            "SYSTEM",
            title,
            bodyText,
            Chapter1StoryText.DefaultInteractionPrompt);
    }

    private Transform ResolveAnchorReference()
    {
        GameObject referenceObject = GameObject.Find(AnchorReferenceObjectName);
        return referenceObject != null ? referenceObject.transform : null;
    }

    private static bool SupportsCurrentScene()
    {
        return string.Equals(SceneManager.GetActiveScene().name, SupportedSceneName, StringComparison.Ordinal);
    }
}