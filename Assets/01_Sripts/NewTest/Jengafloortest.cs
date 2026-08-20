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
    public float delayAfterTracked = 0.3f;

    private ObserverBehaviour observerBehaviour;
    private bool spawned = false;

    void Awake()
    {
        Physics.defaultContactOffset = 0.0003f;
        Physics.defaultSolverIterations = 30;
        Physics.defaultSolverVelocityIterations = 10;
        Physics.sleepThreshold = 0.001f;
        Physics.defaultMaxDepenetrationVelocity = 5.0f;
    }

    void Start()
    {
        AutoDetectDimensions();
        CleanupDuplicateAndStaticObjects();
        observerBehaviour = GetComponentInParent<ObserverBehaviour>();

        if (observerBehaviour != null)
        {
            observerBehaviour.OnTargetStatusChanged += OnTargetStatusChanged;
        }
        else
        {
            SpawnTower();
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
            SpawnTower();
        }
    }

    void OnDestroy()
    {
        if (observerBehaviour != null)
            observerBehaviour.OnTargetStatusChanged -= OnTargetStatusChanged;
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

    [ContextMenu("Spawnear Torre Jenga Limpia")]
    public void SpawnTower()
    {
        if (blockPrefab == null || surfacePlane == null) return;

        Collider floorCollider = surfacePlane.GetComponent<Collider>();
        if (floorCollider == null) return;

        AutoDetectDimensions();
        CleanupDuplicateAndStaticObjects();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Bounds b = floorCollider.bounds;
        // origin.y es b.max.y (la superficie superior exacta del plano del suelo)
        Vector3 origin = new Vector3(b.center.x, b.max.y, b.center.z);

        ConfigureGameManager(surfacePlane);
        EnsureInputController();

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
            // targetY coloca el centro del bloque a la mitad de su altura SOBRE la superficie del suelo
            float targetY = origin.y + (blockHeight / 2f) + floor * (blockHeight + microGap);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform, true);

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * (blockWidth + microGap);

                Vector3 spawnPos = isEvenFloor
                    ? new Vector3(origin.x + offset, targetY, origin.z)
                    : new Vector3(origin.x, targetY, origin.z + offset);

                Quaternion rotation = isEvenFloor
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 90f, 0f);

                GameObject block = Instantiate(blockPrefab, spawnPos, rotation, floorParent.transform);
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
                    rb.useGravity = true;
                    rb.isKinematic = true;
                }

                JengaBlock jengaBlock = block.GetComponent<JengaBlock>();
                if (jengaBlock == null)
                {
                    jengaBlock = block.AddComponent<JengaBlock>();
                }
                jengaBlock.floorLevel = floor + 1;
            }
        }

        // Descongelar las físicas en reposo estático fino (Sleep) para estabilidad absoluta
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
                    rb.Sleep();
                }
            }
        }

        Debug.Log($"[JengaFloorTest] Torre Jenga de {floors} pisos construida perfectamente sin penetración de suelo.");
    }

    private void CleanupDuplicateAndStaticObjects()
    {
        Transform parentTarget = transform.parent;
        if (parentTarget == null) return;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Transform staticBlock = parentTarget.Find("JengaBlock");
        if (staticBlock != null)
        {
            Destroy(staticBlock.gameObject);
        }

        for (int i = parentTarget.childCount - 1; i >= 0; i--)
        {
            Transform child = parentTarget.GetChild(i);
            if (child == transform || child.name == "Plane") continue;

            if (child.name.Contains("TowerBuilder") || child.name.Contains("Jenga_Tower") || child.name.Contains("AR_Jenga_Content"))
            {
                Destroy(child.gameObject);
            }
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