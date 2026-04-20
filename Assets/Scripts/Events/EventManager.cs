using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    [Serializable]
    private class CooldownEntry
    {
        public string eventId;
        public float readyTime;
    }

    public static EventManager Instance { get; private set; }

    [Header("Event Pool")]
    public List<CyberEventData> eventPool = new List<CyberEventData>();

    private readonly HashSet<string> eventFlags = new HashSet<string>();
    private readonly HashSet<string> triggeredEventIds = new HashSet<string>();
    private readonly List<CooldownEntry> cooldowns = new List<CooldownEntry>();

    private CyberEventData currentEvent;
    private bool isEventActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartEvent(CyberEventData data)
    {
        if (!CanStartEvent(data))
            return;

        isEventActive = true;
        currentEvent = data;

        Debug.Log($"[EventManager] StartEvent: {data.eventId}");

        GameState.CanPlayerMove = false;

        DialogueManager dm = FindFirstObjectByType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogError("[EventManager] No DialogueManager found in scene.");
            EndEvent();
            return;
        }

        string[] choiceTexts = BuildChoiceTextArray(data.choices);
        dm.Show(data.title, data.bodyText, choiceTexts, OnChoiceSelected);
    }

    public void StartRandomEvent()
    {
        StartRandomEvent(null);
    }

    public void StartRandomEvent(List<CyberEventData> overridePool)
    {
        List<CyberEventData> pool = overridePool != null && overridePool.Count > 0 ? overridePool : eventPool;
        CyberEventData picked = PickRandomEvent(pool);

        if (picked == null)
        {
            Debug.Log("[EventManager] No valid event found in pool.");
            return;
        }

        StartEvent(picked);
    }

    private void OnChoiceSelected(int choiceIndex)
    {
        if (!isEventActive || currentEvent == null)
            return;

        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count)
        {
            Debug.LogWarning("[EventManager] Choice index out of range.");
            EndEvent();
            return;
        }

        CyberEventChoice choice = currentEvent.choices[choiceIndex];
        if (!HasRequiredFlags(choice.requiredFlags))
        {
            Debug.LogWarning("[EventManager] Choice requirements not met.");
            EndEvent();
            return;
        }

        ApplyChoice(choice);
        EndEvent();
    }

    public void EndEvent()
    {
        if (!isEventActive)
            return;

        Debug.Log("[EventManager] EndEvent");

        if (currentEvent != null)
        {
            if (!currentEvent.isRepeatable && !string.IsNullOrEmpty(currentEvent.eventId))
                triggeredEventIds.Add(currentEvent.eventId);

            if (currentEvent.cooldownSeconds > 0f)
                SetCooldown(currentEvent.eventId, currentEvent.cooldownSeconds);
        }

        isEventActive = false;
        currentEvent = null;

        GameState.CanPlayerMove = true;
    }

    private bool CanStartEvent(CyberEventData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[EventManager] StartEvent called with null data.");
            return false;
        }

        if (isEventActive)
        {
            Debug.Log("[EventManager] Event already active.");
            return false;
        }

        if (!data.isRepeatable && triggeredEventIds.Contains(data.eventId))
        {
            Debug.Log("[EventManager] Event already triggered and not repeatable.");
            return false;
        }

        if (IsOnCooldown(data.eventId))
        {
            Debug.Log("[EventManager] Event is on cooldown.");
            return false;
        }

        return true;
    }

    private CyberEventData PickRandomEvent(List<CyberEventData> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        List<CyberEventData> valid = new List<CyberEventData>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (CanStartEvent(pool[i]))
                valid.Add(pool[i]);
        }

        if (valid.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, valid.Count);
        return valid[index];
    }

    private string[] BuildChoiceTextArray(List<CyberEventChoice> choices)
    {
        if (choices == null || choices.Count == 0)
            return Array.Empty<string>();

        int count = Mathf.Min(choices.Count, 4);
        string[] texts = new string[count];
        for (int i = 0; i < count; i++)
            texts[i] = choices[i].choiceText;

        return texts;
    }

    private void ApplyChoice(CyberEventChoice choice)
    {
        if (choice == null)
            return;

        if (choice.setsFlags != null)
        {
            for (int i = 0; i < choice.setsFlags.Length; i++)
            {
                string flag = choice.setsFlags[i];
                if (!string.IsNullOrEmpty(flag))
                    eventFlags.Add(flag);
            }
        }

        Debug.Log($"[EventManager] Outcome: {choice.outcomeText}");
        Debug.Log($"[EventManager] Risk delta: {choice.riskDelta}, Reward delta: {choice.rewardDelta}");
    }

    private bool HasRequiredFlags(string[] requiredFlags)
    {
        if (requiredFlags == null || requiredFlags.Length == 0)
            return true;

        for (int i = 0; i < requiredFlags.Length; i++)
        {
            string flag = requiredFlags[i];
            if (!string.IsNullOrEmpty(flag) && !eventFlags.Contains(flag))
                return false;
        }

        return true;
    }

    private bool IsOnCooldown(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
            return false;

        for (int i = 0; i < cooldowns.Count; i++)
        {
            if (cooldowns[i].eventId == eventId)
                return Time.time < cooldowns[i].readyTime;
        }

        return false;
    }

    private void SetCooldown(string eventId, float cooldownSeconds)
    {
        if (string.IsNullOrEmpty(eventId) || cooldownSeconds <= 0f)
            return;

        float readyTime = Time.time + cooldownSeconds;
        for (int i = 0; i < cooldowns.Count; i++)
        {
            if (cooldowns[i].eventId == eventId)
            {
                cooldowns[i].readyTime = readyTime;
                return;
            }
        }

        cooldowns.Add(new CooldownEntry { eventId = eventId, readyTime = readyTime });
    }
}
