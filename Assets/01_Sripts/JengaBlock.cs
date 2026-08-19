using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class JengaBlock : MonoBehaviour
{
    [Header("Datos de Reglas")]
    public int floorLevel;                 // Nivel del piso
    public bool hasFallen = false;           // Evita ejecuciones duplicadas
    public bool wasTouchedByPlayer = false;  // Marca si fue interactuado por el usuario
    public bool isExtracting = false;        // Estado de extracción en proceso

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

    /// <summary>
    /// Desliza el bloque suavemente fuera de la torre como una animación.
    /// </summary>
    /// <param name="direction">Dirección del movimiento</param>
    /// <param name="distance">Distancia a recorrer para salir de la estructura</param>
    /// <param name="duration">Tiempo en segundos que dura el deslizamiento</param>
    public void ExtractBlockSmoothly(Vector3 direction, float distance = 0.75f, float duration = 0.3f)
    {
        if (isExtracting) return;
        StartCoroutine(SmoothExtractionRoutine(direction, distance, duration));
    }

    private IEnumerator SmoothExtractionRoutine(Vector3 direction, float distance, float duration)
    {
        isExtracting = true;
        wasTouchedByPlayer = true;

        if (rb == null) rb = GetComponent<Rigidbody>();

        // Desactivamos la física momentáneamente para que se deslice de forma limpia sin explotar
        rb.isKinematic = true;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (direction.normalized * distance);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Curva de suavizado (SmoothStep) para efecto de movimiento manual
            t = t * t * (3f - 2f * t); 
            
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        // Una vez fuera de su nicho, devolvemos las físicas para que caiga por gravedad
        rb.isKinematic = false;
        rb.WakeUp();
        rb.linearVelocity = direction.normalized * 0.8f; // Ligera inercia de salida

        isExtracting = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        bool isGround = collision.gameObject.CompareTag("Ground") || collision.gameObject.name.Contains("Plane");

        if (isGround)
        {
            // Solo si el jugador extrajo este bloque se reubica arriba
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

    public void ResetBlock()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        hasFallen = false;
        wasTouchedByPlayer = false;
        isExtracting = false;
    }
}