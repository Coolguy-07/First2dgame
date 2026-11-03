using UnityEngine;

public class LevelGoal : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Color lockedColor = new Color(0.5f, 0.44f, 0.6f, 1f);
    public Color openColor = Color.white;

    bool unlocked;
    bool fired;
    float pulseTime;
    float warnCooldown;

    public void SetUnlocked(bool value)
    {
        unlocked = value;
        if (spriteRenderer != null) spriteRenderer.color = value ? openColor : lockedColor;
    }

    void Update()
    {
        pulseTime += Time.deltaTime;
        float speed = unlocked ? 2.4f : 0.9f;
        float amount = unlocked ? 0.045f : 0.012f;
        float s = 1f + Mathf.Sin(pulseTime * speed) * amount;
        transform.localScale = new Vector3(s, s, 1f);

        if (warnCooldown > 0f) warnCooldown -= Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (fired) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        if (!unlocked)
        {
            if (warnCooldown <= 0f && UIManager.Instance != null)
            {
                UIManager.Instance.ShowBark("The gate is sealed. Silence the grove.", 2.2f);
                warnCooldown = 2.5f;
            }
            return;
        }

        fired = true;
        if (GameManager.Instance != null) GameManager.Instance.LevelComplete();
    }
}
