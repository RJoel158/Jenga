using UnityEngine;

public class BlockLogic : MonoBehaviour
{
    private Camera arCamera;
    private bool isDragging = false;
    private Vector2 initialTouchPosition;
    private Vector3 initialBlockPosition;
    private JengaBlock attachedJengaBlock;

    [SerializeField] private float movementScale = 0.001f;

    void Start()
    {
        arCamera = Camera.main;
        attachedJengaBlock = GetComponent<JengaBlock>();
        if (attachedJengaBlock == null)
        {
            attachedJengaBlock = gameObject.AddComponent<JengaBlock>();
        }
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
        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                // REGLA 8 & REGLA 2: Verificar si la partida y el manager permiten la interacción
                if (JengaGameManager.Instance != null)
                {
                    if (!JengaGameManager.Instance.CanPlayerInteract())
                    {
                        Debug.LogWarning("No se puede mover el bloque en este momento (Esperando AR o turno).");
                        return;
                    }

                    // REGLA 3: Prohibido retirar bloques del nivel superior activo
                    if (!JengaGameManager.Instance.CanTouchBlock(attachedJengaBlock.floorLevel))
                    {
                        Debug.LogWarning("¡Regla de Jenga! No se pueden retirar bloques del nivel superior.");
                        return;
                    }

                    JengaGameManager.Instance.OnBlockDragStart(attachedJengaBlock);
                }

                isDragging = true;
                initialTouchPosition = screenPosition;
                initialBlockPosition = transform.position;

                attachedJengaBlock.StartArDrag();
            }
        }
    }

    private void DragBlock(Vector2 currentTouchPosition)
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

        Vector3 movement = cameraRight * horizontalMovement + cameraForward * forwardMovement;
        movement.y = 0; // Arrastre horizontal plano para sacar el bloque de la torre

        transform.position = initialBlockPosition + movement;
    }

    private void StopDragging()
    {
        isDragging = false;
        if (attachedJengaBlock != null)
        {
            attachedJengaBlock.StopArDrag(Vector3.zero);
        }
    }
}
