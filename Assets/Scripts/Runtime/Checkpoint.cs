using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public Vector3 respawnOffset = new Vector3(0f, 0.75f, 0f);
    public SpriteRenderer lantern;
    public Color dimColor = new Color(0.55f, 0.48f, 0.6f, 1f);
    public Color litColor = Color.white;

    bool used;

    void Awake()
    {
        if (lantern == null) lantern = GetComponentInChildren<SpriteRenderer>();
        if (lantern != null) lantern.color = dimColor;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        used = true;
        if (lantern != null) lantern.color = litColor;
        if (GameManager.Instance != null)
            GameManager.Instance.SetCheckpoint(transform.position + respawnOffset);
    }
}
