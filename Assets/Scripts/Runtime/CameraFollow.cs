using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance;

    public Transform target;
    public float smoothTime = 0.16f;
    public float minX = 0.9f;
    public float maxX = 55.1f;
    public float fixedY = 2.5f;

    [Header("Boss framing")]
    public float normalSize = 5f;
    public float bossSize = 4.25f;
    public float zoomSmooth = 0.9f;

    Vector3 velocity;
    float shakeTimer;
    float shakeDuration;
    float shakeAmount;
    Camera cam;
    float targetSize = 5f;
    float sizeVelocity;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        targetSize = normalSize;
        if (cam != null) cam.orthographicSize = normalSize;
    }

    void Start()
    {
        if (target != null)
            transform.position = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), fixedY, transform.position.z);
    }

    void LateUpdate()
    {
        if (target == null) return;

        float x = Mathf.Clamp(target.position.x, minX, maxX);
        Vector3 desired = new Vector3(x, fixedY, transform.position.z);
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float falloff = shakeDuration > 0f ? Mathf.Clamp01(shakeTimer / shakeDuration) : 0f;
            pos += (Vector3)(Random.insideUnitCircle * shakeAmount * falloff);
        }

        transform.position = pos;

        if (cam != null)
        {
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref sizeVelocity, zoomSmooth);
        }
    }

    public void SetBossZoom(bool on)
    {
        targetSize = on ? bossSize : normalSize;
    }

    public void Shake(float amount, float duration)
    {
        shakeAmount = amount;
        shakeDuration = duration;
        shakeTimer = duration;
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        sizeVelocity = 0f;
        transform.position = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), fixedY, transform.position.z);
    }
}
