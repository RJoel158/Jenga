using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject optionsMenu;
    public GameObject mainMenu;
    [SerializeField] private string gameplaySceneName = "CameraTest";

    public void OpenOptionsPanel()
    {
        if (mainMenu != null)
        {
            mainMenu.SetActive(false);
        }

        if (optionsMenu != null)
        {
            optionsMenu.SetActive(true);
        }
        else
        {
            Debug.LogWarning("MainMenu: optionsMenu no esta asignado en el Inspector.");
        }
    }

    public void OpenMainMenuPanel()
    {
        if (mainMenu != null)
        {
            mainMenu.SetActive(true);
        }
        else
        {
            Debug.LogWarning("MainMenu: mainMenu no esta asignado en el Inspector.");
        }

        if (optionsMenu != null)
        {
            optionsMenu.SetActive(false);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(); 
#endif
    }

    public void PlayGame()
    {
        if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
        else
        {
            Debug.LogError("MainMenu: la escena '" + gameplaySceneName + "' no esta en Build Settings.");
        }
    }



}
