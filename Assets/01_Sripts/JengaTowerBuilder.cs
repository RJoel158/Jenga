using UnityEngine;
using System.Collections;

public class JengaTowerBuilder : MonoBehaviour
{
    [Header("Prefab de Bloque")]
    public GameObject blockPrefab;
    
    [Header("Dimensiones del Bloque (NewJengaBlock)")]
    public float blockWidth = 0.025f;  // X en NewJengaBlock
    public float blockHeight = 0.015f; // Y en NewJengaBlock
    public float blockLength = 0.075f; // Z en NewJengaBlock

    [Header("Configuración de la Torre")]
    public int totalFloors = 18; 
    public Transform surfacePlane;
    public float microGap = 0.0004f; // Holgura milimétrica entre capas
    public float extraSpawnHeight = 0.002f;

    [Header("Ejecución en Runtime")]
    public bool buildOnStart = true;
    public float delayBeforePhysics = 0.5f;

    [Header("AR/Móvil: Arranque Seguro")]
    public bool keepKinematicOnSpawn = true;
    public bool autoEnablePhysicsAfterPrepare = true;
    [Min(0f)] public float stabilizationDelay = 0.35f;
    public bool detachTowerFromTracking = true;
    [Min(0f)] public float physicsMassOverride = 0.08f;

    [Header("Variación Visual")]
    [Range(0f, 0.2f)] public float colorVariation = 0.12f;

    private bool physicsEnabled;

    void Start()
    {
        if (buildOnStart)
        {
            BuildTower();
            if (autoEnablePhysicsAfterPrepare)
            {
                Invoke(nameof(EnablePhysics), delayBeforePhysics);
            }
        }
    }

    [ContextMenu("Construir Torre Jenga (NewJengaBlock)")]
    public void BuildTower()
    {
        // 1. Limpiar objetos hijos anteriores
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        if (blockPrefab == null)
        {
            Debug.LogError("¡Por favor asigna el Prefab (NewJengaBlock) en el Inspector!", this);
            return;
        }

        AutoDetectDimensions();

        Vector3 origin = transform.position;
        if (surfacePlane != null)
        {
            Collider planeCollider = surfacePlane.GetComponent<Collider>();
            if (planeCollider != null)
            {
                Bounds b = planeCollider.bounds;
                origin = new Vector3(b.center.x, b.max.y, b.center.z);
            }
            else
            {
                origin = surfacePlane.position;
            }
        }

        // 3. Construir los 18 pisos usando la lógica precisa de Bounds de superficie
        for (int floor = 0; floor < totalFloors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
            float currentY = origin.y + (blockHeight / 2f) + extraSpawnHeight + (floor * (blockHeight + microGap));

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform, true);

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * (blockWidth + microGap); 
                
                Vector3 spawnPos = isEvenFloor
                    ? new Vector3(origin.x + offset, currentY, origin.z)
                    : new Vector3(origin.x, currentY, origin.z + offset);

                Quaternion rotation = isEvenFloor
                    ? Quaternion.identity
                    : Quaternion.Euler(0, 90, 0);

                GameObject block = Instantiate(blockPrefab, spawnPos, rotation, floorParent.transform);
                block.name = $"Block_{floor + 1}_{i + 1}";
                block.transform.localScale = new Vector3(blockWidth, blockHeight, blockLength);

                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.useGravity = false;
                    rb.isKinematic = keepKinematicOnSpawn;
                }

                JengaBlock jengaBlock = block.GetComponent<JengaBlock>();
                if (jengaBlock == null)
                {
                    jengaBlock = block.AddComponent<JengaBlock>();
                }
                jengaBlock.floorLevel = floor + 1;

                if (colorVariation > 0f)
                {
                    Renderer rend = block.GetComponent<Renderer>();
                    if (rend != null && rend.sharedMaterial != null)
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
        }

        Debug.Log($"¡Torre Jenga construida con éxito ({totalFloors * 3} bloques) usando la superficie del suelo!");
    }

    private void AutoDetectDimensions()
    {
        if (blockPrefab == null) return;

        Vector3 prefabScale = blockPrefab.transform.localScale;
        if (prefabScale.x > 0 && prefabScale.y > 0 && prefabScale.z > 0)
        {
            blockWidth = prefabScale.x;  // 0.025m
            blockHeight = prefabScale.y; // 0.015m
            blockLength = prefabScale.z; // 0.075m
        }
    }

    [ContextMenu("Activar Físicas")]
    public void EnablePhysics()
    {
        if (physicsEnabled) return;
        physicsEnabled = true;
        StartCoroutine(EnablePhysicsGradually());
    }

    private IEnumerator EnablePhysicsGradually()
    {
        if (detachTowerFromTracking && transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        int totalRbs = 0;

        for (int i = transform.childCount - 1; i >= 0; i--)
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
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.Sleep(); // Mantiene reposo estático fino hasta ser tocados por el jugador
            }

            totalRbs += floorRbs.Length;
            yield return new WaitForFixedUpdate();
        }

        Debug.Log($"Físicas activadas de forma limpia (de arriba a abajo) en {totalRbs} bloques.");
    }
}