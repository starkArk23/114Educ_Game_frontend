using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RandomEncounterZone : MonoBehaviour
{
    [Header("Encounter Settings")]
    [SerializeField] private float encounterChancePerStep = 0.2f;
    [SerializeField] private float stepDistanceThreshold = 1.5f;
    [SerializeField] private float encounterChancePerSecond = 0f;
    [SerializeField] private float cooldownSeconds = 2f;
    [SerializeField] private List<CyberEventData> eventPoolOverride = new List<CyberEventData>();

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    private Rigidbody2D playerRb;
    private Vector2 lastPlayerPosition;
    private float lastEncounterTime;
    private bool alreadyTriggeredThisFrame;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerRb = other.attachedRigidbody;
        lastPlayerPosition = other.transform.position;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerRb = null;
        alreadyTriggeredThisFrame = false;
    }

    private void Update()
    {
        if (playerRb == null)
            return;

        if (alreadyTriggeredThisFrame)
            return;

        if (Time.time < lastEncounterTime + cooldownSeconds)
            return;

        if (!GameState.CanPlayerMove || playerRb.linearVelocity.sqrMagnitude < 0.01f)
        {
            lastPlayerPosition = playerRb.position;
            return;
        }

        if (encounterChancePerSecond > 0f)
        {
            float chance = encounterChancePerSecond * Time.deltaTime;
            TryRoll(chance);
        }
        else
        {
            float distance = Vector2.Distance(lastPlayerPosition, playerRb.position);
            if (distance >= stepDistanceThreshold)
            {
                lastPlayerPosition = playerRb.position;
                TryRoll(encounterChancePerStep);
            }
        }
    }

    private void LateUpdate()
    {
        alreadyTriggeredThisFrame = false;
    }

    private void TryRoll(float chance)
    {
        if (chance <= 0f)
            return;

        if (Random.value <= chance)
        {
            alreadyTriggeredThisFrame = true;
            lastEncounterTime = Time.time;
            Debug.Log("[RandomEncounterZone] Triggered random encounter.");

            if (EventManager.Instance != null)
                EventManager.Instance.StartRandomEvent(eventPoolOverride);
        }
    }
}
