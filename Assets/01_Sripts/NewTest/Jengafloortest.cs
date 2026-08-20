using UnityEngine;
using System.Collections;
using Vuforia;

public class JengaFloorTest : MonoBehaviour
{
    [Header("Prefab del bloque")]
    public GameObject blockPrefab;

    [Header("Dimensiones del bloque (NewJengaBlock)")]
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float blockLength = 0.075f;

    [Header("Configuración de la Torre")]
    public int floors = 18;
    public Transform surfacePlane;

    [Header("Separación entre bloques")]
    public float microGap = 0.0005f;

    [Header("Timing / Altura de Spawn")]
    public float delayAfterTracked = 0.5f;
    public float extraSpawnHeight = 0f;
    public bool keepKinematicOnSpawn = true;

    private ObserverBehaviour observerBehaviour;
    private bool spawned = false;

    void Awake()
    {
        Physics.defaultContactOffset = 0.0003f;
        Physics.defaultSolverIterations = 80;
        Physics.defaultSolverVelocityIterations = 20;
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
        SpawnTower();
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

    [ContextMenu("Spawnear Torre Jenga Completa")]
    public void SpawnTower()
    {
        if (blockPrefab == null || surfacePlane == null) return;

        Collider floorCollider = surfacePlane.GetComponent<Collider>();
        if (floorCollider == null) return;

        AutoDetectDimensions();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Bounds b = floorCollider.bounds;
        Vector3 origin = new Vector3(b.center.x, b.max.y - (blockHeight / 2f), b.center.z);

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
            float currentY = origin.y + (blockHeight / 2f) + extraSpawnHeight + floor * (blockHeight + microGap);

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
                    : Quaternion.Euler(0f, 90f, 0f);

                GameObject block = Instantiate(blockPrefab, spawnPos, rotation, floorParent.transform);
                block.name = $"Block_{floor + 1}_{i + 1}";
                block.transform.localScale = new Vector3(blockWidth, blockHeight, blockLength);

                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.mass = 0.015f;
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
            }
        }

        ConfigureGameManager(surfacePlane);
        EnsureInputController();

        if (keepKinematicOnSpawn)
        {
            StartCoroutine(EnablePhysicsGraduallyWithLowGravity());
        }
    }

    private IEnumerator EnablePhysicsGraduallyWithLowGravity()
    {
        yield return new WaitForSeconds(0.5f);

        Vector3 originalGravity = Physics.gravity;
        Physics.gravity = new Vector3(0, -0.5f, 0);

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform floorParent = transform.GetChild(i);
            Rigidbody[] floorRbs = floorParent.GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody rb in floorRbs)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.WakeUp();
            }

            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.5f);
        Physics.gravity = originalGravity;

        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.WakeUp();
        }
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