using UnityEngine;
using System.Collections;
using Vuforia;

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
    public float stabilityCheckDuration = 3.0f;

    [Header("Dimensiones")]
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;
    public float microGap = 0.00035f;
    public Transform surfacePlane;

    [Header("Estado de la Torre")]
    public int currentTopFloor = 10;
    public int blocksOnTopFloor = 3;

    [Header("Tracking AR")]
    public bool isArTrackingStable = false;
    private ObserverBehaviour observer;

    [Header("Estado")]
    public bool isGameOver = false;
    public int losingPlayerIndex = -1;
    public float stabilityTimer = 0f;
    public JengaBlock currentMovedBlock = null;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
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
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
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
            if (currentState != GameState.GAME_OVER && currentState != GameState.STABILITY_CHECK && currentState != GameState.RELOCATING_BLOCK)
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
        currentMovedBlock = null;
        currentState = GameState.PLAYER_TURN;
    }

    public void Configure(Transform plane, int initialFloors, float width, float height)
    {
        surfacePlane = plane;
        currentTopFloor = initialFloors;
        blocksOnTopFloor = 3;
        blockWidth = width;
        blockHeight = height;
    }

    public bool CanPlayerInteract()
    {
        if (isGameOver) return false;
        if (!isArTrackingStable) return false;
        return currentState == GameState.PLAYER_TURN;
    }

    public bool CanTouchBlock(int blockFloor)
    {
        if (!CanPlayerInteract()) return false;
        return blockFloor < currentTopFloor;
    }

    public void OnBlockDragStart(JengaBlock block)
    {
        if (isGameOver) return;
        currentMovedBlock = block;
        block.wasTouchedByPlayer = true;
    }

    public void OnBlockDragCanceled()
    {
        currentMovedBlock = null;
    }

    public void RelocateBlockToTop(JengaBlock block)
    {
        if (isGameOver) return;
        StartCoroutine(AnimateBlockToTopRoutine(block));
    }

    private IEnumerator AnimateBlockToTopRoutine(JengaBlock block)
    {
        currentState = GameState.RELOCATING_BLOCK;

        Rigidbody rb = block.GetComponent<Rigidbody>();
        BoxCollider col = block.GetComponent<BoxCollider>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        yield return new WaitForSeconds(0.12f);

        if (col != null)
        {
            col.enabled = false;
        }

        if (blocksOnTopFloor >= 3)
        {
            currentTopFloor++;
            blocksOnTopFloor = 0;
        }

        int floorIndex = currentTopFloor - 1;
        bool isEvenFloor = (floorIndex % 2 == 0);

        // ORDEN CENTRADO DE MÁXIMA ESTABILIDAD:
        // 1er bloque (0 presentes) -> Centro (0)    (el peso queda 100% sobre el eje central)
        // 2do bloque (1 presente)  -> Izquierda (-1)
        // 3er bloque (2 presentes) -> Derecha (+1)
        float offsetMultiplier = 0f;
        if (blocksOnTopFloor == 0) offsetMultiplier = 0f;
        else if (blocksOnTopFloor == 1) offsetMultiplier = -1f;
        else if (blocksOnTopFloor == 2) offsetMultiplier = 1f;

        float offset = offsetMultiplier * (blockWidth + microGap);
        float localY = (blockHeight / 2f) + (floorIndex * blockHeight);

        Vector3 localTargetPos = isEvenFloor
            ? new Vector3(offset, localY, 0f)
            : new Vector3(0f, localY, offset);

        Quaternion localTargetRot = isEvenFloor
            ? Quaternion.identity
            : Quaternion.Euler(0f, 90f, 0f);

        Vector3 targetPos = transform.TransformPoint(localTargetPos);
        Quaternion targetRot = transform.rotation * localTargetRot;

        Vector3 startPos = block.transform.position;
        Quaternion startRot = block.transform.rotation;

        float duration = 0.75f;
        float elapsed = 0f;
        float peakY = Mathf.Max(startPos.y, targetPos.y) + (blockHeight * 2.0f);

        while (elapsed < duration)
        {
            if (isGameOver)
            {
                if (col != null) col.enabled = true;
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            float arc = 4f * (smoothT - (smoothT * smoothT));
            currentPos.y += arc * (peakY - Mathf.Lerp(startPos.y, targetPos.y, smoothT));

            block.transform.position = currentPos;
            block.transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null;
        }

        block.transform.position = targetPos;
        block.transform.rotation = targetRot;
        block.floorLevel = currentTopFloor;
        block.hasFallen = false;
        blocksOnTopFloor++;

        if (col != null)
        {
            col.enabled = true;
        }

        yield return new WaitForFixedUpdate();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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

        currentMovedBlock = null;
        currentState = GameState.PLAYER_TURN;
    }

    public void TriggerTowerCollapse(string cause)
    {
        if (isGameOver) return;

        isGameOver = true;
        losingPlayerIndex = currentPlayerIndex;
        currentState = GameState.GAME_OVER;
        StopAllCoroutines();
    }

    [ContextMenu("Reiniciar Partida")]
    public void RestartGame()
    {
        StopAllCoroutines();

        JengaFloorTest floorTest = Object.FindFirstObjectByType<JengaFloorTest>();
        if (floorTest != null)
        {
            currentTopFloor = floorTest.floors;
            blocksOnTopFloor = 3;
            floorTest.SpawnTower();
        }

        StartGame();
    }

    void OnGUI()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        GUIStyle cardStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.022f), 16, 24),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        cardStyle.normal.textColor = Color.white;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.018f), 13, 18),
            fontStyle = FontStyle.Bold
        };
        labelStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

        GUIStyle subLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.016f), 12, 16),
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subLabelStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.018f), 13, 18),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
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

            Color playerColor = currentPlayerIndex switch
            {
                1 => new Color(0f, 0.95f, 1f),
                2 => new Color(1f, 0.84f, 0f),
                3 => new Color(1f, 0.2f, 0.6f),
                4 => new Color(0f, 0.9f, 0.45f),
                _ => Color.cyan
            };

            GUI.color = playerColor;
            GUILayout.Box($"TURNO ACTUAL: JUGADOR {currentPlayerIndex}", cardStyle, GUILayout.Height(45));
            GUI.color = Color.white;

            GUILayout.Space(5);

            if (currentState == GameState.RELOCATING_BLOCK)
            {
                GUI.color = new Color(0.4f, 0.9f, 1f);
                GUILayout.Box("📦 COLOCANDO PIEZA EN LA CIMA...", cardStyle, GUILayout.Height(40));
                GUI.color = Color.white;
            }
            else if (currentState == GameState.STABILITY_CHECK)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f);
                GUILayout.Box($"⏱️ EVALUANDO ESTABILIDAD: {stabilityTimer:F1}s", cardStyle, GUILayout.Height(40));
                GUI.color = Color.white;
                GUILayout.Label("⏳ Espera: validando si la torre se sostiene...", subLabelStyle);
            }
            else if (currentState == GameState.PLAYER_TURN)
            {
                GUILayout.Label("👇 Arrastra un bloque hasta extraerlo completamente fuera.", subLabelStyle);
            }
        }
        else
        {
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