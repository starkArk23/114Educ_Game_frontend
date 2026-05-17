using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls Avi's entrance, walk-to-anchor, and idle-at-anchor behaviours
/// in SystemCoreScene, driven by the active story node.
///
/// Motion phases (per story node):
///   opening.core_intro and earlier  → Hidden  (parked at entrancePoint)
///   chapter1.avi_intro              → Entering (walk entrancePoint → encounterPoint)
///   chapter1.anchor_intro           → WalkingToAnchor (walk toward anchorIdlePoint)
///   chapter1.explore_gate           → IdleAtAnchor (idle animation at anchorIdlePoint)
///   opening.core_exit_ready +       → Gone (sprite disabled)
///
/// Avi's motion is purely visual — it does not gate story progression.
///
/// Editor setup required:
///   1. Create an AmbientNpcVisualProfile asset for Avi and assign her sprites per direction.
///   2. Place three empty GameObjects in SystemCoreScene:
///        AviEntrancePoint  — off-screen starting position
///        AviEncounterPoint — near CoreSystemEntry spawn (where Avi "bumps into" the player)
///        AviAnchorIdlePoint — beside COREHUB crystal (the existing IdlePoint GO can serve this)
///   3. Attach this script to any persistent GameObject in SystemCoreScene (e.g. the StoryManager GO).
///   4. Assign visualProfile, entrancePoint, encounterPoint, and anchorIdlePoint in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class SystemCoreAviController : MonoBehaviour
{
    private enum AviPhase { Hidden, Entering, WalkingToAnchor, IdleAtAnchor, Gone }

    private const string SupportedSceneName = "SystemCoreScene";
    private const string AviIntroNodeKey = "chapter1.avi_intro";
    private const string AnchorIntroNodeKey = "chapter1.anchor_intro";
    private const string ExploreGateNodeKey = "chapter1.explore_gate";
    private const string CoreExitNodeKey = "opening.core_exit_ready";
    private const string AviObjectName = "AviStoryNPC";

    [Header("Story")]
    [SerializeField] private StoryManager storyManager;

    [Header("Motion")]
    [SerializeField] private Transform entrancePoint;
    [SerializeField] private Transform encounterPoint;
    [SerializeField] private Transform anchorIdlePoint;
    [SerializeField] private float moveSpeed = 1.35f;

    [Header("Visuals")]
    [SerializeField] private AmbientNpcVisualProfile visualProfile;
    [SerializeField] private float animationFps = 4f;

    private Transform aviTransform;
    private SpriteRenderer aviSpriteRenderer;

    private AviPhase phase = AviPhase.Hidden;
    private Vector2 facingDirection = Vector2.down;
    private float animTimeOffset;

    private bool entranceArrived;
    private bool anchorArrived;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        if (!SupportsCurrentScene())
            return;

        animTimeOffset = UnityEngine.Random.Range(0f, 100f);

        EnsureReferences();
        PlaceAviAtEntrance();
    }

    private void Update()
    {
        if (!SupportsCurrentScene())
            return;

        EnsureReferences();

        if (aviTransform == null || aviSpriteRenderer == null)
            return;

        string nodeKey = storyManager != null ? storyManager.CurrentNodeKey ?? string.Empty : string.Empty;

        UpdatePhase(nodeKey);
        UpdateBehaviour();
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called in Start() — runs after all Awake() calls so it cleanly overrides
    /// the position set by Chapter1CoreSceneSetup.EnsureAviPlacement().
    /// </summary>
    private void PlaceAviAtEntrance()
    {
        EnsureReferences();

        if (aviTransform == null)
            return;

        aviTransform.position = GetEntrancePosition();

        SetVisible(false);
        phase = AviPhase.Hidden;
    }

    // -------------------------------------------------------------------------
    // Reference resolution
    // -------------------------------------------------------------------------

    private void EnsureReferences()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        if (aviTransform == null)
        {
            GameObject aviObject = GameObject.Find(AviObjectName);
            if (aviObject != null)
            {
                aviTransform = aviObject.transform;
                aviSpriteRenderer = aviObject.GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        if (aviTransform != null && aviSpriteRenderer == null)
            aviSpriteRenderer = aviTransform.GetComponentInChildren<SpriteRenderer>(true);
    }

    // -------------------------------------------------------------------------
    // Phase state machine
    // -------------------------------------------------------------------------

    private void UpdatePhase(string nodeKey)
    {
        switch (nodeKey)
        {
            case AviIntroNodeKey:
                // First time we see this node, begin entrance.
                if (phase == AviPhase.Hidden)
                    phase = AviPhase.Entering;
                break;

            case AnchorIntroNodeKey:
                // Advance to anchor walk regardless of whether entrance completed.
                if (phase == AviPhase.Hidden || phase == AviPhase.Entering)
                    phase = AviPhase.WalkingToAnchor;
                break;

            case ExploreGateNodeKey:
                // Snap to idle at anchor; handles save/resume mid-explore cleanly.
                if (phase != AviPhase.Gone)
                    phase = AviPhase.IdleAtAnchor;
                break;

            case CoreExitNodeKey:
                phase = AviPhase.Gone;
                break;

            default:
                // For any other node (e.g. branching intermediate nodes) do not
                // regress the phase — hold whatever was last set.
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Per-phase movement and visuals
    // -------------------------------------------------------------------------

    private void UpdateBehaviour()
    {
        switch (phase)
        {
            case AviPhase.Hidden:
                SetVisible(false);
                aviTransform.position = GetEntrancePosition();
                break;

            case AviPhase.Entering:
                SetVisible(true);
                if (!entranceArrived)
                {
                    bool moving = MoveToward(GetEncounterPosition());
                    UpdateVisual(moving);

                    if (!moving)
                    {
                        entranceArrived = true;
                        facingDirection = Vector2.down;   // face the player
                    }
                }
                else
                {
                    // Arrived — hold idle facing the player.
                    facingDirection = Vector2.down;
                    UpdateVisual(false);
                }
                break;

            case AviPhase.WalkingToAnchor:
                SetVisible(true);
                if (!anchorArrived)
                {
                    bool moving = MoveToward(GetAnchorPosition());
                    UpdateVisual(moving);

                    if (!moving)
                    {
                        anchorArrived = true;
                        facingDirection = Vector2.down;
                        UpdateVisual(false);
                    }
                }
                else
                {
                    facingDirection = Vector2.down;
                    UpdateVisual(false);
                }
                break;

            case AviPhase.IdleAtAnchor:
                SetVisible(true);
                // Snap to anchor position (also correct on save/resume).
                aviTransform.position = GetAnchorPosition();

                facingDirection = Vector2.down;
                UpdateVisual(false);
                break;

            case AviPhase.Gone:
                SetVisible(false);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Position resolution (serialized Transforms take priority; falls back to
    // scene-object lookups so the controller works without manual editor wiring)
    // -------------------------------------------------------------------------

    private static readonly string[] AnchorObjectNames = { "IdlePoint", "COREHUB" };
    private static readonly Vector3 AnchorAviOffset = new Vector3(-0.75f, 0f, 0f);

    private Vector3 GetAnchorPosition()
    {
        if (anchorIdlePoint != null)
            return anchorIdlePoint.position;

        foreach (string candidateName in AnchorObjectNames)
        {
            GameObject go = GameObject.Find(candidateName);
            if (go != null)
                return go.transform.position + AnchorAviOffset;
        }

        return Vector3.zero;
    }

    private Vector3 GetEncounterPosition()
    {
        if (encounterPoint != null)
            return encounterPoint.position;

        // Use the CoreSystemEntry spawn point as the "bump" location.
        GameObject spawnGO = GameObject.Find("CoreSystemEntry");
        if (spawnGO != null)
            return spawnGO.transform.position;

        // Final fallback: just in front of anchor.
        return GetAnchorPosition() + new Vector3(3f, -0.5f, 0f);
    }

    private Vector3 GetEntrancePosition()
    {
        if (entrancePoint != null)
            return entrancePoint.position;

        // Off-screen to the left of the encounter point.
        return GetEncounterPosition() + new Vector3(-5f, 0f, 0f);
    }

    // -------------------------------------------------------------------------
    // Movement helper
    // -------------------------------------------------------------------------

    /// <returns>True while still moving, false when target is reached.</returns>
    private bool MoveToward(Vector3 target)
    {
        Vector3 current = aviTransform.position;
        Vector3 delta = target - current;
        float distance = delta.magnitude;

        if (distance <= 0.01f)
        {
            aviTransform.position = target;
            return false;
        }

        Vector3 dir = delta / Mathf.Max(distance, 0.0001f);
        facingDirection = new Vector2(dir.x, dir.y).normalized;
        aviTransform.position = Vector3.MoveTowards(current, target, moveSpeed * Time.deltaTime);
        return true;
    }

    // -------------------------------------------------------------------------
    // Sprite animation
    // -------------------------------------------------------------------------

    private void UpdateVisual(bool isMoving)
    {
        if (aviSpriteRenderer == null || visualProfile == null || !visualProfile.IsConfigured)
            return;

        Vector2 dir = facingDirection == Vector2.zero ? Vector2.down : facingDirection;
        float cycleTime = isMoving
            ? (Time.time + animTimeOffset) * Mathf.Max(0.01f, animationFps)
            : 0f;

        Sprite sprite = visualProfile.Evaluate(dir, isMoving, cycleTime);
        if (sprite != null)
            aviSpriteRenderer.sprite = sprite;
    }

    // -------------------------------------------------------------------------
    // Visibility
    // -------------------------------------------------------------------------

    private void SetVisible(bool visible)
    {
        if (aviSpriteRenderer != null)
            aviSpriteRenderer.enabled = visible;
    }

    // -------------------------------------------------------------------------
    // Scene guard
    // -------------------------------------------------------------------------

    private static bool SupportsCurrentScene()
    {
        return string.Equals(
            SceneManager.GetActiveScene().name,
            SupportedSceneName,
            StringComparison.Ordinal);
    }
}
