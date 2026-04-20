using System.Collections;
using TMPro;
using UnityEngine;

public class ChoiceLogUI : MonoBehaviour
{
    public TMP_Text logText;
    public float showSeconds = 2f;

    Coroutine routine;

    void Awake()
    {
        if (logText == null) logText = GetComponent<TMP_Text>();
        logText.text = "";
    }

    public void Show(string message)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine(message));
    }

    IEnumerator ShowRoutine(string message)
    {
        logText.text = message;

        // fade in
        SetAlpha(1f);

        yield return new WaitForSeconds(showSeconds);

        // fade out
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(1f, 0f, t / 0.35f));
            yield return null;
        }

        logText.text = "";
        SetAlpha(1f);
        routine = null;
    }

    void SetAlpha(float a)
    {
        var c = logText.color;
        c.a = a;
        logText.color = c;
    }
}