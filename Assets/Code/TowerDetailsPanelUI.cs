using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TowerDetailsPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TowerSelectionController selectionController;
    [SerializeField] private GameObject panelRoot;

    [Header("Details Text")]
    [SerializeField] private TMP_Text towerNameText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text attackSpeedText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text healthText;

    [Header("Upgrade Buttons (Placeholder)")]
    [SerializeField] private Button upgradeButton1;
    [SerializeField] private Button upgradeButton2;
    [SerializeField] private Button upgradeButton3;
    [SerializeField] private TMP_Text upgradeButton1Label;
    [SerializeField] private TMP_Text upgradeButton2Label;
    [SerializeField] private TMP_Text upgradeButton3Label;

    private PlacedTower currentTower;

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        BindUpgradeButtons();
        SetPanelVisible(false);
    }

    private void OnEnable()
    {
        if (selectionController == null)
        {
            selectionController = FindFirstObjectByType<TowerSelectionController>();
        }

        if (selectionController != null)
        {
            selectionController.SelectionChanged += HandleSelectionChanged;
            HandleSelectionChanged(selectionController.CurrentSelection);
        }
        else
        {
            SetPanelVisible(false);
        }
    }

    private void OnDisable()
    {
        if (selectionController != null)
        {
            selectionController.SelectionChanged -= HandleSelectionChanged;
        }
    }

    private void BindUpgradeButtons()
    {
        if (upgradeButton1 != null)
        {
            upgradeButton1.onClick.RemoveAllListeners();
            upgradeButton1.onClick.AddListener(() => OnUpgradeClicked(1));
        }

        if (upgradeButton2 != null)
        {
            upgradeButton2.onClick.RemoveAllListeners();
            upgradeButton2.onClick.AddListener(() => OnUpgradeClicked(2));
        }

        if (upgradeButton3 != null)
        {
            upgradeButton3.onClick.RemoveAllListeners();
            upgradeButton3.onClick.AddListener(() => OnUpgradeClicked(3));
        }

        SetText(upgradeButton1Label, "Upgrade 1 (Soon)");
        SetText(upgradeButton2Label, "Upgrade 2 (Soon)");
        SetText(upgradeButton3Label, "Upgrade 3 (Soon)");
    }

    private void HandleSelectionChanged(PlacedTower tower)
    {
        currentTower = tower;

        if (currentTower == null || currentTower.TowerData == null)
        {
            SetPanelVisible(false);
            ClearDetails();
            return;
        }

        UpdateDetails(currentTower.TowerData);
        SetPanelVisible(true);
    }

    private void UpdateDetails(TowerData towerData)
    {
        if (towerData == null)
        {
            ClearDetails();
            return;
        }

        SetText(towerNameText, towerData.TowerName);
        SetText(damageText, $"Damage: {towerData.Damage}");
        SetText(attackSpeedText, $"Attack Speed: {towerData.AttackSpeed:0.##}/s");
        SetText(rangeText, $"Range: {towerData.Range:0.##}");
        SetText(healthText, $"Health: {towerData.Health}");
    }

    private void ClearDetails()
    {
        SetText(towerNameText, string.Empty);
        SetText(damageText, string.Empty);
        SetText(attackSpeedText, string.Empty);
        SetText(rangeText, string.Empty);
        SetText(healthText, string.Empty);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }

    private static void SetText(TMP_Text textField, string value)
    {
        if (textField != null)
        {
            textField.text = value;
        }
    }

    private void OnUpgradeClicked(int upgradeIndex)
    {
        if (currentTower == null || currentTower.TowerData == null)
        {
            return;
        }

        Debug.Log($"Upgrade {upgradeIndex} clicked for {currentTower.TowerData.TowerName}. Placeholder only.");
    }
}
