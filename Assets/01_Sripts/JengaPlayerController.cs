using UnityEngine;
// Importante para el nuevo Input System en Unity 6 si usas la clase Mouse / Pointer
using UnityEngine.InputSystem; 

public class JengaPlayerController : MonoBehaviour
{
    public Camera mainCamera;
    public float pushForce = 3.5f;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        // Verificación compatible con el nuevo Input System para el clic izquierdo
        bool leftMouseClicked = false;
        
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            leftMouseClicked = true;
        }

        if (leftMouseClicked)
        {
            // Usamos Mouse.current.position en lugar de Input.mousePosition
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                JengaBlock block = hit.collider.GetComponent<JengaBlock>();
                if (block != null)
                {
                    Vector3 pushDirection = (hit.point - mainCamera.transform.position).normalized;
                    block.PushBlock(pushDirection, pushForce);
                }
            }
        }
    }
}