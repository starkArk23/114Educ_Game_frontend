using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CriticalErrorEffect : MonoBehaviour
{
    public Image redTintOverlay;
    private int lastCyberStatus = 100;


    void Start()
    {
        if (redTintOverlay != null)
        {
            SetAlpha(0f);
            redTintOverlay.raycastTarget = false;
        }
    }

    public void CheckCyberStatus(int currentStatus)
    {
        int damage = lastCyberStatus - currentStatus;

        if (damage >= 15)
        {
            StopAllCoroutines();
            StartCoroutine(BlinkRoutine());
        }

        lastCyberStatus = currentStatus;
    }

    IEnumerator BlinkRoutine()
    {
        SetAlpha(0.5f);
        yield return new WaitForSeconds(0.15f);
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        if (redTintOverlay == null) return;
        Color c = redTintOverlay.color;
        c.a = alpha;
        redTintOverlay.color = c;
    }
}