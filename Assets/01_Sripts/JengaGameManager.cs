using UnityEngine;
using System.Collections;
using Vuforia;

/// <summary>
/// Controlador principal de reglas y turnos para el juego de Jenga AR.
/// Reglas implementadas:
/// 1. Inicia la partida con el Jugador 1.
/// 2. Cada jugador mueve un único bloque por turno.
/// 3. Prohibido retirar bloques del piso superior activo.
/// 4. El bloque retirado debe colocarse sobre la torre.
/// 5. El turno no acaba hasta transcurrir 5 segundos de evaluación de estabilidad.
/// 6. El jugador en cuyo turno cae la torre pierde.
/// 7. Turnos cíclicos entre 2, 3 o 4 jugadores (retorna al Jugador 1).
/// 8. Manipulación bloqueada hasta que el seguimiento AR sea estable.
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

    [Header("Dimensiones de Jenga")]
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public Transform surfacePlane;

    [Header("Estado de la Torre")]
    public int currentTopFloor = 18;
    public int blocksOnTopFloor = 3;

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
        Debug.Log($"¡Partida de Jenga Iniciada! Turno del Jugador {currentPlayerIndex}.");
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
    /// Regla 8: La manipulación solo se habilita cuando el seguimiento AR sea estable.
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
    /// Regla 3: No se pueden retirar bloques del nivel superior activo.
    /// </summary>
    public bool CanTouchBlock(int blockFloor)
    {
        return blockFloor < currentTopFloor;
    }

    /// <summary>
    /// Regla 2: Cada jugador mueve un único bloque por turno.
    /// </summary>
    public void OnBlockDragStart(JengaBlock block)
    {
        if (!CanPlayerInteract()) return;
        hasMovedBlockThisTurn = true;
        currentMovedBlock = block;
        block.wasTouchedByPlayer = true;
    }

    /// <summary>
    /// Regla 4 & 5: Coloca el bloque retirado sobre la torre e inicia el conteo de 5 segundos de estabilidad.
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

        // Si el piso superior ya tiene 3 bloques, creamos un nuevo nivel arriba
        if (blocksOnTopFloor >= 3)
        {
            currentTopFloor++;
            blocksOnTopFloor = 0;
        }

        Transform parentTransform = (surfacePlane != null && surfacePlane.parent != null) ? surfacePlane.parent : transform;
        
        float tableThickness = surfacePlane != null ? surfacePlane.localScale.y : 0.012f;
        float localY = tableThickness + ((currentTopFloor - 1) * blockHeight) + (blockHeight / 2f);
        float offset = (blocksOnTopFloor - 1) * blockWidth;

        bool isEvenFloor = (currentTopFloor % 2 == 0);
        Vector3 localPos = isEvenFloor
            ? new Vector3(offset, localY, 0f)
            : new Vector3(0f, localY, offset);
        Quaternion localRot = isEvenFloor
            ? Quaternion.identity
            : Quaternion.Euler(0f, 90f, 0f);

        Vector3 targetPos = parentTransform.TransformPoint(localPos);
        Quaternion targetRot = parentTransform.rotation * localRot;

        block.floorLevel = currentTopFloor;
        block.hasFallen = false;

        block.transform.SetParent(parentTransform, true);
        block.transform.SetPositionAndRotation(targetPos, targetRot);
        blocksOnTopFloor++;

        yield return new WaitForSeconds(0.1f);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        // REGLA 5: Evaluación de estabilidad durante 5 segundos
        currentState = GameState.STABILITY_CHECK;
        stabilityTimer = stabilityCheckDuration;

        while (stabilityTimer > 0f)
        {
            if (isGameOver) yield break;

            stabilityTimer -= Time.deltaTime;
            yield return null;
        }

        // Si pasaron los 5 segundos sin colapso -> El turno termina exitosamente
        if (!isGameOver)
        {
            AdvanceTurn();
        }
    }

    /// <summary>
    /// Regla 7: Cambia de turno cíclicamente (retorna a Jugador 1).
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
        Debug.Log($"¡Turno completado! Ahora es el turno del Jugador {currentPlayerIndex}.");
    }

    /// <summary>
    /// Regla 6: El jugador que derriba la torre pierde.
    /// </summary>
    public void TriggerTowerCollapse(string cause)
    {
        if (isGameOver) return;

        isGameOver = true;
        losingPlayerIndex = currentPlayerIndex;
        currentState = GameState.GAME_OVER;
        StopAllCoroutines();

        Debug.LogError($"💥 ¡LA TORRE SE CAYÓ! {cause}. El Jugador {losingPlayerIndex} HA PERDIDO LA PARTIDA.");
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

    // Interfaz de Usuario integrada para pantalla móvil
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

        // Regla 8: Estado AR
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
            // Regla 7: Selector de 2, 3 o 4 jugadores
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

            // Regla 1 & 7: Indicador de Turno
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

            // Regla 5: Conteo de 5 segundos de estabilidad
            if (currentState == GameState.STABILITY_CHECK)
            {
                GUI.color = Color.yellow;
                GUILayout.Box($"⏱️ EVALUANDO ESTABILIDAD: {stabilityTimer:F1}s (¡No tocar!)", headerStyle);
                GUI.color = Color.white;
            }
            else if (currentState == GameState.PLAYER_TURN)
            {
                GUILayout.Label("👇 Toca y desliza un bloque (que no sea del piso superior) para sacarlo.", labelStyle);
            }
        }
        else
        {
            // Regla 6: El jugador que derriba la torre pierde
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