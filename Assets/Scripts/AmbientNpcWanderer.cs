using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class AmbientNpcWanderer : MonoBehaviour
{
    private static readonly Vector2[] CardinalDirections =
    {
        Vector2.down,
        Vector2.up,
        Vector2.left,
        Vector2.right
    };

    private static readonly RaycastHit2D[] castHits = new RaycastHit2D[4];

    [Header("Visuals")]
    [SerializeField] private AmbientNpcVisualProfile visualProfile;
    [SerializeField] private Vector2 animationFramesPerSecondRange = new Vector2(3f, 5f);

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float roamRadius = 1.75f;
    [SerializeField] private Vector2 walkDistanceRange = new Vector2(0.5f, 1.5f);
    [SerializeField] private Vector2 idleDurationRange = new Vector2(0.8f, 2.2f);
    [SerializeField] private Vector2 walkDurationRange = new Vector2(0.8f, 2.4f);
    [SerializeField] private float destinationThreshold = 0.05f;
    [SerializeField] private float collisionProbeDistance = 0.08f;
    [SerializeField] private int destinationAttempts = 8;

    [Header("Sprite Sorting")]
    [SerializeField] private bool useYSorting = true;
    [SerializeField] private int sortingOrderOffset;
    [SerializeField] private float sortingOrderScale = 100f;
    [SerializeField] private float sortingPivotOffset = -0.6f;

    [Header("Object Occlusion Sorting")]
    [SerializeField] private bool useObjectOcclusionSorting = true;
    [SerializeField] private string sortingLayerWhenInFront = "ActorsFront";
    [SerializeField] private string sortingLayerWhenBehind = "ActorsBehind";
    [SerializeField] private bool autoFindOccluderTilemaps = true;
    [SerializeField] private string[] preferredOccluderTilemapNames = { "Tilemap_Objects", "Tilemap Objects", "Objects" };
    [SerializeField] private Tilemap[] occluderTilemaps = Array.Empty<Tilemap>();
    [SerializeField] private float occlusionProbeOffset = 0.05f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D movementCollider;
    private ContactFilter2D movementFilter;
    private Vector2 homePosition;
    private Vector2 destination;
    private Vector2 facingDirection = Vector2.down;
    private float stateTimer;
    private float animationFramesPerSecond;
    private float animationTimeOffset;
    private bool isWalking;
    private bool hasCollisionProbe;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        movementCollider = GetComponent<Collider2D>();
        homePosition = transform.position;

        ConfigureRigidbody();
        ConfigureCollisionProbe();
        ResolveOccluderTilemapsIfNeeded();

        animationFramesPerSecond = SampleRange(animationFramesPerSecondRange, 4f);
        animationTimeOffset = UnityEngine.Random.Range(0f, 100f);

        BeginIdle(true);
        UpdateSprite();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        roamRadius = Mathf.Max(0.1f, roamRadius);
        destinationThreshold = Mathf.Max(0.01f, destinationThreshold);
        collisionProbeDistance = Mathf.Max(0f, collisionProbeDistance);
        destinationAttempts = Mathf.Max(1, destinationAttempts);
        ResolveOccluderTilemapsIfNeeded();
    }

    private void Update()
    {
        stateTimer -= Time.deltaTime;

        if (isWalking)
        {
            if (stateTimer <= 0f || Vector2.Distance(rb.position, destination) <= destinationThreshold)
                BeginIdle(false);
        }
        else if (stateTimer <= 0f)
        {
            TryBeginWalk();
        }

        UpdateSprite();
    }

    private void FixedUpdate()
    {
        if (!isWalking)
            return;

        Vector2 currentPosition = rb.position;
        Vector2 toDestination = destination - currentPosition;
        float remainingDistance = toDestination.magnitude;

        if (remainingDistance <= destinationThreshold)
        {
            BeginIdle(false);
            return;
        }

        Vector2 moveDirection = toDestination / Mathf.Max(remainingDistance, 0.0001f);
        facingDirection = ResolveCardinalDirection(moveDirection);

        float stepDistance = moveSpeed * Time.fixedDeltaTime;
        float probeDistance = Mathf.Min(remainingDistance, stepDistance + collisionProbeDistance);

        if (IsPathBlocked(moveDirection, probeDistance))
        {
            BeginIdle(false);
            return;
        }

        rb.MovePosition(Vector2.MoveTowards(currentPosition, destination, stepDistance));
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
            return;

        UpdateOcclusionSortingLayer();

        if (!useYSorting)
            return;

        float sortY = transform.position.y + sortingPivotOffset;
        spriteRenderer.sortingOrder = sortingOrderOffset - Mathf.RoundToInt(sortY * sortingOrderScale);
    }

    private void ConfigureRigidbody()
    {
        if (rb == null)
            return;

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void ConfigureCollisionProbe()
    {
        hasCollisionProbe = movementCollider != null;
        movementFilter = new ContactFilter2D();
        movementFilter.useTriggers = false;
        movementFilter.useLayerMask = false;
    }

    private void BeginIdle(bool randomizeInitialDelay)
    {
        isWalking = false;
        stateTimer = randomizeInitialDelay
            ? UnityEngine.Random.Range(0f, SampleRange(idleDurationRange, 1.2f))
            : SampleRange(idleDurationRange, 1.2f);
    }

    private void TryBeginWalk()
    {
        Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;
        int startIndex = UnityEngine.Random.Range(0, CardinalDirections.Length);

        for (int attempt = 0; attempt < destinationAttempts; attempt++)
        {
            Vector2 direction = CardinalDirections[(startIndex + attempt) % CardinalDirections.Length];
            float stepDistance = SampleRange(walkDistanceRange, roamRadius);
            Vector2 candidate = currentPosition + direction * stepDistance;
            Vector2 homeOffset = candidate - homePosition;

            if (homeOffset.sqrMagnitude > roamRadius * roamRadius)
                candidate = homePosition + Vector2.ClampMagnitude(homeOffset, roamRadius);

            Vector2 travel = candidate - currentPosition;
            float travelDistance = travel.magnitude;

            if (travelDistance <= destinationThreshold)
                continue;

            Vector2 moveDirection = travel / travelDistance;
            if (IsPathBlocked(moveDirection, travelDistance + collisionProbeDistance))
                continue;

            destination = candidate;
            facingDirection = ResolveCardinalDirection(moveDirection);
            isWalking = true;
            stateTimer = SampleRange(walkDurationRange, 1.5f);
            return;
        }

        BeginIdle(false);
    }

    private void UpdateSprite()
    {
        if (spriteRenderer == null || visualProfile == null || !visualProfile.IsConfigured)
            return;

        float cycleTime = (Time.time + animationTimeOffset) * animationFramesPerSecond;
        Sprite nextSprite = visualProfile.Evaluate(facingDirection, isWalking, cycleTime);
        if (nextSprite != null)
            spriteRenderer.sprite = nextSprite;
    }

    private bool IsPathBlocked(Vector2 direction, float distance)
    {
        if (!hasCollisionProbe || rb == null || distance <= 0f)
            return false;

        int hitCount = rb.Cast(direction, movementFilter, castHits, distance);
        for (int index = 0; index < hitCount; index++)
        {
            Collider2D hitCollider = castHits[index].collider;
            if (hitCollider == null || hitCollider == movementCollider || hitCollider.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    private void UpdateOcclusionSortingLayer()
    {
        if (!useObjectOcclusionSorting)
            return;

        ResolveOccluderTilemapsIfNeeded();
        spriteRenderer.sortingLayerName = IsBehindOccluder() ? sortingLayerWhenBehind : sortingLayerWhenInFront;
    }

    private bool IsBehindOccluder()
    {
        if (occluderTilemaps == null || occluderTilemaps.Length == 0)
            return false;

        Vector3 pivotWorldPosition = transform.position + Vector3.up * (sortingPivotOffset + occlusionProbeOffset);

        for (int index = 0; index < occluderTilemaps.Length; index++)
        {
            Tilemap tilemap = occluderTilemaps[index];
            if (tilemap == null)
                continue;

            if (tilemap.HasTile(tilemap.WorldToCell(pivotWorldPosition)))
                return true;
        }

        return false;
    }

    private void ResolveOccluderTilemapsIfNeeded()
    {
        if (!autoFindOccluderTilemaps || HasAssignedOccluderTilemap())
            return;

        Tilemap[] sceneTilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Tilemap> matches = new List<Tilemap>();

        for (int tilemapIndex = 0; tilemapIndex < sceneTilemaps.Length; tilemapIndex++)
        {
            Tilemap tilemap = sceneTilemaps[tilemapIndex];

            if (tilemap == null || !HasPreferredOccluderName(tilemap.name))
                continue;

            matches.Add(tilemap);
        }

        if (matches.Count > 0)
            occluderTilemaps = matches.ToArray();
    }

    private bool HasAssignedOccluderTilemap()
    {
        if (occluderTilemaps == null)
            return false;

        for (int index = 0; index < occluderTilemaps.Length; index++)
        {
            if (occluderTilemaps[index] != null)
                return true;
        }

        return false;
    }

    private bool HasPreferredOccluderName(string tilemapName)
    {
        if (string.IsNullOrWhiteSpace(tilemapName) || preferredOccluderTilemapNames == null)
            return false;

        for (int index = 0; index < preferredOccluderTilemapNames.Length; index++)
        {
            if (string.Equals(tilemapName, preferredOccluderTilemapNames[index], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Vector2 ResolveCardinalDirection(Vector2 rawDirection)
    {
        if (Mathf.Abs(rawDirection.x) >= Mathf.Abs(rawDirection.y))
            return rawDirection.x < 0f ? Vector2.left : Vector2.right;

        return rawDirection.y < 0f ? Vector2.down : Vector2.up;
    }

    private static float SampleRange(Vector2 range, float fallback)
    {
        float minimum = Mathf.Min(range.x, range.y);
        float maximum = Mathf.Max(range.x, range.y);

        if (maximum <= 0f)
            return fallback;

        if (Mathf.Approximately(minimum, maximum))
            return Mathf.Max(0f, minimum);

        return UnityEngine.Random.Range(Mathf.Max(0f, minimum), Mathf.Max(0f, maximum));
    }
}