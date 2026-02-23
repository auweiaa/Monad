using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ResourceManager resourceManager;

    [Header("Selection")]
    [SerializeField] private TowerData selectedTower;

    [Header("Preview")]
    [SerializeField, Range(0f, 1f)] private float previewAlpha = 0.7f;
    [SerializeField] private Color canPlacePreviewColor = Color.green;
    [SerializeField] private Color cannotPlacePreviewColor = Color.red;

    private GameObject previewInstance;
    private SpriteRenderer[] previewSpriteRenderers;
    private Color[] previewBaseColors;

    [Header("Placement")]
    [Tooltip("Parent for placed tower instances (optional).")]
    [SerializeField] private Transform placedTowersParent;

    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    public bool IsInPlacementMode => selectedTower != null;

    /// <summary>Raised whenever the occupied-cell set changes (e.g. tower placed).</summary>
    public event System.Action OnGridChanged;

    private void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid>();
        if (mainCamera == null) mainCamera = Camera.main;

        if (groundTilemap == null)
        {
            // Prefer a tilemap named 'Ground' if it exists.
            var allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (var tm in allTilemaps)
            {
                if (tm != null && tm.name == "Ground")
                {
                    groundTilemap = tm;
                    break;
                }
            }

            // Fallback to any tilemap (better than null, but should be assigned in inspector).
            if (groundTilemap == null && allTilemaps.Length > 0)
            {
                groundTilemap = allTilemaps[0];
            }
        }
    }

    public void SelectTower(TowerData tower)
    {
        selectedTower = tower;
        DestroyPreview();
        EnsurePreviewExists();
    }

    private void Update()
    {
        if (selectedTower == null)
        {
            DestroyPreview();
            return;
        }

        EnsurePreviewExists();
        UpdatePreviewAtMouse();

        // Cancel selection + preview.
        if (Input.GetMouseButtonDown(1))
        {
            ClearSelection();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (TryPlaceSelectedAtMouse())
            {
                // Auto-cancel after a successful placement.
                ClearSelection();
            }
        }
    }

    private bool TryPlaceSelectedAtMouse()
    {
        if (mainCamera == null || grid == null || groundTilemap == null)
        {
            return false;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        if (selectedTower == null || selectedTower.TowerPrefab == null)
        {
            return false;
        }

        Vector3 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        Vector3Int originCell = grid.WorldToCell(world); // clicked cell = bottom-left

        if (!CanPlaceAt(originCell, selectedTower.Footprint))
        {
            return false;
        }

        if (resourceManager != null && !resourceManager.TrySpend(selectedTower.Cost))
        {
            return false;
        }

        Vector3 placementWorldPos = GetFootprintCenterWorld(originCell, selectedTower.Footprint);
        GameObject placed = Instantiate(
            selectedTower.TowerPrefab,
            placementWorldPos,
            Quaternion.identity,
            placedTowersParent
        );

        // Reserve cells and store placement metadata.
        var placedTower = placed.GetComponent<PlacedTower>();
        if (placedTower == null)
        {
            placedTower = placed.AddComponent<PlacedTower>();
        }
        placedTower.Initialize(selectedTower, originCell, selectedTower.Footprint);

        foreach (var cell in EnumerateFootprintCells(originCell, selectedTower.Footprint))
        {
            occupiedCells.Add(cell);
        }

        OnGridChanged?.Invoke();
        return true;
    }

    private bool CanPlaceAt(Vector3Int originCell, Vector2Int footprint)
    {
        if (footprint.x < 1 || footprint.y < 1)
        {
            return false;
        }

        foreach (var cell in EnumerateFootprintCells(originCell, footprint))
        {
            if (!groundTilemap.HasTile(cell))
            {
                return false;
            }

            if (occupiedCells.Contains(cell))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<Vector3Int> EnumerateFootprintCells(Vector3Int originCell, Vector2Int footprint)
    {
        for (int x = 0; x < footprint.x; x++)
        {
            for (int y = 0; y < footprint.y; y++)
            {
                yield return originCell + new Vector3Int(x, y, 0);
            }
        }
    }

    private Vector3 GetFootprintCenterWorld(Vector3Int originCell, Vector2Int footprint)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (var cell in EnumerateFootprintCells(originCell, footprint))
        {
            sum += grid.GetCellCenterWorld(cell);
            count++;
        }

        if (count == 0)
        {
            return grid.GetCellCenterWorld(originCell);
        }

        Vector3 avg = sum / count;
        avg.z = 0f;
        return avg;
    }

    private void ClearSelection()
    {
        selectedTower = null;
        DestroyPreview();
    }

    private void EnsurePreviewExists()
    {
        if (previewInstance != null || selectedTower == null || selectedTower.TowerPrefab == null)
        {
            return;
        }

        previewInstance = Instantiate(selectedTower.TowerPrefab, Vector3.zero, Quaternion.identity, transform);
        previewInstance.name = $"{selectedTower.TowerName}_Preview";

        // Prevent preview from interacting with gameplay systems.
        DisablePreviewInteractions(previewInstance);
        CachePreviewRenderers(previewInstance);
    }

    private void DestroyPreview()
    {
        if (previewInstance == null)
        {
            previewSpriteRenderers = null;
            previewBaseColors = null;
            return;
        }

        Destroy(previewInstance);
        previewInstance = null;
        previewSpriteRenderers = null;
        previewBaseColors = null;
    }

    private void CachePreviewRenderers(GameObject root)
    {
        previewSpriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        previewBaseColors = new Color[previewSpriteRenderers.Length];
        for (int i = 0; i < previewSpriteRenderers.Length; i++)
        {
            previewBaseColors[i] = previewSpriteRenderers[i].color;
        }
    }

    private void DisablePreviewInteractions(GameObject root)
    {
        // Disable colliders.
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }
        foreach (var col2D in root.GetComponentsInChildren<Collider2D>(true))
        {
            col2D.enabled = false;
        }

        // Disable physics bodies if present.
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (var rb2D in root.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rb2D.simulated = false;
        }

        // Disable scripts on the preview so it can't attack/trigger logic while previewing.
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            mb.enabled = false;
        }

        // Keep preview from being raycast-targeted in most setups.
        SetLayerRecursively(root, 2); // Ignore Raycast
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        var t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }

    private void UpdatePreviewAtMouse()
    {
        if (previewInstance == null || mainCamera == null || grid == null || groundTilemap == null || selectedTower == null)
        {
            return;
        }

        Vector3 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        Vector3Int originCell = grid.WorldToCell(world);
        previewInstance.transform.position = GetFootprintCenterWorld(originCell, selectedTower.Footprint);

        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool canAfford = resourceManager == null || resourceManager.CanAfford(selectedTower.Cost);
        bool canPlace = !pointerOverUi && canAfford && CanPlaceAt(originCell, selectedTower.Footprint);

        ApplyPreviewTint(canPlace ? canPlacePreviewColor : cannotPlacePreviewColor);
    }

    private void ApplyPreviewTint(Color tint)
    {
        if (previewSpriteRenderers == null || previewBaseColors == null)
        {
            return;
        }

        float a = Mathf.Clamp01(previewAlpha);
        for (int i = 0; i < previewSpriteRenderers.Length; i++)
        {
            if (previewSpriteRenderers[i] == null)
            {
                continue;
            }

            Color baseColor = previewBaseColors[i];
            previewSpriteRenderers[i].color = new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a * a
            );
        }
    }
    
    /// <summary>Returns true when a tower occupies this cell.</summary>
    public bool IsCellOccupied(Vector3Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    /// <summary>Returns true when this cell has ground and no tower – i.e. enemies can walk through it.</summary>
    public bool IsCellWalkable(Vector3Int cell)
    {
        if (groundTilemap != null && !groundTilemap.HasTile(cell)) return false;
        return !occupiedCells.Contains(cell);
    }

    /// <summary>Marks footprint cells as occupied (e.g. for pre-placed towers in the scene). Fires OnGridChanged.</summary>
    public void OccupyCells(Vector3Int originCell, Vector2Int footprint)
    {
        if (footprint.x < 1 || footprint.y < 1) return;
        for (int x = 0; x < footprint.x; x++)
        for (int y = 0; y < footprint.y; y++)
            occupiedCells.Add(originCell + new Vector3Int(x, y, 0));
        OnGridChanged?.Invoke();
    }

    /// <summary>
    /// Called by a PlacedTower when it is destroyed. Frees its cells and
    /// fires OnGridChanged so enemies can recalculate their paths.
    /// </summary>
    public void FreeCells(Vector3Int originCell, Vector2Int footprint)
    {
        for (int x = 0; x < footprint.x; x++)
        for (int y = 0; y < footprint.y; y++)
            occupiedCells.Remove(originCell + new Vector3Int(x, y, 0));

        OnGridChanged?.Invoke();
    }

    /// <summary>Returns a copy of all occupied cells (for debug / external pathfinding use).</summary>
    public HashSet<Vector3Int> GetOccupiedCells()
    {
        return new HashSet<Vector3Int>(occupiedCells);
    }
}
