using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CyberStatusBarUI : MonoBehaviour
{
    [Serializable]
    private struct VisualState
    {
        public int exactCyberStatus;
        public Sprite sprite;
    }

    [SerializeField] private Image targetImage;
    [SerializeField] private List<VisualState> visualStates = new List<VisualState>
    {
        new VisualState { exactCyberStatus = 100 },
        new VisualState { exactCyberStatus = 80 },
        new VisualState { exactCyberStatus = 65 },
        new VisualState { exactCyberStatus = 50 },
        new VisualState { exactCyberStatus = 35 },
        new VisualState { exactCyberStatus = 20 },
        new VisualState { exactCyberStatus = 0 }
    };

    private GameSession session;

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        session = GameSession.Instance;
        if (session != null)
            session.CyberStatusChanged += OnCyberStatusChanged;

        RefreshVisual(force: true);
    }

    private void OnDisable()
    {
        if (session != null)
            session.CyberStatusChanged -= OnCyberStatusChanged;

        session = null;
    }

    private void OnValidate()
    {
        for (int i = 0; i < visualStates.Count; i++)
        {
            if (!GameSession.IsCyberStatusStepAligned(visualStates[i].exactCyberStatus))
                Debug.LogWarning($"[CyberStatusBarUI] Exact cyber status {visualStates[i].exactCyberStatus} should be divisible by {GameSession.CyberStatusStep}.", this);
        }
    }

    private void OnCyberStatusChanged(GameSession.CyberStatusChange change)
    {
        RefreshVisual(force: false, cyberStatusOverride: change.currentValue);
    }

    public void RefreshVisual(bool force = false)
    {
        int currentCyberStatus = session != null ? session.CurrentCyberStatus : GameSession.Instance.CurrentCyberStatus;
        RefreshVisual(force, currentCyberStatus);
    }

    private void RefreshVisual(bool force, int cyberStatusOverride)
    {
        if (targetImage == null)
            return;

        if (!TryGetSpriteForStatus(cyberStatusOverride, out Sprite sprite))
            return;

        if (force || targetImage.sprite != sprite)
            targetImage.sprite = sprite;
    }

    private bool TryGetSpriteForStatus(int cyberStatus, out Sprite sprite)
    {
        for (int i = 0; i < visualStates.Count; i++)
        {
            if (visualStates[i].exactCyberStatus != cyberStatus)
                continue;

            sprite = visualStates[i].sprite;
            return sprite != null;
        }

        sprite = null;
        return false;
    }
}