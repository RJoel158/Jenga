using UnityEngine;
using System.Collections;

public class JengaGameManager : MonoBehaviour
{
    public static JengaGameManager Instance;

    [Header("Dimensiones de Jenga")]
    public float blockWidth = 0.25f;
    public float blockHeight = 0.15f;
    public Transform surfacePlane;

    [Header("Estado de la Torre")]
    public int currentTopFloor = 18;
    public int blocksOnTopFloor = 0; // Cantidad de bloques en el piso actual (0, 1 o 2)

    private float baseGroundY;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (surfacePlane != null)
        {
            Collider col = surfacePlane.GetComponent<Collider>();
            baseGroundY = (col != null) ? col.bounds.max.y : surfacePlane.position.y;
        }
    }

    /// <summary>
    /// Regla 1: No se pueden retirar bloques del piso superior activo.
    /// </summary>
    public bool CanTouchBlock(int blockFloor)
    {
        return blockFloor < currentTopFloor;
    }

    /// <summary>
    /// Regla 2: Coloca el bloque automáticamente en la cima con la orientación correcta.
    /// </summary>
    public void RelocateBlockToTop(JengaBlock block)
    {
        StartCoroutine(PlaceOnTopRoutine(block));
    }

    private IEnumerator PlaceOnTopRoutine(JengaBlock block)
    {
        // Desactivar físicas mientras se reubica
        Rigidbody rb = block.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Si el piso superior ya tiene 3 bloques, creamos un nuevo nivel arriba
        if (blocksOnTopFloor >= 3)
        {
            currentTopFloor++;
            blocksOnTopFloor = 0;
        }

        // Calcular posición y orientación según paridad de nivel
        bool isEvenFloor = (currentTopFloor % 2 == 0);
        float targetY = baseGroundY + ((currentTopFloor - 1) * blockHeight) + (blockHeight / 2f);
        float offset = (blocksOnTopFloor - 1) * blockWidth;

        Vector3 targetPos;
        Quaternion targetRot;

        if (isEvenFloor)
        {
            targetPos = new Vector3(transform.position.x + offset, targetY, transform.position.z);
            targetRot = Quaternion.Euler(0, 90, 0);
        }
        else
        {
            targetPos = new Vector3(transform.position.x, targetY, transform.position.z + offset);
            targetRot = Quaternion.identity;
        }

        // Actualizar datos del bloque
        block.floorLevel = currentTopFloor;
        block.hasFallen = false;

        // Reposicionar
        block.transform.SetPositionAndRotation(targetPos, targetRot);
        blocksOnTopFloor++;

        yield return new WaitForSeconds(0.1f);

        // Reactivar físicas de forma estable
        rb.isKinematic = false;
        rb.WakeUp();
    }
}