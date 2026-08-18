using UnityEngine;

public class JengaTowerBuilder : MonoBehaviour
{
    public GameObject blockPrefab;
    
    [Header("Dimensiones Reales de Jenga (Largo x Ancho x Alto)")]
    public float blockLength = 0.75f; // 3 partes
    public float blockWidth = 0.25f;  // 1 parte (el ancho coincide con 1/3 del largo)
    public float blockHeight = 0.15f;

    [Header("Configuración de la Torre")]
    public int totalFloors = 18; 
    public Transform surfacePlane;

    [Header("Variación Visual")]
    [Range(0f, 0.2f)] public float colorVariation = 0.12f; // Sutil diferencia de tono entre bloques

    [ContextMenu("Construir Torre Real")]
    public void BuildTower()
    {
        // 1. Limpiar torre anterior
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        float baseHeight = (surfacePlane != null) ? surfacePlane.position.y : 0f;

        for (int floor = 0; floor < totalFloors; floor++)
        {
            // Alternar orientación: Pisos pares en un sentido, impares rotados 90 grados
            bool isEvenFloor = (floor % 2 == 0);
            float yPos = baseHeight + (floor * blockHeight) + (blockHeight / 2f);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform);
            floorParent.transform.position = transform.position;

            for (int i = 0; i < 3; i++)
            {
                Vector3 localPos = Vector3.zero;
                Quaternion rotation = Quaternion.identity;

                // Cálculo exacto para que 3 bloques de ancho formen el largo exacto del piso cruzado
                float offset = (i - 1) * blockWidth; 

                if (isEvenFloor)
                {
                    // Alineados a lo largo del eje Z, distribuidos en X
                    localPos = new Vector3(offset, yPos, 0f);
                    rotation = Quaternion.Euler(0, 90, 0);
                }
                else
                {
                    // Alineados a lo largo del eje X, distribuidos en Z
                    localPos = new Vector3(0f, yPos, offset);
                    rotation = Quaternion.identity;
                }

                GameObject block = Instantiate(blockPrefab, transform.position + localPos, rotation, floorParent.transform);
                
                // Asegurar que empiece en Kinematic para estabilidad al construir
                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                // Aplicar variación de color/brillo individual para crear contraste entre bloques
                Renderer rend = block.GetComponent<Renderer>();
                if (rend != null)
                {
                    // Creamos una instancia de material única para no alterar el prefab original
                    Material uniqueMat = new Material(rend.sharedMaterial);
                    
                    // Variamos ligeramente la luminosidad/color base
                    float randomFactor = 1f + Random.Range(-colorVariation, colorVariation);
                    if (uniqueMat.HasProperty("_BaseColor"))
                    {
                        Color baseColor = uniqueMat.GetColor("_BaseColor");
                        uniqueMat.SetColor("_BaseColor", new Color(baseColor.r * randomFactor, baseColor.g * randomFactor, baseColor.b * randomFactor));
                    }
                    else if (uniqueMat.HasProperty("_Color"))
                    {
                        Color baseColor = uniqueMat.GetColor("_Color");
                        uniqueMat.SetColor("_Color", new Color(baseColor.r * randomFactor, baseColor.g * randomFactor, baseColor.b * randomFactor));
                    }
                    
                    rend.material = uniqueMat;
                }
            }
        }
        Debug.Log("¡Torre construida con emparrillado correcto y contraste de bloques!");
    }

    [ContextMenu("Activar Físicas de la Torre")]
    public void EnablePhysics()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }
        Debug.Log("¡Físicas activadas!");
    }
}