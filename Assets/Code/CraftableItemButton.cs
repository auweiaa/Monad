using UnityEngine;
using UnityEngine.UI;

public class CraftableItemButton : MonoBehaviour
{

    [SerializeField] private ResourceManager resourceManager;
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject closeButton;
    [SerializeField] private TowerData towerData;
    [SerializeField] private UnityEngine.UI.Button button;

    private void UpdateButtonInteractability()
    {
        Debug.Log("UpdateButtonInteractability");
        button.interactable = towerData.Cost.CanAfford(resourceManager);
    }
    public void closeCraftingPanel()
    {
        craftingPanel.SetActive(false);
        closeButton.SetActive(false);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        resourceManager = FindFirstObjectByType<ResourceManager>();
        craftingPanel = GameObject.Find("CraftingPanel");
        closeButton = GameObject.Find("CloseButton");
        resourceManager.ResourcesChanged += UpdateButtonInteractability;
        UpdateButtonInteractability(); 
    }
    void OnDisable()
    {
        resourceManager.ResourcesChanged -= UpdateButtonInteractability;
    }
}
