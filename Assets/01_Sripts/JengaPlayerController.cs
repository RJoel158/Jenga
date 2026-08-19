using UnityEngine;
using UnityEngine.InputSystem;

public class JengaPlayerController : MonoBehaviour
{
    public Camera mainCamera;

    [Header("Ajustes de Extracción")]
    public float slideDistance = 0.85f; // Distancia que recorre el bloque al salir
    public float slideDuration = 0.3f;  // Duración de la animación en segundos

    [Header("Opciones de Ajuste Manual")]
    [Tooltip("Activa esta casilla solo si por la rotación de tu modelo 3D deseas invertir los sentidos")]
    public bool invertDirections = false;

    void Start()
    {
        EnsureCameraReference();
    }

    private void EnsureCameraReference()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        EnsureCameraReference();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                JengaBlock block = hit.collider.GetComponent<JengaBlock>();
                if (block != null && !block.isExtracting)
                {
                    if (JengaGameManager.Instance == null) return;

                    // Regla de Jenga: No retirar bloques del piso superior activo
                    if (!JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
                    {
                        Debug.LogWarning("¡No se pueden retirar bloques del nivel superior!");
                        return;
                    }

                    DetermineAndExecuteSlide(block, hit);
                }
            }
        }
    }

    private void DetermineAndExecuteSlide(JengaBlock block, RaycastHit hit)
    {
        BoxCollider box = block.GetComponent<BoxCollider>();
        Vector3 localNormal = block.transform.InverseTransformDirection(hit.normal);

        float absX = Mathf.Abs(localNormal.x);
        float absZ = Mathf.Abs(localNormal.z);

        // 1. Calculamos las dimensiones REALES tomando en cuenta el Transform Scale
        Vector3 realSize = Vector3.one;
        if (box != null)
        {
            realSize = Vector3.Scale(box.size, block.transform.lossyScale);
        }
        else
        {
            realSize = block.transform.lossyScale;
        }

        // 2. Evaluamos cuál es la dimensión larga en el espacio del bloque
        bool isXLonger = realSize.x > realSize.z;

        // 3. Detectamos con precisión si el toque fue en la cara CORTA o LARGA
        bool isShortFace = isXLonger ? (absX > absZ) : (absZ > absX);

        Vector3 slideDirection;

        if (isShortFace)
        {
            // CARA CORTA (Extremo): Empuja atravesando la torre (lejos de la cámara)
            slideDirection = -hit.normal;
        }
        else
        {
            // CARA LARGA (Lateral): Jala el bloque hacia ti (hacia la cámara)
            slideDirection = hit.normal;
        }

        // Permitir inversión rápida si fuera necesario por tu modelo 3D
        if (invertDirections)
        {
            slideDirection = -slideDirection;
        }

        // Ejecutar extracción animada
        block.ExtractBlockSmoothly(slideDirection, slideDistance, slideDuration);
    }
}   