using System.Collections;
using UnityEngine;

// Gives a Monster a ranged bile attack. The Monster still handles patrol, health
// and death; this only adds the telegraph and the projectile.
[RequireComponent(typeof(Monster))]
public class MonsterRanged : MonoBehaviour
{
    public Sprite boltSprite;
    public Material boltMaterial;
    public LayerMask groundMask;
    public float range = 9f;
    public float cooldown = 2.6f;
    public float windup = 0.7f;
    public float projectileSpeed = 7f;

    Monster monster;
    SpriteRenderer sr;
    Color baseColor;
    float timer;
    bool firing;

    void Awake()
    {
        monster = GetComponent<Monster>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        timer = Random.Range(1.1f, cooldown);
    }

    void Update()
    {
        if (monster == null || monster.IsDead || firing) return;
        if (DialogueSystem.Active) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null || player.IsDead) return;
        float dx = player.transform.position.x - transform.position.x;
        float dy = player.transform.position.y - transform.position.y;
        if (Mathf.Abs(dx) > range || Mathf.Abs(dy) > 3.5f) return;

        timer = cooldown;
        StartCoroutine(Fire(dx));
    }

    IEnumerator Fire(float dx)
    {
        firing = true;

        // Telegraph: the sac glows bile-green before the throw.
        float t = 0f;
        while (t < windup)
        {
            t += Time.deltaTime;
            if (sr != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 26f);
                sr.color = Color.Lerp(baseColor, new Color(1.4f, 1.9f, 0.9f, 1f), k * 0.75f);
            }
            yield return null;
        }
        if (sr != null) sr.color = baseColor;

        if (!monster.IsDead)
        {
            float dir = dx >= 0f ? 1f : -1f;
            if (sr != null) sr.flipX = dir < 0f;
            Vector3 muzzle = transform.position + new Vector3(0.45f * dir, 0.55f, 0f);
            float speed = projectileSpeed;
            Vector2 velocity = new Vector2(dir * speed * 0.72f, speed * 0.62f);
            AcidBolt.Spawn(muzzle, velocity, groundMask, boltSprite, boltMaterial);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySpit();
        }

        firing = false;
    }
}
