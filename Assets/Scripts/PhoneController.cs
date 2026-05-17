using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PhoneController : MonoBehaviour
{
    private const string MovementLockId = "PhoneController";

    [System.Serializable]
    private struct StoryPhoneBeat
    {
        public string nodeKey;
        public string interactionId;
        public string groupKey;
        public string titleText;
        [TextArea(2, 5)] public string bodyText;
    }

    private static readonly StoryPhoneBeat[] DefaultStoryPhoneBeats =
    {
        new StoryPhoneBeat
        {
            nodeKey = "opening.room_free_roam",
            interactionId = "opening.room_free_roam.phone",
            groupKey = "opening.system_core_phone",
            titleText = "INCOMING CALL",
            bodyText = "The device is ringing. This is the call that wakes the operator in the System Core.\n\nPress Tab to answer and continue the story."
        },
        new StoryPhoneBeat
        {
            nodeKey = "opening.phone_ring",
            interactionId = "opening.phone_ring.phone",
            groupKey = "opening.system_core_phone_followup",
            titleText = "INCOMING CALL",
            bodyText = "The device is ringing again. Press Tab to answer and continue the next part of the story."
        }
    };

    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private GameObject phonePanel;
    [SerializeField] private TMP_Text phoneTitleText;
    [SerializeField] private TMP_Text phoneBodyText;

    [Header("Story Phone")]
    [SerializeField] private StoryManager storyManager;
    [SerializeField] private StoryPhoneBeat[] storyPhoneBeats = DefaultStoryPhoneBeats;

    private bool isOpen;
    private GameSession session;

    private void Awake()
    {
        EnsureStoryPhoneBeats();
        SetPhoneVisible(false);
    }

    private void OnEnable()
    {
        session = GameSession.Instance;
        if (session != null)
        {
            session.CyberStatusChanged += OnSessionStatsChanged;
            session.TrustTokensChanged += OnTrustTokensChanged;
        }
    }

    private void OnDisable()
    {
        if (session != null)
        {
            session.CyberStatusChanged -= OnSessionStatsChanged;
            session.TrustTokensChanged -= OnTrustTokensChanged;
        }

        session = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isOpen)
            {
                // If a story phone beat is active, Tab answers the call and closes the panel.
                TryAnswerStoryPhone();
                ClosePhone();
            }
            else if (PlayerMovement.CanMove || TryGetActiveStoryPhoneBeat(out _))
            {
                // Allow Tab to answer a ringing story phone even when movement is locked.
                HandlePhoneInput();
            }
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            ClosePhone();
    }

    private void HandlePhoneInput()
    {
        if (TryAnswerStoryPhone())
            return;

        OpenPhone();
    }

    public void OpenPhone()
    {
        if (phonePanel == null)
            return;

        RefreshPhoneText();
        SetPhoneVisible(true);
        isOpen = true;
        GameState.CanPlayerMove = false;
        PlayerMovement.AddMovementLock(MovementLockId);
    }

    public void ClosePhone()
    {
        SetPhoneVisible(false);
        isOpen = false;
        GameState.CanPlayerMove = true;
        PlayerMovement.RemoveMovementLock(MovementLockId);
    }

    private void OnSessionStatsChanged(GameSession.CyberStatusChange _change)
    {
        if (isOpen)
            RefreshPhoneText();
    }

    private void OnTrustTokensChanged(GameSession.TrustTokenChange _change)
    {
        if (isOpen)
            RefreshPhoneText();
    }

    private void RefreshPhoneText()
    {
        bool hasActiveStoryPhoneBeat = TryGetActiveStoryPhoneBeat(out StoryPhoneBeat storyPhoneBeat);

        if (phoneTitleText != null)
            phoneTitleText.text = hasActiveStoryPhoneBeat && !string.IsNullOrWhiteSpace(storyPhoneBeat.titleText)
                ? storyPhoneBeat.titleText
                : (hasActiveStoryPhoneBeat ? "INCOMING CALL" : "FIELD DEVICE");

        if (phoneBodyText == null)
            return;

        GameSession activeSession = session ?? GameSession.Instance;
        string operatorName = !string.IsNullOrWhiteSpace(activeSession.OperatorName)
            ? activeSession.OperatorName
            : (string.IsNullOrWhiteSpace(LoadingScreen.operatorName) ? "UNKNOWN" : LoadingScreen.operatorName);

        phoneBodyText.text =
            BuildPhoneBody(operatorName, activeSession);
    }

    private string BuildPhoneBody(string operatorName, GameSession session)
    {
        if (TryGetActiveStoryPhoneBeat(out StoryPhoneBeat storyPhoneBeat))
            return string.IsNullOrWhiteSpace(storyPhoneBeat.bodyText)
                ? "The device is ringing. Press Tab to answer and continue the story."
                : storyPhoneBeat.bodyText;

        return
            "OPERATOR: " + operatorName + "\n" +
            "CYBERSTATUS: " + session.CurrentCyberStatus + "\n" +
            "TRUST TOKENS: " + session.CurrentTrustTokens + "\n\n" +
            "Press Tab to close.";
    }

    private bool TryAnswerStoryPhone()
    {
        if (!TryGetActiveStoryPhoneBeat(out StoryPhoneBeat storyPhoneBeat))
            return false;

        StoryManager manager = ResolveStoryManager();
        if (manager == null)
            return false;

        string interactionId = string.IsNullOrWhiteSpace(storyPhoneBeat.interactionId)
            ? storyPhoneBeat.nodeKey
            : storyPhoneBeat.interactionId;

        manager.HandleWorldInteraction(interactionId, storyPhoneBeat.groupKey, "PHONE", string.Empty, string.Empty);
        return true;
    }

    private bool TryGetActiveStoryPhoneBeat(out StoryPhoneBeat activeBeat)
    {
        activeBeat = default;
        EnsureStoryPhoneBeats();

        StoryManager manager = ResolveStoryManager();
        if (manager == null || storyPhoneBeats == null)
            return false;

        string currentNodeKey = manager.CurrentNodeKey;
        for (int index = 0; index < storyPhoneBeats.Length; index++)
        {
            StoryPhoneBeat storyPhoneBeat = storyPhoneBeats[index];
            if (string.IsNullOrWhiteSpace(storyPhoneBeat.nodeKey) || string.IsNullOrWhiteSpace(storyPhoneBeat.groupKey))
                continue;

            if (!string.Equals(currentNodeKey, storyPhoneBeat.nodeKey, System.StringComparison.Ordinal))
                continue;

            activeBeat = storyPhoneBeat;
            return true;
        }

        return false;
    }

    private void EnsureStoryPhoneBeats()
    {
        if (storyPhoneBeats == null || storyPhoneBeats.Length == 0)
            storyPhoneBeats = DefaultStoryPhoneBeats;
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }

    private void SetPhoneVisible(bool visible)
    {
        if (phonePanel != null)
            phonePanel.SetActive(visible);
    }

    private void EnsurePhoneUi()
    {
        // Intentionally left blank. The phone UI must be scene-authored if used.
    }
}