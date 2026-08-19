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
    public float minDistance = 1.5f;
    public float maxDistance = 12.0f;

    [Header("Movimiento WASD")]
    public float moveSpeed = 3.0f;
    private Vector3 targetOffset = new Vector3(0, 1.3f, 0);

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Movimiento libre con teclas WASD
        if (Keyboard.current != null)
        {
            Vector3 moveDir = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) moveDir += transform.forward;
            if (Keyboard.current.sKey.isPressed) moveDir -= transform.forward;
            if (Keyboard.current.aKey.isPressed) moveDir -= transform.right;
            if (Keyboard.current.dKey.isPressed) moveDir += transform.right;

            targetOffset += moveDir * moveSpeed * Time.deltaTime;
        }

        // 2. Rotación con Clic Derecho
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            x += mouseDelta.x * xSpeed * 0.02f;
            y -= mouseDelta.y * ySpeed * 0.02f;
            y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        }

        // 3. Zoom con rueda del ratón
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            distance = Mathf.Clamp(distance - (scroll * 0.005f * zoomSpeed), minDistance, maxDistance);
        }

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + focusPoint;

        transform.rotation = rotation;
        transform.position = position;
    }
}