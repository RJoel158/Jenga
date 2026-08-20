using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Controlador ultra-robusto para el Menú Principal en Móviles (Android/iOS) y Editor.
/// Combina auto-vinculación de botones C#, escalado responsivo y detector directo de Raycast Táctil.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Paneles del Menú")]
    public GameObject mainMenu;
    public GameObject optionsMenu;

    private void Awake()
    {
        EnsureEventSystem();
        EnsureResponsiveCanvas();
        AutoHookupButtons();
    }

    private void Start()
    {
        EnsureEventSystem();
        EnsureResponsiveCanvas();
        AutoHookupButtons();
    }

    private void Update()
    {
        // Detector de Toque Directo Móvil como garantía infalible para pantalla táctil
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                CheckDirectTouchOnUI(touch.position);
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            CheckDirectTouchOnUI(Input.mousePosition);
        }
    }

    /// <summary>
    /// Realiza un Raycast gráfico directo sobre la UI al tocar la pantalla táctil móvil,
    /// asegurando que el botón se ejecute incluso si el módulo de InputSystem tiene problemas en Android/iOS.
    /// </summary>
    private void CheckDirectTouchOnUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) EnsureEventSystem();
        if (EventSystem.current == null) return;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPos;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            Button btn = result.gameObject.GetComponentInParent<Button>();
            if (btn != null && btn.interactable)
            {
                Debug.Log($"[MainMenu] ¡Toque táctil directo detectado en botón: '{btn.gameObject.name}'!");
                btn.onClick.Invoke();
                break;
            }
        }
    }

    /// <summary>
    /// Garantiza la presencia de un EventSystem en la escena.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
            Debug.Log("[MainMenu] EventSystem auto-creado.");
        }
    }

    /// <summary>
    /// Adapta la escala del Canvas a 1080x1920 (pantallas móviles portrait) o 1920x1080 (landscape).
    /// </summary>
    private void EnsureResponsiveCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        bool isPortrait = Screen.height > Screen.width;

        foreach (Canvas canvas in canvases)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = isPortrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    /// <summary>
    /// Desbloquea paneles decorativos y vincula C# listeners a todos los botones del menú.
    /// </summary>
    private void AutoHookupButtons()
    {
        // 1. Quitar bloqueo de raycast en paneles decorativos
        Image[] allImages = Object.FindObjectsByType<Image>(FindObjectsSortMode.None);
        foreach (Image img in allImages)
        {
            if (img.GetComponent<Button>() == null && img.gameObject.name.ToLower().Contains("panel"))
            {
                img.raycastTarget = false;
            }
        }

        // 2. Vincular listeners C#
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (Button btn in buttons)
        {
            string btnName = btn.gameObject.name.ToLower();

            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null) btnImg.raycastTarget = true;

            btn.onClick.RemoveAllListeners();

            if (btnName.Contains("play") || btnName.Contains("jugar") || btnName.Contains("iniciar"))
            {
                btn.onClick.AddListener(PlayGame);
            }
            else if (btnName.Contains("quit") || btnName.Contains("salir"))
            {
                btn.onClick.AddListener(QuitGame);
            }
            else if (btnName.Contains("option") || btnName.Contains("opcion"))
            {
                btn.onClick.AddListener(OpenOptionsPanel);
            }
            else if (btnName.Contains("back") || btnName.Contains("atras") || btnName.Contains("regresar"))
            {
                btn.onClick.AddListener(OpenMainMenuPanel);
            }
        }
    }

    public void PlayGame()
    {
        Debug.Log("[MainMenu] 🚀 Cargando escena de juego 'CameraTest'...");
        SceneManager.LoadScene("CameraTest");
    }

    public void OpenOptionsPanel()
    {
        if (mainMenu != null) mainMenu.SetActive(false);
        if (optionsMenu != null) optionsMenu.SetActive(true);
    }

    public void OpenMainMenuPanel()
    {
        if (mainMenu != null) mainMenu.SetActive(true);
        if (optionsMenu != null) optionsMenu.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("[MainMenu] Saliendo del juego...");
        Application.Quit();
    }
}
