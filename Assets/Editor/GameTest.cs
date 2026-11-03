using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only helpers used by the OpenCode bridge during play-mode smoke tests.
// Capture() saves the real game view (including the screen-space UI overlay),
// which the bridge's camera-only screenshot cannot see. Key() injects real
// keyboard state so movement, jumping and attacks run through the Input System.
public static class GameTest
{
    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static readonly HashSet<Key> held = new HashSet<Key>();

    public static string Capture(Dictionary<string, object> a)
    {
        string path = Path.Combine(Root, "Screenshots", "ui_" + DateTime.Now.ToString("HHmmss_fff") + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        ScreenCapture.CaptureScreenshot(path, 1);
        return path;
    }

    public static string State()
    {
        var gm = GameManager.Instance;
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var am = AudioManager.Instance;

        int alive = 0;
        foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
            if (m != null && !m.IsDead) alive++;

        string music = "no-audio";
        if (am != null)
        {
            var src = am.GetComponentInChildren<AudioSource>();
            music = src == null ? "no-source" : (src.isPlaying ? "playing vol=" + src.volume.ToString("F2") : "stopped");
        }

        float fade = -1f;
        if (UIManager.Instance != null && UIManager.Instance.fadeImage != null)
            fade = UIManager.Instance.fadeImage.color.a;

        string dialogue = DialogueSystem.Instance == null
            ? "none"
            : (DialogueSystem.Active ? "open" : "closed");

        string boss = "none";
        bool bossBar = false;
        foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (m == null || !m.IsBoss) continue;
            boss = m.Health + "/" + m.MaxHealth;
            bossBar = UIManager.Instance != null && UIManager.Instance.bossRoot != null &&
                      UIManager.Instance.bossRoot.activeSelf;
        }

        return string.Format(
            "state={0} kills={1}/{2} alive={3} hp={4} pos={5} vy={6} grounded={7} cp={8} fade={9} music={10} t={11} f={12} dlg={13} boss={14} bar={15}",
            gm != null ? gm.State.ToString() : "null",
            gm != null ? gm.Kills : -1,
            gm != null ? gm.TotalMonsters : -1,
            alive,
            pc != null ? pc.Health : -1,
            pc != null ? pc.transform.position.ToString("F2") : "none",
            pc != null ? pc.GetComponent<Rigidbody2D>().linearVelocity.y.ToString("F2") : "0",
            pc != null ? pc.IsGrounded.ToString() : "false",
            gm != null ? gm.Checkpoint.ToString("F2") : "none",
            fade.ToString("F2"),
            music,
            Time.timeSinceLevelLoad.ToString("F2"),
            Time.frameCount,
            dialogue,
            boss,
            bossBar.ToString());
    }

    public static string Probe()
    {
        var kb = Keyboard.current;
        if (kb == null) return "kb=NULL devices=" + InputSystem.devices.Count;
        return "kb=" + kb.displayName + " id=" + kb.deviceId +
               " enabled=" + kb.enabled + " added=" + kb.added +
               " d=" + kb.dKey.isPressed + " space=" + kb.spaceKey.isPressed;
    }

    public static string KillOne(Dictionary<string, object> a)
    {
        string path = a.ContainsKey("path") ? a["path"].ToString() : "";
        var go = GameObject.Find(path);
        if (go == null) return "no object " + path;
        var m = go.GetComponent<Monster>();
        if (m == null) return "no monster on " + path;
        try
        {
            m.DebugKill();
            return "killed " + path;
        }
        catch (Exception e)
        {
            return e.ToString();
        }
    }

    public static string Key(Dictionary<string, object> a)
    {
        string name = a.ContainsKey("name") ? a["name"].ToString() : "";
        bool down = a.ContainsKey("down") && Convert.ToBoolean(a["down"]);

        Key key;
        if (!Enum.TryParse(name, true, out key))
            return "unknown key " + name;

        if (down) held.Add(key);
        else held.Remove(key);

        var kb = Keyboard.current;
        if (kb != null)
        {
            var keys = new List<Key>(held).ToArray();
            InputSystem.QueueStateEvent(kb, keys.Length == 0 ? new KeyboardState() : new KeyboardState(keys));
        }

        var names = new List<string>();
        foreach (var k in held) names.Add(k.ToString());
        return "held=" + string.Join(",", names) + " | " + Probe();
    }

    public static string FocusGame()
    {
        // Route all device input to the game even if the Game view itself is not
        // the focused editor window, then focus it for good measure.
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        var type = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (type == null) return "no GameView type";

        EditorWindow win = null;
        foreach (var obj in Resources.FindObjectsOfTypeAll(type))
        {
            win = obj as EditorWindow;
            if (win != null) break;
        }
        if (win == null) return "no GameView window";
        win.Focus();
        return "focused " + win.titleContent.text + " | " + Probe();
    }

    public static string Teleport(Dictionary<string, object> a)
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return "no player";

        float x = Convert.ToSingle(a.ContainsKey("x") ? a["x"] : 0f);
        float y = Convert.ToSingle(a.ContainsKey("y") ? a["y"] : 0f);

        var rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = new Vector2(x, y);
            rb.linearVelocity = Vector2.zero;
        }
        pc.transform.position = new Vector3(x, y, 0f);
        Physics2D.SyncTransforms();
        if (CameraFollow.Instance != null) CameraFollow.Instance.SnapToTarget();
        return "teleported " + x + "," + y +
               " tf=" + pc.transform.position.ToString("F2") +
               " rb=" + (rb != null ? rb.position.ToString("F2") : "none") +
               " sim=" + (rb != null ? rb.simulated.ToString() : "?");
    }

    public static string Rb()
    {
        var pcs = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        var gms = UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
        var pc = pcs.Length > 0 ? pcs[0] : null;
        if (pc == null) return "no player";

        var rb = pc.GetComponent<Rigidbody2D>();
        string cam = CameraFollow.Instance != null
            ? CameraFollow.Instance.gameObject.name + "->" +
              (CameraFollow.Instance.target != null ? CameraFollow.Instance.target.gameObject.name : "null")
            : "none";

        string list = "";
        foreach (var p in pcs) list += p.gameObject.name + "@" + p.transform.position.ToString("F1") + " ";

        return string.Format(
            "players={0}[{1}] gms={2} go={3} parent={4} tf={5} rb={6} sim={7} type={8} vel={9} sleep={10} constraints={11} cam={12} gmcp={13} t={14} ft={15} ts={16} simmode={17}",
            pcs.Length, list.Trim(), gms.Length,
            pc.gameObject.name, pc.transform.parent != null ? pc.transform.parent.name : "root",
            pc.transform.position.ToString("F2"),
            rb != null ? rb.position.ToString("F2") : "none",
            rb != null ? rb.simulated.ToString() : "?",
            rb != null ? rb.bodyType.ToString() : "?",
            rb != null ? rb.linearVelocity.ToString("F2") : "?",
            rb != null ? rb.IsSleeping().ToString() : "?",
            rb != null ? rb.constraints.ToString() : "?",
            cam,
            gms.Length > 0 ? gms[0].Checkpoint.ToString("F2") : "none",
            Time.time.ToString("F2"),
            Time.fixedTime.ToString("F2"),
            Time.timeScale.ToString("F2"),
            Physics2D.simulationMode.ToString());
    }

    public static string Monsters()
    {
        var list = new List<string>();
        foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
            if (m != null)
                list.Add(m.gameObject.name + (m.IsDead ? "(dead)" : "") + "@" +
                         m.transform.position.ToString("F1"));
        return string.Join(" ", list);
    }

    public static string Hurt()
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return "no player";
        pc.DebugHurt();
        return "hurt hp=" + pc.Health;
    }

    public static string Heal()
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return "no player";
        pc.DebugHeal();
        return "healed hp=" + pc.Health;
    }

    public static string AttackNow()
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return "no player";
        pc.DebugAttackNow();
        return "attacked";
    }

    public static string FaceRight()
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return "no player";
        pc.DebugFace(1f);
        return "facing right";
    }

    public static string FaceLeft()
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc == null) return "no player";
        pc.DebugFace(-1f);
        return "facing left";
    }
}
