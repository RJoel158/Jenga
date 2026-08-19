using UnityEngine;

public class BlockLogic : MonoBehaviour
{
    private Camera arCamera;

    private bool isDragging = false;

    private Vector2 initialTouchPosition;
    private Vector3 initialBlockPosition;
    [SerializeField] private float movementScale = 0.001f;

    void Start()
    {
        arCamera = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                StartDragging(touch.position);
                break;

            case TouchPhase.Moved:
                if (isDragging)
                {
                    DragBlock(touch.position);
                }
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                StopDragging();
                break;
        }
    }

    void StartDragging(Vector2 touchPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(touchPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                isDragging = true;

                initialTouchPosition = touchPosition;
                initialBlockPosition = transform.position;
            }
        }
    }

    void DragBlock(Vector2 currentTouchPosition)
    {
        Vector2 touchDelta = currentTouchPosition - initialTouchPosition;
        float horizontalMovement = touchDelta.x * movementScale;
        float forwardMovement = touchDelta.y * movementScale;
        Vector3 cameraRight = arCamera.transform.right;
        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        cameraRight.y = 0;
        cameraRight.Normalize();
        Vector3 movement =
            cameraRight * horizontalMovement +
            cameraForward * forwardMovement;
        movement.y = 0;

        transform.position = initialBlockPosition + movement;
    }

    void StopDragging()
    {
        isDragging = false;
    }
}
