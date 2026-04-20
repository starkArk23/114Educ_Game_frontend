using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 3f;

    private static readonly HashSet<string> movementLocks = new HashSet<string>();

    private Rigidbody2D rb;
    private Animator animator;
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
}
