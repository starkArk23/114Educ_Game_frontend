using UnityEngine;

[DisallowMultipleComponent]
public class SceneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId;

    public bool Matches(string candidateId)
    {
        return !string.IsNullOrWhiteSpace(spawnPointId)
            && string.Equals(spawnPointId, candidateId, System.StringComparison.Ordinal);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.2f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
    }
}