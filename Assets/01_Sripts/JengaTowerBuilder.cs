using UnityEngine;

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
    public void EnablePhysics()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }
    }
}