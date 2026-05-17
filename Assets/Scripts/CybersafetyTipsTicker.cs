using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays randomly ordered cybersafety tips in the top-left corner of the main menu.
/// Each tip appears with a typewriter effect and stays on screen for <see cref="displaySeconds"/> before
/// fading out and being replaced by the next randomly chosen tip.
/// </summary>
public class CybersafetyTipsTicker : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text tipLabel;
    [SerializeField] private TMP_Text headerLabel;

    [Header("Timing")]
    [SerializeField] private float displaySeconds = 10f;
    [SerializeField] private float typewriterCharDelay = 0.03f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float fadeInDuration = 0.2f;

    // ── Tip bank ────────────────────────────────────────────────────────────────
    private static readonly string[] Tips =
    {
        "Use strong, unique passwords for every account.",
        "Enable two-factor authentication (2FA) wherever possible.",
        "Never share your password with anyone, including IT staff.",
        "Keep your operating system and software updated.",
        "Use a reputable antivirus and keep it updated.",
        "Don't click links in unexpected emails or messages.",
        "Verify the sender before opening email attachments.",
        "Use a password manager instead of reusing passwords.",
        "Back up your data regularly — follow the 3-2-1 rule.",
        "Lock your screen when you step away from your device.",
        "Avoid using public Wi-Fi for sensitive tasks.",
        "Use a VPN when connecting to public networks.",
        "Check that websites use HTTPS before entering personal data.",
        "Log out of accounts when using shared or public computers.",
        "Be cautious of unsolicited phone calls asking for personal info.",
        "Don't plug in unknown USB drives or external devices.",
        "Review app permissions — only grant what's necessary.",
        "Use a separate email address for online sign-ups.",
        "Enable full-disk encryption on your devices.",
        "Regularly review which apps have access to your accounts.",
        "Phishing emails often create urgency — slow down and verify.",
        "Never enter login credentials on a site you reached via a link.",
        "Delete accounts you no longer use.",
        "Set your social media profiles to private by default.",
        "Don't overshare personal details on social media.",
        "Use biometric locks on your mobile devices.",
        "Enable 'Find My Device' features on phones and laptops.",
        "Treat free public charging stations with caution — use your own charger.",
        "Monitor your bank and credit card statements regularly.",
        "Freeze your credit if you're not actively applying for loans.",
        "'Too good to be true' online offers are usually scams.",
        "Don't reuse security questions — treat them like passwords.",
        "Keep your router firmware updated and change default credentials.",
        "Use a guest network for IoT and smart home devices.",
        "Disable Bluetooth when you're not actively using it.",
        "Hover over links to preview the URL before clicking.",
        "Be skeptical of pop-ups claiming your device is infected.",
        "Report phishing attempts to your email provider or IT team.",
        "Don't store passwords in your browser on shared devices.",
        "Regularly audit who has access to your shared files and drives.",
        "Use encrypted messaging apps for sensitive conversations.",
        "Use fictitious but memorable answers for security questions.",
        "Treat your personal devices as seriously as work devices.",
        "Be aware of shoulder surfing in public places.",
        "Don't auto-connect to Wi-Fi networks in public spaces.",
        "Enable login notifications so you're alerted to new sign-ins.",
        "Educate family members about cybersafety, especially children and the elderly.",
        "Check breach databases to see if your info was exposed.",
        "When in doubt — don't click, don't share, and ask for help.",
        "Cybersecurity is everyone's responsibility."
    };

    // ── State ────────────────────────────────────────────────────────────────────
    private int[] _shuffledIndices;
    private int _cursor;
    private Coroutine _tickerRoutine;

    // ── Unity ────────────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        BuildShuffledDeck();
        _tickerRoutine = StartCoroutine(TickerLoop());
    }

    private void OnDisable()
    {
        if (_tickerRoutine != null)
        {
            StopCoroutine(_tickerRoutine);
            _tickerRoutine = null;
        }
    }

    // ── Core loop ────────────────────────────────────────────────────────────────
    private IEnumerator TickerLoop()
    {
        if (tipLabel == null)
            yield break;

        SetAlpha(0f);

        while (true)
        {
            string tip = NextTip();

            // Fade in
            yield return StartCoroutine(FadeAlpha(0f, 1f, fadeInDuration));

            // Typewrite
            yield return StartCoroutine(TypewriteTip(tip));

            // Hold
            float holdRemaining = displaySeconds - typewriterCharDelay * tip.Length - fadeInDuration;
            if (holdRemaining > 0f)
                yield return new WaitForSecondsRealtime(holdRemaining);

            // Fade out
            yield return StartCoroutine(FadeAlpha(1f, 0f, fadeOutDuration));

            tipLabel.text = string.Empty;
        }
    }

    private IEnumerator TypewriteTip(string text)
    {
        tipLabel.text = string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            tipLabel.text += text[i];
            yield return new WaitForSecondsRealtime(typewriterCharDelay);
        }
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        if (tipLabel != null)
        {
            Color c = tipLabel.color;
            c.a = a;
            tipLabel.color = c;
        }

        if (headerLabel != null)
        {
            Color c = headerLabel.color;
            c.a = a;
            headerLabel.color = c;
        }
    }

    // ── Shuffle / deck ───────────────────────────────────────────────────────────
    private void BuildShuffledDeck()
    {
        _shuffledIndices = new int[Tips.Length];
        for (int i = 0; i < Tips.Length; i++)
            _shuffledIndices[i] = i;

        // Fisher-Yates shuffle
        for (int i = Tips.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }

        _cursor = 0;
    }

    private string NextTip()
    {
        if (_cursor >= _shuffledIndices.Length)
            BuildShuffledDeck(); // reshuffle once the deck is exhausted

        return Tips[_shuffledIndices[_cursor++]];
    }
}
