using System;
using UnityEngine;

[DisallowMultipleComponent]
public class StoryNpcEntranceController : MonoBehaviour
{
    public event Action EntranceCompleted;
    public event Action ExitCompleted;

    [Header("Story")]
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private string requiredChapterKey = "opening";
    [SerializeField] private string entranceNodeKey = "opening.mentor_arrives";
    [SerializeField] private string exitNodeKey = "opening.room_exit_ready";
    [SerializeField] private string[] visibleNodeKeys = Array.Empty<string>();

    [Header("Motion")]
    [SerializeField] private Transform entrancePoint;
    [SerializeField] private Transform idlePoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float moveSpeed = 1.35f;
    [SerializeField] private Vector2 idleFacingDirection = Vector2.down;

    [Header("Visuals")]
    [SerializeField] private AmbientNpcVisualProfile visualProfile;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float animationFramesPerSecond = 4f;
    [SerializeField] private bool hideUntilTriggered = true;
    [SerializeField] private bool hideAfterExit = true;

    private bool hasStartedEntrance;
    private bool entranceCompleted;
    private bool hasStartedExit;
    private bool exitCompleted;
    private Vector2 facingDirection = Vector2.down;
    private float animationTimeOffset;
    private Vector3 entranceWorldPosition;
    private Vector3 idleWorldPosition;
    private Vector3 exitWorldPosition;

    public string EntranceNodeKey => entranceNodeKey;
    public bool HasCompletedEntrance => entranceCompleted;
    public bool IsEntranceInProgress => hasStartedEntrance && !entranceCompleted;
    public string ExitNodeKey => exitNodeKey;
    public bool HasCompletedExit => exitCompleted;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        animationTimeOffset = UnityEngine.Random.Range(0f, 100f);
        CacheMotionAnchors();

        transform.position = entranceWorldPosition;

        if (hideUntilTriggered)
            SetVisible(false);
    }

    private void Update()
    {
        StoryManager manager = ResolveStoryManager();
        string currentChapterKey = manager != null ? manager.CurrentChapterKey : string.Empty;
        string currentNodeKey = manager != null ? manager.CurrentNodeKey : string.Empty;

        if (!MatchesChapter(currentChapterKey))
        {
            if (!entranceCompleted && hideUntilTriggered)
                SetVisible(false);

            UpdateVisual(false);
            return;
        }

        if (exitCompleted && hideAfterExit)
        {
            SetVisible(false);
            UpdateVisual(false);
            return;
        }

        bool shouldShow = ShouldShowForNode(currentNodeKey);
        if (!shouldShow)
        {
            if (!entranceCompleted && hideUntilTriggered)
                SetVisible(false);

            UpdateVisual(false);
            return;
        }

        SetVisible(true);

        bool isMoving = false;
        if (ShouldRunExitMotion(currentNodeKey))
            isMoving = UpdateExitMotion(currentNodeKey);
        else if (!entranceCompleted)
            isMoving = UpdateEntranceMotion(currentNodeKey);

        if (!isMoving && !exitCompleted)
            transform.position = idleWorldPosition;

        UpdateVisual(isMoving);
    }

    private bool UpdateEntranceMotion(string currentNodeKey)
    {
        if (!string.Equals(currentNodeKey, entranceNodeKey, StringComparison.Ordinal))
        {
            transform.position = idleWorldPosition;

            hasStartedEntrance = true;
            CompleteEntrance();
            facingDirection = idleFacingDirection == Vector2.zero ? Vector2.down : idleFacingDirection.normalized;
            return false;
        }

        if (idlePoint == null)
        {
            CompleteEntrance();
            facingDirection = idleFacingDirection == Vector2.zero ? Vector2.down : idleFacingDirection.normalized;
            return false;
        }

        if (!hasStartedEntrance)
        {
            transform.position = entranceWorldPosition;

            hasStartedEntrance = true;
        }

        Vector3 targetPosition = idleWorldPosition;
        Vector3 currentPosition = transform.position;
        Vector3 delta = targetPosition - currentPosition;
        float remainingDistance = delta.magnitude;

        if (remainingDistance <= 0.01f)
        {
            transform.position = targetPosition;
            CompleteEntrance();
            facingDirection = idleFacingDirection == Vector2.zero ? Vector2.down : idleFacingDirection.normalized;
            return false;
        }

        Vector3 moveDirection = delta / Mathf.Max(remainingDistance, 0.0001f);
        facingDirection = new Vector2(moveDirection.x, moveDirection.y).normalized;
        transform.position = Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * Time.deltaTime);
        return true;
    }

    private bool UpdateExitMotion(string currentNodeKey)
    {
        if (exitCompleted || !string.Equals(currentNodeKey, exitNodeKey, StringComparison.Ordinal))
            return false;

        if (!hasStartedExit)
        {
            transform.position = idleWorldPosition;
            hasStartedExit = true;
        }

        Vector3 targetPosition = exitWorldPosition;
        Vector3 currentPosition = transform.position;
        Vector3 delta = targetPosition - currentPosition;
        float remainingDistance = delta.magnitude;

        if (remainingDistance <= 0.01f)
        {
            transform.position = targetPosition;
            CompleteExit();
            return false;
        }

        Vector3 moveDirection = delta / Mathf.Max(remainingDistance, 0.0001f);
        facingDirection = new Vector2(moveDirection.x, moveDirection.y).normalized;
        transform.position = Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * Time.deltaTime);
        return true;
    }

    private void UpdateVisual(bool isMoving)
    {
        if (spriteRenderer == null || visualProfile == null || !visualProfile.IsConfigured)
            return;

        float cycleTime = isMoving ? (Time.time + animationTimeOffset) * Mathf.Max(0.01f, animationFramesPerSecond) : 0f;
        Sprite sprite = visualProfile.Evaluate(facingDirection == Vector2.zero ? Vector2.down : facingDirection, isMoving, cycleTime);
        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private bool MatchesChapter(string currentChapterKey)
    {
        return string.IsNullOrWhiteSpace(requiredChapterKey)
            || string.Equals(currentChapterKey, requiredChapterKey, StringComparison.Ordinal);
    }

    private bool ShouldShowForNode(string currentNodeKey)
    {
        if (string.Equals(currentNodeKey, entranceNodeKey, StringComparison.Ordinal))
            return true;

        if (!exitCompleted && string.Equals(currentNodeKey, exitNodeKey, StringComparison.Ordinal))
            return true;

        if (visibleNodeKeys == null || visibleNodeKeys.Length == 0)
            return entranceCompleted;

        for (int index = 0; index < visibleNodeKeys.Length; index++)
        {
            string nodeKey = visibleNodeKeys[index];
            if (string.IsNullOrWhiteSpace(nodeKey))
                continue;

            if (string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }

    private void CacheMotionAnchors()
    {
        entranceWorldPosition = entrancePoint != null ? entrancePoint.position : transform.position;
        idleWorldPosition = idlePoint != null ? idlePoint.position : entranceWorldPosition;
        exitWorldPosition = exitPoint != null ? exitPoint.position : entranceWorldPosition;
    }

    public bool ControlsNode(string nodeKey)
    {
        return string.Equals(nodeKey, entranceNodeKey, StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(exitNodeKey) && string.Equals(nodeKey, exitNodeKey, StringComparison.Ordinal));
    }

    public bool IsPresentationComplete(string nodeKey)
    {
        if (string.Equals(nodeKey, entranceNodeKey, StringComparison.Ordinal))
            return entranceCompleted;

        if (!string.IsNullOrWhiteSpace(exitNodeKey) && string.Equals(nodeKey, exitNodeKey, StringComparison.Ordinal))
            return exitCompleted;

        return true;
    }

    private bool ShouldRunExitMotion(string currentNodeKey)
    {
        return !exitCompleted
            && !string.IsNullOrWhiteSpace(exitNodeKey)
            && string.Equals(currentNodeKey, exitNodeKey, StringComparison.Ordinal);
    }

    private void CompleteEntrance()
    {
        if (entranceCompleted)
            return;

        entranceCompleted = true;
        EntranceCompleted?.Invoke();
    }

    private void CompleteExit()
    {
        if (exitCompleted)
            return;

        exitCompleted = true;
        ExitCompleted?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }
}