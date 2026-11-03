using UnityEngine;

// A once-only conversation volume. Place it in the level; the first time the
// hero walks in, the lines play and the hero is frozen by DialogueSystem.
public class DialogueTrigger : MonoBehaviour
{
    public string[] lines;
    public bool once = true;
    public bool requireGateOpen;

    bool fired;

    public void Fire()
    {
        if (fired && once) return;
        if (GameManager.Instance == null ||
            GameManager.Instance.State != GameManager.GameState.Playing) return;
        if (requireGateOpen && !GameManager.Instance.AllMonstersDefeated) return;
        if (DialogueSystem.Instance == null) return;
        if (DialogueSystem.Instance.Play(lines)) fired = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        Fire();
    }
}
