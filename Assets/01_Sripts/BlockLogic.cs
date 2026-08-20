using UnityEngine;

public class BlockLogic : MonoBehaviour
{
    [SerializeField] private float movementScale = 0.001f;

    private Camera arCamera;
    private bool isDragging = false;

    private Transform draggedBlock;
    private Rigidbody draggedRb;
    private JengaBlock draggedJengaBlock;

    private Vector2 initialTouchPosition;
    private Vector3 initialBlockPosition;

    void Start()
    {
        arCamera = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    StartDragging(touch.position);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
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
        else if (Input.GetMouseButtonDown(0))
        {
            StartDragging(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            DragBlock(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            StopDragging();
        }
    }

    void StartDragging(Vector2 touchPosition)
    {
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null) return;
        }

        Ray ray = arCamera.ScreenPointToRay(touchPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        JengaBlock block = hit.collider.GetComponentInParent<JengaBlock>();
        if (block == null)
            return;

        if (JengaGameManager.Instance != null &&
            !JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
        {
            return;
        }

        draggedBlock = block.transform;
        draggedJengaBlock = block;
        draggedRb = block.GetComponent<Rigidbody>();

        if (draggedRb != null)
        {
            draggedRb.linearVelocity = Vector3.zero;
            draggedRb.angularVelocity = Vector3.zero;
            draggedRb.isKinematic = true;
        }

        isDragging = true;
        initialTouchPosition = touchPosition;
        initialBlockPosition = draggedBlock.position;
    }

    void DragBlock(Vector2 currentTouchPosition)
    {
        if (draggedBlock == null)
        {
            isDragging = false;
            return;
        }

        Vector2 touchDelta = currentTouchPosition - initialTouchPosition;
        float horizontalMovement = touchDelta.x * movementScale;
        float forwardMovement = touchDelta.y * movementScale;

        Vector3 cameraRight = arCamera.transform.right;
        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();
        cameraRight.y = 0;
        cameraRight.Normalize();

        Vector3 rawMovement =
            cameraRight * horizontalMovement +
            cameraForward * forwardMovement;
        rawMovement.y = 0;

        // Proyectar el movimiento únicamente sobre el eje longitudinal del bloque (draggedBlock.forward)
        // Esto previene empujones laterales que derriban los bloques contiguos.
        Vector3 slideAxis = draggedBlock.forward;
        float slideAmount = Vector3.Dot(rawMovement, slideAxis);
        Vector3 constrainedMovement = slideAxis * slideAmount;

        Vector3 targetPos = initialBlockPosition + constrainedMovement;

        if (draggedRb != null)
        {
            draggedRb.MovePosition(targetPos);
        }
        else
        {
            draggedBlock.position = targetPos;
        }
    }

    void StopDragging()
    {
        if (draggedRb != null)
        {
            draggedRb.linearVelocity = Vector3.zero;
            draggedRb.angularVelocity = Vector3.zero;
            draggedRb.isKinematic = false;
            draggedRb.useGravity = true;
            draggedRb.WakeUp();
        }

        if (draggedJengaBlock != null)
        {
            draggedJengaBlock.wasTouchedByPlayer = true;
        }

        isDragging = false;
        draggedBlock = null;
        draggedRb = null;
        draggedJengaBlock = null;
    }
}