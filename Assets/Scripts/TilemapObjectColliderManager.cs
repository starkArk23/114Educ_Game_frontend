using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TilemapObjectColliderManager : MonoBehaviour
{
    [Serializable]
    private class TilemapColliderTarget
    {
        public Tilemap tilemap;
        public bool colliderEnabled = true;
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
    [SerializeField] private bool useCompositeCollider = false;
    [SerializeField] private CompositeCollider2D.GeometryType compositeGeometryType = CompositeCollider2D.GeometryType.Outlines;
    [SerializeField] private float compositeVertexDistance = 0.01f;
    [SerializeField] private float tileExtrusionFactor = 0.01f;
    [SerializeField] private PhysicsMaterial2D compositeMaterial;
    [SerializeField] private bool compositeIsTrigger;

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
}