using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    private const string AviPrefabPath = "Assets/Prefabs/StoryNPCs/AviChapter1.prefab";

    [Header("Avi Character")]
    [SerializeField] private GameObject aviPrefab;

    private void Awake()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureAviSpawned();
        EnsureAviController();
        EnsureAviPlacement();
        EnsureAnchorInteraction();
        EnsureExploreInteractions();
    }

    /// <summary>
    /// Instantiates the Avi prefab into the scene as "AviStoryNPC" if it is not
    /// already present. Must run before EnsureAviPlacement.
    /// In the editor the prefab is loaded from its known asset path when the
    /// serialized field is not assigned, removing the need for manual wiring.
    /// </summary>
    private void EnsureAviSpawned()
    {
        if (GameObject.Find(AviObjectName) != null)
            return;

        GameObject prefab = ResolveAviPrefab();
        if (prefab == null)
            return;

        GameObject avi = Instantiate(prefab);
        avi.name = AviObjectName;
    }

    private GameObject ResolveAviPrefab()
    {
        if (aviPrefab != null)
            return aviPrefab;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(AviPrefabPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Adds SystemCoreAviController to this GameObject if one does not already
    /// exist in the scene. The controller's Start() runs after all Awake() calls,
    /// so AviStoryNPC is guaranteed to be present by then.
    /// </summary>
    private void EnsureAviController()
    {
        if (FindFirstObjectByType<SystemCoreAviController>() != null)
            return;

        gameObject.AddComponent<SystemCoreAviController>();
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

        // Add a quest marker so the player can see which objects are interactable
        // during the chapter1.explore_gate beat. The marker disappears automatically
        // once the node advances beyond explore_gate.
        StoryObjectiveMarkerTarget markerTarget = obj.GetComponent<StoryObjectiveMarkerTarget>();
        if (markerTarget == null)
            markerTarget = obj.AddComponent<StoryObjectiveMarkerTarget>();

        markerTarget.ConfigureRuntime("chapter1", new[] { ExploreGateNodeKey }, requireInteractableValue: false);

        if (obj.GetComponent<QuestMarker>() == null)
            obj.AddComponent<QuestMarker>();
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