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
    public float microGap = 0.001f; // Holgura milimétrica para evitar presión entre capas

    [Header("Ejecución en Runtime")]
    public bool buildOnStart = true;
    public float delayBeforePhysics = 0.5f; // segundos que la torre queda "congelada" antes de soltarse

    [Header("AR/Móvil: Arranque Seguro")]
    public bool preparePhysicsOnStart = true;
    public bool autoEnablePhysicsAfterPrepare = true;
    [Min(0f)] public float stabilizationDelay = 0.35f;
    public bool detachTowerFromTracking = true;
    public bool detachEachBlockFromTower = false;
    [Min(0f)] public float physicsMassOverride = 0.08f;

    private bool physicsEnabled;

    void Start()
    {
        if (buildOnStart)
        {
            BuildTower();
            Invoke(nameof(EnablePhysics), delayBeforePhysics);
        }
    }

    private IEnumerator PreparePhysicsRoutine()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody rb in rbs)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.Sleep();
        }

        // Evita que la simulación dependa del jitter del seguimiento AR.
        if (detachTowerFromTracking && transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        if (detachEachBlockFromTower)
        {
            foreach (Rigidbody rb in rbs)
            {
                if (rb.transform.parent != null)
                {
                    rb.transform.SetParent(null, true);
                }
            }
        }

        if (stabilizationDelay > 0f)
        {
            yield return new WaitForSeconds(stabilizationDelay);
        }

        if (autoEnablePhysicsAfterPrepare)
        {
            EnablePhysics();
        }
    }

    [Header("Variación Visual")]
    [Range(0f, 0.2f)] public float colorVariation = 0.12f;

    [ContextMenu("Construir Torre Estable")]
    public void BuildTower()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

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
            
            // Sumamos el microGap para dar espacio de reposo
            float currentY = groundY + (floor * (blockHeight + microGap)) + (blockHeight / 2f);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform);

            for (int i = 0; i < 3; i++)
            {
                // Separación horizontal ligeramente holgada
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

                GameObject block = Instantiate(blockPrefab, spawnPos, rotation, floorParent.transform);
                
                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true; // Inicia inmóvil
                   
                }

                Renderer rend = block.GetComponent<Renderer>();
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
        Debug.Log("¡Torre generada con holgura física antiexplosión!");
    }

    [ContextMenu("Activar Físicas")]
    [ContextMenu("Activar Físicas")]
    public void EnablePhysics()
    {
        if (physicsEnabled) return;
        physicsEnabled = true; // lo marcamos ya para bloquear ejecuciones duplicadas durante la corrutina
        StartCoroutine(EnablePhysicsGradually());
    }

    private IEnumerator EnablePhysicsGradually()
    {
        int totalRbs = 0;

        // Recorremos cada "Floor_X" (child de este objeto) de abajo hacia arriba
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform floorParent = transform.GetChild(i);
            Rigidbody[] floorRbs = floorParent.GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody rb in floorRbs)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                if (physicsMassOverride > 0f)
                {
                    rb.mass = physicsMassOverride;
                }
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.WakeUp();
            }

            totalRbs += floorRbs.Length;
            yield return new WaitForFixedUpdate(); // deja que el solver "digiera" este piso antes del siguiente
        }

        Debug.Log($"Físicas activadas de forma segura (gradual) en {totalRbs} bloques.");
    }
}