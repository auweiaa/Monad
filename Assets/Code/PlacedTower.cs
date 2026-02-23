using UnityEngine;

public class PlacedTower : MonoBehaviour
{
    [SerializeField] private TowerData towerData;
    [SerializeField] private Vector3Int originCell;
    [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);

    private bool registeredWithPlacementManager;

    public TowerData TowerData => towerData;
    public Vector3Int OriginCell => originCell;
    public Vector2Int Footprint => footprint;

    public void Initialize(TowerData data, Vector3Int origin, Vector2Int footprint)
    {
        towerData  = data;
        originCell = origin;
        this.footprint = footprint;
    }

    private void Start()
    {
        RegisterCellsIfPrePlaced();
    }

    /// <summary>
    /// Pre-placed towers (in the scene, not placed at runtime) never had their cells
    /// registered with PlacementManager, so enemies path through them. Register now.
    /// Runtime-placed towers already had cells added by PlacementManager; we only free on destroy.
    /// </summary>
    private void RegisterCellsIfPrePlaced()
    {
        if (registeredWithPlacementManager || footprint.x < 1 || footprint.y < 1) return;

        var pm = FindFirstObjectByType<PlacementManager>();
        if (pm == null) return;

        // Pre-placed towers have default originCell (0,0,0). Compute from world position and register.
        if (originCell == Vector3Int.zero)
        {
            var grid = FindFirstObjectByType<Grid>();
            if (grid == null) return;
            Vector3Int centerCell = grid.WorldToCell(transform.position);
            Vector3Int cellOrigin = centerCell - new Vector3Int(footprint.x / 2, footprint.y / 2, 0);
            pm.OccupyCells(cellOrigin, footprint);
            originCell = cellOrigin; // so OnDestroy frees the correct cells
        }

        registeredWithPlacementManager = true;
    }

    private void OnDestroy()
    {
        if (!registeredWithPlacementManager) return;
        PlacementManager pm = FindFirstObjectByType<PlacementManager>();
        if (pm != null)
            pm.FreeCells(originCell, footprint);
    }
}








