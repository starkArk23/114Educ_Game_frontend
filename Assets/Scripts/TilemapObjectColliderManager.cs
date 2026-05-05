using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TilemapObjectColliderManager : MonoBehaviour
{
    private const string GeneratedFrontEdgeRootName = "__GeneratedFrontEdgeColliders";

    [Serializable]
    private class TilemapColliderTarget
    {
        public Tilemap tilemap;
        public bool colliderEnabled = true;
        public bool useFrontEdge = true;
        public bool useComposite = false;
        public bool isTrigger;
        public PhysicsMaterial2D physicsMaterial;
        public bool overrideLayer;
        public int layer;
    }

    [Header("Discovery")]
    [SerializeField] private bool autoFindObjectTilemaps = true;
    [SerializeField] private string[] preferredObjectTilemapNames = { "Tilemap_Objects", "Tilemap Objects", "Objects" };
    [SerializeField] private TilemapColliderTarget[] targets = Array.Empty<TilemapColliderTarget>();

    [Header("Collider Setup")]
    [SerializeField] private bool autoConfigureInEditor = true;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool useFrontEdgeColliders = true;
    [SerializeField] private bool useCompositeCollider = false;
    [SerializeField] private CompositeCollider2D.GeometryType compositeGeometryType = CompositeCollider2D.GeometryType.Outlines;
    [SerializeField] private float compositeVertexDistance = 0.01f;
    [SerializeField] private float tileExtrusionFactor = 0.01f;
    [SerializeField] private PhysicsMaterial2D compositeMaterial;
    [SerializeField] private bool compositeIsTrigger;

    [Header("Front Edge Colliders")]
    [SerializeField] [Range(0.05f, 1f)] private float frontEdgeHeight = 0.2f;
    [SerializeField] private float frontEdgeVerticalOffset;

    private readonly List<TilemapColliderTarget> resolvedTargets = new List<TilemapColliderTarget>();

    private void Reset()
    {
        PopulateTargetsIfNeeded(true);
        ApplyColliderSetup();
    }

    private void Awake()
    {
        if (!Application.isPlaying && !autoConfigureInEditor)
            return;

        if (Application.isPlaying && !applyOnAwake)
            return;

        ApplyColliderSetup();
    }

    private void OnValidate()
    {
        if (!autoConfigureInEditor)
            return;

        ApplyColliderSetup();
    }

    [ContextMenu("Apply Collider Setup")]
    public void ApplyColliderSetup()
    {
        PopulateTargetsIfNeeded(false);
        CollectResolvedTargets();

        if (resolvedTargets.Count == 0)
            return;

        bool wantsComposite = useCompositeCollider && HasCompositeTarget();
        CompositeCollider2D compositeCollider = null;

        if (wantsComposite)
            compositeCollider = EnsureCompositeCollider();

        for (int index = 0; index < resolvedTargets.Count; index++)
        {
            TilemapColliderTarget target = resolvedTargets[index];

            if (target.tilemap == null)
                continue;

            if (target.overrideLayer && target.layer >= 0 && target.layer <= 31)
                target.tilemap.gameObject.layer = target.layer;

            bool useFrontEdgeForTarget = useFrontEdgeColliders && target.useFrontEdge;

            if (useFrontEdgeForTarget)
            {
                DisableTilemapCollider(target.tilemap.gameObject);

                if (target.colliderEnabled)
                    RebuildFrontEdgeColliders(target);
                else
                    ClearGeneratedFrontEdgeColliders(target.tilemap.gameObject);

                continue;
            }

            ClearGeneratedFrontEdgeColliders(target.tilemap.gameObject);

            TilemapCollider2D tilemapCollider = GetOrAddComponent<TilemapCollider2D>(target.tilemap.gameObject);
            tilemapCollider.enabled = target.colliderEnabled;
            tilemapCollider.extrusionFactor = target.useComposite && wantsComposite ? tileExtrusionFactor : 0f;

            if (target.useComposite && wantsComposite)
            {
                tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
                tilemapCollider.sharedMaterial = null;
                tilemapCollider.isTrigger = false;
            }
            else
            {
                tilemapCollider.compositeOperation = Collider2D.CompositeOperation.None;
                tilemapCollider.sharedMaterial = target.physicsMaterial;
                tilemapCollider.isTrigger = target.isTrigger;
            }

            if (tilemapCollider.hasTilemapChanges)
                tilemapCollider.ProcessTilemapChanges();
        }

        if (compositeCollider != null)
        {
            compositeCollider.geometryType = compositeGeometryType;
            compositeCollider.vertexDistance = Mathf.Max(0.001f, compositeVertexDistance);
            compositeCollider.sharedMaterial = compositeMaterial;
            compositeCollider.isTrigger = compositeIsTrigger;
            compositeCollider.GenerateGeometry();
        }
    }

    private void DisableTilemapCollider(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (!targetObject.TryGetComponent(out TilemapCollider2D tilemapCollider))
            return;

        tilemapCollider.enabled = false;
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.None;
        tilemapCollider.sharedMaterial = null;
        tilemapCollider.isTrigger = false;
    }

    [ContextMenu("Auto-Find Object Tilemaps")]
    public void AutoFindObjectTilemaps()
    {
        PopulateTargetsIfNeeded(true);
        ApplyColliderSetup();
    }

    private void CollectResolvedTargets()
    {
        resolvedTargets.Clear();

        for (int index = 0; index < targets.Length; index++)
        {
            TilemapColliderTarget target = targets[index];

            if (target == null || target.tilemap == null)
                continue;

            resolvedTargets.Add(target);
        }
    }

    private bool HasCompositeTarget()
    {
        for (int index = 0; index < resolvedTargets.Count; index++)
        {
            if (resolvedTargets[index].useComposite)
                return true;
        }

        return false;
    }

    private void PopulateTargetsIfNeeded(bool replaceExisting)
    {
        if (!autoFindObjectTilemaps)
            return;

        if (!replaceExisting && targets.Length > 0)
            return;

        Tilemap[] childTilemaps = GetComponentsInChildren<Tilemap>(true);
        List<TilemapColliderTarget> discoveredTargets = new List<TilemapColliderTarget>();

        for (int index = 0; index < childTilemaps.Length; index++)
        {
            Tilemap childTilemap = childTilemaps[index];

            if (!ShouldIncludeInDefaults(childTilemap))
                continue;

            TilemapColliderTarget target = new TilemapColliderTarget
            {
                tilemap = childTilemap,
                colliderEnabled = true,
                useFrontEdge = true,
                useComposite = false,
                isTrigger = false,
                overrideLayer = false,
                layer = childTilemap.gameObject.layer,
            };

            discoveredTargets.Add(target);
        }

        if (discoveredTargets.Count > 0)
            targets = discoveredTargets.ToArray();
    }

    private bool ShouldIncludeInDefaults(Tilemap tilemap)
    {
        if (tilemap == null)
            return false;

        for (int index = 0; index < preferredObjectTilemapNames.Length; index++)
        {
            string preferredName = preferredObjectTilemapNames[index];

            if (string.IsNullOrWhiteSpace(preferredName))
                continue;

            if (string.Equals(tilemap.gameObject.name, preferredName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return tilemap.gameObject.name.IndexOf("object", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private CompositeCollider2D EnsureCompositeCollider()
    {
        Rigidbody2D rigidbody2D = GetOrAddComponent<Rigidbody2D>(gameObject);
        rigidbody2D.bodyType = RigidbodyType2D.Static;
        rigidbody2D.simulated = true;

        CompositeCollider2D compositeCollider = GetOrAddComponent<CompositeCollider2D>(gameObject);
        return compositeCollider;
    }

    private static T GetOrAddComponent<T>(GameObject targetObject) where T : Component
    {
        if (targetObject.TryGetComponent(out T existingComponent))
            return existingComponent;

        return targetObject.AddComponent<T>();
    }

    private void RebuildFrontEdgeColliders(TilemapColliderTarget target)
    {
        if (target == null || target.tilemap == null)
            return;

        GameObject rootObject = GetOrCreateFrontEdgeRoot(target.tilemap.gameObject);
        ClearFrontEdgeChildren(rootObject);

        GridLayout grid = target.tilemap.layoutGrid;
        Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
        float colliderHeight = Mathf.Clamp(frontEdgeHeight, 0.05f, cellSize.y);
        BoundsInt bounds = target.tilemap.cellBounds;
        int colliderIndex = 0;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            int x = bounds.xMin;

            while (x < bounds.xMax)
            {
                Vector3Int cellPosition = new Vector3Int(x, y, 0);

                if (!ShouldCreateFrontEdge(target.tilemap, cellPosition))
                {
                    x++;
                    continue;
                }

                int startX = x;
                x++;

                while (x < bounds.xMax && ShouldCreateFrontEdge(target.tilemap, new Vector3Int(x, y, 0)))
                    x++;

                int widthInCells = x - startX;
                CreateFrontEdgeCollider(rootObject.transform, target, startX, y, widthInCells, cellSize, colliderHeight, colliderIndex);
                colliderIndex++;
            }
        }
    }

    private static bool ShouldCreateFrontEdge(Tilemap tilemap, Vector3Int cellPosition)
    {
        if (tilemap == null || !tilemap.HasTile(cellPosition))
            return false;

        Vector3Int cellBelow = new Vector3Int(cellPosition.x, cellPosition.y - 1, cellPosition.z);
        return !tilemap.HasTile(cellBelow);
    }

    private void CreateFrontEdgeCollider(
        Transform parent,
        TilemapColliderTarget target,
        int startX,
        int y,
        int widthInCells,
        Vector3 cellSize,
        float colliderHeight,
        int colliderIndex)
    {
        GameObject colliderObject = new GameObject($"FrontEdgeCollider_{colliderIndex:000}");
        colliderObject.transform.SetParent(parent, false);
        colliderObject.layer = target.overrideLayer && target.layer >= 0 && target.layer <= 31
            ? target.layer
            : target.tilemap.gameObject.layer;

        BoxCollider2D collider = colliderObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = target.isTrigger;
        collider.sharedMaterial = target.physicsMaterial;

        Vector3 startCenter = target.tilemap.GetCellCenterLocal(new Vector3Int(startX, y, 0));
        Vector3 endCenter = target.tilemap.GetCellCenterLocal(new Vector3Int(startX + widthInCells - 1, y, 0));
        float width = widthInCells * cellSize.x;
        float centerX = (startCenter.x + endCenter.x) * 0.5f;
        float bottomY = startCenter.y - (cellSize.y * 0.5f);
        float centerY = bottomY + (colliderHeight * 0.5f) + frontEdgeVerticalOffset;

        collider.offset = new Vector2(centerX, centerY);
        collider.size = new Vector2(width, colliderHeight);
    }

    private GameObject GetOrCreateFrontEdgeRoot(GameObject tilemapObject)
    {
        Transform existing = tilemapObject.transform.Find(GeneratedFrontEdgeRootName);

        if (existing != null)
            return existing.gameObject;

        GameObject rootObject = new GameObject(GeneratedFrontEdgeRootName);
        rootObject.transform.SetParent(tilemapObject.transform, false);
        rootObject.layer = tilemapObject.layer;
        return rootObject;
    }

    private void ClearGeneratedFrontEdgeColliders(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        Transform root = targetObject.transform.Find(GeneratedFrontEdgeRootName);

        if (root == null)
            return;

        ClearFrontEdgeChildren(root.gameObject);

        if (root.childCount == 0)
            DestroySafely(root.gameObject);
    }

    private void ClearFrontEdgeChildren(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        for (int index = rootObject.transform.childCount - 1; index >= 0; index--)
            DestroySafely(rootObject.transform.GetChild(index).gameObject);
    }

    private static void DestroySafely(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(targetObject);
            return;
        }

        DestroyImmediate(targetObject);
    }
}