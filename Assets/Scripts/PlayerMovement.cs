using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 3f;
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

    private static readonly HashSet<string> movementLocks = new HashSet<string>();

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 rawInput;              // Direct WASD input
    private Vector2 moveDirection;         // Normalized direction used for physics
    private Vector2 lastLookDirection = Vector2.down;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    public static bool CanMove => movementLocks.Count == 0 && GameState.CanPlayerMove;

    public static void AddMovementLock(string lockId)
    {
        if (!string.IsNullOrEmpty(lockId))
            movementLocks.Add(lockId);
    }

    public static void RemoveMovementLock(string lockId)
    {
        if (!string.IsNullOrEmpty(lockId))
            movementLocks.Remove(lockId);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ResolveOccluderTilemapsIfNeeded();
    }

    private void OnValidate()
    {
        ResolveOccluderTilemapsIfNeeded();
    }

    private void Update()
    {
        if (!CanMove)
        {
            rawInput = Vector2.zero;
            moveDirection = Vector2.zero;

            if (animator != null)
                animator.SetBool(IsMoving, false);

            return;
        }

        rawInput.x = Input.GetAxisRaw("Horizontal");
        rawInput.y = Input.GetAxisRaw("Vertical");

        moveDirection = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;
        bool isMoving = rawInput.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            if (Mathf.Abs(rawInput.y) >= Mathf.Abs(rawInput.x))
            {
                lastLookDirection = new Vector2(0f, Mathf.Sign(rawInput.y));
            }
            else
            {
                lastLookDirection = new Vector2(Mathf.Sign(rawInput.x), 0f);
            }
        }

        if (animator != null)
        {
            animator.SetFloat(MoveX, lastLookDirection.x);
            animator.SetFloat(MoveY, lastLookDirection.y);
            animator.SetBool(IsMoving, isMoving);
        }
    }

    private void FixedUpdate()
    {
        if (!CanMove)
            return;

        rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);
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
}
