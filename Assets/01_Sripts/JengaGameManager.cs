using UnityEngine;
using System.Collections;

public class JengaGameManager : MonoBehaviour
{
    public static JengaGameManager Instance;

    [Header("Dimensiones de Jenga")]
    public float blockWidth = 0.05f;
    public float blockHeight = 0.03f;
    public Transform surfacePlane;

    [Header("Estado de la Torre")]
    public int currentTopFloor = 18;
    public int blocksOnTopFloor = 3;

    private float baseGroundY;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateGroundHeight();
    }

    public void Configure(Transform plane, int initialFloors, float width, float height)
    {
        surfacePlane = plane;
        currentTopFloor = initialFloors;
        blocksOnTopFloor = 3;
        blockWidth = width;
        blockHeight = height;
        UpdateGroundHeight();
    }

    private void UpdateGroundHeight()
    {
        if (surfacePlane == null) return;

        Collider col = surfacePlane.GetComponent<Collider>();
        baseGroundY = col != null ? col.bounds.max.y : surfacePlane.position.y;
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
        Rigidbody rb = block.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        // Si el piso superior ya tiene 3 bloques, creamos un nuevo nivel arriba
        if (blocksOnTopFloor >= 3)
        {
            currentTopFloor++;
            blocksOnTopFloor = 0;
        }

        Transform parentTransform = (surfacePlane != null && surfacePlane.parent != null) ? surfacePlane.parent : transform;
        
        float tableThickness = surfacePlane != null ? surfacePlane.localScale.y : 0.012f;
        float localY = tableThickness + ((currentTopFloor - 1) * blockHeight) + (blockHeight / 2f);
        float offset = (blocksOnTopFloor - 1) * blockWidth;

        bool isEvenFloor = (currentTopFloor % 2 == 0);
        Vector3 localPos = isEvenFloor
            ? new Vector3(0f, localY, offset)
            : new Vector3(offset, localY, 0f);
        Quaternion localRot = isEvenFloor
            ? Quaternion.identity
            : Quaternion.Euler(0f, 90f, 0f);

        Vector3 targetPos = parentTransform.TransformPoint(localPos);
        Quaternion targetRot = parentTransform.rotation * localRot;

        // Actualizar datos del bloque
        block.floorLevel = currentTopFloor;
        block.hasFallen = false;

        // Reanclarlo a la torre en espacio mundial
        block.transform.SetParent(parentTransform, true);
        block.transform.SetPositionAndRotation(targetPos, targetRot);
        blocksOnTopFloor++;

        yield return new WaitForSeconds(0.1f);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.isKinematic = false;
        }
    }
}
