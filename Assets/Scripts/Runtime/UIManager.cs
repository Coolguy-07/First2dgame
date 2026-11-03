using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public Image[] hearts;
    public Text messageText;
    public Text promptText;
    public Text objectiveText;
    public Image fadeImage;

    [Header("Boss")]
    public GameObject bossRoot;
    public Text bossNameText;
    public Image bossFillImage;

    [Header("Feedback")]
    public Image damageFlash;

    public Color messageColor = new Color(0.96f, 0.96f, 0.86f, 1f);
    public Color barkColor = new Color(0.88f, 0.60f, 0.23f, 1f);
    public Color promptColor = new Color(0.72f, 0.66f, 0.79f, 1f);

    Coroutine messageRoutine;
    Coroutine flashRoutine;
    int lastHealth = -1;

    void Awake()
    {
        Instance = this;
        if (bossRoot != null) bossRoot.SetActive(false);
        if (damageFlash != null) SetFlashAlpha(0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetHealth(int current, int max)
    {
        if (hearts == null) return;

        int lost = lastHealth >= 0 && current < lastHealth ? current : -1;
        lastHealth = current;

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;
            bool on = i < current;
            hearts[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.16f);
            hearts[i].transform.localScale = on ? Vector3.one : new Vector3(0.88f, 0.88f, 1f);
        }

        if (lost >= 0 && lost < hearts.Length && hearts[lost] != null)
            StartCoroutine(PulseHeart(hearts[lost].transform));
    }

    IEnumerator PulseHeart(Transform heart)
    {
        float t = 0f;
        const float duration = 0.4f;
        while (t < duration && heart != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.65f * (1f - k);
            heart.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (heart != null) heart.localScale = new Vector3(0.88f, 0.88f, 1f);
    }

    public void SetObjective(int killed, int total)
    {
        if (objectiveText == null) return;
        bool open = total > 0 && killed >= total;
        objectiveText.text = open ? "THE GATE IS OPEN" : "SHADES   " + killed + " / " + total;
        objectiveText.color = open
            ? new Color(0.96f, 0.84f, 0.50f, 1f)
            : new Color(0.72f, 0.66f, 0.79f, 0.9f);
    }

    public void ShowBossBar(string displayName, int health, int max, bool phase2)
    {
        if (bossRoot == null) return;
        if (!bossRoot.activeSelf) bossRoot.SetActive(true);

        if (bossNameText != null)
            bossNameText.text = string.IsNullOrEmpty(displayName) ? "THORNE" : displayName.ToUpperInvariant();

        if (bossFillImage != null)
        {
            bossFillImage.fillAmount = max > 0 ? Mathf.Clamp01((float)health / max) : 0f;
            bossFillImage.color = phase2
                ? new Color(1f, 0.46f, 0.22f, 1f)
                : new Color(0.86f, 0.32f, 0.32f, 1f);
        }
    }

    public void HideBossBar()
    {
        if (bossRoot != null) bossRoot.SetActive(false);
    }

    public void FlashDamage()
    {
        if (damageFlash == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashDamageRoutine());
    }

    IEnumerator FlashDamageRoutine()
    {
        float t = 0f;
        while (t < 0.07f)
        {
            t += Time.unscaledDeltaTime;
            SetFlashAlpha(Mathf.Lerp(0f, 0.34f, Mathf.Clamp01(t / 0.07f)));
            yield return null;
        }
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            SetFlashAlpha(Mathf.Lerp(0.34f, 0f, Mathf.Clamp01(t / 0.4f)));
            yield return null;
        }
        SetFlashAlpha(0f);
        flashRoutine = null;
    }

    void SetFlashAlpha(float alpha)
    {
        if (damageFlash == null) return;
        var c = damageFlash.color;
        damageFlash.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
    }

    public void ShowMessage(string text, float hold = 2.4f)
    {
        ShowMessageInternal(text, hold, messageColor);
    }

    public void ShowBark(string text, float hold = 1.8f)
    {
        ShowMessageInternal(text, hold, barkColor);
    }

    public IEnumerator ShowMessageRoutine(string text, float hold)
    {
        yield return MessageRoutine(text, hold, messageColor);
    }

    void ShowMessageInternal(string text, float hold, Color color)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text, hold, color));
    }

    IEnumerator MessageRoutine(string text, float hold, Color color)
    {
        if (messageText == null) yield break;

        messageText.text = text;
        messageText.color = new Color(color.r, color.g, color.b, 0f);

        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            messageText.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(t / 0.22f));
            yield return null;
        }
        messageText.color = color;

        yield return new WaitForSeconds(hold);

        t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            messageText.color = new Color(color.r, color.g, color.b, 1f - Mathf.Clamp01(t / 0.35f));
            yield return null;
        }
        messageText.text = "";
        messageText.color = color;
    }

    public void ShowPrompt(string text)
    {
        if (promptText == null) return;
        promptText.text = text;
        promptText.color = promptColor;
    }

    public void HidePrompt()
    {
        if (promptText == null) return;
        promptText.text = "";
    }

    public void SetFade(float alpha)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color;
        fadeImage.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
    }

    public IEnumerator FadeTo(float target, float duration)
    {
        if (fadeImage == null) yield break;
        float start = fadeImage.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetFade(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetFade(target);
    }
}
