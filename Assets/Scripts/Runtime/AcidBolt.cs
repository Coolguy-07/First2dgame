using UnityEngine;

// A slow arcing glob of bile thrown by the spitters. Jump it or close in.
public class AcidBolt : MonoBehaviour
{
    Sprite boltSprite;
    Material boltMaterial;
    LayerMask groundMask;
    float life = 4.5f;
    bool spent;

    public static AcidBolt Spawn(Vector3 position, Vector2 velocity, LayerMask ground,
                                  Sprite sprite, Material material)
    {
        var go = new GameObject("AcidBolt");
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingOrder = 18;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.55f;
        rb.mass = 0.05f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearVelocity = velocity;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.17f;
        col.isTrigger = true;

        var bolt = go.AddComponent<AcidBolt>();
        bolt.groundMask = ground;
        bolt.boltSprite = sprite;
        bolt.boltMaterial = material;
        return bolt;
    }

    void Update()
    {
        life -= Time.deltaTime;
        transform.Rotate(0f, 0f, 420f * Time.deltaTime);
        float s = 0.75f + Mathf.Sin(Time.time * 22f) * 0.08f;
        transform.localScale = new Vector3(s, s, 1f);
        if (life <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;
        if (other.GetComponentInParent<Monster>() != null) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            spent = true;
            player.ApplyDamage(1, transform.position.x, false);
            HitFx.Burst(transform.position, new Color(0.75f, 1f, 0.5f), 10, 2.2f, 0.12f, 0.4f,
                        boltMaterial, 22);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySpitSplat();
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & groundMask.value) != 0)
        {
            spent = true;
            HitFx.Burst(transform.position, new Color(0.55f, 0.85f, 0.4f), 8, 1.6f, 0.1f, 0.35f,
                        boltMaterial, 22);
            Destroy(gameObject);
        }
    }
}
