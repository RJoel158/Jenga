using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class JengaBlock : MonoBehaviour
{
    [Header("Datos de Reglas")]
    public int floorLevel;       // Nivel del piso al que pertenece actualmente
    public bool hasFallen = false; // Evita que el evento de caída se dispare varias veces

    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Guardamos posición y rotación por si quieres reiniciar la escena
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        // Centro de masa centrado para estabilidad física
        rb.centerOfMass = Vector3.zero;
    }

    /// <summary>
    /// Aplica la fuerza física concentrada en el punto exacto de impacto.
    /// </summary>
    public Vector3 PushBlock(Vector3 direction, float force, Vector3 hitPoint)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Si estaba en kinematic por estar en la cima, lo liberamos
        if (rb.isKinematic)
        {
            rb.isKinematic = false;
        }

        if (rb.IsSleeping())
        {
            rb.WakeUp();
        }

        // Aplica el impulso en el punto donde se hizo clic (AddForceAtPosition)
        rb.AddForceAtPosition(direction * force, hitPoint, ForceMode.Impulse);

        // Torque leve y controlado para que gire de forma orgánica
        Vector3 randomTorque = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f)) * force;
        rb.AddTorque(randomTorque, ForceMode.Impulse);

        return direction * force;
    }

    /// <summary>
    /// Detecta cuando el bloque cae al suelo (Plane) para recolocarlo en la cima.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (!hasFallen && (collision.gameObject.CompareTag("Ground") || collision.gameObject.name.Contains("Plane")))
        {
            hasFallen = true;
            // Damos 0.5s para que la caída se sienta fluida antes de reposicionarlo arriba
            Invoke(nameof(TriggerRelocation), 0.5f);
        }
    }

    private void TriggerRelocation()
    {
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.RelocateBlockToTop(this);
        }
    }

    /// <summary>
    /// Reinicia las velocidades físicas (compatible con Unity 6).
    /// </summary>
    public void StopPhysics()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.linearVelocity = Vector3.zero;  // Sintaxis oficial Unity 6
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Método para reiniciar el bloque a su posición original en el tablero.
    /// </summary>
    public void ResetBlock()
    {
        StopPhysics();
        rb.Sleep();
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        hasFallen = false;
    }
}