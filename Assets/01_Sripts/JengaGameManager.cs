using UnityEngine;
using System.Collections;
using Vuforia;

/// <summary>
/// Gestor principal de reglas, turnos y UI dinámica para Jenga AR.
/// Reglas:
/// - Selector dinámico de 2, 3 o 4 jugadores.
/// - Turnos cíclicos con distintivos de color por jugador.
/// - Restricción de retiro en el nivel superior activo.
/// - Evaluación de estabilidad de 5 segundos tras recolocar un bloque arriba.
/// - Detección automática del perdedor al derrumbarse la torre.
/// - Botón de reinicio inmediato y modal de Game Over.
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
    public float microGap = 0.00015f;
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
        bool isStable = (status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED || status.Status == Status.LIMITED);
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
        Debug.Log($"[JengaGameManager] ¡Partida iniciada! Jugadores: {totalPlayers}. Turno del Jugador {currentPlayerIndex}.");
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

    public bool CanPlayerInteract()
    {
        if (isGameOver) return false;
        if (!isArTrackingStable) return false;
        if (currentState != GameState.PLAYER_TURN) return false;
        if (hasMovedBlockThisTurn) return false;
        return true;
    }

    public bool CanTouchBlock(int blockFloor)
    {
        if (isGameOver) return false;
        if (currentState == GameState.STABILITY_CHECK || currentState == GameState.RELOCATING_BLOCK) return false;
        return blockFloor < currentTopFloor;
    }

    public void OnBlockDragStart(JengaBlock block)
    {
        if (isGameOver) return;
        hasMovedBlockThisTurn = true;
        currentMovedBlock = block;
        block.wasTouchedByPlayer = true;
    }

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

        if (blocksOnTopFloor >= 3)
        {
            currentTopFloor++;
            blocksOnTopFloor = 0;
        }

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

        float extraHeight = 0.0005f;
        float targetY = origin.y + (blockHeight / 2f) + extraHeight + (currentTopFloor - 1) * blockHeight;

        // Orden equilibrado de colocación en nivel superior:
        // 1er bloque (0 presentes) -> Centro (0)
        // 2do bloque (1 presente)  -> Izquierda (-1)
        // 3er bloque (2 presentes) -> Derecha (+1)
        float offsetMultiplier = 0f;
        if (blocksOnTopFloor == 1) offsetMultiplier = -1f;
        else if (blocksOnTopFloor == 2) offsetMultiplier = 1f;

        float offset = offsetMultiplier * (blockWidth + microGap);

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
            rb.Sleep();
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

        if (!isGameOver)
        {
            AdvanceTurn();
        }
    }

    public void AdvanceTurn()
    {
        if (isGameOver) return;

        currentPlayerIndex++;
        if (currentPlayerIndex > totalPlayers)
        {
            currentPlayerIndex = 1;
        }

        hasMovedBlockThisTurn = false;
        currentMovedBlock = null;
        currentState = GameState.PLAYER_TURN;
        Debug.Log($"[JengaGameManager] ¡Turno superado! Ahora es el turno del Jugador {currentPlayerIndex}.");
    }

    public void TriggerTowerCollapse(string cause)
    {
        if (isGameOver) return;

        isGameOver = true;
        losingPlayerIndex = currentPlayerIndex;
        currentState = GameState.GAME_OVER;
        StopAllCoroutines();

        Debug.LogError($"💥 ¡LA TORRE HA CAÍDO! {cause}. EL JUGADOR {losingPlayerIndex} HA PERDIDO.");
    }

    [ContextMenu("Reiniciar Partida")]
    public void RestartGame()
    {
        StopAllCoroutines();

        currentTopFloor = 18;
        blocksOnTopFloor = 3;

        // Reiniciar el monitor de caídas
        JengaTowerMonitor monitor = Object.FindFirstObjectByType<JengaTowerMonitor>();
        if (monitor != null)
        {
            Destroy(monitor); 
        }
        gameObject.AddComponent<JengaTowerMonitor>();

        JengaFloorTest floorTest = Object.FindFirstObjectByType<JengaFloorTest>();
        if (floorTest != null)
        {
            floorTest.SpawnTower();
        }

        StartGame();
    }
    // Interfaz gráfica Móvil OnGUI elegante, estilizada y moderna
    void OnGUI()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        // Estilos para Paneles y Textos
        GUIStyle cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.022f), 16, 24);
        cardStyle.fontStyle = FontStyle.Bold;
        cardStyle.alignment = TextAnchor.MiddleCenter;
        cardStyle.normal.textColor = Color.white;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.018f), 13, 18);
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

        GUIStyle subLabelStyle = new GUIStyle(GUI.skin.label);
        subLabelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.016f), 12, 16);
        subLabelStyle.fontStyle = FontStyle.Italic;
        subLabelStyle.alignment = TextAnchor.MiddleCenter;
        subLabelStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.018f), 13, 18);
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.alignment = TextAnchor.MiddleCenter;
        btnStyle.normal.textColor = Color.white;

       
        float resetBtnW = Mathf.Clamp(sw * 0.3f, 110f, 160f);
        float resetBtnH = Mathf.Clamp(sh * 0.05f, 40f, 55f);
        GUI.color = new Color(0.15f, 0.65f, 1f);
        if (GUI.Button(new Rect(sw - resetBtnW - 20, 20, resetBtnW, resetBtnH), "🔄 REINICIAR", btnStyle))
        {
            RestartGame();
        }
        GUI.color = Color.white;

        float arPillW = Mathf.Clamp(sw * 0.45f, 180f, 260f);
        if (!isArTrackingStable)
        {
            GUI.color = new Color(1f, 0.2f, 0.2f);
            GUI.Box(new Rect(20, 20, arPillW, resetBtnH), "🔴 BUSCANDO AR...", cardStyle);
        }
        else
        {
            GUI.color = new Color(0.1f, 0.85f, 0.4f);
            GUI.Box(new Rect(20, 20, arPillW, resetBtnH), "🟢 AR CONECTADO", cardStyle);
        }
        GUI.color = Color.white;


        float panelW = sw - 40f;
        float panelH = Mathf.Clamp(sh * 0.28f, 190f, 260f);
        GUILayout.BeginArea(new Rect(20, resetBtnH + 30, panelW, panelH));

        if (!isGameOver)
        {
            // Selector de Jugadores Elegante (2, 3 o 4)
            GUILayout.BeginHorizontal();
            GUILayout.Label("👥 Jugadores:", labelStyle, GUILayout.Width(110));
            for (int p = 2; p <= 4; p++)
            {
                GUI.color = (totalPlayers == p) ? Color.cyan : Color.gray;
                if (GUILayout.Toggle(totalPlayers == p, $"{p}P", "Button", GUILayout.Width(55), GUILayout.Height(36)))
                {
                    if (totalPlayers != p)
                    {
                        totalPlayers = p;
                        if (currentPlayerIndex > totalPlayers) currentPlayerIndex = 1;
                    }
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Card de Turno del Jugador Activo con Color Neón Distintivo
            Color playerColor;
            string playerEmoji;
            switch (currentPlayerIndex)
            {
                case 1: playerColor = new Color(0f, 0.95f, 1f); playerEmoji = "💎"; break;
                case 2: playerColor = new Color(1f, 0.84f, 0f); playerEmoji = "👑"; break;
                case 3: playerColor = new Color(1f, 0.2f, 0.6f); playerEmoji = "⚡"; break;
                case 4: playerColor = new Color(0f, 0.9f, 0.45f); playerEmoji = "🔥"; break;
                default: playerColor = Color.cyan; playerEmoji = "👤"; break;
            }

            GUI.color = playerColor;
            GUILayout.Box($"{playerEmoji} TURNO ACTUAL: JUGADOR {currentPlayerIndex}", cardStyle, GUILayout.Height(45));
            GUI.color = Color.white;

            GUILayout.Space(5);

            // Estado de Estabilidad / Instrucción de Turno
            if (currentState == GameState.STABILITY_CHECK)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f);
                GUILayout.Box($"⏱️ EVALUANDO ESTABILIDAD: {stabilityTimer:F1}s", cardStyle, GUILayout.Height(40));
                GUI.color = Color.white;
                GUILayout.Label("¡No toques! Evaluando si la torre se sostiene...", subLabelStyle);
            }
            else if (currentState == GameState.PLAYER_TURN)
            {
                GUILayout.Label("👇 Toca un bloque para extraerlo (prohibido del nivel superior).", subLabelStyle);
            }
        }
        else
        {
            // Pantalla / Modal de Game Over
            GUI.color = new Color(1f, 0.25f, 0.25f);
            GUILayout.Box($"💥 ¡LA TORRE HA CAÍDO!\n❌ EL JUGADOR {losingPlayerIndex} HA PERDIDO LA PARTIDA", cardStyle, GUILayout.Height(85));
            GUI.color = Color.white;

            GUILayout.Space(10);
            GUI.color = new Color(0.2f, 0.9f, 0.4f);
            if (GUILayout.Button("🔄 NUEVA PARTIDA", btnStyle, GUILayout.Height(50)))
            {
                RestartGame();
            }
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }
}