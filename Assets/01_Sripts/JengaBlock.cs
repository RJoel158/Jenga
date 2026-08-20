using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class JengaBlock : MonoBehaviour
{
    [Header("Datos de Reglas")]
    public int floorLevel; 
    public bool hasFallen = false; 
    public bool wasTouchedByPlayer = false;
    public bool isExtracting = false; 
    public bool isBeingDragged = false; // NUEVO: Estado para arrastre AR

    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        rb.centerOfMass = Vector3.zero;
    }

    // --- MANTENEMOS ESTA FUNCIÓN para la extracción suave automática si se desea ---
    public void ExtractBlockSmoothly(Vector3 direction, float distance = 0.75f, float duration = 0.3f)
    {
        if (isExtracting || isBeingDragged) return;
        StartCoroutine(SmoothExtractionRoutine(direction, distance, duration));
    }

    private IEnumerator SmoothExtractionRoutine(Vector3 direction, float distance, float duration)
    {
        // ... (Lógica de extracción suave idéntica a la tuya)
        yield return null; 
        // ...
    }

    // --- NUEVO: Funciones para que BlockLogic controle la física ---
    public void StartArDrag()
    {
        if (isExtracting) return;
        if (rb == null) rb = GetComponent<Rigidbody>();

        isBeingDragged = true;
        wasTouchedByPlayer = true; // Importante para la recolocación
        rb.useGravity = false;
        rb.isKinematic = true; // Congelamos física para arrastre limpio
        transform.SetParent(null, true);
    }

    public void StopArDrag(Vector3 throwVelocity)
    {
        if (!isBeingDragged) return;
        isBeingDragged = false;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.WakeUp();
        rb.linearVelocity = throwVelocity; // Le damos inercia al soltar
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Si estamos arrastrando activamente, ignoramos colisiones para recolocación
        if (isBeingDragged) return; 

        bool isGround = collision.gameObject.CompareTag("Ground") || collision.gameObject.name.Contains("Plane");

        if (isGround)
        {
            if (wasTouchedByPlayer && !hasFallen)
            {
                hasFallen = true;
                Invoke(nameof(TriggerRelocation), 0.4f);
            }
        }
    }

    private void TriggerRelocation()
    {
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.RelocateBlockToTop(this);
        }
        wasTouchedByPlayer = false;
        hasFallen = false;
    }
}
