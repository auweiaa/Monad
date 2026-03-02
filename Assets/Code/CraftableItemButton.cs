using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class CraftableItemButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [SerializeField] private ResourceManager resourceManager;
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject closeButton;
    [SerializeField] private TowerData towerData;
    [SerializeField] private Button button;
    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;

    private const string TooltipRootName = "CraftCostTooltip";
    private const string TooltipTextName = "CostText";

    private void UpdateButtonInteractability()
    {
        if (button == null || towerData == null || resourceManager == null)
        {
            return;
        }

        button.interactable = towerData.Cost.CanAfford(resourceManager);
    }

    public void closeCraftingPanel()
    {
        if (craftingPanel != null)
        {
            craftingPanel.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.SetActive(false);
        }
    }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        EnsureTooltipReferences();
        HideTooltip();
    }

    void OnEnable()
    {
        if (resourceManager == null)
        {
            resourceManager = FindFirstObjectByType<ResourceManager>();
        }

        if (craftingPanel == null)
        {
            craftingPanel = GameObject.Find("CraftingPanel");
        }

        if (closeButton == null)
        {
            closeButton = GameObject.Find("CloseButton");
        }

        if (resourceManager != null)
        {
            resourceManager.ResourcesChanged += UpdateButtonInteractability;
        }

        UpdateButtonInteractability();
        HideTooltip();
    }

    void OnDisable()
    {
        if (resourceManager != null)
        {
            resourceManager.ResourcesChanged -= UpdateButtonInteractability;
        }

        HideTooltip();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureTooltipReferences();
        if (towerData == null || tooltipPanel == null || tooltipText == null)
        {
            return;
        }

        tooltipText.text = towerData.Cost.GetCostDisplayString();
        tooltipPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void EnsureTooltipReferences()
    {
        if (tooltipPanel == null)
        {
            var existing = transform.Find(TooltipRootName);
            if (existing != null)
            {
                tooltipPanel = existing.gameObject;
            }
        }

        if (tooltipPanel == null)
        {
            tooltipPanel = CreateTooltipPanel();
        }

        if (tooltipText == null && tooltipPanel != null)
        {
            tooltipText = tooltipPanel.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private GameObject CreateTooltipPanel()
    {
        var tooltipRoot = new GameObject(TooltipRootName, typeof(RectTransform), typeof(Image));
        tooltipRoot.transform.SetParent(transform, false);

        var rootRect = tooltipRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 8f);
        rootRect.sizeDelta = new Vector2(130f, 74f);

        var background = tooltipRoot.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.85f);
        background.raycastTarget = false;

        var textObject = new GameObject(TooltipTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(tooltipRoot.transform, false);

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 6f);
        textRect.offsetMax = new Vector2(-8f, -6f);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.color = Color.white;
        text.font = TMP_Settings.defaultFontAsset;

        tooltipPanel = tooltipRoot;
        tooltipText = text;
        tooltipPanel.SetActive(false);
        return tooltipRoot;
    }
}
