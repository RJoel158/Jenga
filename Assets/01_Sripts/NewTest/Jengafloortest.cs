using UnityEngine;
using System.Collections;
using Vuforia;

public class JengaFloorTest : MonoBehaviour
{
    [Header("Prefab del Bloque")]
    public GameObject blockPrefab;

    [Header("Dimensiones (NewJengaBlock)")]
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float blockLength = 0.075f;

    [Header("Configuración de la Torre")]
    public int floors = 18;
    public Transform surfacePlane;
    public float microGap = 0.0004f;

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
        bool isTracked = (status.Status == Status.TRACKED || 
                          status.Status == Status.EXTENDED_TRACKED || 
                          status.Status == Status.LIMITED);

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
        if (blockPrefab == null || surfacePlane == null)
        {
            Debug.LogError("[JengaFloorTest] Asigna blockPrefab y surfacePlane en el Inspector.");
            return;
        }

        Collider floorCollider = surfacePlane.GetComponent<Collider>();
        if (floorCollider == null)
        {
            Debug.LogError("[JengaFloorTest] surfacePlane no tiene Collider.");
            return;
        }

        AutoDetectDimensions();
        CleanupDuplicateAndStaticObjects();

        // 1. Limpiar bloques anteriores
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Bounds b = floorCollider.bounds;
        Vector3 origin = new Vector3(b.center.x, b.max.y, b.center.z);

        // 2. Construcción instantánea y precisa de los 18 pisos sin huecos ni desfases
        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
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
                    rb.useGravity = false;
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

        // 3. Activar físicas en reposo estático fino (Sleep) para que la torre quede firme
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

        // 4. Configurar e Iniciar la partida en JengaGameManager
        ConfigureGameManager(surfacePlane);
        EnsureInputController();

        Debug.Log($"[JengaFloorTest] Torre Jenga de {floors} pisos construida limpiamente. Partida Lista.");
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
        manager.isArTrackingStable = true;
        manager.StartGame(); // ¡ACTIVAR EL ESTADO DE JUEGO PARA PERMITIR ARRASTRE!
    }

    private static void EnsureInputController()
    {
        if (Object.FindFirstObjectByType<BlockLogic>() == null && Camera.main != null)
        {
            Camera.main.gameObject.AddComponent<BlockLogic>();
        }
    }
}