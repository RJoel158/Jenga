using UnityEngine;
using System.Collections;
using Vuforia;

public class JengaFloorTest : MonoBehaviour
{
    public GameObject blockPrefab;

    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float blockLength = 0.075f;

    public int floors = 18;
    public Transform surfacePlane;

    public float microGap = 0.0004f;

    public float delayAfterTracked = 0.5f;
    public float dropHeightOffset = 0.04f; // Distancia de descenso animado (4 cm)
    public float animDurationPerFloor = 0.15f; // Duración del descenso suave por piso

    private ObserverBehaviour observerBehaviour;
    private bool spawned = false;

    void Awake()
    {
        Physics.defaultContactOffset = 0.0003f;
        Physics.defaultSolverIterations = 30;
        Physics.defaultSolverVelocityIterations = 10;
        Physics.sleepThreshold = 0.001f;
        Physics.defaultMaxDepenetrationVelocity = 0.1f;
    }

    void Start()
    {
        AutoDetectDimensions();
        observerBehaviour = GetComponentInParent<ObserverBehaviour>();

        if (observerBehaviour != null)
        {
            observerBehaviour.OnTargetStatusChanged += OnTargetStatusChanged;
        }
        else
        {
            StartCoroutine(SpawnAfterDelay());
        }
    }

    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool isTracked = status.Status == Status.TRACKED ||
                         status.Status == Status.EXTENDED_TRACKED ||
                         status.Status == Status.LIMITED;

        if (isTracked && !spawned)
        {
            spawned = true;
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
        StartCoroutine(SpawnTowerFloorByFloor());
    }

    private void AutoDetectDimensions()
    {
        if (blockPrefab == null) return;

        Vector3 scale = blockPrefab.transform.localScale;
        if (scale.x > 0 && scale.y > 0 && scale.z > 0)
        {
            blockWidth = scale.x;
            blockHeight = scale.y;
            blockLength = scale.z;
        }
    }

    [ContextMenu("Spawnear Torre Jenga Piso por Piso")]
    public void SpawnTower()
    {
        StopAllCoroutines();
        StartCoroutine(SpawnTowerFloorByFloor());
    }

    private IEnumerator SpawnTowerFloorByFloor()
    {
        if (blockPrefab == null || surfacePlane == null) yield break;

        Collider floorCollider = surfacePlane.GetComponent<Collider>();
        if (floorCollider == null) yield break;

        AutoDetectDimensions();

        // Limpiar bloques anteriores
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Bounds b = floorCollider.bounds;
        Vector3 origin = new Vector3(b.center.x, b.max.y, b.center.z);

        ConfigureGameManager(surfacePlane);
        EnsureInputController();

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);

            float targetY = origin.y + (blockHeight / 2f) + floor * (blockHeight + microGap);
            float startY = targetY + dropHeightOffset;

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform, true);

            GameObject[] floorBlocks = new GameObject[3];

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * (blockWidth + microGap);

                Vector3 startPos = isEvenFloor
                    ? new Vector3(origin.x + offset, startY, origin.z)
                    : new Vector3(origin.x, startY, origin.z + offset);

                Quaternion rotation = isEvenFloor
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 90f, 0f);

                GameObject block = Instantiate(blockPrefab, startPos, rotation, floorParent.transform);
                block.name = $"Block_{floor + 1}_{i + 1}";
                block.transform.localScale = new Vector3(blockWidth, blockHeight, blockLength);

                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.mass = 0.08f;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.useGravity = false;
                    rb.isKinematic = true; // Cinemático durante la animación de descenso
                }

                floorBlocks[i] = block;

                JengaBlock jengaBlock = block.GetComponent<JengaBlock>();
                if (jengaBlock == null)
                {
                    jengaBlock = block.AddComponent<JengaBlock>();
                }
                jengaBlock.floorLevel = floor + 1;
            }

            // Descenso controlado suave de la fila hacia su posición de reposo
            float elapsed = 0f;
            while (elapsed < animDurationPerFloor)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDurationPerFloor;
                t = t * t * (3f - 2f * t); // SmoothStep

                float currentY = Mathf.Lerp(startY, targetY, t);

                for (int i = 0; i < 3; i++)
                {
                    if (floorBlocks[i] != null)
                    {
                        Vector3 pos = floorBlocks[i].transform.position;
                        pos.y = currentY;
                        floorBlocks[i].transform.position = pos;
                    }
                }

                yield return null;
            }

            // Asegurar posición exacta final del piso
            for (int i = 0; i < 3; i++)
            {
                if (floorBlocks[i] != null)
                {
                    Vector3 pos = floorBlocks[i].transform.position;
                    pos.y = targetY;
                    floorBlocks[i].transform.position = pos;
                }
            }

            yield return new WaitForSeconds(0.02f);
        }

        // Una vez completada toda la torre, activar físicas en reposo estático fino (Sleep)
        for (int f = 0; f < transform.childCount; f++)
        {
            Transform floorGroup = transform.GetChild(f);
            Rigidbody[] rbs = floorGroup.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody rb in rbs)
            {
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.Sleep(); // Reposo perfecto hasta ser tocados
                }
            }
        }

        Debug.Log($"[JengaFloorTest] Torre Jenga ensamblada piso por piso de forma 100% estable.");
    }

    private void ConfigureGameManager(Transform ground)
    {
        JengaGameManager manager = GetComponent<JengaGameManager>();
        if (manager == null)
        {
            manager = gameObject.AddComponent<JengaGameManager>();
        }

        manager.Configure(ground, floors, blockWidth, blockHeight);
    }

    private static void EnsureInputController()
    {
        if (Object.FindFirstObjectByType<BlockLogic>() == null && Camera.main != null)
        {
            Camera.main.gameObject.AddComponent<BlockLogic>();
        }
    }
}