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
        if (Input.touchCount == 0)
            return;

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

        // Buscamos el componente JengaBlock en el objeto tocado (o en sus padres,
        // por si el collider está en un hijo del bloque).
        JengaBlock block = hit.collider.GetComponentInParent<JengaBlock>();
        if (block == null)
            return;

        // Regla de Jenga: no se puede mover un bloque del piso superior activo.
        if (JengaGameManager.Instance != null &&
            !JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
        {
            Debug.LogWarning("[BlockLogic] No se puede mover un bloque del nivel superior activo.");
            return;
        }

        draggedBlock = block.transform;
        draggedJengaBlock = block;
        draggedRb = block.GetComponent<Rigidbody>();

        // Mientras se arrastra, lo pasamos a kinemático para moverlo a mano
        // sin que la física interna pelee contra el movimiento manual.
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

        Vector3 movement =
            cameraRight * horizontalMovement +
            cameraForward * forwardMovement;
        movement.y = 0;

        // Movemos el BLOQUE arrastrado, no la cámara.
        draggedBlock.position = initialBlockPosition + movement;
    }

    void StopDragging()
    {
        if (draggedRb != null)
        {
            // Devolvemos la física real al soltar, para que caiga/asiente naturalmente.
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