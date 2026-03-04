using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TowerSelectionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlacementManager placementManager;

    [Header("Click Selection")]
    [Tooltip("Layer mask used when checking what was clicked. Default selects everything.")]
    [SerializeField] private LayerMask towerSelectionMask = ~0;
    [Tooltip("Trigger colliders are only allowed for selection when they are on a child object with this name.")]
    [SerializeField] private string clickableTriggerChildName = "Clickable";
    [SerializeField] private string rangeCenterChildName = "RangeCenter";

    [Header("Range Visual")]
    [SerializeField] private Color rangeColor = new Color(0.1f, 0.9f, 1f, 0.9f);
    [SerializeField, Min(0.01f)] private float rangeLineWidth = 0.12f;
    [SerializeField, Min(12)] private int rangeSegments = 64;
    [SerializeField] private float rangeZOffset = 0f;
    [SerializeField] private int sortingOrder = 50;
    [SerializeField] private bool followSelectedTowerSorting = true;

    [Header("Isometric Range Projection")]
    [SerializeField] private bool useIsometricProjection = true;
    [SerializeField, Min(0.01f)] private float isometricYScale = 0.5f;
    [SerializeField] private float isometricShear = 0f;

    private LineRenderer rangeRenderer;

    public event Action<PlacedTower> SelectionChanged;
    public PlacedTower CurrentSelection { get; private set; }

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
        }

        EnsureRangeRenderer();
        HideRange();
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        if (placementManager != null && placementManager.IsInPlacementMode)
        {
            if (CurrentSelection != null)
            {
                ClearSelection();
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (placementManager == null || !placementManager.PlacementConsumedClickThisFrame)
            {
                HandleLeftClick();
            }
        }

        if (CurrentSelection == null)
        {
            return;
        }

        if (CurrentSelection.gameObject == null || !CurrentSelection.gameObject.activeInHierarchy)
        {
            ClearSelection();
            return;
        }

        DrawRange(CurrentSelection);
    }

    public void SelectTower(PlacedTower tower)
    {
        if (tower == CurrentSelection)
        {
            return;
        }

        CurrentSelection = tower;
        if (CurrentSelection != null)
        {
            DrawRange(CurrentSelection);
        }
        else
        {
            HideRange();
        }

        SelectionChanged?.Invoke(CurrentSelection);
    }

    public void ClearSelection()
    {
        if (CurrentSelection == null)
        {
            return;
        }

        CurrentSelection = null;
        HideRange();
        SelectionChanged?.Invoke(null);
    }

    private void HandleLeftClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector3 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        Collider2D[] hits = Physics2D.OverlapPointAll(world, towerSelectionMask);
        PlacedTower clickedTower = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
            {
                continue;
            }

            if (col.isTrigger && !IsAllowedClickableTrigger(col))
            {
                continue;
            }

            clickedTower = col.GetComponentInParent<PlacedTower>();
            if (clickedTower != null)
            {
                break;
            }
        }

        if (clickedTower != null)
        {
            SelectTower(clickedTower);
        }
        else
        {
            ClearSelection();
        }
    }

    private bool IsAllowedClickableTrigger(Collider2D col)
    {
        if (col == null || !col.isTrigger)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(clickableTriggerChildName))
        {
            return false;
        }

        string objectName = col.gameObject.name;
        if (string.Equals(objectName, clickableTriggerChildName, StringComparison.Ordinal))
        {
            return true;
        }

        // Unity may append " (Clone)" on runtime-created objects.
        return objectName.StartsWith(clickableTriggerChildName + " (", StringComparison.Ordinal);
    }

    private void EnsureRangeRenderer()
    {
        if (rangeRenderer != null)
        {
            return;
        }

        var child = new GameObject("SelectedTowerRange");
        child.transform.SetParent(transform, false);

        rangeRenderer = child.AddComponent<LineRenderer>();
        rangeRenderer.useWorldSpace = true;
        rangeRenderer.loop = true;
        rangeRenderer.alignment = LineAlignment.View;
        rangeRenderer.textureMode = LineTextureMode.Stretch;
        rangeRenderer.numCapVertices = 4;
        rangeRenderer.numCornerVertices = 4;
        rangeRenderer.startWidth = rangeLineWidth;
        rangeRenderer.endWidth = rangeLineWidth;
        rangeRenderer.sortingOrder = sortingOrder;
        rangeRenderer.startColor = rangeColor;
        rangeRenderer.endColor = rangeColor;
        rangeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRenderer.receiveShadows = false;
        rangeRenderer.enabled = false;
        rangeRenderer.allowOcclusionWhenDynamic = false;

        EnsureRangeMaterial();
    }

    private void EnsureRangeMaterial()
    {
        if (rangeRenderer == null)
        {
            return;
        }

        if (rangeRenderer.sharedMaterial != null)
        {
            return;
        }

        Shader shader =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            rangeRenderer.material = new Material(shader);
        }
    }

    private void HideRange()
    {
        if (rangeRenderer != null)
        {
            rangeRenderer.enabled = false;
        }
    }

    private void DrawRange(PlacedTower tower)
    {
        if (tower == null || tower.TowerData == null)
        {
            HideRange();
            return;
        }

        EnsureRangeRenderer();
        if (rangeRenderer == null)
        {
            return;
        }

        float radius = Mathf.Max(0f, tower.TowerData.Range);
        if (radius <= 0f)
        {
            HideRange();
            return;
        }

        rangeRenderer.startWidth = rangeLineWidth;
        rangeRenderer.endWidth = rangeLineWidth;
        rangeRenderer.startColor = rangeColor;
        rangeRenderer.endColor = rangeColor;
        rangeRenderer.sortingOrder = sortingOrder;

        if (followSelectedTowerSorting)
        {
            var towerRenderer = tower.GetComponentInChildren<Renderer>();
            if (towerRenderer != null)
            {
                rangeRenderer.sortingLayerID = towerRenderer.sortingLayerID;
                rangeRenderer.sortingOrder = towerRenderer.sortingOrder + 1;
            }
        }

        int segments = Mathf.Max(12, rangeSegments);
        rangeRenderer.positionCount = segments;

        Vector3 center = GetRangeCenterPosition(tower);
        center.z = rangeZOffset;

        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            float localX = Mathf.Cos(angle) * radius;
            float localY = Mathf.Sin(angle) * radius;

            if (useIsometricProjection)
            {
                float isoX = localX + (localY * isometricShear);
                float isoY = localY * isometricYScale;
                localX = isoX;
                localY = isoY;
            }

            Vector3 point = new Vector3(center.x + localX, center.y + localY, center.z);
            rangeRenderer.SetPosition(i, point);
        }

        rangeRenderer.enabled = true;
    }

    private Vector3 GetRangeCenterPosition(PlacedTower tower)
    {
        if (tower == null)
        {
            return Vector3.zero;
        }

        if (!string.IsNullOrWhiteSpace(rangeCenterChildName))
        {
            Transform[] children = tower.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null)
                {
                    continue;
                }

                if (string.Equals(child.name, rangeCenterChildName, StringComparison.Ordinal))
                {
                    return child.position;
                }
            }
        }

        return tower.transform.position;
    }
}
