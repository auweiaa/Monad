using UnityEngine;

[DisallowMultipleComponent]
public class GridOccupant : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private GridObjectKind kind = GridObjectKind.Other;

    [Header("Footprint (in cells)")]
    [Tooltip("Size in cells. For placed towers, this should match TowerData.Footprint.")]
    [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);

    [Header("Auto registration (scene objects)")]
    [Tooltip("If true, attempts to register this object on enable using transform position as origin cell.\n" +
             "For multi-tile objects placed at a footprint center, prefer explicit registration (e.g. PlacementManager).")]
    [SerializeField] private bool autoRegisterOnEnable = true;

    [Tooltip("If true, unregisters on disable/destroy if previously registered.")]
    [SerializeField] private bool autoUnregisterOnDisable = true;

    public GridObjectKind Kind => kind;
    public Vector2Int Footprint => footprint;

    public bool IsRegistered => gridOccupancy != null && isRegistered;
    public Vector3Int OriginCell => originCell;

    private IGridOccupancy gridOccupancy;
    private bool isRegistered;
    private Vector3Int originCell;

    private void OnValidate()
    {
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;
    }

    private void OnEnable()
    {
        if (!autoRegisterOnEnable)
        {
            return;
        }

        ResolveGridOccupancy();
        if (gridOccupancy == null)
        {
            return;
        }

        // NOTE: This treats this transform position as the origin cell (bottom-left).
        // For objects positioned at footprint center, use explicit registration instead.
        var grid = FindFirstObjectByType<Grid>();
        if (grid == null)
        {
            return;
        }

        Vector3Int origin = grid.WorldToCell(transform.position);
        gridOccupancy.TryRegister(this, origin, footprint);
    }

    private void OnDisable()
    {
        if (!autoUnregisterOnDisable)
        {
            return;
        }

        if (gridOccupancy != null && isRegistered)
        {
            gridOccupancy.Unregister(this);
        }
    }

    private void OnDestroy()
    {
        if (!autoUnregisterOnDisable)
        {
            return;
        }

        if (gridOccupancy != null && isRegistered)
        {
            gridOccupancy.Unregister(this);
        }
    }

    public void Configure(GridObjectKind objectKind, Vector2Int newFootprint)
    {
        kind = objectKind;
        footprint = newFootprint;
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;
    }

    internal void NotifyRegistered(IGridOccupancy manager, Vector3Int origin, Vector2Int size)
    {
        gridOccupancy = manager;
        isRegistered = true;
        originCell = origin;
        footprint = size;
    }

    internal void NotifyUnregistered(IGridOccupancy manager)
    {
        if (gridOccupancy == manager)
        {
            isRegistered = false;
        }
    }

    private void ResolveGridOccupancy()
    {
        if (gridOccupancy != null)
        {
            return;
        }

        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var b in behaviours)
        {
            if (b is IGridOccupancy occ)
            {
                gridOccupancy = occ;
                return;
            }
        }
    }
}

