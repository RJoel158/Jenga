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
        if (arCamera == null) arCamera = Camera.main;
        if (arCamera == null) return;

        // Soporte táctil (Móvil)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    TryStartDrag(touch.position);
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
                    if (isDragging)
                    {
                        StopDragging();
                    }
                    break;
            }
        }
        else
        {
            // Fallback para pruebas en Editor con Mouse
            if (Input.GetMouseButtonDown(0))
            {
                TryStartDrag(Input.mousePosition);
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
    }

    private void TryStartDrag(Vector2 screenPosition)
    {
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null) return;
        }

        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        JengaBlock block = hit.collider.GetComponentInParent<JengaBlock>();
        if (block == null)
            return;

        // Regla AR & Regla de Turno: Verificar estado de partida
        if (JengaGameManager.Instance != null)
        {
            if (!JengaGameManager.Instance.CanPlayerInteract())
            {
                Debug.LogWarning("[BlockLogic] No se puede mover el bloque en este momento (Esperando AR o turno).");
                return;
            }

            // Regla 3: No se puede mover un bloque del nivel superior activo
            if (!JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
            {
                Debug.LogWarning("[BlockLogic] No se puede mover un bloque del nivel superior activo.");
                return;
            }

            JengaGameManager.Instance.OnBlockDragStart(block);
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
        initialTouchPosition = screenPosition;
        initialBlockPosition = draggedBlock.position;

        if (draggedJengaBlock != null)
        {
            draggedJengaBlock.StartArDrag();
        }
    }

    private void DragBlock(Vector2 currentTouchPosition)
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

        Vector3 movement = cameraRight * horizontalMovement + cameraForward * forwardMovement;
        movement.y = 0; // Arrastre horizontal plano para sacar el bloque de la torre

        draggedBlock.position = initialBlockPosition + movement;
    }

    private void StopDragging()
    {
        if (draggedJengaBlock != null)
        {
            draggedJengaBlock.StopArDrag(Vector3.zero);
        }
        else if (draggedRb != null)
        {
            draggedRb.isKinematic = false;
            draggedRb.useGravity = true;
            draggedRb.WakeUp();
        }

        isDragging = false;
        draggedBlock = null;
        draggedRb = null;
        draggedJengaBlock = null;
    }
}