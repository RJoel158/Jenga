using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class JengaBlock : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Guardamos posición y rotación por si el bloque se cae de la mesa y quieres reiniciarlo
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        // Opcional: Ajustar el centro de masa de forma automática para estabilidad realista
        rb.centerOfMass = Vector3.zero;
    }

    /// <summary>
    /// Aplica un golpe físico con dirección (ideal para cuando el jugador empuja el bloque con el dedo/cursor).
    /// </summary>
    public Vector3 PushBlock(Vector3 direction, float force)
    {
        // Despierta el Rigidbody por si estaba en reposo (Sleeping)
        if (rb.IsSleeping())
        {
            rb.WakeUp();
        }

        // Aplica fuerza de impulso directo
        rb.AddForce(direction * force, ForceMode.Impulse);

        // Añade un pequeño torque aleatorio para que rote de forma natural al ser empujado
        Vector3 randomTorque = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (force * 0.2f);
        rb.AddTorque(randomTorque, ForceMode.Impulse);

        return direction * force;
    }

    /// <summary>
    /// Método para reiniciar el bloque a su posición original si se cae de la torre.
    /// </summary>
    public void ResetBlock()
    {
        rb.linearVelocity = Vector3.zero; // Unity 6 usa linearVelocity en lugar de velocity
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        transform.SetPositionAndRotation(initialPosition, initialRotation);
    }
}