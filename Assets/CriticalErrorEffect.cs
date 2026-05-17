using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CriticalErrorEffect : MonoBehaviour
{
    public Image redTintOverlay;
    private int lastCyberStatus = -1; // -1 = uninitialized; syncs to session on first check

    void Start()
    {
        if (redTintOverlay != null)
        {
            redTintOverlay.raycastTarget = false;
            redTintOverlay.enabled = false;
        }
    }

    public void CheckCyberStatus(int currentStatus)
    {
        // First call: sync baseline so a save-load restore doesn't trigger a false flash.
        if (lastCyberStatus < 0)
        {
            lastCyberStatus = currentStatus;
            return;
        }

        int damage = lastCyberStatus - currentStatus;

        if (damage > 0)
        {
            StopAllCoroutines();
            StartCoroutine(BlinkRoutine());
        }

        lastCyberStatus = currentStatus;
    }

    IEnumerator BlinkRoutine()
    {
        if (redTintOverlay == null) yield break;
        redTintOverlay.enabled = true;
        SetAlpha(0.6f);
        yield return new WaitForSecondsRealtime(0.12f);
        SetAlpha(0.3f);
        yield return new WaitForSecondsRealtime(0.08f);
        SetAlpha(0.5f);
        yield return new WaitForSecondsRealtime(0.12f);
        SetAlpha(0f);
        redTintOverlay.enabled = false;
    }

    private void SetAlpha(float alpha)
    {
        if (redTintOverlay == null) return;
        Color c = redTintOverlay.color;
        c.a = alpha;
        redTintOverlay.color = c;
    }
}