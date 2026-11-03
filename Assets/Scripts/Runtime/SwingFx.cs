using UnityEngine;

public class SwingFx : MonoBehaviour
{
    public float lifetime = 0.16f;

    SpriteRenderer sr;
    float time;
    Vector3 startScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
    }

    void Update()
    {
        time += Time.deltaTime;
        float k = Mathf.Clamp01(time / lifetime);
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - k);
        transform.localScale = startScale * (1f + 0.35f * k);
        if (time >= lifetime) Destroy(gameObject);
    }
}
