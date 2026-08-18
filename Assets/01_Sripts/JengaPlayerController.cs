using UnityEngine;
using UnityEngine.InputSystem;

public class JengaPlayerController : MonoBehaviour
{
    public Camera mainCamera;
    public float pushForce = 2.5f;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        // Clic Izquierdo para golpear/retirar bloque
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                JengaBlock block = hit.collider.GetComponent<JengaBlock>();
                if (block != null)
                {
                    // Validación de Regla: No se pueden retirar bloques del nivel superior
                    if (!JengaGameManager.Instance.CanTouchBlock(block.floorLevel))
                    {
                        Debug.LogWarning("¡No se pueden retirar bloques del nivel superior!");
                        return;
                    }

                    ApplyFaceSpecificForce(block, hit);
                }
            }
        }
    }
    
   private void ApplyFaceSpecificForce(JengaBlock block, RaycastHit hit)
    {
        // Transformar la normal del impacto al espacio local del bloque
        Vector3 localNormal = block.transform.InverseTransformDirection(hit.normal);
        Vector3 pushDirection;

        // Determinar si es cara frontal/trasera o lateral
        if (Mathf.Abs(localNormal.z) > 0.5f || Mathf.Abs(localNormal.x) > 0.5f)
        {
            pushDirection = -hit.normal; // Hacia adentro/contrario
        }
        else
        {
            pushDirection = hit.normal;  // Hacia afuera
        }

        // Usamos TU método PushBlock pasándole dirección, fuerza y punto de contacto
        block.PushBlock(pushDirection, pushForce, hit.point);
    }
}