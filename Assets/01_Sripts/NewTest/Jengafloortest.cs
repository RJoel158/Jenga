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
    private PhysicsMaterial jengaWoodMaterial;

    void Awake()
    {
        Physics.defaultContactOffset = 0.0002f;
        Physics.defaultSolverIterations = 40;
        Physics.defaultSolverVelocityIterations = 15;
        Physics.sleepThreshold = 0.0001f;
        Physics.defaultMaxDepenetrationVelocity = 0.3f;
    }

    private PhysicsMaterial GetWoodMaterial()
    {
        if (jengaWoodMaterial == null)
        {
            jengaWoodMaterial = new PhysicsMaterial("JengaWoodMaterial")
            {
                dynamicFriction = 0.40f,
                staticFriction = 0.50f,
                bounciness = 0.0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }
        return jengaWoodMaterial;
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

        PhysicsMaterial woodMat = GetWoodMaterial();

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
            // targetY coloca el centro del bloque exactamente al nivel que le corresponde en Y (sin hueco vertical flotante)
            float targetY = origin.y + (blockHeight / 2f) + (floor * blockHeight);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform, true);

            for (int i = 0; i < 3; i++)
            {
                // microGap aplica separación horizontal sutil entre los 3 bloques del mismo nivel
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

                BoxCollider boxCol = block.GetComponent<BoxCollider>();
                if (boxCol != null)
                {
                    boxCol.sharedMaterial = woodMat;
                }

                Rigidbody rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.mass = 0.12f;
                    rb.linearDamping = 0.5f;
                    rb.angularDamping = 0.8f;
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

                ApplyWoodColorVariation(block, floor, i);
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

    private static void ApplyWoodColorVariation(GameObject block, int floor, int index)
    {
        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer == null) return;

        // Generar un matiz pseudo-aleatorio consistente por bloque
        int seed = ((floor + 1) * 31) + ((index + 1) * 17);
        Random.State previousState = Random.state;
        Random.InitState(seed);

        float baseHue = Random.Range(0.07f, 0.11f);   // Tonos cálidos de madera (pino / roble)
        float baseSat = Random.Range(0.22f, 0.45f);   // Variación de saturación
        float baseVal = Random.Range(0.72f, 0.95f);   // Variación de brillo

        Color woodColor = Color.HSVToRGB(baseHue, baseSat, baseVal);
        Random.state = previousState;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", woodColor);
        propBlock.SetColor("_BaseColor", woodColor);
        renderer.SetPropertyBlock(propBlock);

        if (renderer.material != null)
        {
            if (renderer.material.HasProperty("_Color"))
                renderer.material.color = woodColor;
            else if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", woodColor);
        }
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