using UnityEngine;

public class MenuManager : MonoBehaviour
{
    [Header("MenuPanel")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsMenuPanel;

    private readonly MainMenuController mainMenuController;
    private readonly SettingsMenuController settingsMenuController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowMainMenu();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowMainMenu()
    {
        settingsMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void ShowSettingsMenu()
    {
        mainMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(true);
    }

}
