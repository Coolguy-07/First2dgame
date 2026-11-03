using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 80f;
    public float deceleration = 100f;
    public float jumpVelocity = 13.5f;
    public float gravityScale = 3f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;
    public float feetOffset = 0.72f;
    public LayerMask groundMask;

    [Header("Combat")]
    public int maxHealth = 3;
    public float attackCooldown = 0.34f;
    public float attackWindup = 0.07f;
    public Vector2 attackSize = new Vector2(1.3f, 1f);
    public float attackOffset = 0.7f;
    public int attackDamage = 1;
    public LayerMask enemyMask;
    public Sprite swingSprite;
    public Material swingMaterial;

    [Header("Visuals")]
    public SpriteRenderer bodyRenderer;

    Rigidbody2D rb;
    BoxCollider2D box;
    GameManager game;
    UIManager ui;

    int health;
    bool facingRight = true;
    float moveInput;
    float coyoteCounter;
    float bufferCounter;
    float attackTimer;
    float iframeTimer;
    float attackPoseTimer;
    bool jumpHeld;
    bool grounded;
    bool wasGrounded;
    float airTime;
    bool moving;
    bool frozen;
    bool dead;
    Coroutine attackRoutine;
    Vector3 bodyHome;
    bool bodyIsChild;

    public bool IsDead { get { return dead; } }
    public int Health { get { return health; } }
    public int MaxHealth { get { return maxHealth; } }
    public bool FacingRight { get { return facingRight; } }
    public bool IsGrounded { get { return grounded; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null)
        {
            bodyIsChild = bodyRenderer.transform != transform;
            bodyHome = bodyIsChild ? bodyRenderer.transform.localPosition : Vector3.zero;
        }
        health = maxHealth;
    }

    void Start()
    {
        game = GameManager.Instance;
        ui = UIManager.Instance;
        if (ui != null) ui.SetHealth(health, maxHealth);
    }

    void Update()
    {
        if (frozen || dead) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

        var kb = Keyboard.current;
        if (kb == null) return;
        var mouse = Mouse.current;

        moveInput = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput += 1f;
        moving = Mathf.Abs(moveInput) > 0.01f;

        if (moving && attackPoseTimer <= 0f) Flip(moveInput > 0f);

        wasGrounded = grounded;
        grounded = Physics2D.OverlapCircle(
            new Vector2(transform.position.x, transform.position.y - feetOffset),
            0.16f, groundMask);
        if (grounded)
        {
            if (!wasGrounded && airTime > 0.18f && AudioManager.Instance != null) AudioManager.Instance.PlayLand();
            coyoteCounter = coyoteTime;
            airTime = 0f;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
            airTime += Time.deltaTime;
        }

        bool jumpPressed = kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
        jumpHeld = kb.spaceKey.isPressed || kb.wKey.isPressed;
        if (jumpPressed) bufferCounter = jumpBufferTime;
        else bufferCounter -= Time.deltaTime;

        if (bufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
            bufferCounter = 0f;
            coyoteCounter = 0f;
            grounded = false;
            airTime = 0.2f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayJump();
        }

        if (!jumpHeld && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.9f);

        bool attackPressed = kb.jKey.wasPressedThisFrame ||
                             (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (attackPressed && attackTimer <= 0f) BeginAttack();

        attackTimer -= Time.deltaTime;
        attackPoseTimer -= Time.deltaTime;
        if (iframeTimer > 0f)
        {
            iframeTimer -= Time.deltaTime;
            if (bodyRenderer != null)
            {
                float a = Mathf.PingPong(Time.time * 22f, 1f) > 0.5f ? 0.35f : 1f;
                bodyRenderer.color = new Color(1f, 1f, 1f, a);
            }
            if (iframeTimer <= 0f && bodyRenderer != null) bodyRenderer.color = Color.white;
        }

        UpdateVisuals();

        if (transform.position.y < -7f && GameManager.Instance != null)
            GameManager.Instance.PlayerFell();
    }

    void FixedUpdate()
    {
        if (frozen || dead) return;
        float target = moveInput * moveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, target, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    void Flip(bool right)
    {
        facingRight = right;
        if (bodyRenderer != null) bodyRenderer.flipX = !right;
    }

    void UpdateVisuals()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.flipX = !facingRight;

        // Only animate a child sprite. Writing localPosition on the root would
        // pin the Rigidbody2D to its spawn point every frame.
        if (!bodyIsChild) return;

        float bob = 0f;
        if (attackPoseTimer > 0f)
        {
            bob = 0.02f;
            bodyRenderer.transform.localEulerAngles = new Vector3(0f, 0f, facingRight ? -7f : 7f);
        }
        else
        {
            bodyRenderer.transform.localEulerAngles = Vector3.zero;
            if (grounded && Mathf.Abs(rb.linearVelocity.x) > 0.5f)
                bob = Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.045f;
        }

        bodyRenderer.transform.localPosition = bodyHome + new Vector3(0f, bob, 0f);
    }

    void BeginAttack()
    {
        attackTimer = attackCooldown;
        attackPoseTimer = 0.2f;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySwing();

        float dir = facingRight ? 1f : -1f;
        var fx = new GameObject("SwingFX");
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = swingSprite;
        if (swingMaterial != null) sr.sharedMaterial = swingMaterial;
        sr.sortingOrder = 20;
        sr.color = new Color(1f, 1f, 1f, 0.95f);
        fx.transform.position = transform.position + new Vector3(dir * 0.62f, 0.02f, 0f);
        fx.transform.localScale = new Vector3(dir * 1.25f, 1.25f, 1f);
        fx.transform.rotation = Quaternion.Euler(0f, 0f, dir * -18f);
        fx.AddComponent<SwingFx>();

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(AttackHit());
    }

    IEnumerator AttackHit()
    {
        yield return new WaitForSeconds(attackWindup);
        if (dead || frozen) yield break;

        float dir = facingRight ? 1f : -1f;
        Vector2 center = (Vector2)transform.position + new Vector2(dir * attackOffset, 0.03f);
        var hits = Physics2D.OverlapBoxAll(center, attackSize, 0f, enemyMask);
        var seen = new HashSet<Monster>();
        bool hitAny = false;

        foreach (var c in hits)
        {
            if (c == null) continue;
            var monster = c.GetComponentInParent<Monster>();
            if (monster == null || monster.IsDead || !seen.Add(monster)) continue;
            monster.TakeHit(attackDamage, new Vector2(dir, 0.4f));
            hitAny = true;
        }

        if (hitAny)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayClash();
            if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.18f, 0.12f);
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (dead || frozen) return;
        if (((1 << collision.gameObject.layer) & enemyMask.value) == 0) return;

        var monster = collision.gameObject.GetComponentInParent<Monster>();
        if (monster != null && !monster.IsDead)
            ApplyDamage(monster.contactDamage, monster.transform.position.x, false);
    }

    public void ApplyDamage(int damage, float sourceX, bool ignoreIFrames)
    {
        if (dead || frozen) return;
        if (!ignoreIFrames && iframeTimer > 0f) return;

        health = Mathf.Max(0, health - damage);
        iframeTimer = 1f;
        if (rb.simulated)
            rb.linearVelocity = new Vector2((transform.position.x < sourceX ? -1f : 1f) * 6.5f, 6.8f);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayHurt();
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.35f, 0.22f);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetHealth(health, maxHealth);
            UIManager.Instance.FlashDamage();
        }

        if (health == 1 && UIManager.Instance != null)
            UIManager.Instance.ShowBark("Still standing. Barely.");

        if (health <= 0) Die();
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        if (rb.simulated) rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied();
    }

    public void Respawn(Vector3 position)
    {
        dead = false;
        frozen = false;
        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        rb.position = position;
        transform.position = position;
        coyoteCounter = 0f;
        bufferCounter = 0f;
        grounded = false;
        wasGrounded = false;
        airTime = 0f;
        iframeTimer = 1f;
        if (bodyRenderer != null) bodyRenderer.color = Color.white;
        if (UIManager.Instance != null) UIManager.Instance.SetHealth(health, maxHealth);
    }

    public void Freeze()
    {
        frozen = true;
        if (rb.simulated) rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (bodyRenderer != null) bodyRenderer.color = Color.white;
    }

    public void Unfreeze()
    {
        frozen = false;
        dead = false;
        if (!rb.simulated) rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        if (bodyRenderer != null) bodyRenderer.color = Color.white;
    }

    // Used by automated smoke tests only.
    public void DebugHurt()
    {
        ApplyDamage(1, transform.position.x + 1f, true);
    }

    public void DebugHeal()
    {
        if (dead) return;
        health = maxHealth;
        iframeTimer = 0f;
        if (bodyRenderer != null) bodyRenderer.color = Color.white;
        if (UIManager.Instance != null) UIManager.Instance.SetHealth(health, maxHealth);
    }

    public void DebugAttackNow()
    {
        if (attackTimer <= 0f) BeginAttack();
    }

    public void DebugFace(float direction)
    {
        Flip(direction >= 0f);
    }
}
