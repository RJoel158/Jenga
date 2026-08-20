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
    public bool isBeingDragged = false;

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

    public void ExtractBlockSmoothly(Vector3 direction, float distance = 0.75f, float duration = 0.3f)
    {
        if (isExtracting || isBeingDragged) return;
        StartCoroutine(SmoothExtractionRoutine(direction, distance, duration));
    }

    private IEnumerator SmoothExtractionRoutine(Vector3 direction, float distance, float duration)
    {
        isExtracting = true;
        wasTouchedByPlayer = true;
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + direction.normalized * distance;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();
        rb.linearVelocity = direction.normalized * 0.8f;
        isExtracting = false;

        Invoke(nameof(CheckRelocateAfterDrop), 0.3f);
    }

    public void StartArDrag()
    {
        if (isExtracting) return;
        if (rb == null) rb = GetComponent<Rigidbody>();

        isBeingDragged = true;
        wasTouchedByPlayer = true;
        rb.useGravity = false;
        rb.isKinematic = true;
        transform.SetParent(null, true);
    }

    public void StopArDrag(Vector3 throwVelocity)
    {
        if (!isBeingDragged) return;
        isBeingDragged = false;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.WakeUp();
        rb.linearVelocity = throwVelocity;

        // Si el bloque se soltó fuera de la torre, iniciar recolocación arriba
        Invoke(nameof(CheckRelocateAfterDrop), 0.3f);
    }

    private void CheckRelocateAfterDrop()
    {
        if (wasTouchedByPlayer && !hasFallen)
        {
            hasFallen = true;
            TriggerRelocation();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isBeingDragged) return; 

        bool isGround = collision.gameObject.CompareTag("Ground") || collision.gameObject.name.Contains("Plane");

        if (isGround)
        {
            if (wasTouchedByPlayer && !hasFallen)
            {
                hasFallen = true;
                Invoke(nameof(TriggerRelocation), 0.3f);
            }
            else if (!wasTouchedByPlayer && !hasFallen)
            {
                hasFallen = true;
                // Si cae un bloque que no fue sacado por el jugador -> La torre colapsó!
                if (JengaGameManager.Instance != null)
                {
                    JengaGameManager.Instance.TriggerTowerCollapse($"Caída del bloque nivel {floorLevel}");
                }
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