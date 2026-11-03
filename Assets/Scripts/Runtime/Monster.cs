using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Monster : MonoBehaviour
{
    public enum Kind { Slime, Stalker, Brute, Boss }

    public string displayName = "Slime";
    public Kind kind = Kind.Slime;
    public int maxHealth = 1;
    public int contactDamage = 1;
    public float moveSpeed = 1.4f;
    public float leftX;
    public float rightX;
    public bool isBrute;
    public float deathDuration = 0.4f;
    public Material fxMaterial;
    public Sprite waveSprite;
    public Material waveMaterial;

    [Header("Boss")]
    public int phase2Health = 3;
    public float slamWindup = 0.7f;
    public float chargeWindup = 0.5f;

    SpriteRenderer sr;
    Collider2D col;
    Rigidbody2D rb;
    int health;
    int direction = 1;
    float waitTimer;
    float animTimer;
    bool dead;
    Vector3 baseScale;
    Color baseColor;
    Color originalColor;
    float flashTimer;

    // stalker
    int stalkState;          // 0 patrol, 1 windup, 2 lunge, 3 recover
    float stalkTimer;
    float stalkCooldown;
    float lungeDir = 1f;

    // boss
    int bossState;           // 0 idle, 1 slam windup, 2 recover, 3 charge windup, 4 charge
    float bossTimer;
    float bossCooldown;
    float chargeDir = 1f;
    bool engaged;

    public bool IsDead { get { return dead; } }
    public int Health { get { return health; } }
    public int MaxHealth { get { return maxHealth; } }
    public bool IsBoss { get { return kind == Kind.Boss; } }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        health = maxHealth;
        baseScale = transform.localScale;
        if (sr != null) baseColor = originalColor = sr.color;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.freezeRotation = true;
        }
        isBrute = kind == Kind.Brute || kind == Kind.Boss;
        bossCooldown = 1.4f;
        stalkCooldown = Random.Range(0.6f, 1.4f);
    }

    Transform Player
    {
        get
        {
            var pc = GameManager.Instance != null && GameManager.Instance.player != null
                ? GameManager.Instance.player.GetComponent<PlayerController>()
                : FindAnyObjectByType<PlayerController>();
            return pc != null && !pc.IsDead ? pc.transform : null;
        }
    }

    void FixedUpdate()
    {
        if (dead) return;

        // Conversations freeze the grove: no patrols, lunges or boss attacks
        // while the dialogue panel is up.
        if (DialogueSystem.Active) return;

        if (kind == Kind.Boss) { BossFixed(); return; }
        if (kind == Kind.Stalker) { StalkerFixed(); return; }

        Patrol();
    }

    void Patrol()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }
        if (rb == null) return;

        Vector2 pos = rb.position;
        if (direction > 0 && pos.x >= rightX) { direction = -1; waitTimer = 0.35f; }
        else if (direction < 0 && pos.x <= leftX) { direction = 1; waitTimer = 0.35f; }

        float nextX = Mathf.Clamp(pos.x + direction * moveSpeed * Time.fixedDeltaTime, leftX, rightX);
        rb.MovePosition(new Vector2(nextX, pos.y));
        if (sr != null) sr.flipX = direction < 0;
    }

    void StalkerFixed()
    {
        if (rb == null) return;
        var player = Player;
        Vector2 pos = rb.position;

        if (stalkState == 1) // crouched windup
        {
            stalkTimer -= Time.fixedDeltaTime;
            if (player != null)
            {
                lungeDir = player.position.x >= pos.x ? 1f : -1f;
                if (sr != null) sr.flipX = lungeDir < 0f;
            }
            if (stalkTimer <= 0f)
            {
                stalkState = 2;
                stalkTimer = 0.22f;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayLunge();
            }
            return;
        }

        if (stalkState == 2) // lunge
        {
            stalkTimer -= Time.fixedDeltaTime;
            float x = Mathf.Clamp(pos.x + lungeDir * 10f * Time.fixedDeltaTime, leftX, rightX);
            rb.MovePosition(new Vector2(x, pos.y));
            if (stalkTimer <= 0f)
            {
                stalkState = 3;
                stalkTimer = 0.7f;
            }
            return;
        }

        if (stalkState == 3) // exposed recovery
        {
            stalkTimer -= Time.fixedDeltaTime;
            if (stalkTimer <= 0f)
            {
                stalkState = 0;
                stalkCooldown = 1.7f;
            }
            return;
        }

        stalkCooldown -= Time.fixedDeltaTime;
        if (stalkCooldown <= 0f && player != null)
        {
            float dx = Mathf.Abs(player.position.x - pos.x);
            float dy = Mathf.Abs(player.position.y - pos.y);
            if (dx < 6f && dy < 2.2f)
            {
                stalkState = 1;
                stalkTimer = 0.55f;
                return;
            }
        }

        Patrol();
    }

    void BossFixed()
    {
        if (rb == null) return;
        var player = Player;
        Vector2 pos = rb.position;
        bool phase2 = health <= phase2Health;

        if (player != null)
        {
            float toward = player.position.x >= pos.x ? 1f : -1f;
            if (sr != null) sr.flipX = toward < 0f;
        }

        bossTimer -= Time.fixedDeltaTime;
        bossCooldown -= Time.fixedDeltaTime;

        if (bossState == 1) // slam windup
        {
            bossTimer -= Time.fixedDeltaTime;
            if (bossTimer <= 0f)
            {
                // Two crescents race out along the floor; jump them.
                Shockwave.Spawn(transform.position, -1f, waveSprite, waveMaterial);
                Shockwave.Spawn(transform.position, 1f, waveSprite, waveMaterial);
                if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.5f, 0.3f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySlam();
                bossState = 2;
                bossTimer = 0.85f;
            }
            return;
        }

        if (bossState == 2) // recovery
        {
            bossTimer -= Time.fixedDeltaTime;
            if (bossTimer <= 0f)
            {
                bossState = 0;
                bossCooldown = phase2 ? 1.5f : 2.1f;
            }
            return;
        }

        if (bossState == 3) // charge windup
        {
            bossTimer -= Time.fixedDeltaTime;
            if (player != null) chargeDir = player.position.x >= pos.x ? 1f : -1f;
            if (bossTimer <= 0f)
            {
                bossState = 4;
                bossTimer = 0.45f;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayLunge();
            }
            return;
        }

        if (bossState == 4) // charge
        {
            bossTimer -= Time.fixedDeltaTime;
            float x = Mathf.Clamp(pos.x + chargeDir * 10.5f * Time.fixedDeltaTime, leftX, rightX);
            rb.MovePosition(new Vector2(x, pos.y));
            if (bossTimer <= 0f)
            {
                bossState = 2;
                bossTimer = 0.7f;
                if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.25f, 0.15f);
            }
            return;
        }

        // idle: drift toward the hero, then pick an attack
        if (player != null)
        {
            float dx = player.position.x - pos.x;
            if (Mathf.Abs(dx) > 1.7f)
            {
                float x = Mathf.Clamp(pos.x + Mathf.Sign(dx) * 0.95f * Time.fixedDeltaTime, leftX, rightX);
                rb.MovePosition(new Vector2(x, pos.y));
            }

            // Thorne waits at the arena threshold; the fight starts when the
            // hero steps past the ruined arch.
            engaged |= player.position.x > leftX - 0.3f;
        }

        if (engaged && player != null && bossCooldown <= 0f)
        {
            float dx = Mathf.Abs(player.position.x - pos.x);
            if (phase2 && dx > 2.6f && Random.value < 0.55f)
            {
                bossState = 3;
                bossTimer = chargeWindup;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayRoar();
            }
            else
            {
                bossState = 1;
                bossTimer = slamWindup;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayRoar();
            }
        }
    }

    void Update()
    {
        if (dead) return;

        animTimer += Time.deltaTime;
        float pace = kind == Kind.Boss ? 2.4f : kind == Kind.Stalker ? 4f : 5f;
        float squash = Mathf.Sin(animTimer * pace);

        float ampX = 0.035f;
        float ampY = 0.05f;
        if (kind == Kind.Boss && (bossState == 1 || bossState == 3))
        {
            ampX = 0.12f;
            ampY = 0.16f;
        }
        else if (kind == Kind.Stalker && stalkState == 1)
        {
            ampX = 0.14f;
            ampY = 0.2f;
        }

        transform.localScale = new Vector3(
            baseScale.x * (1f + squash * ampX),
            baseScale.y * (1f - squash * ampY),
            1f);

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (sr != null) sr.color = Color.white;
        }
        else if (sr != null)
        {
            Color target = baseColor;
            if (kind == Kind.Boss && (bossState == 1 || bossState == 3))
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 26f);
                target = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.25f, 1f), 0.65f * k);
            }
            else if (kind == Kind.Stalker && stalkState == 1)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                target = Color.Lerp(baseColor, new Color(0.85f, 0.75f, 1f, 1f), 0.6f * k);
            }
            sr.color = target;
        }

        if (kind == Kind.Boss && !dead)
        {
            if (engaged && CameraFollow.Instance != null) CameraFollow.Instance.SetBossZoom(true);
            bool show = engaged && GameManager.Instance != null &&
                        GameManager.Instance.State == GameManager.GameState.Playing;
            if (show && UIManager.Instance != null)
                UIManager.Instance.ShowBossBar(displayName, health, maxHealth, health <= phase2Health);
            else if (!show && UIManager.Instance != null)
                UIManager.Instance.HideBossBar();
        }
    }

    public void TakeHit(int damage, Vector2 knockback)
    {
        if (dead) return;
        health -= damage;
        flashTimer = 0.09f;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayHit(health <= 0 || isBrute);
        if (GameManager.Instance != null) GameManager.Instance.HitStop(0.045f);
        HitFx.SwordSpark(transform.position + new Vector3(0f, 0.2f, 0f), fxMaterial);

        if (health <= 0)
        {
            StartCoroutine(Die(knockback));
        }
        else if (rb != null && kind != Kind.Boss)
        {
            Vector2 p = rb.position;
            float x = Mathf.Clamp(p.x + knockback.x * 0.18f, leftX, rightX);
            rb.position = new Vector2(x, p.y);
        }
        if (kind == Kind.Boss && UIManager.Instance != null)
            UIManager.Instance.ShowBossBar(displayName, health, maxHealth, health <= phase2Health);
    }

    IEnumerator Die(Vector2 knockback)
    {
        dead = true;
        if (col != null) col.enabled = false;
        if (rb != null) rb.simulated = false;
        if (kind == Kind.Boss)
        {
            if (UIManager.Instance != null) UIManager.Instance.HideBossBar();
            if (CameraFollow.Instance != null) CameraFollow.Instance.SetBossZoom(false);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayMonsterDeath(isBrute);
        HitFx.DeathBurst(transform.position, fxMaterial, isBrute);
        if (GameManager.Instance != null) GameManager.Instance.OnMonsterKilled(isBrute);

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < deathDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / deathDuration);
            transform.position = startPos + (Vector3)(knockback.normalized * 0.35f * k) + Vector3.up * 0.15f * k;
            transform.localScale = Vector3.Lerp(startScale, new Vector3(startScale.x * 0.15f, startScale.y * 0.15f, 1f), k);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }

        if (kind == Kind.Boss)
        {
            if (GameManager.Instance != null) GameManager.Instance.OnBossDefeated();
            yield return new WaitForSecondsRealtime(0.35f);
        }
        Destroy(gameObject);
    }

    // Used by automated smoke tests only.
    public void DebugKill()
    {
        if (!dead) StartCoroutine(Die(Vector2.up));
    }
}
