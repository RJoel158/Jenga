using UnityEngine;
using System.Collections;
using Vuforia;

public class JengaFloorTest : MonoBehaviour
{
    [Header("Prefab del bloque")]
    public GameObject blockPrefab;

    [Header("Dimensiones del bloque (deben coincidir con el prefab)")]
    public float blockWidth = 0.025f;   // eje X
    public float blockHeight = 0.015f;  // eje Y

    [Header("Referencia al suelo")]
    public Transform surfacePlane; 

    [Header("Separación entre bloques")]
    public float microGap = 0.002f;

    [Header("Timing / Altura de Spawn")]
    public float delayAfterTracked = 3f;
    public float extraSpawnHeight = 0.05f; 

    private ObserverBehaviour observerBehaviour;
    private bool spawned = false;

    void Start()
    {
        Debug.Log($"[JengaFloorTest] JengaSpawner lossyScale: {transform.lossyScale}");

        observerBehaviour = GetComponentInParent<ObserverBehaviour>();

        if (observerBehaviour != null)
        {
            observerBehaviour.OnTargetStatusChanged += OnTargetStatusChanged;
            Debug.Log("[JengaFloorTest] Suscripto a eventos de tracking.");
        }
        else
        {
            Debug.LogWarning("[JengaFloorTest] No se encontró ObserverBehaviour en los padres. Spawneando directo (sin esperar tracking).");
            StartCoroutine(SpawnAfterDelay());
        }
    }

    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool isTracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

        if (isTracked && !spawned)
        {
            spawned = true; // marcamos ya para no disparar el coroutine de nuevo si sigue actualizando status
            Debug.Log($"[JengaFloorTest] Target trackeado, esperando {delayAfterTracked}s antes de spawnear.");
            StartCoroutine(SpawnAfterDelay());
        }
    }

    void OnDestroy()
    {
        if (observerBehaviour != null)
            observerBehaviour.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterTracked);
        SpawnFirstFloor();
    }

    [ContextMenu("Spawnear Primer Piso")]
    public void SpawnFirstFloor()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("Falta asignar blockPrefab.");
            return;
        }
        if (surfacePlane == null)
        {
            Debug.LogError("Falta asignar surfacePlane.");
            return;
        }

        Collider floorCollider = surfacePlane.GetComponent<Collider>();
        if (floorCollider == null)
        {
            Debug.LogError("surfacePlane no tiene Collider.");
            return;
        }

        Bounds b = floorCollider.bounds;
        Vector3 origin = new Vector3(b.center.x, b.max.y, b.center.z);

        Debug.Log($"[JengaFloorTest] Suelo detectado en: {origin} (bounds min={b.min} max={b.max})");

        // Altura extra para asegurar que no haya colisión/superposición al spawnear
        float spawnY = origin.y + (blockHeight / 2f) + extraSpawnHeight;

        for (int i = 0; i < 3; i++)
        {
            float offset = (i - 1) * (blockWidth + microGap);
            Vector3 spawnPos = new Vector3(origin.x + offset, spawnY, origin.z);

            GameObject block = Instantiate(blockPrefab, spawnPos, Quaternion.identity, transform);
            block.name = $"TestBlock_{i}";

            Debug.Log($"[JengaFloorTest] Bloque {i} spawneado en {spawnPos} (world), lossyScale={block.transform.lossyScale}");
        }
    }
}