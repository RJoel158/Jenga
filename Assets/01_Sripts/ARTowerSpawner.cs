using System.Collections;
using Vuforia;
using UnityEngine;

/// <summary>
/// Construye una torre Jenga limpia y estable adaptada a NewJengaBlock en AR.
/// </summary>
public class ARJengaTowerSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject blockPrefab;

    [Header("Dimensiones Bloque (Autodetectadas si 0)")]
    [SerializeField, Min(0.001f)] private float blockLength = 0.075f; // Z
    [SerializeField, Min(0.001f)] private float blockWidth = 0.025f;  // X
    [SerializeField, Min(0.001f)] private float blockHeight = 0.015f; // Y
    [SerializeField, Min(0f)] private float gap = 0.0004f;
    [SerializeField, Min(1)] private int floors = 18;

    [Header("Físicas y Control")]
    [SerializeField] private bool keepKinematicOnSpawn = true;
    [SerializeField] private bool enablePhysicsOnBuild = true;
    [SerializeField, Min(0f)] private float physicsDelay = 0.5f;

    private const string ContentName = "AR_Jenga_Content";
    private ObserverBehaviour observer;
    private bool isBuilt;
    private Transform contentRoot;

    private void Awake()
    {
        observer = GetComponentInParent<ObserverBehaviour>();
    }

    private void OnEnable()
    {
        if (observer != null)
        {
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        }
    }

    private void Start()
    {
        BuildTower();
    }

    private void OnDisable()
    {
        if (observer != null)
        {
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }

    private void LateUpdate()
    {
        if (contentRoot != null && contentRoot.parent == transform)
        {
            contentRoot.position = transform.position;
            contentRoot.rotation = Quaternion.identity;
        }
    }

    private void OnTargetStatusChanged(ObserverBehaviour _, TargetStatus status)
    {
        if (!isBuilt && (status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED || status.Status == Status.LIMITED))
        {
            BuildTower();
        }
    }

    [ContextMenu("Reconstruir Jenga AR")]
    public void BuildTower()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("Asigna el prefab (NewJengaBlock) en el Inspector.", this);
            return;
        }

        AutoDetectDimensions();
        CleanupOldObjects();

        Transform content = CreateContentRoot();
        contentRoot = content;
        
        CreateGroundCollider(content);

        Transform tower = new GameObject("Jenga_Tower").transform;
        tower.SetParent(content, false);

        BuildBlocks(tower);
        ConfigureGameManager(content);
        EnsureInputController();
        isBuilt = true;

        if (enablePhysicsOnBuild && Application.isPlaying)
        {
            StartCoroutine(EnablePhysicsTopToBottom(tower, physicsDelay));
        }

        Debug.Log($"Torre Jenga AR construida con éxito ({floors * 3} bloques) usando NewJengaBlock.", this);
    }

    private void AutoDetectDimensions()
    {
        if (blockPrefab == null) return;

        Vector3 scale = blockPrefab.transform.localScale;
        if (scale.x > 0 && scale.y > 0 && scale.z > 0)
        {
            blockWidth = scale.x;  // 0.025m
            blockHeight = scale.y; // 0.015m
            blockLength = scale.z; // 0.075m
        }
    }

    private Transform CreateContentRoot()
    {
        Transform previous = transform.Find(ContentName);
        if (previous != null)
        {
            Destroy(previous.gameObject);
        }

        Transform content = new GameObject(ContentName).transform;
        content.SetParent(transform, false);
        content.position = transform.position;
        content.rotation = Quaternion.identity;
        content.localScale = Vector3.one;
        return content;
    }

    private void CreateGroundCollider(Transform content)
    {
        GameObject groundObj = new GameObject("AR_Jenga_Ground");
        groundObj.tag = "Ground";
        groundObj.transform.SetParent(content, false);
        groundObj.transform.localPosition = new Vector3(0f, -0.005f, 0f);

        BoxCollider col = groundObj.AddComponent<BoxCollider>();
        col.size = new Vector3(blockLength * 3.5f, 0.01f, blockLength * 3.5f);
        col.center = Vector3.zero;
    }

    private void BuildBlocks(Transform tower)
    {
        float startY = blockHeight * 0.5f;

        for (int floor = 0; floor < floors; floor++)
        {
            bool isEvenFloor = (floor % 2 == 0);
            float y = startY + floor * (blockHeight + gap);

            GameObject floorParent = new GameObject($"Floor_{floor + 1}");
            floorParent.transform.SetParent(tower, false);

            for (int index = 0; index < 3; index++)
            {
                float offset = (index - 1) * (blockWidth + gap);
                Vector3 position = isEvenFloor
                    ? new Vector3(offset, y, 0f)
                    : new Vector3(0f, y, offset);
                Quaternion rotation = isEvenFloor
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 90f, 0f);

                GameObject blockObject = Instantiate(blockPrefab, floorParent.transform);
                blockObject.name = $"Block_{floor + 1}_{index + 1}";
                blockObject.transform.SetLocalPositionAndRotation(position, rotation);
                blockObject.transform.localScale = new Vector3(blockWidth, blockHeight, blockLength);

                Rigidbody body = blockObject.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.useGravity = false;
                    body.isKinematic = keepKinematicOnSpawn;
                }

                JengaBlock block = blockObject.GetComponent<JengaBlock>();
                if (block == null)
                {
                    block = blockObject.AddComponent<JengaBlock>();
                }
                block.floorLevel = floor + 1;
            }
        }
    }

    private IEnumerator EnablePhysicsTopToBottom(Transform tower, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (tower == null) yield break;

        // Desbloqueo suave de arriba a abajo por pisos
        for (int i = tower.childCount - 1; i >= 0; i--)
        {
            Transform floorParent = tower.GetChild(i);
            Rigidbody[] floorRbs = floorParent.GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody rb in floorRbs)
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.mass = 0.08f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    rb.Sleep(); // Mantiene reposo estático fino hasta ser tocados por el jugador
                }
            }

            yield return new WaitForFixedUpdate();
        }

        Debug.Log("Físicas activadas progresivamente (de arriba a abajo) para NewJengaBlock.");
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

    private void CleanupOldObjects()
    {
        Transform targetRoot = transform.parent;
        if (targetRoot == null) return;

        Transform oldBlock = targetRoot.Find("JengaBlock");
        if (oldBlock != null)
        {
            Destroy(oldBlock.gameObject);
        }

        Transform oldPlane = targetRoot.Find("Plane");
        if (oldPlane != null)
        {
            oldPlane.gameObject.SetActive(false);
        }
    }
}
