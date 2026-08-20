using UnityEngine;
using System.Collections;
using Vuforia;

/// <summary>
/// Gestor principal de reglas y turnos de Jenga AR.
/// Reglas:
/// 1. Selección de 2, 3 o 4 jugadores (partida inicia con Jugador 1).
/// 2. Prohibido retirar bloques del nivel superior activo (currentTopFloor).
/// 3. Bloque retirado se recoloca automáticamente en la posición libre de la cima (con paridad 0°/90°).
/// 4. Evaluación de estabilidad durante 5 segundos tras colocar el bloque arriba.
/// 5. El jugador en cuyo turno cae la torre PERDIÓ.
/// 6. Turno cíclico hacia el siguiente jugador (Jugador N -> Jugador 1).
/// 7. Manipulación bloqueada si el tracking AR no es estable.
/// </summary>
public class JengaGameManager : MonoBehaviour
{
    public static JengaGameManager Instance;

    public enum GameState
    {
        WAITING_FOR_TRACKING,
        PLAYER_TURN,
        RELOCATING_BLOCK,
        STABILITY_CHECK,
        GAME_OVER
    }

    [Header("Configuración de Partida")]
    [Range(2, 4)] public int totalPlayers = 2;
    public int currentPlayerIndex = 1;
    public GameState currentState = GameState.WAITING_FOR_TRACKING;
    public float stabilityCheckDuration = 5.0f;

    [Header("Dimensiones del Bloque")]
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float microGap = 0.0004f;
    public Transform surfacePlane;

    [Header("Estado de la Torre")]
    public int currentTopFloor = 18;
    public int blocksOnTopFloor = 3; // Cantidad de bloques en la cima (0, 1, 2 o 3)

    [Header("Tracking AR")]
    public bool isArTrackingStable = false;
    private ObserverBehaviour observer;

    [Header("Estado de Partida")]
    public bool isGameOver = false;
    public int losingPlayerIndex = -1;
    public float stabilityTimer = 0f;
    public bool hasMovedBlockThisTurn = false;
    public JengaBlock currentMovedBlock = null;

    private float baseGroundY;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateGroundHeight();
        observer = GetComponentInParent<ObserverBehaviour>();

        if (observer != null)
        {
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
            UpdateTrackingState(observer.TargetStatus);
        }
        else
        {
            isArTrackingStable = true;
            if (currentState == GameState.WAITING_FOR_TRACKING)
            {
                StartGame();
            }
        }
    }

    void OnDestroy()
    {
        if (observer != null)
        {
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }

    private void OnTargetStatusChanged(ObserverBehaviour _, TargetStatus status)
    {
        UpdateTrackingState(status);
    }

    private void UpdateTrackingState(TargetStatus status)
    {
        bool isStable = (status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED);
        isArTrackingStable = isStable;

        if (isStable)
        {
            if (currentState == GameState.WAITING_FOR_TRACKING && !isGameOver)
            {
                StartGame();
            }
        }
        else
        {
            if (currentState != GameState.GAME_OVER && currentState != GameState.STABILITY_CHECK)
            {
                currentState = GameState.WAITING_FOR_TRACKING;
            }
        }
    }

    public void StartGame()
    {
        isGameOver = false;
        losingPlayerIndex = -1;
        currentPlayerIndex = 1;
        hasMovedBlockThisTurn = false;
        currentMovedBlock = null;
        currentState = GameState.PLAYER_TURN;
        Debug.Log($"¡Partida de Jenga Iniciada! Jugadores: {totalPlayers}. Turno del Jugador {currentPlayerIndex}.");
    }

    public void Configure(Transform plane, int initialFloors, float width, float height)
    {
        surfacePlane = plane;
        currentTopFloor = initialFloors;
        blocksOnTopFloor = 3;
        blockWidth = width;
        blockHeight = height;
        UpdateGroundHeight();
    }

    private void UpdateGroundHeight()
    {
        if (surfacePlane != null)
        {
            Collider col = surfacePlane.GetComponent<Collider>();
            baseGroundY = (col != null) ? col.bounds.max.y : surfacePlane.position.y;
        }
    }

    /// <summary>
    /// Regla AR: Solo se permite interacción si el seguimiento AR es estable y es el turno activo.
    /// </summary>
    public bool CanPlayerInteract()
    {
        if (isGameOver) return false;
        if (!isArTrackingStable) return false;
        if (currentState != GameState.PLAYER_TURN) return false;
        if (hasMovedBlockThisTurn) return false;
        return true;
    }

    /// <summary>
    /// Regla: No se pueden retirar bloques del piso superior activo.
    /// </summary>
    public bool CanTouchBlock(int blockFloor)
    {
        return blockFloor < currentTopFloor;
    }

    /// <summary>
    /// Regla de Turno: Registra el inicio de extracción de 1 bloque.
    /// </summary>
    public void OnBlockDragStart(JengaBlock block)
    {
        if (!CanPlayerInteract()) return;
        hasMovedBlockThisTurn = true;
        currentMovedBlock = block;
        block.wasTouchedByPlayer = true;
    }

    /// <summary>
    /// Recoloca el bloque extraído en la posición correspondiente del nivel superior activo (cima).
    /// </summary>
    public void RelocateBlockToTop(JengaBlock block)
    {
        if (isGameOver) return;
        StartCoroutine(PlaceOnTopAndCheckStabilityRoutine(block));
    }

    private IEnumerator PlaceOnTopAndCheckStabilityRoutine(JengaBlock block)
    {
        currentState = GameState.RELOCATING_BLOCK;

        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        // Si el piso superior ya completó sus 3 bloques, creamos un nuevo nivel arriba
        if (blocksOnTopFloor >= 3)
        {
            currentTopFloor++;
            blocksOnTopFloor = 0;
        }

        // Calcular posición del centro del suelo
        Vector3 origin = transform.position;
        if (surfacePlane != null)
        {
            Collider col = surfacePlane.GetComponent<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds;
                origin = new Vector3(b.center.x, b.max.y, b.center.z);
            }
            else
            {
                origin = surfacePlane.position;
            }
        }

        float extraHeight = 0.002f;
        float targetY = origin.y + (blockHeight / 2f) + extraHeight + (currentTopFloor - 1) * (blockHeight + microGap);
        float offset = (blocksOnTopFloor - 1) * (blockWidth + microGap);

        int floorIndex = currentTopFloor - 1;
        bool isEvenFloor = (floorIndex % 2 == 0);

        Vector3 targetPos = isEvenFloor
            ? new Vector3(origin.x + offset, targetY, origin.z)
            : new Vector3(origin.x, targetY, origin.z + offset);

        Quaternion targetRot = isEvenFloor
            ? Quaternion.identity
            : Quaternion.Euler(0f, 90f, 0f);

        block.floorLevel = currentTopFloor;
        block.hasFallen = false;

        block.transform.SetPositionAndRotation(targetPos, targetRot);
        blocksOnTopFloor++;

        yield return new WaitForSeconds(0.1f);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        // Evaluación de estabilidad durante 5 segundos
        currentState = GameState.STABILITY_CHECK;
        stabilityTimer = stabilityCheckDuration;

        while (stabilityTimer > 0f)
        {
            if (isGameOver) yield break;

            stabilityTimer -= Time.deltaTime;
            yield return null;
        }

        // Si pasaron los 5 segundos sin colapso -> El turno termina exitosamente y pasa al siguiente jugador
        if (!isGameOver)
        {
            AdvanceTurn();
        }
    }

    /// <summary>
    /// Cambia de turno cíclicamente según el total de jugadores (2, 3 o 4).
    /// </summary>
    public void AdvanceTurn()
    {
        if (isGameOver) return;

        currentPlayerIndex++;
        if (currentPlayerIndex > totalPlayers)
        {
            currentPlayerIndex = 1; // Retorna al Jugador 1
        }

        hasMovedBlockThisTurn = false;
        currentMovedBlock = null;
        currentState = GameState.PLAYER_TURN;
        Debug.Log($"¡Turno completado exitosamente! Ahora es el turno del Jugador {currentPlayerIndex}.");
    }

    /// <summary>
    /// Detecta el derrumbe de la torre y determina el Jugador perdedor del turno activo.
    /// </summary>
    public void TriggerTowerCollapse(string cause)
    {
        if (isGameOver) return;

        isGameOver = true;
        losingPlayerIndex = currentPlayerIndex;
        currentState = GameState.GAME_OVER;
        StopAllCoroutines();

        Debug.LogError($"💥 ¡LA TORRE HA CAÍDO! Motivo: {cause}. EL JUGADOR {losingPlayerIndex} HA PERDIDO.");
    }

    [ContextMenu("Reiniciar Partida")]
    public void RestartGame()
    {
        StopAllCoroutines();

        JengaFloorTest floorTest = Object.FindFirstObjectByType<JengaFloorTest>();
        if (floorTest != null)
        {
            floorTest.SpawnTower();
        }

        ARJengaTowerSpawner spawner = Object.FindFirstObjectByType<ARJengaTowerSpawner>();
        if (spawner != null)
        {
            spawner.BuildTower();
        }

        JengaTowerBuilder builder = Object.FindFirstObjectByType<JengaTowerBuilder>();
        if (builder != null)
        {
            builder.BuildTower();
        }

        StartGame();
    }

    // Interfaz gráfica Móvil OnGUI
    void OnGUI()
    {
        GUIStyle headerStyle = new GUIStyle(GUI.skin.box);
        headerStyle.fontSize = 20;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.normal.textColor = Color.white;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = Color.yellow;

        GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, 240));

        // Estado AR
        if (!isArTrackingStable)
        {
            GUI.color = Color.red;
            GUILayout.Box("🔴 AR BUSCANDO MARCADOR... Apunte la cámara a la imagen", headerStyle);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.green;
            GUILayout.Box("🟢 SEGUIMIENTO AR ESTABLE", headerStyle);
            GUI.color = Color.white;
        }

        GUILayout.Space(10);

        if (!isGameOver)
        {
            // Selector de Jugadores (2, 3 o 4)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cantidad de Jugadores:", labelStyle, GUILayout.Width(200));
            for (int p = 2; p <= 4; p++)
            {
                if (GUILayout.Toggle(totalPlayers == p, $"{p} Jugadores", "Button", GUILayout.Width(90), GUILayout.Height(35)))
                {
                    if (totalPlayers != p)
                    {
                        totalPlayers = p;
                        if (currentPlayerIndex > totalPlayers) currentPlayerIndex = 1;
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Turno del Jugador Activo
            Color playerColor = Color.cyan;
            switch (currentPlayerIndex)
            {
                case 1: playerColor = Color.cyan; break;
                case 2: playerColor = Color.yellow; break;
                case 3: playerColor = Color.magenta; break;
                case 4: playerColor = Color.green; break;
            }

            GUI.color = playerColor;
            GUILayout.Box($"👤 TURNO ACTUAL: JUGADOR {currentPlayerIndex}", headerStyle);
            GUI.color = Color.white;

            // Timer de 5 Segundos de Estabilidad
            if (currentState == GameState.STABILITY_CHECK)
            {
                GUI.color = Color.yellow;
                GUILayout.Box($"⏱️ EVALUANDO ESTABILIDAD: {stabilityTimer:F1}s (¡No tocar!)", headerStyle);
                GUI.color = Color.white;
            }
            else if (currentState == GameState.PLAYER_TURN)
            {
                GUILayout.Label("👇 Toca y saca un bloque (que no sea del piso superior).", labelStyle);
            }
        }
        else
        {
            // Pantalla de Derrota / Game Over
            GUI.color = Color.red;
            GUILayout.Box($"💥 ¡LA TORRE HA CAÍDO!\n❌ EL JUGADOR {losingPlayerIndex} HA PERDIDO LA PARTIDA", headerStyle);
            GUI.color = Color.white;

            GUILayout.Space(10);
            if (GUILayout.Button("🔄 REINICIAR PARTIDA", GUILayout.Height(50)))
            {
                RestartGame();
            }
        }

        GUILayout.EndArea();
    }
}