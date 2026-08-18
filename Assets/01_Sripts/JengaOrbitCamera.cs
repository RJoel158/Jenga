using UnityEngine;
using UnityEngine.InputSystem;

public class JengaOrbitCamera : MonoBehaviour
{
    public Transform target; // Arrastra el objeto TowerBuilder aquí
    public float distance = 4.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 80.0f;
    public float yMinLimit = -10f;
    public float yMaxLimit = 80f;
    public float zoomSpeed = 2.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 10.0f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (target != null)
        {
            // Apuntar al centro aproximado de la torre
            transform.position = target.position + new Vector3(0, 1.3f, 0);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Mantener presionado el Clic Derecho para rotar la cámara
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            x += mouseDelta.x * xSpeed * 0.02f;
            y -= mouseDelta.y * ySpeed * 0.02f;
            y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        }

        // Zoom con la rueda del ratón
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            distance = Mathf.Clamp(distance - (scroll * 0.005f * zoomSpeed), minDistance, maxDistance);
        }

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 targetCenter = target.position + new Vector3(0, 1.3f, 0);
        Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + targetCenter;

        transform.rotation = rotation;
        transform.position = position;
    }
}