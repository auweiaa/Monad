using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Base"; //gameplay scene
    [SerializeField] private MenuManager menuManager;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Play()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("Base");
    }

    public void OpenSettingsMenu()
    {
        menuManager.ShowSettingsMenu();
        Debug.Log("Open Settings Menu");
    }
    
    public void Quit()
    {
        Application.Quit();
    }
}
