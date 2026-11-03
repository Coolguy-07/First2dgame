using UnityEngine;

// Thorne's ground slam: a fast dust crescent that hugs the floor. Jump it.
public class Shockwave : MonoBehaviour
{
    public float speed = 7.5f;
    public float life = 1.6f;
    public int damage = 1;

    float dir = 1f;
    SpriteRenderer sr;
    Color baseColor;
    bool hitPlayer;

    public static void Spawn(Vector3 position, float direction, Sprite sprite, Material material)
    {
        var go = new GameObject("Shockwave");
        go.transform.position = position + new Vector3(0.9f * direction, 0.28f, 0f);
        go.transform.localScale = new Vector3(direction * 1.1f, 1.1f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingOrder = 16;
        sr.color = new Color(1.05f, 0.92f, 0.78f, 0.95f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.15f, 0.55f);
        col.offset = new Vector2(0.15f, 0.12f);
        col.isTrigger = true;

        var wave = go.AddComponent<Shockwave>();
        wave.dir = direction;
        wave.sr = sr;
        wave.baseColor = sr.color;
    }

    void FixedUpdate()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.MovePosition(rb.position + new Vector2(dir * speed, 0f) * Time.fixedDeltaTime);
    }

    void Update()
    {
        life -= Time.deltaTime;
        float k = Mathf.Clamp01(life / 1.6f);
        if (sr != null)
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
        if (life <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hitPlayer) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        hitPlayer = true;
        player.ApplyDamage(damage, transform.position.x, false);
    }
}
