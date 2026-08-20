using UnityEngine;
using System.Collections;

public class JengaTowerBuilder : MonoBehaviour
{
    public GameObject blockPrefab;

    [Header("Dimensiones del Bloque")]
    public float blockLength = 0.75f;
    public float blockWidth = 0.25f;
    public float blockHeight = 0.15f;

    [Header("Configuración de la Torre")]
    public int totalFloors = 18;
    public Transform surfacePlane;

    [Header("Ajustes de Estabilidad Física")]
    [Tooltip("Separación milimétrica para evitar la superposición de Colliders al spawnear")]
    public float microGap = 0.002f;

    [Header("Ejecución en Runtime")]
    public bool buildOnStart = true;
    [Min(0f)] public float physicsMassOverride = 0.08f;

    [Header("Variación Visual")]
    [Range(0f, 0.2f)] public float colorVariation = 0.12f;

    void Start()
    {
        if (buildOnStart)
        {
            BuildTower();
        }
    }

    [ContextMenu("Construir Torre Estable")]
    public void BuildTower()
    {
        // Limpiar bloques anteriores
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // Calcular la altura real de la superficie
        float groundY = transform.position.y;
        if (surfacePlane != null)
        {
            Collider planeCollider = surfacePlane.GetComponent<Collider>();
            groundY = (planeCollider != null) ? planeCollider.bounds.max.y : surfacePlane.position.y;
        }

        float centerX = transform.position.x;
        float centerZ = transform.position.z;

        for (int floor = 0; floor < totalFloors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);

            // Calculamos la posición en Y agregando el microGap de seguridad
            float currentY = groundY + (floor * (blockHeight + microGap)) + (blockHeight / 2f);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform);

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * (blockWidth + microGap);
                Vector3 spawnPos;
                Quaternion rotation;

                if (isEvenFloor)
                {
                    spawnPos = new Vector3(centerX + offset, currentY, centerZ);
                    rotation = Quaternion.Euler(0, 90, 0);
                }
                else
                {
                    spawnPos = new Vector3(centerX, currentY, centerZ + offset);
                    rotation = Quaternion.identity;
                }

                GameObject blockObj = Instantiate(blockPrefab, spawnPos, rotation, floorParent.transform);

                // Configuración de Rigidbody para spawn seguro
                Rigidbody rb = blockObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (physicsMassOverride > 0f) rb.mass = physicsMassOverride;

                    // MANTENER CINEMÁTICO AL SPAWNEAR (Evita colapsos por gravedad)
                    rb.isKinematic = true;
                    rb.useGravity = true;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                // Asignación de datos al componente JengaBlock
                JengaBlock blockScript = blockObj.GetComponent<JengaBlock>();
                if (blockScript != null)
                {
                    blockScript.floorLevel = floor + 1;
                }

                // Variación de color opcional
                Renderer rend = blockObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material uniqueMat = new Material(rend.sharedMaterial);
                    float randomFactor = 1f + Random.Range(-colorVariation, colorVariation);

                    if (uniqueMat.HasProperty("_BaseColor"))
                        uniqueMat.SetColor("_BaseColor", uniqueMat.GetColor("_BaseColor") * randomFactor);
                    else if (uniqueMat.HasProperty("_Color"))
                        uniqueMat.SetColor("_Color", uniqueMat.GetColor("_Color") * randomFactor);

                    rend.material = uniqueMat;
                }
            }
        }
        Debug.Log("¡Torre construida de forma sólida e inmóvil!");
    }

    /// <summary>
    /// Activa la gravedad en toda la torre (Llamar desde eventos o al tocar un bloque)
    /// </summary>
    [ContextMenu("Activar Físicas")]
    public void EnablePhysics()
    {
        Rigidbody[] allBlocks = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in allBlocks)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }
        Debug.Log("Físicas de la torre activadas.");
    }
}