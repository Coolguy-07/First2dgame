using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    public Transform cameraTransform;
    public float factor = 0.15f;

    Vector3 startPosition;
    float startCameraX;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        startPosition = transform.position;
        if (cameraTransform != null) startCameraX = cameraTransform.position.x;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;
        float x = startPosition.x + (cameraTransform.position.x - startCameraX) * factor;
        transform.position = new Vector3(x, startPosition.y, startPosition.z);
    }
}
