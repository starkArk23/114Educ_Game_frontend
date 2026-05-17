using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and controls a scrollable Quest Log panel entirely in code.
/// Press J (configurable) to toggle it open / closed.
/// </summary>
[DisallowMultipleComponent]
public class QuestLogUI : MonoBehaviour
{
    // -----------------------------------------------------------------------
    //  Inspector / configurable fields
    // -----------------------------------------------------------------------

    [Header("Toggle Key")]
    [SerializeField] private KeyCode toggleKey = KeyCode.J;

    [Header("Panel Appearance")]
    [SerializeField] private int panelWidth = 520;
    [SerializeField] private int panelHeight = 420;
    [SerializeField] private Color panelColor = new Color(0.04f, 0.06f, 0.12f, 0.94f);
    [SerializeField] private Color headerColor = new Color(0.08f, 0.14f, 0.24f, 1f);
    [SerializeField] private Color borderColor = new Color(0.25f, 0.55f, 1f, 0.7f);

    [Header("Entry Colors")]
    [SerializeField] private Color storyColor = new Color(0.85f, 0.92f, 1f, 1f);
    [SerializeField] private Color choiceColor = new Color(0.65f, 1f, 0.65f, 1f);
    [SerializeField] private Color cyberColor = new Color(1f, 0.55f, 0.3f, 1f);
    [SerializeField] private Color trustColor = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color systemColor = new Color(0.7f, 0.7f, 0.8f, 1f);

    [Header("Max displayed entries")]
    [SerializeField] private int maxDisplayed = 80;

    // -----------------------------------------------------------------------
    //  Runtime state
    // -----------------------------------------------------------------------

    private Canvas canvas;
    private GameObject panelRoot;
    private ScrollRect scrollRect;
    private Transform contentParent;
    private TMP_Text headerLabel;
    private bool isOpen;
    private readonly List<TMP_Text> rowPool = new List<TMP_Text>();

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    private void OnEnable()
    {
        QuestLogManager.OnLogUpdated += RefreshIfVisible;
    }

    private void OnDisable()
    {
        QuestLogManager.OnLogUpdated -= RefreshIfVisible;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    public void Toggle()
    {
        isOpen = !isOpen;
        SetVisible(isOpen);
        if (isOpen)
            Refresh();
    }

    public void Open()
    {
        if (isOpen)
            return;
        isOpen = true;
        SetVisible(true);
        Refresh();
    }

    public void Close()
    {
        if (!isOpen)
            return;
        isOpen = false;
        SetVisible(false);
    }

    // -----------------------------------------------------------------------
    //  Build UI
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        // Root canvas (overlay)
        GameObject canvasGo = new GameObject("QuestLogCanvas");
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel root
        panelRoot = CreatePanel(canvasGo.transform, "QuestLogPanel",
            new Vector2(panelWidth, panelHeight),
            new Vector2(0.5f, 0.5f),
            panelColor);

        // Border outline (Image with larger rect, drawn behind)
        AddBorderImage(panelRoot.transform);

        // Header bar
        GameObject header = CreatePanel(panelRoot.transform, "Header",
            new Vector2(panelWidth, 38), new Vector2(0.5f, 1f), headerColor);
        SetAnchors(header, new Vector2(0, 1), new Vector2(1, 1));
        SetOffsets(header, 0, -38, 0, 0);

        headerLabel = CreateText(header.transform, "HeaderLabel", "QUEST LOG  [J]",
            16, FontStyles.Bold, new Color(0.6f, 0.85f, 1f, 1f));
        StretchRect(headerLabel.gameObject);

        // Close button
        CreateCloseButton(header.transform);

        // Scroll area
        GameObject scrollArea = new GameObject("ScrollArea", typeof(RectTransform));
        scrollArea.transform.SetParent(panelRoot.transform, false);
        RectTransform scrollRect2 = scrollArea.GetComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0, 0);
        scrollRect2.anchorMax = new Vector2(1, 1);
        scrollRect2.offsetMin = new Vector2(6, 6);
        scrollRect2.offsetMax = new Vector2(-6, -42);

        Image scrollBg = scrollArea.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.3f);

        scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollArea.transform, false);
        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = new Vector2(4, 4);
        vpRect.offsetMax = new Vector2(-4, -4);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = Color.clear;

        scrollRect.viewport = vpRect;

        // Content
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(6, 6, 4, 4);
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        contentParent = content.transform;
        scrollRect.content = contentRect;
    }

    // -----------------------------------------------------------------------
    //  Refresh
    // -----------------------------------------------------------------------

    private void RefreshIfVisible()
    {
        if (isOpen)
            Refresh();
    }

    private void Refresh()
    {
        QuestLogManager mgr = QuestLogManager.Instance;
        if (mgr == null)
            return;

        IReadOnlyList<QuestLogManager.LogEntry> entries = mgr.Entries;

        int start = Mathf.Max(0, entries.Count - maxDisplayed);
        int count = entries.Count - start;

        // Grow pool if needed
        while (rowPool.Count < count)
            rowPool.Add(CreateRowText(contentParent));

        // Hide all, then show/fill
        for (int i = 0; i < rowPool.Count; i++)
            rowPool[i].gameObject.SetActive(false);

        for (int i = 0; i < count; i++)
        {
            QuestLogManager.LogEntry entry = entries[start + i];
            TMP_Text row = rowPool[i];
            row.gameObject.SetActive(true);

            string timestamp = FormatTime(entry.sessionTime);
            string prefix = EntryPrefix(entry.type);
            row.text = $"<color=#888888>[{timestamp}]</color> {prefix}{entry.message}";
            row.color = EntryColor(entry.type);
        }

        // Scroll to bottom on next frame
        Canvas.ForceUpdateCanvases();
        scrollRect.normalizedPosition = new Vector2(0, 0);
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:D2}:{s:D2}";
    }

    private static string EntryPrefix(QuestLogManager.EntryType type)
    {
        switch (type)
        {
            case QuestLogManager.EntryType.Story:       return "📖 ";
            case QuestLogManager.EntryType.Choice:      return "✅ ";
            case QuestLogManager.EntryType.CyberStatus: return "🛡 ";
            case QuestLogManager.EntryType.TrustToken:  return "🪙 ";
            case QuestLogManager.EntryType.Interaction: return "🔎 ";
            default:                                    return "• ";
        }
    }

    private Color EntryColor(QuestLogManager.EntryType type)
    {
        switch (type)
        {
            case QuestLogManager.EntryType.Story:       return storyColor;
            case QuestLogManager.EntryType.Choice:      return choiceColor;
            case QuestLogManager.EntryType.CyberStatus: return cyberColor;
            case QuestLogManager.EntryType.TrustToken:  return trustColor;
            default:                                    return systemColor;
        }
    }

    // -----------------------------------------------------------------------
    //  UI factory helpers
    // -----------------------------------------------------------------------

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pivot, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private void AddBorderImage(Transform parent)
    {
        GameObject border = new GameObject("Border", typeof(RectTransform));
        border.transform.SetParent(parent, false);
        border.transform.SetAsFirstSibling();
        RectTransform rt = border.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-2, -2);
        rt.offsetMax = new Vector2(2, 2);
        Image img = border.AddComponent<Image>();
        img.color = borderColor;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, int fontSize,
        FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.margin = new Vector4(8, 0, 8, 0);
        return tmp;
    }

    private TMP_Text CreateRowText(Transform parent)
    {
        GameObject go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 22);

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = true;
        tmp.color = storyColor;
        return tmp;
    }

    private void CreateCloseButton(Transform parent)
    {
        GameObject go = new GameObject("CloseBtn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(1, 0.5f);
        rt.sizeDelta = new Vector2(34, 28);
        rt.anchoredPosition = new Vector2(-4, 0);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Close);

        TMP_Text label = CreateText(go.transform, "X", "✕", 14, FontStyles.Bold, Color.white);
        StretchRect(label.gameObject);
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
    }

    private static void SetOffsets(GameObject go, float left, float top, float right, float bottom)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    private static void StretchRect(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
