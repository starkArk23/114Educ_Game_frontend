using System.Collections;
using UnityEngine;

/// <summary>
/// Place this on a trigger Collider2D at the hallway-door area of SystemCoreScene.
/// When the player walks through it while the story is at <c>chapter1.core_exit_ready</c>,
/// it registers the <c>chapter1.core_exit</c> interaction (which advances the backend node
/// to <c>chapter1.alert_intro</c>) and then transitions to HallwayScene at the
/// <c>FromSystemCore</c> spawn point.
///
/// Unity setup:
///   1. Create an empty GameObject in SystemCoreScene near the hallway doorway.
///   2. Add a BoxCollider2D — tick "Is Trigger" — resize to cover the doorway.
///   3. Add this component.
///   4. Assign the StoryManager field in the Inspector.
///   5. Optionally assign a SpriteRenderer on a child object for a door/portal visual.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SystemCoreHallwayExit : MonoBehaviour
{
    // ── Story gate ────────────────────────────────────────────────────────────
    private const string ActiveNodeKey    = "chapter1.core_exit_ready";
    private const string InteractionId    = "core_exit_door";
    private const string InteractionGroup = "chapter1.core_exit";

    // ── Destination ───────────────────────────────────────────────────────────
    private const string TargetScene      = "HallwayScene";
    private const string TargetSpawnPoint = "FromSystemCore";

    // ── Player tag ────────────────────────────────────────────────────────────
    private const string PlayerTag        = "Player";

    [Header("References")]
    [SerializeField] private StoryManager storyManager;

    [Header("Optional Visual")]
    [Tooltip("Assign a child SpriteRenderer to highlight the exit when it becomes active.")]
    [SerializeField] private SpriteRenderer exitIndicator;
    [SerializeField] private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    [SerializeField] private Color activeColor   = new Color(0.3f, 0.9f, 1f, 0.85f);

    // ── State ─────────────────────────────────────────────────────────────────
    private bool requestInFlight;
    private bool requireFreshEntry;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        ApplyIndicatorColor(false);
    }

    private void Update()
    {
        bool available = IsAvailable();
        ApplyIndicatorColor(available);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        requireFreshEntry = false;
        TryExit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryExit(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(PlayerTag))
            requireFreshEntry = false;
    }

    // ── Core logic ────────────────────────────────────────────────────────────
    private void TryExit(Collider2D other)
    {
        if (requestInFlight || RuntimeSceneTransition.IsTransitioning)
            return;

        if (!other.CompareTag(PlayerTag))
            return;

        if (!PlayerMovement.CanMove || !IsAvailable())
        {
            requireFreshEntry = true;
            return;
        }

        if (requireFreshEntry)
            return;

        requireFreshEntry = true;
        StartCoroutine(RegisterAndTransition());
    }

    private IEnumerator RegisterAndTransition()
    {
        requestInFlight = true;

        GameSession.StoryNodeDetail resultNode = null;
        string resultError = null;

        yield return StartCoroutine(
            GameSession.Instance.RegisterStoryInteraction(
                InteractionId,
                InteractionGroup,
                (node, error) => { resultNode = node; resultError = error; }
            )
        );

        requestInFlight = false;

        if (!string.IsNullOrEmpty(resultError))
        {
            Debug.LogError($"[SystemCoreHallwayExit] Interaction registration failed: {resultError}", this);
            yield break;
        }

        if (resultNode == null)
        {
            Debug.LogWarning("[SystemCoreHallwayExit] Interaction returned no node; transition aborted.", this);
            yield break;
        }

        RuntimeSceneTransition.TransitionTo(TargetScene, TargetSpawnPoint);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool IsAvailable()
    {
        StoryManager manager = ResolveStoryManager();
        if (manager == null)
            return false;

        return string.Equals(manager.CurrentNodeKey, ActiveNodeKey, System.StringComparison.Ordinal);
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager != null)
            return storyManager;

        storyManager = FindFirstObjectByType<StoryManager>();
        return storyManager;
    }

    private void ApplyIndicatorColor(bool available)
    {
        if (exitIndicator == null)
            return;

        exitIndicator.color = available ? activeColor : inactiveColor;
    }

    // ── Editor gizmo ──────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.4f);
        Collider2D col = GetComponent<Collider2D>();
        if (col is BoxCollider2D box)
        {
            Vector3 center = transform.TransformPoint(box.offset);
            Vector3 size   = Vector3.Scale(box.size, transform.lossyScale);
            Gizmos.DrawWireCube(center, size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.8f,
            $"→ {TargetScene}\n[{ActiveNodeKey}]"
        );
#endif
    }
}
