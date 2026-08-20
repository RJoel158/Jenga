using System.Collections;
using Vuforia;
using UnityEngine;

/// <summary>
/// Construye una torre Jenga erguida hacia el techo (World +Y) anclada al Image Target de Vuforia.
/// </summary>
public class ARJengaTowerSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject blockPrefab;

    [Header("Dimensiones Bloque Jenga (metros)")]
    [SerializeField, Min(0.001f)] private float blockLength = 0.15f;
    [SerializeField, Min(0.001f)] private float blockWidth = 0.05f;
    [SerializeField, Min(0.001f)] private float blockHeight = 0.03f;
    [SerializeField, Min(0f)] private float gap = 0.0005f;
    [SerializeField, Min(1)] private int floors = 18;

    [Header("Físicas y Control")]
    [SerializeField] private bool keepKinematicOnSpawn = true; // Mantiene la torre 100% firme y limpia

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
        // Construye la torre de 54 bloques inmediatamente en Start para garantizar que no quede solo 1 bloque demo
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
        // Mantiene la base de la torre anclada al Image Target pero siempre erguida hacia el techo (World +Y)
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
            Debug.LogError("Asigna el prefab JengaBlock en el Inspector.", this);
            return;
        }

        CleanupOldObjects();

        Transform content = CreateContentRoot();
        contentRoot = content;
        
        // Suelo invisible anclado a la base
        CreateGroundCollider(content);

        Transform tower = new GameObject("Jenga_Tower").transform;
        tower.SetParent(content, false);

        BuildBlocks(tower);
        ConfigureGameManager(content);
        EnsureInputController();
        isBuilt = true;

        Debug.Log($"Torre Jenga AR construida con éxito: {floors * 3} bloques ({floors} pisos).", this);
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

    private static void CreateGroundCollider(Transform content)
    {
        GameObject groundObj = new GameObject("AR_Jenga_Ground");
        groundObj.tag = "Ground";
        groundObj.transform.SetParent(content, false);
        groundObj.transform.localPosition = new Vector3(0f, -0.005f, 0f);

        BoxCollider col = groundObj.AddComponent<BoxCollider>();
        col.size = new Vector3(0.5f, 0.01f, 0.5f);
        col.center = Vector3.zero;
    }

    private void BuildBlocks(Transform tower)
    {
        float startY = blockHeight * 0.5f;

        for (int floor = 0; floor < floors; floor++)
        {
            bool alongX = floor % 2 == 0;
            float y = startY + floor * (blockHeight + gap);

            for (int index = 0; index < 3; index++)
            {
                float offset = (index - 1) * (blockWidth + gap);
                Vector3 position = alongX
                    ? new Vector3(0f, y, offset)
                    : new Vector3(offset, y, 0f);
                Quaternion rotation = alongX
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 90f, 0f);

                GameObject blockObject = Instantiate(blockPrefab, tower);
                blockObject.name = $"Block_{floor + 1}_{index + 1}";
                blockObject.transform.SetLocalPositionAndRotation(position, rotation);
                blockObject.transform.localScale = new Vector3(blockLength, blockHeight, blockWidth);

                Rigidbody body = blockObject.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.useGravity = false;
                    body.isKinematic = keepKinematicOnSpawn; // 100% estable al aparecer
                }

                JengaBlock block = blockObject.GetComponent<JengaBlock>();
                if (block != null)
                {
                    block.floorLevel = floor + 1;
                }
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
        if (Object.FindFirstObjectByType<ArBlockInputController>() == null && Camera.main != null)
        {
            Camera.main.gameObject.AddComponent<ArBlockInputController>();
        }
    }

    private void CleanupOldObjects()
    {
        Transform targetRoot = transform.parent;
        if (targetRoot == null) return;

        // Limpiar el bloque estático de demostración del proyecto
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
