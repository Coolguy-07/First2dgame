using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Letterboxed conversation player. Lines are "Speaker|Text" strings supplied by
// DialogueTrigger or GameManager. Freezes the hero while a scene plays.
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;

    public GameObject panel;
    public Text speakerText;
    public Text bodyText;
    public Text hintText;
    public float charsPerSecond = 46f;
    public Color ashColor = new Color(0.96f, 0.96f, 0.86f, 1f);
    public Color emberColor = new Color(0.92f, 0.66f, 0.30f, 1f);
    public Color thorneColor = new Color(0.78f, 0.66f, 0.92f, 1f);

    string[] lines;
    int index;
    string speaker;
    string fullLine;
    bool playing;
    bool typing;
    PlayerController player;
    int openedFrame;
    Coroutine typer;

    public static bool Active { get { return Instance != null && Instance.playing; } }

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Play(string[] newLines)
    {
        if (playing || newLines == null || newLines.Length == 0) return false;

        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.Freeze();
        if (UIManager.Instance != null) UIManager.Instance.HidePrompt();

        lines = newLines;
        index = 0;
        playing = true;
        openedFrame = Time.frameCount;
        if (panel != null) panel.SetActive(true);
        if (GameManager.Instance != null) GameManager.Instance.OnDialogueOpened();
        StartLine();
        return true;
    }

    void Update()
    {
        if (!playing || Time.frameCount == openedFrame) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool advance = (kb != null && (kb.eKey.wasPressedThisFrame ||
                                       kb.spaceKey.wasPressedThisFrame ||
                                       kb.enterKey.wasPressedThisFrame)) ||
                       (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (!advance) return;

        if (typing)
        {
            ShowFullLine();
            return;
        }

        index++;
        if (index >= lines.Length) End();
        else StartLine();
    }

    void StartLine()
    {
        string raw = lines[index] ?? "";
        int split = raw.IndexOf('|');
        speaker = split >= 0 ? raw.Substring(0, split).Trim() : "";
        fullLine = split >= 0 ? raw.Substring(split + 1).Trim() : raw;

        if (speakerText != null)
        {
            speakerText.text = speaker.ToUpperInvariant();
            speakerText.color = speaker == "Ash" ? ashColor
                : speaker == "Thorne" ? thorneColor : emberColor;
        }
        if (bodyText != null) bodyText.text = "";
        if (hintText != null) hintText.text = "";

        if (typer != null) StopCoroutine(typer);
        typer = StartCoroutine(TypeLine());
    }

    IEnumerator TypeLine()
    {
        typing = true;
        float shown = 0f;
        int lastBlip = 0;
        while (shown < fullLine.Length)
        {
            shown += charsPerSecond * Time.unscaledDeltaTime;
            int count = Mathf.Clamp(Mathf.FloorToInt(shown), 0, fullLine.Length);
            if (bodyText != null) bodyText.text = fullLine.Substring(0, count);
            if (count - lastBlip >= 3)
            {
                lastBlip = count;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBlip(speaker);
            }
            yield return null;
        }
        ShowFullLine();
    }

    void ShowFullLine()
    {
        if (typer != null) StopCoroutine(typer);
        typing = false;
        if (bodyText != null) bodyText.text = fullLine;
        string hint = index >= lines.Length - 1 ? "E / SPACE   close" : "E / SPACE   continue";
        if (hintText != null) hintText.text = hint;
    }

    void End()
    {
        playing = false;
        typing = false;
        lines = null;
        if (panel != null) panel.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.OnDialogueClosed();
        bool canMove = GameManager.Instance == null ||
                       GameManager.Instance.State == GameManager.GameState.Playing;
        if (player != null && canMove) player.Unfreeze();
    }
}
