using UnityEngine;
using System.Collections;
using Vuforia;

public class JengaFloorTest : MonoBehaviour
{
    public GameObject blockPrefab;

    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float blockLength = 0.075f;

    public int floors = 6;
    public Transform surfacePlane;

    public float microGap = 0.0005f;

    public float delayAfterTracked = 0.5f;
    public float dropHeightOffset = 0.15f;
    public float settleTimePerFloor = 0.35f;

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

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Bounds b = floorCollider.bounds;
        Vector3 origin = new Vector3(b.center.x, b.max.y - (blockHeight / 2f), b.center.z);

        ConfigureGameManager(surfacePlane);
        EnsureInputController();

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);

            float targetY = origin.y + (blockHeight / 2f) + floor * (blockHeight + microGap);
            float spawnY = targetY + dropHeightOffset;

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform, true);

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * (blockWidth + microGap);

                Vector3 spawnPos = isEvenFloor
                    ? new Vector3(origin.x + offset, spawnY, origin.z)
                    : new Vector3(origin.x, spawnY, origin.z + offset);

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
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    rb.WakeUp();
                }

                JengaBlock jengaBlock = block.GetComponent<JengaBlock>();
                if (jengaBlock == null)
                {
                    jengaBlock = block.AddComponent<JengaBlock>();
                }
                jengaBlock.floorLevel = floor + 1;
            }

            yield return new WaitForSeconds(settleTimePerFloor);
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