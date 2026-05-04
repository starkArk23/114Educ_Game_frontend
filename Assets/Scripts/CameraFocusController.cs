using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFocusController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform defaultTarget;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool snapOnStart = true;

    [Header("Follow")]
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private Transform activeTarget;
    private Vector3 followVelocity;

    public Transform ActiveTarget => activeTarget;

    private void Start()
    {
        activeTarget = ResolveTarget();

        if (snapOnStart)
            SnapToTarget();
    }

    private void LateUpdate()
    {
        if (activeTarget == null)
            activeTarget = ResolveTarget();

        if (activeTarget == null)
            return;

        Vector3 desiredPosition = GetDesiredPosition(activeTarget.position);

        if (smoothTime <= 0f)
        {
            transform.position = desiredPosition;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime);
    }

    public void SetFocusTarget(Transform newTarget, bool snapImmediately = false)
    {
        activeTarget = newTarget != null ? newTarget : ResolveTarget();
        followVelocity = Vector3.zero;

        if (snapImmediately)
            SnapToTarget();
    }

    public void ClearFocusTarget(bool snapImmediately = false)
    {
        activeTarget = ResolveTarget();
        followVelocity = Vector3.zero;

        if (snapImmediately)
            SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (activeTarget == null)
            activeTarget = ResolveTarget();

        if (activeTarget == null)
            return;

        followVelocity = Vector3.zero;
        transform.position = GetDesiredPosition(activeTarget.position);
    }

    private Transform ResolveTarget()
    {
        if (defaultTarget != null)
            return defaultTarget;

        if (string.IsNullOrEmpty(playerTag))
            return null;

        GameObject playerObject = GameObject.FindWithTag(playerTag);
        return playerObject != null ? playerObject.transform : null;
    }

    private Vector3 GetDesiredPosition(Vector3 targetPosition)
    {
        Vector3 desiredPosition = targetPosition + offset;
        desiredPosition.z = offset.z;
        return desiredPosition;
    }
}