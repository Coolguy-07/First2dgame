using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Intro, Playing, Dead, Complete }

    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; }

    public Transform player;
    public Vector3 spawnPoint = new Vector3(1f, 0.75f, 0f);

    [Header("Conversations")]
    public string[] introLines;
    public string[] bossDefeatLines;
    public float bossArenaX = 49.9f;

    int kills;
    int totalMonsters;
    bool busy;
    bool bossMusicStarted;
    bool bossDefeated;
    bool gateAnnounced;
    PlayerController pc;
    UIManager ui;
    LevelGoal goal;
    Monster boss;
    Coroutine hitStopRoutine;

    public int Kills { get { return kills; } }
    public int TotalMonsters { get { return totalMonsters; } }
    public bool AllMonstersDefeated { get { return totalMonsters > 0 && kills >= totalMonsters; } }
    public Vector3 Checkpoint { get; private set; }

    void Awake()
    {
        Instance = this;
        State = GameState.Intro;
        Checkpoint = spawnPoint;

        // Keep the grove alive when the window is not focused (and let the
        // automated bridge tests drive real input while the editor is in the
        // background).
        Application.runInBackground = true;
#if UNITY_EDITOR
        if (InputSystem.settings != null)
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif

        pc = player != null ? player.GetComponent<PlayerController>() : null;
        if (pc == null) pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        ui = FindAnyObjectByType<UIManager>();
        goal = FindAnyObjectByType<LevelGoal>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator Start()
    {
        var monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        totalMonsters = monsters.Length;
        kills = 0;
        boss = null;
        foreach (var m in monsters)
            if (m != null && m.IsBoss) { boss = m; break; }

        if (pc != null) pc.Freeze();
        if (goal != null) goal.SetUnlocked(false);

        if (ui != null)
        {
            ui.SetHealth(pc != null ? pc.Health : 3, pc != null ? pc.MaxHealth : 3);
            ui.SetObjective(0, totalMonsters);
            ui.SetFade(1f);
            ui.HidePrompt();
            ui.HideBossBar();
        }

        yield return new WaitForSeconds(0.2f);

        if (ui != null)
            yield return ui.FadeTo(0f, 1.1f);

        var dialogue = DialogueSystem.Instance;
        if (dialogue != null && introLines != null && introLines.Length > 0)
        {
            dialogue.Play(introLines);
            while (DialogueSystem.Active) yield return null;
        }
        else if (ui != null)
        {
            yield return ui.ShowMessageRoutine("Dusk bleeds through the pines.", 2.1f);
            yield return ui.ShowMessageRoutine("Beyond the shades, the Dusk Gate waits.", 2.3f);
        }

        State = GameState.Playing;
        if (pc != null) pc.Unfreeze();

        if (ui != null)
        {
            ui.ShowPrompt("A / D   move        SPACE   jump        J   attack");
            StartCoroutine(HidePromptAfter(7f));
        }
    }

    void Update()
    {
        if (State == GameState.Dead || State == GameState.Complete)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
            return;
        }

        // The old theme slows and darkens when Thorne wakes.
        if (State == GameState.Playing && !bossMusicStarted && boss != null && !boss.IsDead &&
            pc != null && pc.transform.position.x > bossArenaX)
        {
            bossMusicStarted = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBossMusic();
        }
    }

    IEnumerator HidePromptAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (State == GameState.Playing && ui != null) ui.HidePrompt();
    }

    void Restart()
    {
        Time.timeScale = 1f;
        var active = SceneManager.GetActiveScene();
        if (active.buildIndex >= 0) SceneManager.LoadScene(active.buildIndex);
        else SceneManager.LoadScene(active.name);
    }

    public void OnDialogueOpened()
    {
        if (ui != null) ui.HidePrompt();
    }

    public void OnDialogueClosed()
    {
    }

    public void HitStop(float duration)
    {
        if (State != GameState.Playing || hitStopRoutine != null) return;
        hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.06f;
        yield return new WaitForSecondsRealtime(duration);
        if (State == GameState.Playing) Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    public void OnMonsterKilled(bool brute)
    {
        kills++;
        if (ui != null) ui.SetObjective(kills, totalMonsters);

        if (kills == 1 && ui != null) ui.ShowBark("One less shadow in the grove.");

        if (AllMonstersDefeated)
        {
            if (goal != null) goal.SetUnlocked(true);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPortalOpen();
            // If the boss was the last shade, let the defeat scene finish
            // before the gate announcement.
            if (!brute) AnnounceGate();
        }
    }

    void AnnounceGate()
    {
        if (gateAnnounced) return;
        gateAnnounced = true;
        if (ui != null) ui.ShowMessage("The Dusk Gate opens.", 2.6f);
    }

    public void OnBossDefeated()
    {
        if (bossDefeated) return;
        bossDefeated = true;
        StartCoroutine(BossDefeatRoutine());
    }

    IEnumerator BossDefeatRoutine()
    {
        // Brief slow-motion on the killing blow, then let the scene breathe.
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(0.95f);
        Time.timeScale = 1f;

        if (CameraFollow.Instance != null) CameraFollow.Instance.SetBossZoom(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic();

        if (State != GameState.Playing) yield break;

        if (pc != null) pc.Freeze();
        var dialogue = DialogueSystem.Instance;
        if (dialogue != null && bossDefeatLines != null && bossDefeatLines.Length > 0)
        {
            dialogue.Play(bossDefeatLines);
            while (DialogueSystem.Active) yield return null;
        }
        if (State == GameState.Playing && pc != null) pc.Unfreeze();

        if (AllMonstersDefeated) AnnounceGate();
    }

    public void SetCheckpoint(Vector3 position)
    {
        Checkpoint = position;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCheckpoint();
        if (ui != null) ui.ShowBark("Ember lit. The grove remembers.");
    }

    public void PlayerFell()
    {
        if (busy || State != GameState.Playing) return;
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        busy = true;
        if (pc != null) pc.Freeze();
        if (ui != null) yield return ui.FadeTo(1f, 0.22f);
        yield return new WaitForSeconds(0.08f);

        if (pc != null)
        {
            pc.Respawn(Checkpoint);
            pc.Freeze();
        }
        if (CameraFollow.Instance != null) CameraFollow.Instance.SnapToTarget();

        yield return new WaitForSeconds(0.08f);
        if (ui != null) yield return ui.FadeTo(0f, 0.3f);
        if (pc != null) pc.Unfreeze();
        busy = false;
        if (ui != null) ui.ShowBark("The lantern pulls me back.");
    }

    public void OnPlayerDied()
    {
        if (State != GameState.Playing) return;
        State = GameState.Dead;
        Time.timeScale = 1f;
        if (ui != null) ui.HideBossBar();
        if (CameraFollow.Instance != null) CameraFollow.Instance.SetBossZoom(false);
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        if (ui != null)
        {
            // Keep a sliver of the grove behind the death words.
            yield return ui.FadeTo(0.82f, 0.6f);
            ui.ShowMessage("The dusk takes me.", 4f);
            ui.ShowPrompt("R   rise again");
        }
    }

    public void LevelComplete()
    {
        if (State != GameState.Playing) return;
        State = GameState.Complete;
        if (pc != null) pc.Freeze();
        if (ui != null) ui.HideBossBar();
        if (CameraFollow.Instance != null) CameraFollow.Instance.SetBossZoom(false);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
            AudioManager.Instance.PlayVictory();
        }
        if (ui != null) ui.SetObjective(totalMonsters, totalMonsters);
        StartCoroutine(CompleteRoutine());
    }

    IEnumerator CompleteRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        if (ui == null) yield break;

        ui.ShowMessage("The Dusk Gate opens to my resolve.", 3.2f);
        yield return new WaitForSeconds(3.4f);
        ui.ShowBark("Walk with me, Ember.", 3f);
        yield return new WaitForSeconds(3.0f);
        ui.ShowPrompt("R   play again");
    }
}
