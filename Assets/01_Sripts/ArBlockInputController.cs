using UnityEngine;

// Este script ya no va EN CADA BLOQUE.
// Va en un objeto controlador vacío en la escena.
public class ArBlockInputController : MonoBehaviour
{
    private Camera arCamera;
    private JengaBlock currentDraggedBlock; // Bloque actual que estamos moviendo

    private Vector2 initialTouchPosition;
    private Vector3 initialBlockPosition;
    [SerializeField] private float movementScale = 0.001f;

    // Para calcular la velocidad de lanzamiento al soltar
    private Vector3 lastBlockPosition;
    private Vector3 dragVelocity;

    void Start()
    {
        arCamera = Camera.main;
    }

    void Update()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (arCamera == null) return;

        // Soporte para Pantalla Táctil (Móvil) y Ratón (Editor)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    StartDragging(touch.position);
                    break;

                case TouchPhase.Moved:
                    if (currentDraggedBlock != null)
                    {
                        DragBlock(touch.position);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (currentDraggedBlock != null)
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
                StartDragging(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && currentDraggedBlock != null)
            {
                DragBlock(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) && currentDraggedBlock != null)
            {
                StopDragging();
            }
        }
    }

    void StartDragging(Vector2 touchPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(touchPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            JengaBlock block = hit.transform.GetComponent<JengaBlock>();
            
            // Verificamos que sea un bloque y que las reglas permitan tocarlo
            if (block != null && JengaGameManager.Instance != null && JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
            {
                currentDraggedBlock = block;
                currentDraggedBlock.StartArDrag(); // Avisamos al bloque que inicia arrastre

                initialTouchPosition = touchPosition;
                initialBlockPosition = currentDraggedBlock.transform.position;
                lastBlockPosition = initialBlockPosition;
            }
        }
    }

    void DragBlock(Vector2 currentTouchPosition)
    {
        Vector2 touchDelta = currentTouchPosition - initialTouchPosition;
        
        // Usamos tu excelente lógica de cálculo de dirección basada en cámara
        float horizontalMovement = touchDelta.x * movementScale;
        float forwardMovement = touchDelta.y * movementScale;

        Vector3 cameraRight = arCamera.transform.right;
        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();
        cameraRight.y = 0;
        cameraRight.Normalize();

        Vector3 movement = cameraRight * horizontalMovement + cameraForward * forwardMovement;
        movement.y = 0; // Arrastre puramente horizontal para sacarlo de la torre

        // Aplicamos la posición
        Vector3 newPosition = initialBlockPosition + movement;
        currentDraggedBlock.transform.position = newPosition;

        // Calculamos velocidad para el lanzamiento (distancia recorrida por segundo)
        dragVelocity = (newPosition - lastBlockPosition) / Time.deltaTime;
        lastBlockPosition = newPosition;
    }

    void StopDragging()
    {
        // Soltamos el bloque y le pasamos la velocidad calculada para darle inercia
        // Ajusta el multiplicador 0.1f según sientas la física en el móvil
        currentDraggedBlock.StopArDrag(dragVelocity * 0.1f); 
        currentDraggedBlock = null;
    }
}