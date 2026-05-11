using UnityEngine;

[DisallowMultipleComponent]
public class QuestMarker : MonoBehaviour
{
    private static readonly Color DefaultMarkerColor = new Color32(255, 214, 10, 255);

    [SerializeField] private MonoBehaviour targetSource;
    [SerializeField] private Sprite customMarkerSprite;
    [SerializeField] private Color markerColor = new Color32(255, 214, 10, 255);
    [SerializeField] private Vector3 fallbackOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private int sortingOrderOffset = 10;
    [SerializeField] private string fallbackSortingLayer = "ActorsFront";

    private static Sprite generatedMarkerSprite;

    private IQuestMarkerTarget target;
    private SpriteRenderer targetRenderer;
    private SpriteRenderer markerRenderer;
    private Transform markerVisual;

    private void Awake()
    {
        ResolveTarget();
        EnsureVisual();
        UpdateMarkerImmediate();
    }

    private void OnEnable()
    {
        UpdateMarkerImmediate();
    }

    private void LateUpdate()
    {
        UpdateMarkerImmediate();
    }

    private void OnValidate()
    {
        if (targetSource != null && targetSource is not IQuestMarkerTarget)
        {
            Debug.LogWarning("[QuestMarker] targetSource must implement IQuestMarkerTarget.", this);
            targetSource = null;
        }

        if (Application.isPlaying && markerRenderer != null)
        {
            markerRenderer.color = markerColor;
            markerRenderer.sprite = customMarkerSprite != null ? customMarkerSprite : GetOrCreateDefaultSprite();
        }
    }

    private void ResolveTarget()
    {
        if (targetSource != null)
            target = targetSource as IQuestMarkerTarget;

        if (target == null)
            target = GetComponent<IQuestMarkerTarget>();

        if (target == null)
            target = GetComponentInParent<IQuestMarkerTarget>();

        if (target == null)
            target = GetComponentInChildren<IQuestMarkerTarget>();

        targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInParent<SpriteRenderer>();
    }

    private void EnsureVisual()
    {
        if (markerVisual == null)
        {
            Transform existing = transform.Find("QuestMarkerVisual");
            markerVisual = existing;

            if (markerVisual == null)
            {
                GameObject visual = new GameObject("QuestMarkerVisual");
                visual.transform.SetParent(transform, false);
                markerVisual = visual.transform;
            }
        }

        markerRenderer = markerVisual.GetComponent<SpriteRenderer>();
        if (markerRenderer == null)
            markerRenderer = markerVisual.gameObject.AddComponent<SpriteRenderer>();

        markerRenderer.sprite = customMarkerSprite != null ? customMarkerSprite : GetOrCreateDefaultSprite();
        markerRenderer.color = markerColor == default ? DefaultMarkerColor : markerColor;
    }

    private void UpdateMarkerImmediate()
    {
        if (target == null)
            ResolveTarget();

        EnsureVisual();

        bool shouldShow = target != null && target.ShouldShowQuestMarker;
        markerVisual.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        Transform anchor = target.QuestMarkerAnchor != null ? target.QuestMarkerAnchor : transform;
        Vector3 offset = target.QuestMarkerOffset != default ? target.QuestMarkerOffset : fallbackOffset;

        markerVisual.position = anchor.position + offset;
        markerVisual.rotation = Quaternion.identity;
        markerVisual.localScale = Vector3.one;

        SyncSorting();
    }

    private void SyncSorting()
    {
        if (targetRenderer != null)
        {
            markerRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            markerRenderer.sortingOrder = targetRenderer.sortingOrder + sortingOrderOffset;
            return;
        }

        markerRenderer.sortingLayerName = fallbackSortingLayer;
        markerRenderer.sortingOrder = sortingOrderOffset;
    }

    private static Sprite GetOrCreateDefaultSprite()
    {
        if (generatedMarkerSprite != null)
            return generatedMarkerSprite;

        Texture2D texture = new Texture2D(8, 12, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "GeneratedQuestMarker"
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = DefaultMarkerColor;

        Color[] pixels = new Color[8 * 12];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        int[,] pattern =
        {
            { 3, 11 }, { 4, 11 },
            { 2, 10 }, { 3, 10 }, { 4, 10 }, { 5, 10 },
            { 2, 9 }, { 3, 9 }, { 4, 9 }, { 5, 9 },
            { 4, 8 },
            { 4, 7 },
            { 3, 6 }, { 4, 6 },
            { 3, 5 }, { 4, 5 },
            { 3, 3 }, { 4, 3 },
            { 3, 2 }, { 4, 2 }
        };

        for (int i = 0; i < pattern.GetLength(0); i++)
        {
            int x = pattern[i, 0];
            int y = pattern[i, 1];
            pixels[(y * 8) + x] = fill;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedMarkerSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 12f), new Vector2(0.5f, 0f), 8f);
        generatedMarkerSprite.name = "GeneratedQuestMarkerSprite";
        return generatedMarkerSprite;
    }
}