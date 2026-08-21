using UnityEngine;
using System.Collections;
using Vuforia;

public class JengaFloorTest : MonoBehaviour
{
    public GameObject blockPrefab;

    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float blockLength = 0.075f;

    public int floors = 10;
    public Transform surfacePlane;
    public float microGap = 0.00035f;

    private ObserverBehaviour observerBehaviour;
    private bool spawned = false;
    private PhysicsMaterial jengaWoodMaterial;

    void Awake()
    {
        Physics.defaultContactOffset = 0.0001f;
        Physics.defaultSolverIterations = 60;
        Physics.defaultSolverVelocityIterations = 30;
        Physics.defaultMaxDepenetrationVelocity = 0.15f;
        Physics.sleepThreshold = 0.001f;
        Physics.bounceThreshold = 2.0f;
    }

    private PhysicsMaterial GetWoodMaterial()
    {
        if (jengaWoodMaterial == null)
        {
            jengaWoodMaterial = new PhysicsMaterial("JengaPolishedWood")
            {
                dynamicFriction = 0.45f,
                staticFriction = 0.58f,
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

    [ContextMenu("Spawnear Torre Jenga")]
    public void SpawnTower()
    {
        StopAllCoroutines();
        StartCoroutine(SpawnTowerRoutine());
    }

    private IEnumerator SpawnTowerRoutine()
    {
        if (blockPrefab == null || surfacePlane == null) yield break;

        AutoDetectDimensions();
        CleanupDuplicateAndStaticObjects();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        ConfigureGameManager(surfacePlane);
        EnsureInputController();

        PhysicsMaterial woodMat = GetWoodMaterial();

        Collider tableCol = surfacePlane.GetComponent<Collider>();
        if (tableCol != null)
        {
            tableCol.sharedMaterial = woodMat;
        }

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
            float localY = (blockHeight / 2f) + (floor * blockHeight);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(transform, false);

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * (blockWidth + microGap);

                Vector3 localPos = isEvenFloor
                    ? new Vector3(offset, localY, 0f)
                    : new Vector3(0f, localY, offset);

                Quaternion localRot = isEvenFloor
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 90f, 0f);

                Vector3 worldPos = transform.TransformPoint(localPos);
                Quaternion worldRot = transform.rotation * localRot;

                GameObject block = Instantiate(blockPrefab, worldPos, worldRot, floorParent.transform);
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
                    rb.mass = 0.15f;             // 150 gramos: masa sólida y estable
                    rb.linearDamping = 0.5f;
                    rb.angularDamping = 4.0f;    // Evita balanceos excesivos
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.isKinematic = true;
                    rb.useGravity = false;
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

        yield return new WaitForSeconds(0.1f);

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
    }

    private static void ApplyWoodColorVariation(GameObject block, int floor, int index)
    {
        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer == null) return;

        int seed = ((floor + 1) * 31) + ((index + 1) * 17);
        Random.State previousState = Random.state;
        Random.InitState(seed);

        float baseHue = Random.Range(0.07f, 0.11f);
        float baseSat = Random.Range(0.22f, 0.40f);
        float baseVal = Random.Range(0.75f, 0.95f);

        Color woodColor = Color.HSVToRGB(baseHue, baseSat, baseVal);
        Random.state = previousState;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", woodColor);
        propBlock.SetColor("_BaseColor", woodColor);
        renderer.SetPropertyBlock(propBlock);
    }

    private void CleanupDuplicateAndStaticObjects()
    {
        Transform parentTarget = transform.parent;
        if (parentTarget == null) return;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Transform staticBlock = parentTarget.Find("JengaBlock");
        if (staticBlock != null) Destroy(staticBlock.gameObject);

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