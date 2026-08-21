using UnityEngine;

public class BlockLogic : MonoBehaviour
{
    [Header("Sensibilidad")]
    [SerializeField] private float movementSensitivity = 0.0012f;

    private Camera arCamera;
    private bool isDragging = false;

    private Transform draggedBlock;
    private Rigidbody draggedRb;
    private JengaBlock draggedJengaBlock;
    private BoxCollider draggedCollider;

    private Vector2 initialTouchPosition;
    private Vector3 initialBlockPosition;
    private Vector3 targetPhysicsPosition;

    private const float FULL_EXTRACTION_DISTANCE = 0.080f;

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
                    if (isDragging) UpdateDragTarget(touch.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (isDragging) StopDragging();
                    break;
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            StartDragging(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDragTarget(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            StopDragging();
        }
    }

    void FixedUpdate()
    {
        if (isDragging && draggedRb != null)
        {
            draggedRb.MovePosition(targetPhysicsPosition);
        }
    }

    void StartDragging(Vector2 screenPosition)
    {
        if (arCamera == null) arCamera = Camera.main;
        if (arCamera == null) return;

        if (JengaGameManager.Instance != null && !JengaGameManager.Instance.CanPlayerInteract())
            return;

        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        JengaBlock block = hit.collider.GetComponentInParent<JengaBlock>();
        if (block == null) return;

        if (JengaGameManager.Instance != null && !JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
            return;

        draggedBlock = block.transform;
        draggedJengaBlock = block;
        draggedRb = block.GetComponent<Rigidbody>();
        draggedCollider = block.GetComponent<BoxCollider>();

        if (JengaGameManager.Instance != null)
            JengaGameManager.Instance.OnBlockDragStart(block);

        // Al extraerlo, desactivamos su colisión sólida para que no levante ni empuje los pisos vecinos
        if (draggedCollider != null)
        {
            draggedCollider.isTrigger = true;
        }

        if (draggedRb != null)
        {
            draggedRb.linearVelocity = Vector3.zero;
            draggedRb.angularVelocity = Vector3.zero;
            draggedRb.isKinematic = true;
        }

        initialTouchPosition = screenPosition;
        initialBlockPosition = draggedBlock.position;
        targetPhysicsPosition = initialBlockPosition;
        isDragging = true;
    }

    void UpdateDragTarget(Vector2 currentScreenPosition)
    {
        if (draggedBlock == null)
        {
            isDragging = false;
            return;
        }

        Vector2 delta = currentScreenPosition - initialTouchPosition;

        Vector3 camRight = arCamera.transform.right;
        Vector3 camForward = arCamera.transform.forward;
        camForward.y = 0; camForward.Normalize();
        camRight.y = 0; camRight.Normalize();

        Vector3 moveWorld = (camRight * delta.x + camForward * delta.y) * movementSensitivity;

        Vector3 slideAxis = draggedBlock.forward;
        float slideAmount = Vector3.Dot(moveWorld, slideAxis);
        targetPhysicsPosition = initialBlockPosition + (slideAxis * slideAmount);

        if (Mathf.Abs(slideAmount) >= FULL_EXTRACTION_DISTANCE)
        {
            CompleteExtraction();
        }
    }

    void CompleteExtraction()
    {
        isDragging = false;

        if (draggedCollider != null)
        {
            draggedCollider.isTrigger = false;
        }

        JengaBlock extracted = draggedJengaBlock;
        ResetDragState();

        if (JengaGameManager.Instance != null && extracted != null)
        {
            JengaGameManager.Instance.RelocateBlockToTop(extracted);
        }
    }

    void StopDragging()
    {
        if (!isDragging || draggedBlock == null)
        {
            ResetDragState();
            return;
        }

        if (draggedCollider != null)
        {
            draggedCollider.isTrigger = false;
        }

        float extractedDist = Vector3.Distance(targetPhysicsPosition, initialBlockPosition);

        if (extractedDist >= FULL_EXTRACTION_DISTANCE)
        {
            CompleteExtraction();
        }
        else
        {
            if (draggedRb != null)
            {
                draggedRb.isKinematic = false;
                draggedRb.useGravity = true;
                draggedRb.linearVelocity = Vector3.zero;
                draggedRb.angularVelocity = Vector3.zero;
            }

            if (JengaGameManager.Instance != null)
            {
                JengaGameManager.Instance.OnBlockDragCanceled();
            }

            ResetDragState();
        }
    }

    private void ResetDragState()
    {
        isDragging = false;
        draggedBlock = null;
        draggedRb = null;
        draggedJengaBlock = null;
        draggedCollider = null;
    }
}