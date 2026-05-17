using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent singleton that records gameplay events as quest-log entries.
/// Other systems call QuestLogManager.AddEntry() to push a new line.
/// StoryManager fires static events that QuestLogManager subscribes to automatically.
/// </summary>
public class QuestLogManager : MonoBehaviour
{
    public enum EntryType
    {
        Story,
        Choice,
        CyberStatus,
        TrustToken,
        Interaction,
        System
    }

    public struct LogEntry
    {
        public EntryType type;
        public string message;
        public string nodeKey;
        public string chapterKey;
        public float sessionTime;
    }

    private const int MaxEntries = 150;

    private static QuestLogManager instance;

    // Fired whenever a new entry is appended so the UI can refresh.
    public static event Action OnLogUpdated;

    private readonly List<LogEntry> entries = new List<LogEntry>(MaxEntries);
    private float sessionStartTime;

    public static QuestLogManager Instance
    {
        get
        {
            if (instance == null)
                EnsureExists();
            return instance;
        }
    }

    public IReadOnlyList<LogEntry> Entries => entries;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartTime = Time.time;
    }

    private void OnEnable()
    {
        // GameSession stat events
        GameSession.Instance.CyberStatusChanged += OnCyberStatusChanged;
        GameSession.Instance.TrustTokensChanged += OnTrustTokensChanged;

        // StoryManager static events
        StoryManager.OnNodePresented += OnStoryNodePresented;
        StoryManager.OnChoiceSelected += OnChoiceSelected;
    }

    private void OnDisable()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.CyberStatusChanged -= OnCyberStatusChanged;
            GameSession.Instance.TrustTokensChanged -= OnTrustTokensChanged;
        }

        StoryManager.OnNodePresented -= OnStoryNodePresented;
        StoryManager.OnChoiceSelected -= OnChoiceSelected;
    }

    // -----------------------------------------------------------------------
    //  Static helpers
    // -----------------------------------------------------------------------

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("QuestLogManager");
        go.AddComponent<QuestLogManager>();
    }

    /// <summary>Push a custom entry from any system.</summary>
    public static void AddEntry(EntryType type, string message, string nodeKey = "", string chapterKey = "")
    {
        EnsureExists();
        instance.AppendEntry(type, message, nodeKey, chapterKey);
    }

    // -----------------------------------------------------------------------
    //  Internal
    // -----------------------------------------------------------------------

    private void AppendEntry(EntryType type, string message, string nodeKey = "", string chapterKey = "")
    {
        if (entries.Count >= MaxEntries)
            entries.RemoveAt(0);

        entries.Add(new LogEntry
        {
            type = type,
            message = message,
            nodeKey = nodeKey,
            chapterKey = chapterKey,
            sessionTime = Time.time - sessionStartTime
        });

        OnLogUpdated?.Invoke();
    }

    // -----------------------------------------------------------------------
    //  Event handlers
    // -----------------------------------------------------------------------

    private void OnStoryNodePresented(GameSession.StoryNodeDetail node)
    {
        if (node == null)
            return;

        string speaker = string.IsNullOrWhiteSpace(node.speaker) ? "Narrator" : node.speaker;
        string title = string.IsNullOrWhiteSpace(node.title) ? node.nodeKey : node.title;
        string snippet = string.IsNullOrWhiteSpace(node.bodyText)
            ? string.Empty
            : (node.bodyText.Length > 80 ? node.bodyText.Substring(0, 80) + "…" : node.bodyText);

        string message = string.IsNullOrWhiteSpace(snippet)
            ? $"[{speaker}] {title}"
            : $"[{speaker}] {title} — {snippet}";

        AppendEntry(EntryType.Story, message, node.nodeKey, node.chapterKey);
    }

    private void OnChoiceSelected(GameSession.StoryNodeDetail node, GameSession.StoryChoiceDetail choice)
    {
        if (choice == null)
            return;

        string label = choice.trustTokenCost > 0
            ? $"{choice.label} (–{choice.trustTokenCost} Token)"
            : choice.label;

        AppendEntry(EntryType.Choice, $"Choice: {label}", node?.nodeKey ?? string.Empty, node?.chapterKey ?? string.Empty);
    }

    private void OnCyberStatusChanged(GameSession.CyberStatusChange change)
    {
        if (change.delta == 0)
            return;

        string sign = change.delta > 0 ? "+" : string.Empty;
        string reason = string.IsNullOrWhiteSpace(change.reason) ? string.Empty : $" ({change.reason})";
        AppendEntry(EntryType.CyberStatus,
            $"Cyber Status {sign}{change.delta} → {change.currentValue}{reason}");
    }

    private void OnTrustTokensChanged(GameSession.TrustTokenChange change)
    {
        if (change.delta == 0)
            return;

        string sign = change.delta > 0 ? "+" : string.Empty;
        string reason = string.IsNullOrWhiteSpace(change.reason) ? string.Empty : $" ({change.reason})";
        AppendEntry(EntryType.TrustToken,
            $"Trust Tokens {sign}{change.delta} → {change.currentValue}{reason}");
    }
}
