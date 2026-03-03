using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Core core;

    void Start()
    {
        // Find Core if not assigned
        if (core == null)
        {
            core = FindAnyObjectByType<Core>();
        }

        // Hide game over panel at start
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // Subbing to core death event
        if (core != null)
        {
            core.OnCoreDeathEvent += ShowGameOver;
        }
    }

    void OnDestroy()
    {
        if (core != null)
        {
            core.OnCoreDeathEvent -= ShowGameOver;
        }
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            //Time.timeScale = 0f; // Pause the game? Not sure if we want to do this, as it might interfere with any game over animations or effects.
        }
    }

    public void GoBackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
