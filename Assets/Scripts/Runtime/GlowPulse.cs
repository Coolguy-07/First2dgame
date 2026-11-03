using UnityEngine;

// Breathes an HDR sprite (lantern / portal / moon halo) so the bloom pass
// picks up a slow, organic pulse instead of a flat glow.
public class GlowPulse : MonoBehaviour
{
    public float speed = 1.5f;
    public float amount = 0.16f;
    public float scaleAmount = 0.035f;

    SpriteRenderer sr;
    Color baseColor;
    Vector3 baseScale;
    float seed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        baseScale = transform.localScale;
        seed = Mathf.Abs(transform.position.x) * 0.7f;
    }

    void Update()
    {
        if (sr == null) return;
        float t = Time.time * speed + seed;
        float k = 1f + Mathf.Sin(t) * amount;
        sr.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
        transform.localScale = baseScale * (1f + Mathf.Sin(t * 1.7f) * scaleAmount);
    }
}
